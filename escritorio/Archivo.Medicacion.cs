using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Gineco;

internal sealed partial class Archivo
{
    private void InicializarSugerencias()
    {
        using var comando = Comando("""
            CREATE TABLE IF NOT EXISTS sugerencia_medicacion(
                perfil TEXT NOT NULL REFERENCES perfil(id), clave TEXT NOT NULL,
                diagnostico TEXT NOT NULL, texto TEXT NOT NULL, PRIMARY KEY(perfil,clave,texto));
            """, null);
        comando.ExecuteNonQuery();
    }

    internal List<MedicacionHistorica> SugerenciasDisponibles(string? perfil, string diagnostico)
    {
        var clave = RepertorioMedicacion.Clave(diagnostico);
        if (perfil is null || clave.Length == 0) return [];
        using var comando = Comando("""
            SELECT diagnostico,texto FROM sugerencia_medicacion WHERE perfil=$perfil AND clave=$clave ORDER BY texto
            """, null, ("$perfil", perfil), ("$clave", clave));
        using var lector = comando.ExecuteReader();
        var sugerencias = new List<MedicacionHistorica>();
        while (lector.Read()) sugerencias.Add(new(lector.GetString(0), lector.GetString(1), 0, true));
        return sugerencias;
    }

    internal List<MedicacionHistorica> MedicacionParaRevisar(Medico admin, string perfil)
    {
        ExigirAdministrador(admin);
        var registros = LeerMedicacionHistorica(perfil);
        var aprobadas = LeerMedicacionAprobada(perfil);
        return AgruparMedicacion(registros, aprobadas);
    }

    private List<MedicacionHistorica> LeerMedicacionAprobada(string perfil)
    {
        using var comando = Comando("SELECT diagnostico,texto FROM sugerencia_medicacion WHERE perfil=$perfil", null, ("$perfil", perfil));
        using var lector = comando.ExecuteReader();
        var items = new List<MedicacionHistorica>();
        while (lector.Read()) items.Add(new(lector.GetString(0), lector.GetString(1), 0, true));
        return items;
    }

    private List<MedicacionHistorica> LeerMedicacionHistorica(string perfil)
    {
        using var comando = Comando("""
            SELECT a.campos,p.texto FROM propuesta p JOIN atencion a ON a.id=p.atencion
            WHERE p.perfil=$perfil AND p.campo='manejo' AND a.finalizada=1
            """, null, ("$perfil", perfil));
        using var lector = comando.ExecuteReader();
        var items = new List<MedicacionHistorica>();
        while (lector.Read())
        {
            var campos = JsonSerializer.Deserialize<Dictionary<string, string>>(lector.GetString(0))!;
            var diagnostico = campos.GetValueOrDefault("diagnosticos", "").Trim();
            if (diagnostico.Length > 0) items.Add(new(diagnostico, lector.GetString(1), 1, false));
        }
        return items;
    }

    private static List<MedicacionHistorica> AgruparMedicacion(
        IEnumerable<MedicacionHistorica> registros, IEnumerable<MedicacionHistorica> aprobadas) =>
        registros.Concat(aprobadas)
            .GroupBy(item => (RepertorioMedicacion.Clave(item.Diagnostico), item.Texto))
            .Select(grupo => grupo.First() with
            {
                Historias = grupo.Sum(item => item.Historias),
                Aprobada = grupo.Any(item => item.Aprobada)
            })
            .OrderByDescending(item => item.Historias).ThenBy(item => item.Diagnostico).ThenBy(item => item.Texto).ToList();

    internal void CambiarAprobacionMedicacion(Medico admin, string perfil, MedicacionHistorica item, bool aprobar)
    {
        var clave = RepertorioMedicacion.Clave(item.Diagnostico);
        using var transaccion = conexion.BeginTransaction();
        ExigirAdministrador(admin, transaccion);
        if (!PerfilClinico.EsValido(perfil) || clave.Length == 0 || string.IsNullOrWhiteSpace(item.Texto))
            throw new ArgumentException("El perfil, diagnóstico y texto son obligatorios.");
        const string alta = """
            INSERT OR IGNORE INTO sugerencia_medicacion(perfil,clave,diagnostico,texto)
            VALUES($perfil,$clave,$diagnostico,$texto)
            """;
        const string baja = "DELETE FROM sugerencia_medicacion WHERE perfil=$perfil AND clave=$clave AND texto=$texto";
        using var comando = Comando(aprobar ? alta : baja, transaccion,
            ("$perfil", perfil), ("$clave", clave), ("$diagnostico", item.Diagnostico), ("$texto", item.Texto));
        if (comando.ExecuteNonQuery() > 0)
            RegistrarConfiguracion(admin, aprobar ? "medicación: aprobación" : "medicación: retiro", JsonSerializer.Serialize(item), transaccion);
        transaccion.Commit();
    }
}
