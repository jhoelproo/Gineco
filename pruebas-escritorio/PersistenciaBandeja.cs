using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Gineco.Pruebas;

public sealed class PersistenciaBandeja
{
    [Fact]
    public void LaHoraDeSincronizacionSobreviveAlCierre()
    {
        string ruta = Path.GetTempFileName();
        try
        {
            var primera = new DateTimeOffset(2026, 11, 18, 14, 42, 0, TimeSpan.Zero);
            var reciente = primera.AddMinutes(8);
            using (var archivo = new Archivo(ruta))
            {
                Assert.Null(archivo.UltimaSincronizacion());
                archivo.RegistrarSincronizacion(primera);
                Assert.Equal(primera, archivo.UltimaSincronizacion());
                archivo.RegistrarSincronizacion(reciente);
            }
            using var reabierto = new Archivo(ruta);
            Assert.Equal(reciente, reabierto.UltimaSincronizacion());
            Assert.Equal(new DateTime(2026, 11, 18, 10, 50, 0), Clinica.HoraHospital(reciente));
        }
        finally { File.Delete(ruta); }
    }

    [Fact]
    public void LaImpresionLocalCorrespondeALaVersionGuardada()
    {
        using var archivo = new Archivo(":memory:");
        var medica = new Medico("01", "Médica ficticia", false);
        archivo.Guardar(medica, new(0, medica, new DateTime(2026, 11, 18), new() { ["nombre"] = "Paciente QA" }), null, "");
        var borrador = Assert.Single(archivo.Listar(medica));
        Assert.Throws<InvalidOperationException>(() => archivo.RegistrarImpresion(borrador));
        archivo.Guardar(medica, borrador with { Finalizada = true }, borrador, "");
        var finalizada = Assert.Single(archivo.Listar(medica));
        archivo.RegistrarImpresion(finalizada);
        archivo.RegistrarImpresion(finalizada);
        Assert.True(Assert.Single(archivo.Listar(medica)).Impresa);
        archivo.Guardar(medica, finalizada, finalizada, "Corrección de prueba");
        Assert.False(Assert.Single(archivo.Listar(medica)).Impresa);
        Assert.Throws<InvalidOperationException>(() => archivo.RegistrarImpresion(finalizada));
        Assert.Empty(archivo.CambiosPendientes());
    }

    [Theory]
    [InlineData(99, true)]
    [InlineData(100, false)]
    public async Task ElLimiteDelLoteIndicaSiAunPuedeHaberCambiosEnElServidor(int cantidad, bool alDia)
    {
        var cambios = Enumerable.Range(1, cantidad).Select(numero => new CambioConsulta(
            Guid.NewGuid(), Guid.NewGuid(), $"Paciente ficticia {numero}", "", EstadoConsulta.EnAtencion,
            numero, numero, new DateTime(2026, 11, 18))).ToArray();
        using var archivo = new Archivo(":memory:");
        using var cliente = new HttpClient(new RespuestaDeListado(cambios)) { BaseAddress = new("https://localhost/") };
        var enlace = new EnlaceAdmision(cliente);
        Assert.Equal((true, true, alDia), await enlace.SincronizarListado(archivo, default));
        Assert.Equal(cantidad, archivo.Bandeja().Count);
    }

    private sealed class RespuestaDeListado(CambioConsulta[] cambios) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancelacion) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(cambios) });
    }
}
