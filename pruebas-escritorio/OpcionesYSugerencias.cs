using Gineco;
using MigraDoc.DocumentObjectModel.IO;
using Xunit;

namespace Gineco.Pruebas;

public sealed class OpcionesYSugerencias
{
    [Fact]
    public void ElPdfSoloImprimeSexoFemeninoYOmiteProcedimientos()
    {
        var historia = Historia("Diagnóstico de prueba", "Texto ficticio");
        historia.Campos["sexo"] = "Otro valor anterior";
        historia.Campos["procedimientos"] = "Procedimiento anterior de prueba";
        historia.Campos["observaciones"] = "Observación anterior de prueba";
        historia.Campos["estatus"] = "Admitida";
        historia.Campos["origen"] = "Maternidad";
        var documento = DdlWriter.WriteToString(Informe.CrearDocumento(historia));
        Assert.Contains("Femenino", documento);
        Assert.Contains("Admitida", documento);
        Assert.Contains("Maternidad", documento);
        Assert.DoesNotContain("Otro valor anterior", documento);
        Assert.DoesNotContain("Procedimiento anterior de prueba", documento);
        Assert.DoesNotContain("Observación anterior de prueba", documento);
    }

    [Fact]
    public void UsaSoloHistorialFinalizadoParaProponerYRequiereAprobacionPorPerfilYDiagnostico()
    {
        using var archivo = new Archivo(":memory:");
        archivo.Alta(null, "a", "Administrador", "abcdef");
        var admin = Assert.IsType<Medico>(archivo.Entrar("a", "Administrador", "abcdef"));
        archivo.Alta(admin, "m", "Médica", "abcdef");
        var medica = Assert.IsType<Medico>(archivo.Entrar("m", "Médica", "abcdef"));
        var historia = Historia("Diagnóstico de prueba", "Texto ficticio");
        archivo.Guardar(medica, historia, null, "");
        archivo.Guardar(medica, historia with { Finalizada = false }, null, "");
        archivo.Guardar(medica, Historia("", "Texto sin diagnóstico"), null, "");

        var candidata = Assert.Single(archivo.MedicacionParaRevisar(admin, "embarazada"));
        Assert.Equal(1, candidata.Historias);
        Assert.False(candidata.Aprobada);
        Assert.Empty(archivo.SugerenciasDisponibles("embarazada", candidata.Diagnostico));
        Assert.Throws<InvalidOperationException>(() => archivo.MedicacionParaRevisar(medica, "embarazada"));
        Assert.Throws<InvalidOperationException>(() => archivo.CambiarAprobacionMedicacion(medica, "embarazada", candidata, true));
        Assert.Throws<ArgumentException>(() => archivo.CambiarAprobacionMedicacion(admin, "otro", candidata, true));
        archivo.CambiarAprobacionMedicacion(admin, "embarazada", candidata, true);
        archivo.CambiarAprobacionMedicacion(admin, "embarazada", candidata, true);
        Assert.True(Assert.Single(archivo.MedicacionParaRevisar(admin, "embarazada")).Aprobada);
        Assert.Equal(candidata.Texto, Assert.Single(archivo.SugerenciasDisponibles("embarazada", " DIAGNÓSTICO   DE PRUEBA ")).Texto);
        Assert.Empty(archivo.SugerenciasDisponibles("no_embarazada", candidata.Diagnostico));
        Assert.Empty(archivo.SugerenciasDisponibles("embarazada", candidata.Diagnostico + " y otro"));
        Assert.Empty(archivo.SugerenciasDisponibles(null, candidata.Diagnostico));
        Assert.Empty(archivo.SugerenciasDisponibles("embarazada", ""));
        archivo.CambiarAprobacionMedicacion(admin, "embarazada", candidata, false);
        Assert.Empty(archivo.SugerenciasDisponibles("embarazada", candidata.Diagnostico));
        Assert.False(Assert.Single(archivo.MedicacionParaRevisar(admin, "embarazada")).Aprobada);
    }

    [Fact]
    public void ElPerfilNoAplicaUnTratamientoAnteriorAutomaticamente()
    {
        var perfil = new PerfilClinico("embarazada", "Paciente embarazada", new() { ["manejo"] = "Tratamiento anterior" });
        var datos = new Dictionary<string, string>();
        var aplicado = Perfiles.Aplicar(perfil, datos, null);
        Assert.False(aplicado.Predeterminados.ContainsKey("manejo"));
        Assert.False(datos.ContainsKey("manejo"));
        Assert.Equal("Sugerencia", RepertorioMedicacion.Anadir("", "Sugerencia"));
        Assert.Equal("Apunte" + Environment.NewLine + "Sugerencia", RepertorioMedicacion.Anadir("Apunte ", "Sugerencia"));
    }

    internal static Atencion Historia(string diagnostico, string texto) =>
        new(0, new Medico("m", "Médica", false), new DateTime(2026, 11, 15),
            new() { ["nombre"] = "Paciente ficticia", ["diagnosticos"] = diagnostico, ["manejo"] = texto }, true)
        { Perfil = new("embarazada", 0, []) };
}
