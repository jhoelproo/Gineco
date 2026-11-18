using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Gineco;

internal sealed partial class Archivo
{
    private void InicializarPerfiles()
    {
        using var comando = Comando("""
            CREATE TABLE IF NOT EXISTS perfil(
                id TEXT PRIMARY KEY CHECK(id IN ('embarazada','no_embarazada')),
                campos TEXT NOT NULL, version INTEGER NOT NULL);
            INSERT OR IGNORE INTO perfil VALUES('embarazada','{}',0),('no_embarazada','{}',0);
            CREATE TABLE IF NOT EXISTS perfil_atencion(
                atencion INTEGER PRIMARY KEY REFERENCES atencion(id), datos TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS correccion(incorrecta TEXT PRIMARY KEY, correcta TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS configuracion_revision(
                id INTEGER PRIMARY KEY, autor TEXT NOT NULL, fecha TEXT NOT NULL, cambio TEXT NOT NULL, anterior TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS propuesta(
                atencion INTEGER REFERENCES atencion(id), perfil TEXT REFERENCES perfil(id),
                campo TEXT NOT NULL, texto TEXT NOT NULL, PRIMARY KEY(atencion,campo));
            """, null);
        comando.ExecuteNonQuery();
        InicializarSugerencias();
    }

    internal void ExigirAdministrador(Medico usuario, SqliteTransaction? transaccion = null)
    {
        using var comando = Comando("SELECT COUNT(*) FROM medico WHERE exequatur=$id AND admin=1 AND activo=1",
            transaccion, ("$id", usuario.Exequatur));
        if ((long)comando.ExecuteScalar()! != 1)
            throw new InvalidOperationException("Solo un administrador activo puede abrir o modificar la configuración.");
    }

    internal List<PerfilClinico> LeerPerfiles()
    {
        using var comando = Comando("SELECT id,campos,version FROM perfil ORDER BY id", null);
        using var lector = comando.ExecuteReader();
        var perfiles = new List<PerfilClinico>();
        while (lector.Read())
            perfiles.Add(new(lector.GetString(0), PerfilClinico.Tipos.Single(tipo => tipo.Id == lector.GetString(0)).Nombre,
                JsonSerializer.Deserialize<Dictionary<string, string>>(lector.GetString(1))!, lector.GetInt32(2)));
        return perfiles;
    }

    internal void GuardarPerfil(Medico usuario, PerfilClinico perfil)
    {
        using var transaccion = conexion.BeginTransaction();
        ExigirAdministrador(usuario, transaccion);
        if (!PerfilClinico.EsValido(perfil.Id) || perfil.Campos.Keys.Any(clave => !Perfiles.CamposClinicos.ContainsKey(clave) || !Perfiles.EsPredeterminadoEditable(clave)))
            throw new ArgumentException("El perfil contiene una opción o un campo desconocido.");
        string anterior = LeerConfiguracion("SELECT campos FROM perfil WHERE id=$id", perfil.Id, transaccion);
        using var comando = Comando("UPDATE perfil SET campos=$campos,version=version+1 WHERE id=$id AND version=$version",
            transaccion, ("$id", perfil.Id), ("$version", perfil.Version), ("$campos", JsonSerializer.Serialize(perfil.Campos)));
        if (comando.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("El perfil cambió en otra ventana. Cierra y vuelve a abrir la configuración.");
        RegistrarConfiguracion(usuario, perfil.Id, anterior, transaccion);
        transaccion.Commit();
    }

    private string LeerConfiguracion(string sql, string id, SqliteTransaction transaccion)
    {
        using var comando = Comando(sql, transaccion, ("$id", id));
        return (string)comando.ExecuteScalar()!;
    }

    private void RegistrarConfiguracion(Medico usuario, string cambio, string anterior, SqliteTransaction transaccion)
    {
        using var comando = Comando("""
            INSERT INTO configuracion_revision(autor,fecha,cambio,anterior) VALUES($autor,$fecha,$cambio,$anterior)
            """, transaccion, ("$autor", usuario.Exequatur), ("$fecha", DateTime.UtcNow),
            ("$cambio", cambio), ("$anterior", anterior));
        comando.ExecuteNonQuery();
    }

    private List<Correccion> LeerCorrecciones(SqliteTransaction? transaccion = null)
    {
        using var comando = Comando("SELECT incorrecta,correcta FROM correccion ORDER BY incorrecta", transaccion);
        using var lector = comando.ExecuteReader();
        var reglas = new List<Correccion>();
        while (lector.Read()) reglas.Add(new(lector.GetString(0), lector.GetString(1)));
        return reglas;
    }

    internal List<Correccion> ConsultarCorrecciones(Medico usuario)
    {
        ExigirAdministrador(usuario);
        return LeerCorrecciones();
    }

    internal void GuardarCorrecciones(Medico usuario, IReadOnlyList<Correccion> reglas, IReadOnlyList<Correccion> anteriores)
    {
        using var transaccion = conexion.BeginTransaction();
        ExigirAdministrador(usuario, transaccion);
        Ortografia.Validar(reglas);
        var actuales = LeerCorrecciones(transaccion);
        if (!actuales.SequenceEqual(anteriores))
            throw new InvalidOperationException("El diccionario cambió. Cierra y vuelve a abrir la configuración.");
        using var borrar = Comando("DELETE FROM correccion", transaccion);
        borrar.ExecuteNonQuery();
        foreach (var regla in reglas)
        {
            using var guardar = Comando("INSERT INTO correccion VALUES($incorrecta,$correcta)", transaccion,
                ("$incorrecta", regla.Incorrecta), ("$correcta", regla.Correcta));
            guardar.ExecuteNonQuery();
        }
        RegistrarConfiguracion(usuario, "ortografía", JsonSerializer.Serialize(actuales), transaccion);
        transaccion.Commit();
    }

    internal ExportacionPreparada PrepararExportacion(Atencion atencion) =>
        Ortografia.Preparar(atencion, LeerCorrecciones());

    private Dictionary<long, PerfilAplicado> LeerPerfilesDeHistorias()
    {
        using var comando = Comando("SELECT atencion,datos FROM perfil_atencion", null);
        using var lector = comando.ExecuteReader();
        var perfiles = new Dictionary<long, PerfilAplicado>();
        while (lector.Read()) perfiles.Add(lector.GetInt64(0), JsonSerializer.Deserialize<PerfilAplicado>(lector.GetString(1))!);
        return perfiles;
    }

    private void GuardarPerfilDeHistoria(long id, Atencion actual, SqliteTransaction transaccion)
    {
        using var borrar = Comando("DELETE FROM propuesta WHERE atencion=$id", transaccion, ("$id", id));
        borrar.ExecuteNonQuery();
        if (actual.Perfil is null) return;
        if (!PerfilClinico.EsValido(actual.Perfil.Id)) throw new ArgumentException("Perfil desconocido.");
        using var guardar = Comando("""
            INSERT INTO perfil_atencion VALUES($id,$datos)
            ON CONFLICT(atencion) DO UPDATE SET datos=excluded.datos
            """, transaccion, ("$id", id), ("$datos", JsonSerializer.Serialize(actual.Perfil)));
        guardar.ExecuteNonQuery();
        foreach (var clave in Perfiles.CamposClinicos.Keys.Where(Perfiles.EsTextoLibre))
        {
            string valor = actual.Valor(clave).Trim();
            if (valor.Length == 0 || Perfiles.EsPredeterminado(actual, clave)) continue;
            using var propuesta = Comando("INSERT INTO propuesta VALUES($id,$perfil,$campo,$texto)",
                transaccion, ("$id", id), ("$perfil", actual.Perfil.Id), ("$campo", clave), ("$texto", valor));
            propuesta.ExecuteNonQuery();
        }
    }

    internal List<Propuesta> LeerPropuestas(Medico usuario, string perfil)
    {
        ExigirAdministrador(usuario);
        using var comando = Comando("""
            SELECT campo,texto,COUNT(*) FROM propuesta WHERE perfil=$perfil AND campo<>'manejo' GROUP BY campo,texto ORDER BY COUNT(*) DESC,campo,texto
            """, null, ("$perfil", perfil));
        using var lector = comando.ExecuteReader();
        var propuestas = new List<Propuesta>();
        while (lector.Read()) propuestas.Add(new(lector.GetString(0), lector.GetString(1), lector.GetInt64(2)));
        return propuestas.Where(propuesta => Perfiles.CamposClinicos.ContainsKey(propuesta.Campo) && Perfiles.EsTextoLibre(propuesta.Campo) && Perfiles.EsPredeterminadoEditable(propuesta.Campo)).ToList();
    }
}
