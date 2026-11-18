using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Gineco.Pruebas;

public sealed class Listado
{
    private static readonly Medico Medica = new("01", "Médica de prueba", false);
    private static Derivacion Visita(long numero) => new(Guid.NewGuid(), numero, new(2026, 1, 1),
        new() { ["nombre"] = "Paciente ficticia", ["nss"] = "001-77", ["telefono"] = "809" });

    [Fact]
    public void LosCambiosDelMedicoActualizanOtraBandejaSinMezclarReingresos()
    {
        using var consulta = new Archivo(":memory:");
        using var otra = new Archivo(":memory:");
        var original = Visita(15);
        var reingreso = Visita(16);
        consulta.Recibir([original, reingreso]);
        var abierta = consulta.AbrirDerivacion(original.Id, Medica);
        var enviado = Assert.Single(consulta.CambiosPendientes());
        Assert.Equal(EstadoConsulta.EnAtencion, enviado.Estado);
        abierta.Campos["nombre"] = "Nombre corregido";
        abierta.Campos["nss"] = "000-0123";
        consulta.Guardar(Medica, abierta with { Finalizada = true }, abierta, "");
        Assert.Equal("000-0123", Assert.Single(consulta.Listar(Medica)).Valor("nss"));
        consulta.IntegrarCambios([], [enviado]);
        var pendiente = Assert.Single(consulta.CambiosPendientes());
        Assert.NotEqual(enviado.Evento, pendiente.Evento);
        var desdeServidor = pendiente with { Orden = 2, Visita = original.Visita, Llegada = original.Llegada };
        otra.Recibir([reingreso]);
        Assert.True(otra.IntegrarCambios([desdeServidor], []));
        Assert.False(otra.IntegrarCambios([desdeServidor with { Orden = 1, Nombre = "Anterior" }], []));
        var remota = otra.Bandeja().Single(fila => fila.Id == original.Id);
        Assert.Equal("Nombre corregido", remota.Nombre);
        Assert.Equal("Finalizada", remota.Estado);
        Assert.True(remota.SoloRemota);
        Assert.Equal(2L, otra.UltimoCambioRecibido());
        Assert.Throws<InvalidOperationException>(() => otra.AbrirDerivacion(original.Id, Medica));
        Assert.Equal("Pendiente", otra.Bandeja().Single(fila => fila.Id == reingreso.Id).Estado);
    }

    [Fact]
    public void SoloElAdministradorRetiraUnaAtencionYAseguraQueSigaRetirada()
    {
        using var consulta = new Archivo(":memory:");
        using var otra = new Archivo(":memory:");
        consulta.Alta(null, "001", "Administradora ficticia", "Clave ficticia 2026");
        var admin = Assert.IsType<Medico>(consulta.Entrar("001", "Administradora ficticia", "Clave ficticia 2026"));
        var derivacion = Visita(10);
        var reingreso = Visita(11);
        consulta.Recibir([derivacion, reingreso]);
        var abierta = consulta.AbrirDerivacion(derivacion.Id, Medica);
        Assert.Throws<InvalidOperationException>(() => consulta.Retirar(Medica, abierta.Id, derivacion.Id));
        consulta.Retirar(admin, abierta.Id, derivacion.Id);
        Assert.Empty(consulta.Listar(admin));
        Assert.Throws<InvalidOperationException>(() => consulta.Guardar(Medica, abierta, abierta, ""));
        Assert.Throws<InvalidOperationException>(() => consulta.AbrirDerivacion(derivacion.Id, Medica));
        Assert.Throws<InvalidOperationException>(() => consulta.Retirar(admin, abierta.Id, derivacion.Id));
        Assert.Equal(reingreso.Id, Assert.Single(consulta.Bandeja()).Id);

        Assert.Empty(consulta.CambiosPendientes());
        otra.Recibir([derivacion, reingreso]);
        Assert.Equal(2, otra.Bandeja().Count);
        consulta.Recibir([derivacion]);
        Assert.Equal(reingreso.Id, Assert.Single(consulta.Bandeja()).Id);

        long independiente = consulta.Guardar(Medica, new(0, Medica, derivacion.Llegada, new() { ["nombre"] = "Ficticia" }), null, "");
        consulta.Retirar(admin, independiente, null);
        Assert.Empty(consulta.Listar(admin));
        Assert.Throws<InvalidOperationException>(() => consulta.Retirar(admin, independiente, null));
        Assert.Throws<ArgumentException>(() => consulta.Retirar(admin, null, null));
    }

    [Fact]
    public async Task ColaPersistenteConServidorAnteriorYReintento()
    {
        using var archivo = new Archivo(":memory:");
        var derivacion = Visita(31);
        archivo.Recibir([derivacion]);
        archivo.AbrirDerivacion(derivacion.Id, Medica);
        var salida = Assert.Single(archivo.CambiosPendientes());
        using var servidor = new Servidor(salida with { Orden = 1, Visita = derivacion.Visita, Llegada = derivacion.Llegada });
        using var cliente = new HttpClient(servidor) { BaseAddress = new("https://localhost/") };
        var enlace = new EnlaceAdmision(cliente);
        Assert.Equal((false, false), await enlace.SincronizarListado(archivo, default));
        Assert.Equal(salida.Evento, Assert.Single(archivo.CambiosPendientes()).Evento);
        servidor.Activo = true;
        Assert.Equal((true, true), await enlace.SincronizarListado(archivo, default));
        Assert.Empty(archivo.CambiosPendientes());
        servidor.ServirCambios = false;
        Assert.Equal((true, false), await enlace.SincronizarListado(archivo, default));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.OK)]
    public async Task LaLecturaDelListadoDistingueFaltaDeServidorPermisoYRespuestaInvalida(HttpStatusCode estado)
    {
        using var archivo = new Archivo(":memory:");
        using var cliente = new HttpClient(new RespuestaVacia(estado)) { BaseAddress = new("https://localhost/") };
        var enlace = new EnlaceAdmision(cliente);
        if (estado == HttpStatusCode.NotFound)
            Assert.Equal((false, false), await enlace.SincronizarListado(archivo, default));
        else if (estado == HttpStatusCode.Forbidden)
            await Assert.ThrowsAsync<HttpRequestException>(() => enlace.SincronizarListado(archivo, default));
        else
            await Assert.ThrowsAsync<InvalidDataException>(() => enlace.SincronizarListado(archivo, default));
        Assert.Empty(archivo.Bandeja());
    }

    [Fact]
    public void UnLoteInvalidoRevierteElCursor()
    {
        using var archivo = new Archivo(":memory:");
        var visita = Visita(10);
        var valida = new CambioConsulta(Guid.NewGuid(), visita.Id, "Ficticia", "", EstadoConsulta.EnAtencion, 1, visita.Visita, visita.Llegada);
        Assert.Throws<InvalidDataException>(() => archivo.IntegrarCambios([valida, valida with { Visita = 0, Orden = 2 }], []));
        Assert.Equal(0, archivo.UltimoCambioRecibido());
        Assert.Empty(archivo.Bandeja());
    }

    private sealed class Servidor(CambioConsulta cambio) : HttpMessageHandler
    {
        internal bool Activo { get; set; }
        internal bool ServirCambios { get; set; } = true;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            if (!Activo) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            if (req.RequestUri!.AbsolutePath.EndsWith("registrar_cambios_consulta"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = JsonContent.Create(ServirCambios ? new[] { cambio } : Array.Empty<CambioConsulta>()) });
        }
    }

    private sealed class RespuestaVacia(HttpStatusCode estado) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken cancelacion) =>
            Task.FromResult(new HttpResponseMessage(estado) { Content = JsonContent.Create<object?>(null) });
    }
}
