using Gineco;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using Xunit;

namespace Gineco.Pruebas;

public sealed class Validacion
{
    [Theory]
    [InlineData("", true)]
    [InlineData("0", true)]
    [InlineData("1", true)]
    [InlineData("130", true)]
    [InlineData("-1", false)]
    [InlineData("131", false)]
    [InlineData("texto", false)]
    public void ValidaLimitesDeEdad(string edad, bool valida)
    {
        var campos = new Dictionary<string, string> { ["nombre"] = "Paciente ficticia", ["edad"] = edad };
        if (valida) Clinica.Validar(campos);
        else Assert.Throws<ArgumentException>(() => Clinica.Validar(campos));
    }

    [Fact]
    public void ValidaIdentificacion()
    {
        Assert.Throws<ArgumentException>(() => Clinica.Validar([]));
        var campos = new Dictionary<string, string> { ["nombre"] = "Paciente ficticia", ["cedula"] = "123-1234567-1" };
        Clinica.Validar(campos);
        Assert.Equal("12312345671", campos["cedula"]);
        campos["telefono"] = "123456789X";
        Assert.Throws<ArgumentException>(() => Clinica.Validar(campos));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("00-12345678900")]
    [InlineData("S/N")]
    [InlineData("")]
    public void AdmiteNssTalComoSeEdita(string nss)
    {
        var campos = new Dictionary<string, string> { ["nombre"] = "Paciente ficticia", ["nss"] = nss };
        Clinica.Validar(campos);
        Assert.Equal(nss, campos["nss"]);
    }

    [Theory]
    [InlineData("alergias", "detalle_alergias")]
    [InlineData("previas", "lugar")]
    public void RequiereDetalleAfirmativoSoloAlFinalizar(string campo, string detalle)
    {
        var campos = new Dictionary<string, string> { ["nombre"] = "Paciente ficticia", [campo] = "Sí", [detalle] = " " };
        Clinica.Validar(campos);
        Assert.Equal(detalle, Clinica.DetallePendiente(campos)!.Value.Campo);
        Assert.Throws<ArgumentException>(() => Clinica.Validar(campos, finalizada: true));
        campos[detalle] = "Dato ficticio";
        Clinica.Validar(campos, finalizada: true);
        campos[campo] = "No";
        campos.Remove(detalle);
        Clinica.Validar(campos, finalizada: true);
    }

    [Fact]
    public void PersisteHistoriaYAplicaPermisosYVersion()
    {
        string ruta = Path.GetTempFileName();
        try
        {
            using (var archivo = new Archivo(ruta))
            {
                Assert.True(archivo.SinCuentas());
                archivo.Alta(null, "0000", "Administración ficticia", "Clave de prueba 2026");
                Assert.Null(archivo.Entrar("0000", "Otro nombre", "Clave de prueba 2026"));
                Assert.Null(archivo.Entrar("0000", "Administración ficticia", "Clave incorrecta"));
                var admin = Assert.IsType<Medico>(archivo.Entrar("0000", "Administración ficticia", "Clave de prueba 2026"));
                Assert.Throws<InvalidOperationException>(() => archivo.Alta(null, "0009", "Otra cuenta", "Clave de prueba 2026"));
                archivo.Alta(admin, "0001", "Médica ficticia", "Otra clave de prueba");
                archivo.Alta(admin, "0002", "Médico ficticio", "Nueva clave de prueba");
                var medica = Assert.IsType<Medico>(archivo.Entrar("0001", "Médica ficticia", "Otra clave de prueba"));
                var otro = Assert.IsType<Medico>(archivo.Entrar("0002", "Médico ficticio", "Nueva clave de prueba"));
                var historia = Nueva(medica);
                archivo.Guardar(medica, historia, null, "");
                Assert.Empty(archivo.Listar(otro));
                var borrador = Assert.Single(archivo.Listar(medica));
                Assert.False(borrador.Finalizada);
                archivo.Guardar(medica, borrador with { Finalizada = true }, borrador, "");
                Assert.Throws<InvalidOperationException>(() => archivo.Guardar(medica, borrador, borrador, ""));
                var finalizada = Assert.Single(archivo.Listar(admin));
                Assert.Throws<ArgumentException>(() => archivo.Guardar(admin, finalizada, finalizada, " "));
                archivo.Guardar(admin, finalizada, finalizada, "Corrección ficticia");
            }
            using var reabierto = new Archivo(ruta);
            var medico = Assert.IsType<Medico>(reabierto.Entrar("0001", "Médica ficticia", "Otra clave de prueba"));
            var guardada = Assert.Single(reabierto.Listar(medico));
            Assert.True(guardada.Finalizada);
            Assert.Equal(2, guardada.Version);
            Assert.Equal(medico.Exequatur, guardada.Responsable.Exequatur);
        }
        finally { File.Delete(ruta); }
    }

    [Fact]
    public void ColocaLosDosLogosEnTodasLasPaginas()
    {
        string ruta = Path.GetTempFileName();
        try
        {
            var historia = Nueva(new("0001", "Médica ficticia", false));
            historia.Campos["motivo"] = string.Join("\r\n", Enumerable.Repeat("Evolución ficticia para verificar el salto de página.", 90));
            Informe.Exportar(historia, ruta);
            using var documento = PdfReader.Open(ruta, PdfDocumentOpenMode.Import);
            Assert.True(documento.PageCount > 1);
            foreach (var pagina in documento.Pages)
            {
                Assert.Equal(612, pagina.Width.Point, 1);
                Assert.Equal(792, pagina.Height.Point, 1);
                var objetos = pagina.Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/XObject")!;
                var imagenes = objetos.Elements.Values.OfType<PdfReference>()
                    .Select(referencia => referencia.Value).OfType<PdfDictionary>()
                    .Where(objeto => objeto.Elements.GetName("/Subtype") == "/Image").ToList();
                Assert.Contains(imagenes, imagen => imagen.Elements.GetInteger("/Width") == 857 && imagen.Elements.GetInteger("/Height") == 195);
                Assert.Contains(imagenes, imagen => imagen.Elements.GetInteger("/Width") == 119 && imagen.Elements.GetInteger("/Height") == 190);
            }
            var muestra = Environment.GetEnvironmentVariable("GINECO_PDF_QA");
            if (muestra is not null) File.Copy(ruta, muestra, overwrite: true);
        }
        finally { File.Delete(ruta); }
    }

    private static Atencion Nueva(Medico medico) =>
        new(0, medico, new DateTime(2026, 1, 1, 12, 30, 0), new() { ["nombre"] = "Paciente ficticia" });
}
