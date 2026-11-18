using System.Text.Json;

namespace Gineco;

internal sealed record Derivacion(Guid Id, long Visita, DateTime Llegada, Dictionary<string, string> Campos);

internal sealed record FilaAdmision(Guid Id, long Visita, DateTime Llegada, string Nombre, string Cedula, long? Historia, EstadoConsulta Situacion)
{
    public bool Impresa => Situacion == EstadoConsulta.Impresa;
    public bool SoloRemota => !Historia.HasValue && Situacion != EstadoConsulta.Pendiente;
    public string Estado => Situacion switch
    {
        EstadoConsulta.Impresa => "Enviada a impresión",
        EstadoConsulta.Finalizada => "Finalizada",
        EstadoConsulta.EnAtencion => "En atención",
        _ => "Pendiente"
    };
}

internal sealed partial class Archivo
{
    private void InicializarDerivaciones()
    {
        using var orden = Comando("""
            CREATE TABLE IF NOT EXISTS derivacion(
                id TEXT PRIMARY KEY, visita INTEGER NOT NULL, llegada TEXT NOT NULL,
                prioridad TEXT NOT NULL, campos TEXT NOT NULL,
                historia INTEGER UNIQUE REFERENCES atencion(id), version_impresa INTEGER);
            """, null);
        orden.ExecuteNonQuery();
    }

    internal int Recibir(IEnumerable<Derivacion> lote)
    {
        using var tx = conexion.BeginTransaction();
        int nuevas = 0;
        foreach (var fila in lote)
        {
            if (fila.Id == Guid.Empty || fila.Visita < 1 || fila.Llegada == default ||
                string.IsNullOrWhiteSpace(fila.Campos?.GetValueOrDefault("nombre")))
                throw new InvalidDataException("La derivación está incompleta.");
            using var orden = Comando("""
                INSERT INTO derivacion(id,visita,llegada,prioridad,campos) VALUES($id,$visita,$fecha,'',$campos)
                ON CONFLICT(id) DO NOTHING
                """, tx, ("$id", fila.Id.ToString()), ("$visita", fila.Visita), ("$fecha", fila.Llegada),
                ("$campos", JsonSerializer.Serialize(DatosAdmision(fila.Campos))));
            nuevas += orden.ExecuteNonQuery();
        }
        tx.Commit();
        return nuevas;
    }

    internal List<FilaAdmision> Bandeja()
    {
        using var orden = Comando("""
            SELECT d.id,d.visita,d.llegada,
                   coalesce(json_extract(a.campos,'$.nombre'),r.nombre,json_extract(d.campos,'$.nombre')) AS nombre,
                   coalesce(json_extract(a.campos,'$.cedula'),r.cedula,json_extract(d.campos,'$.cedula'),'') AS cedula,
                   d.historia,
                   CASE WHEN d.version_impresa=a.version THEN 3 WHEN a.finalizada=1 THEN 2
                        WHEN d.historia IS NOT NULL THEN 1 ELSE coalesce(r.estado,0) END AS estado
            FROM derivacion d LEFT JOIN atencion a ON a.id=d.historia
            LEFT JOIN cambio_consulta_recibido r ON r.id=d.id
            WHERE NOT EXISTS (SELECT 1 FROM retiro_derivacion b WHERE b.derivacion=d.id)
            UNION ALL
            SELECT r.id,r.visita,r.llegada,r.nombre,r.cedula,NULL,r.estado
            FROM cambio_consulta_recibido r
            WHERE NOT EXISTS (SELECT 1 FROM derivacion d WHERE d.id=r.id)
              AND NOT EXISTS (SELECT 1 FROM retiro_derivacion b WHERE b.derivacion=r.id)
            ORDER BY estado ASC,llegada DESC,visita DESC
            """, null);
        using var filas = orden.ExecuteReader();
        var bandeja = new List<FilaAdmision>();
        while (filas.Read())
        {
            bandeja.Add(new(Guid.Parse(filas.GetString(0)), filas.GetInt64(1), filas.GetDateTime(2), filas.GetString(3), filas.GetString(4),
                filas.IsDBNull(5) ? null : filas.GetInt64(5), (EstadoConsulta)filas.GetInt32(6)));
        }
        return bandeja;
    }

    internal Atencion AbrirDerivacion(Guid id, Medico usuario)
    {
        using var tx = conexion.BeginTransaction();
        using var orden = Comando("""
            SELECT historia,llegada,campos FROM derivacion WHERE id=$id
              AND NOT EXISTS(SELECT 1 FROM retiro_derivacion WHERE derivacion=$id)
              AND (historia IS NOT NULL OR NOT EXISTS(SELECT 1 FROM cambio_consulta_recibido WHERE id=$id))
            """, tx, ("$id", id.ToString()));
        long historia;
        using (var fila = orden.ExecuteReader())
        {
            if (!fila.Read()) throw new InvalidOperationException("La admisión ya no está disponible.");
            if (!fila.IsDBNull(0)) historia = fila.GetInt64(0);
            else
            {
                var llegada = fila.GetDateTime(1);
                var campos = JsonSerializer.Deserialize<Dictionary<string, string>>(fila.GetString(2))!;
                campos = DatosAdmision(campos);
                campos["origen"] = OpcionesClinicas.Maternidad;
                fila.Close();
                historia = Insertar(usuario, new(0, usuario, llegada, campos), tx);
                using var vincular = Comando("UPDATE derivacion SET historia=$historia WHERE id=$id", tx,
                    ("$historia", historia), ("$id", id.ToString()));
                vincular.ExecuteNonQuery();
                EncolarCambio(historia, tx);
            }
        }
        tx.Commit();
        return Listar(usuario).Single(a => a.Id == historia);
    }

    private static Dictionary<string, string> DatosAdmision(Dictionary<string, string> datos)
    {
        var campos = new Dictionary<string, string>(datos);
        campos.Remove("motivo");
        campos.Remove("prioridad");
        return campos;
    }

    internal void RegistrarImpresion(Atencion historia)
    {
        using var tx = conexion.BeginTransaction();
        using var orden = Comando("""
            UPDATE derivacion SET version_impresa=$version WHERE historia=$id
            AND EXISTS(SELECT 1 FROM atencion WHERE id=$id AND version=$version AND finalizada=1)
            AND NOT EXISTS(SELECT 1 FROM retiro_derivacion WHERE retiro_derivacion.derivacion=derivacion.id)
            """, tx, ("$id", historia.Id), ("$version", historia.Version));
        if (orden.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("La versión impresa ya no coincide con la historia actual.");
        EncolarCambio(historia.Id, tx);
        tx.Commit();
    }
}
