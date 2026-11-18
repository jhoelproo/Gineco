using Gineco;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.IO;
using Xunit;

namespace Gineco.Pruebas;

public sealed class Laboratorios
{
    [Theory]
    [InlineData("", "", "")]
    [InlineData("Hemograma, perfil renal", "", "Hemograma, perfil renal")]
    [InlineData(" hEmOgRaMa\r\nExamen de orina\r\nHemograma\r\nPerfil renal\r\n\r\nCoagulación", "Hemograma\nExamen de orina", "Perfil renal\n\nCoagulación")]
    public void RestauraSeleccionesYConservaTextoLibre(string guardado, string habituales, string otros)
    {
        var analisis = AnalisisLaboratorio.Leer(guardado);
        Assert.Equal(habituales, string.Join("\n", analisis.Habituales));
        Assert.Equal(otros, analisis.Otros);
        Assert.Equal(analisis.Guardar(), AnalisisLaboratorio.Leer(analisis.Guardar()).Guardar());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InformeOmiteEstadoDeHistoriaYConservaEstatusClinico(bool finalizada)
    {
        var atencion = new Atencion(1, new Medico("01", "Médica ficticia", false), new DateTime(2026, 11, 15, 12, 20, 0),
            new() { ["pruebas"] = "Hemograma\nPerfil renal", ["estatus"] = "Admitida" }, finalizada);
        string documento = DdlWriter.WriteToString(Informe.CrearDocumento(atencion));
        Assert.Contains("15/11/2026 12:20", documento);
        Assert.DoesNotContain("Borrador", documento);
        Assert.DoesNotContain("Finalizada", documento);
        Assert.Contains("Hemograma", documento);
        Assert.Contains("Perfil renal", documento);
        Assert.Contains("Admitida", documento);
    }

    [Theory]
    [InlineData("Hemograma\r\n\r\n Examen de orina \nPerfil renal", "Hemograma, Examen de orina, Perfil renal")]
    [InlineData("Hemograma, perfil renal", "Hemograma, perfil renal")]
    [InlineData("Hemograma", "Hemograma")]
    public void ElPdfResaltaLosAnalisisEnUnaListaConComas(string pruebas, string esperado)
    {
        var atencion = new Atencion(1, new Medico("01", "Médica ficticia", false), new DateTime(2026, 11, 15),
            new() { ["pruebas"] = pruebas });
        var parrafos = Informe.CrearDocumento(atencion).Sections[0].Elements.Cast<DocumentObject>().OfType<Paragraph>();
        var analisis = Assert.Single(parrafos.SelectMany(parrafo => parrafo.Elements.Cast<DocumentObject>())
            .OfType<FormattedText>(), texto =>
                string.Concat(texto.Elements.Cast<DocumentObject>().OfType<Text>().Select(fragmento => fragmento.Content)) == esperado);
        Assert.True(analisis.Bold);
    }
}
