using Xunit;

namespace Gineco.Pruebas;

public sealed class RevisionOrtografica
{
    [Theory]
    [InlineData("pediatria", "pediatría")]
    [InlineData("tamibén", "también")]
    [InlineData("normal", null)]
    [InlineData("NSS", null)]
    [InlineData("qzxwsr", null)]
    [InlineData("palabramuylargaquenohayquecorregirporquepuedeseruncodigo", null)]
    public void DetectaPalabrasConElLexicoInstalado(string palabra, string? esperada) =>
        Assert.Equal(esperada, OrtografiaAutomatica.Sugerir(palabra));

    [Fact]
    public void ConservaTextoAmbiguoOAlejado()
    {
        Assert.Null(OrtografiaAutomatica.Elegir("partaa", ["partas", "partan"]));
        Assert.Null(OrtografiaAutomatica.Elegir("pediatria", ["pediatría", "pèdiatría"]));
        Assert.Null(OrtografiaAutomatica.Elegir("falta", ["falda"]));
        Assert.Null(OrtografiaAutomatica.Elegir("partaa", ["paciente", "partes", "partó"]));
        Assert.Equal("partan", OrtografiaAutomatica.Elegir("partann", ["partan", "paciente"]));
    }

    [Fact]
    public void CorrigeSoloPalabrasDeNarracionEnLaCopiaDelPdf()
    {
        using var archivo = new Archivo(":memory:");
        var historia = new Atencion(0, new("M", "Médica ficticia", false), new(2026, 11, 18), new()
        {
            ["nombre"] = "Pediatria ficticia",
            ["motivo"] = "Pediatria, tamibén: NSS 100. Pediatria10.",
            ["manejo"] = "pediatria",
            ["ta"] = "110/70",
            ["diagnosticos"] = "pediatria"
        });
        var preparada = archivo.PrepararExportacion(historia);
        Assert.Equal("Pediatría, también: NSS 100. Pediatria10.", preparada.Atencion.Valor("motivo"));
        Assert.Equal("Pediatria, tamibén: NSS 100. Pediatria10.", historia.Valor("motivo"));
        Assert.Equal("Pediatria ficticia", preparada.Atencion.Nombre);
        Assert.Equal("110/70", preparada.Atencion.Valor("ta"));
        Assert.Equal("pediatria", preparada.Atencion.Valor("manejo"));
        Assert.Equal("pediatria", preparada.Atencion.Valor("diagnosticos"));
        Assert.Equal(2, preparada.Correcciones.Count);
    }
}
