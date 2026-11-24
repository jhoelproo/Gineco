using Gineco;
using PdfSharp.Pdf.IO;
using Xunit;

namespace Gineco.Pruebas;

public sealed class CambiosClinicos
{
    [Fact]
    public void RecuperacionExigeAdministradorYReemplazaLaClave()
    {
        using var archivo = new Archivo(":memory:");
        Assert.Throws<ArgumentException>(() => archivo.Alta(null, "01", "Ada", "12345"));
        archivo.Alta(null, "01", "Ada", "123456");
        var admin = Assert.IsType<Medico>(archivo.Entrar("01", "Ada", "123456"));
        archivo.Alta(admin, "02", "Luz", "234567");
        var medico = Assert.IsType<Medico>(archivo.Entrar("02", "Luz", "234567"));
        Assert.Equal(new[] { "Ada", "Luz" }, archivo.UsuariosActivos().Select(cuenta => cuenta.Nombre));
        Assert.False(archivo.RestablecerClave("02", medico with { Administrador = true }, "234567", "987654"));
        Assert.False(archivo.RestablecerClave("02", admin, "incorrecta", "987654"));
        Assert.False(archivo.RestablecerClave("99", admin, "123456", "987654"));
        Assert.Throws<ArgumentException>(() => archivo.RestablecerClave("02", admin, "123456", "12345"));
        Assert.NotNull(archivo.Entrar("02", "Luz", "234567"));
        Assert.True(archivo.RestablecerClave("02", admin, "123456", "987654"));
        Assert.Null(archivo.Entrar("02", "Luz", "234567"));
        Assert.NotNull(archivo.Entrar("02", "Luz", "987654"));
    }

    [Fact]
    public void HistoriaBreveConTodasLasSeccionesCabeEnUnaPagina()
    {
        var campos = Clinica.Secciones.Values.SelectMany(seccion => seccion)
            .ToDictionary(campo => campo.Key, _ => "Dato ficticio");
        campos["nombre"] = "Paciente de demostración";
        campos["nss"] = "001234567890";
        campos["telefono"] = "8095550123";
        campos["ars"] = "ARS de demostración";
        campos["pruebas"] = new AnalisisLaboratorio(AnalisisLaboratorio.Opciones, "Perfil renal").Guardar();
        campos["diagnosticos"] = "Diagnóstico principal de demostración\nDiagnóstico adicional de demostración";
        campos["sonografias"] = "Sonografía de demostración";
        campos["alergias"] = "Sí";
        campos["origen"] = "Maternidad";
        campos["observaciones_origen"] = "Observación ficticia de una historia anterior";
        var historia = new Atencion(1, new("01", "Dra. de demostración", false),
            new DateTime(2026, 11, 15, 12, 20, 0), campos, true);
        string ruta = Path.GetTempFileName();
        try
        {
            Informe.Exportar(historia, ruta);
            using var pdf = PdfReader.Open(ruta, PdfDocumentOpenMode.Import);
            Assert.Equal(1, pdf.PageCount);
            Assert.Equal(612, pdf.Pages[0].Width.Point, 1);
            Assert.Equal(792, pdf.Pages[0].Height.Point, 1);
            var muestra = Environment.GetEnvironmentVariable("GINECO_PDF_BREVE_QA");
            if (muestra is not null) File.Copy(ruta, muestra, overwrite: true);
        }
        finally { File.Delete(ruta); }
    }

    [Fact]
    public void HistoriaLargaContinuaSinPerderPaginaCartaNiEncabezados()
    {
        var historia = new Atencion(1, new("01", "Médica de demostración", false), new DateTime(2026, 11, 15),
            new()
            {
                ["nombre"] = "Paciente de demostración",
                ["diagnosticos"] = string.Join("\n", Enumerable.Range(1, 65).Select(numero => $"Diagnóstico de demostración {numero}")),
                ["sonografias"] = "Sonografía de demostración"
            }, true);
        string ruta = Path.GetTempFileName();
        try
        {
            Informe.Exportar(historia, ruta);
            using var pdf = PdfReader.Open(ruta, PdfDocumentOpenMode.Import);
            Assert.True(pdf.PageCount > 1);
            foreach (var pagina in pdf.Pages)
            {
                Assert.Equal(612, pagina.Width.Point, 1);
                Assert.Equal(792, pagina.Height.Point, 1);
                var recursos = pagina.Elements.GetDictionary("/Resources");
                Assert.Equal(2, recursos!.Elements.GetDictionary("/XObject")!.Elements.Count);
            }
            var muestra = Environment.GetEnvironmentVariable("GINECO_PDF_LARGO_QA");
            if (muestra is not null) File.Copy(ruta, muestra, overwrite: true);
        }
        finally { File.Delete(ruta); }
    }
}
