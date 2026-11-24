using Gineco;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.IO;
using MigraDoc.DocumentObjectModel.Tables;
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
        Assert.Contains("15/11/2026 12:20 PM", documento);
        Assert.Contains(finalizada ? "Finalizada" : "Borrador", atencion.ToString());
        Assert.DoesNotContain("Borrador", documento);
        Assert.DoesNotContain("Finalizada", documento);
        Assert.Contains("Hemograma", documento);
        Assert.Contains("Perfil renal", documento);
        Assert.Contains("Admitida", documento);
    }

    [Theory]
    [InlineData(0, 0, "12:00 AM")]
    [InlineData(11, 59, "11:59 AM")]
    [InlineData(12, 0, "12:00 PM")]
    [InlineData(13, 5, "01:05 PM")]
    [InlineData(23, 59, "11:59 PM")]
    public void ElPdfPresentaLaHoraDeDoceHorasEnUnTamanoMayor(int hora, int minuto, string esperado)
    {
        var atencion = new Atencion(1, new Medico("01", "Médica ficticia", false), new DateTime(2026, 11, 15, hora, minuto, 0), []);
        var fecha = Assert.Single(TextosDelInforme(atencion), texto => Contenido(texto) == $"ATENCIÓN  15/11/2026 {esperado}");
        Assert.True(fecha.Bold);
        Assert.True(fecha.Size.Point >= 12);
        Assert.Contains(esperado, atencion.ToString());
    }

    [Fact]
    public void ElPdfResaltaLosValoresSolicitadosDeLaPaciente()
    {
        var atencion = new Atencion(1, new Medico("01", "Médica ficticia", false), new DateTime(2026, 11, 15), new()
        {
            ["nombre"] = "Paciente ficticia",
            ["nss"] = "001234567890",
            ["telefono"] = "8095550123",
            ["ars"] = "ARS ficticia",
            ["direccion"] = "Domicilio ficticio"
        });
        string[] negritas = TextosDelInforme(atencion).Where(texto => texto.Bold).Select(Contenido).ToArray();
        Assert.Contains("Paciente ficticia", negritas);
        Assert.Contains("001234567890", negritas);
        Assert.Contains("8095550123", negritas);
        Assert.Contains("ARS ficticia", negritas);
        Assert.DoesNotContain("Domicilio ficticio", negritas);
        Assert.DoesNotContain("Sin registrar", negritas);
    }

    [Fact]
    public void ElPdfConservaElOrigenYOcultaLasObservacionesGuardadas()
    {
        const string observacion = "Observación ficticia de una historia anterior";
        var atencion = new Atencion(1, new Medico("01", "Médica ficticia", false), new DateTime(2026, 11, 15),
            new() { ["origen"] = "Maternidad", ["observaciones_origen"] = observacion });

        string documento = DdlWriter.WriteToString(Informe.CrearDocumento(atencion));

        Assert.Contains("Maternidad", documento);
        Assert.DoesNotContain("Observaciones del origen", documento);
        Assert.DoesNotContain(observacion, documento);
        Assert.Equal(observacion, atencion.Valor("observaciones_origen"));
    }

    [Fact]
    public void ElPdfResaltaElNombreYExequaturDelMedico()
    {
        var medico = new Medico("EX-02451", "Dra. de demostración", false);
        var atencion = new Atencion(1, medico, new DateTime(2026, 11, 15), []);

        string[] negritas = TextosDelInforme(atencion).Where(texto => texto.Bold).Select(Contenido).ToArray();

        Assert.Contains(medico.Nombre, negritas);
        Assert.Contains(medico.Exequatur, negritas);
    }

    private static IEnumerable<FormattedText> TextosDelInforme(Atencion atencion)
    {
        var elementos = Informe.CrearDocumento(atencion).Sections[0].Elements;
        var celdas = elementos.OfType<Table>().SelectMany(tabla => tabla.Rows.OfType<Row>())
            .SelectMany(fila => fila.Cells.OfType<Cell>())
            .SelectMany(celda => celda.Elements.OfType<Paragraph>());
        return elementos.OfType<Paragraph>().Concat(celdas)
            .SelectMany(parrafo => parrafo.Elements.OfType<FormattedText>());
    }

    private static string Contenido(FormattedText texto) =>
        string.Concat(texto.Elements.OfType<Text>().Select(fragmento => fragmento.Content));

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

    [Fact]
    public void LosDiagnosticosYLasSonografiasResaltanTodasLasLineasRegistradas()
    {
        var atencion = new Atencion(1, new Medico("01", "Médica ficticia", false), new DateTime(2026, 11, 15),
            new()
            {
                ["diagnosticos"] = "Diagnóstico A\r\n\r\nDiagnóstico B",
                ["sonografias"] = "Sonografía A\nSonografía B",
                ["hallazgos"] = "Hallazgo de referencia"
            });
        var parrafos = Informe.CrearDocumento(atencion).Sections[0].Elements.OfType<Paragraph>().ToArray();
        var resaltados = parrafos.SelectMany(parrafo => parrafo.Elements.OfType<FormattedText>())
            .Where(texto => texto.Bold).SelectMany(texto => texto.Elements.OfType<Text>()).Select(texto => texto.Content).ToArray();
        Assert.Contains("Diagnóstico A", resaltados);
        Assert.Contains("Diagnóstico B", resaltados);
        Assert.Contains("Sonografía A", resaltados);
        Assert.Contains("Sonografía B", resaltados);
        Assert.DoesNotContain("Hallazgo de referencia", resaltados);
        Assert.DoesNotContain("Sin registrar", resaltados);
        Assert.DoesNotContain("Diagnósticos (uno por línea): ", resaltados);
    }
}
