using System.Drawing.Printing;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace Gineco.Pruebas;

public sealed class Derivaciones
{
    private static readonly Medico Ana = new("01", "Médica A", false);
    private static readonly Medico Eva = new("02", "Médica B", false);
    private static Derivacion Nueva() => new(Guid.NewGuid(), 15, new(2026, 1, 1, 12, 0, 0),
        "Prioritaria", new() { ["nombre"] = "Paciente ficticia", ["motivo"] = "Motivo de prueba" });

    [Fact]
    public void LoteInvalidoRevierteYReintentoConservaUnaAtencion()
    {
        using var archivo = new Archivo(":memory:");
        var d = Nueva();
        foreach (var mala in new[] { d with { Id = Guid.Empty }, d with { Visita = 0 }, d with { Llegada = default },
                 d with { Campos = [] }, d with { Campos = new() { ["nombre"] = " " } } })
        {
            Assert.Throws<InvalidDataException>(() => archivo.Recibir([d, mala]));
            Assert.Empty(archivo.Bandeja());
        }
        Assert.Equal(1, archivo.Recibir([d, d]));
        Assert.Equal(0, archivo.Recibir([]));
        Assert.Equal("Pendiente", Assert.Single(archivo.Bandeja()).Estado);
        Assert.Throws<InvalidOperationException>(() => archivo.AbrirDerivacion(Guid.NewGuid(), Ana));
    }

    [Fact]
    public void CambioDeTurnoCorreccionYReapertura()
    {
        var ruta = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        try
        {
            var d = Nueva();
            using (var a = new Archivo(ruta))
            {
                a.Recibir([d]);
                var historia = a.AbrirDerivacion(d.Id, Ana);
                Assert.Equal("Maternidad", historia.Valor("origen"), ignoreCase: true);
                Assert.Equal("En atención", a.Bandeja().Single().Estado);
                Assert.Throws<InvalidOperationException>(() => a.RegistrarImpresion(historia));
                a.Guardar(Ana, historia with { Id = 0 }, null, "");
                var turno = a.AbrirDerivacion(d.Id, Eva);
                Assert.Equal(historia.Id, turno.Id);
                Assert.Single(a.Listar(Eva));
                turno.Campos["nombre"] = "Nombre revisado";
                a.Guardar(Eva, turno with { Finalizada = true }, turno, "");
                Assert.Throws<InvalidOperationException>(() => a.Guardar(Ana, historia, historia, ""));
                var final = a.Listar(Eva).Single();
                Assert.Equal(Eva.Nombre, final.Responsable.Nombre);
                Assert.Throws<InvalidOperationException>(() => a.RegistrarImpresion(historia));
                a.RegistrarImpresion(final);
                Assert.Equal("Enviada a impresión", a.Bandeja().Single().Estado);
            }
            using var reabierto = new Archivo(ruta);
            Assert.Equal(0, reabierto.Recibir([d]));
            Assert.Equal("Nombre revisado", reabierto.Bandeja().Single().Nombre);
            var ultima = reabierto.AbrirDerivacion(d.Id, Ana);
            reabierto.Guardar(Ana, ultima, ultima, "Corrección de prueba");
            Assert.False(reabierto.Bandeja().Single().Impresa);
        }
        finally { File.Delete(ruta); }
    }

    [Fact]
    public async Task ReintentaAcusePerdidoSinDuplicarNiNotificarDosVeces()
    {
        using var archivo = new Archivo(":memory:");
        var d = Nueva();
        int llamadas = 0, avisos = 0;
        using var cliente = new HttpClient(new Transporte(async req =>
        {
            llamadas++;
            if (req.RequestUri!.AbsolutePath.EndsWith("recibir_derivaciones"))
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { d }) };
            Assert.Contains(d.Id.ToString(), await req.Content!.ReadAsStringAsync());
            return new(llamadas == 2 ? HttpStatusCode.BadGateway : HttpStatusCode.NoContent);
        }))
        { BaseAddress = new("https://localhost/") };
        var enlace = new EnlaceAdmision(cliente);
        await Assert.ThrowsAsync<HttpRequestException>(() => enlace.Sincronizar(archivo, n => avisos += n, default));
        await enlace.Sincronizar(archivo, n => avisos += n, default);
        Assert.Equal(4, llamadas);
        Assert.Equal(1, avisos);
        Assert.Single(archivo.Bandeja());
    }

    [Theory]
    [InlineData("[]", false)]
    [InlineData("null", true)]
    public async Task BandejaVaciaORespuestaInvalida(string json, bool invalida)
    {
        using var archivo = new Archivo(":memory:");
        using var cliente = new HttpClient(new Transporte(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(json, Encoding.UTF8, "application/json") })))
        { BaseAddress = new("https://localhost/") };
        var tarea = new EnlaceAdmision(cliente).Sincronizar(archivo, _ => Assert.Fail("Aviso inesperado"), default);
        if (invalida) await Assert.ThrowsAsync<InvalidDataException>(() => tarea);
        else await tarea;
        Assert.Empty(archivo.Bandeja());
    }

    [Fact]
    public void RestringeDestinoYCredenciales()
    {
        const string proyecto = "abcdefghijklmnopqrst";
        Assert.Null(EnlaceAdmision.CrearCliente(null, null, null));
        Assert.Throws<ArgumentException>(() => EnlaceAdmision.CrearCliente("otro.servidor", "sb_publishable_x", "t"));
        Assert.Throws<ArgumentException>(() => EnlaceAdmision.CrearCliente(proyecto, "sb_secret_x", "t"));
        Assert.Throws<ArgumentException>(() => EnlaceAdmision.CrearCliente(proyecto, "sb_publishable_x", ""));
        using var http = EnlaceAdmision.CrearCliente(proyecto, "sb_publishable_x", "t")!;
        Assert.Equal(proyecto + ".supabase.co", http.BaseAddress!.Host);
    }

    [Fact]
    public void RenderizaHojaParaElControladorDeImpresion()
    {
        var d = Nueva();
        var historia = new Atencion(1, Ana, d.Llegada, d.Campos, true);
        var doc = new MigraDoc.Rendering.DocumentRenderer(Informe.CrearDocumento(historia));
        doc.PrepareDocument();
        using var imagen = Impresion.DibujarPagina(doc, 1);
        Assert.True(imagen.Width > 1800);
        Assert.True(imagen.Height > imagen.Width);
        // El logo del SNS está en el extremo derecho y el folio en el margen inferior.
        Assert.True(TieneTinta(imagen, 0.78, 0.02, 0.95, 0.12), "El encabezado no cubre el ancho de la hoja");
        Assert.True(TieneTinta(imagen, 0.60, 0.90, 0.96, 0.99), "El pie de página no llega al margen inferior");
        Assert.Throws<InvalidOperationException>(() => Impresion.Enviar(historia, new PrinterSettings { PrinterName = "Impresora inexistente QA" }));
    }

    private static bool TieneTinta(Bitmap imagen, double x1, double y1, double x2, double y2)
    {
        for (int y = (int)(imagen.Height * y1); y < imagen.Height * y2; y += 2)
            for (int x = (int)(imagen.Width * x1); x < imagen.Width * x2; x += 2)
                if (imagen.GetPixel(x, y).GetBrightness() < 0.65) return true;
        return false;
    }

    private sealed class Transporte(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => responder(request);
    }
}
