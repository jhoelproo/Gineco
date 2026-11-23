using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Gineco.Pruebas;

public sealed class RetirosCompartidos
{
    private static Derivacion Visita(Guid id) => new(id, 50, new(2026, 11, 18), new() { ["nombre"] = "Paciente ficticia" });

    [Fact]
    public async Task EnvioPendienteReintentaYLaOtraPcReconoceElRetiro()
    {
        using var origen = new Archivo(":memory:");
        using var destino = new Archivo(":memory:");
        origen.Alta(null, "A", "Administradora ficticia", "abcdef");
        var admin = Assert.IsType<Medico>(origen.Entrar("A", "Administradora ficticia", "abcdef"));
        var visita = Visita(Guid.NewGuid());
        origen.Recibir([visita]);
        destino.Recibir([visita]);
        origen.Retirar(admin, null, visita.Id);

        using var servidor = new ServidorRetiros();
        using var cliente = new HttpClient(servidor) { BaseAddress = new("https://localhost/") };
        var enlaceComun = new EnlaceAdmision(cliente);
        var enlaceAdmin = new EnlaceAdmision(cliente, "credencial-de-prueba");
        Assert.Equal((false, false), await enlaceComun.SincronizarRetiros(origen, default));
        Assert.Empty(servidor.Publicados);
        Assert.Equal((false, false), await enlaceAdmin.SincronizarRetiros(origen, default));
        Assert.Equal(visita.Id, Assert.Single(origen.RetirosPendientes()));
        servidor.Activo = true;
        servidor.Escritura = HttpStatusCode.Forbidden;
        Assert.Equal((false, false), await enlaceAdmin.SincronizarRetiros(origen, default));
        Assert.Equal(visita.Id, Assert.Single(origen.RetirosPendientes()));
        servidor.Escritura = HttpStatusCode.Unauthorized;
        Assert.Equal((false, false), await enlaceAdmin.SincronizarRetiros(origen, default));
        Assert.Equal(visita.Id, Assert.Single(origen.RetirosPendientes()));
        servidor.Escritura = HttpStatusCode.NoContent;
        Assert.Equal((false, true), await enlaceAdmin.SincronizarRetiros(origen, default));
        Assert.Empty(origen.RetirosPendientes());
        Assert.Equal((true, true), await enlaceComun.SincronizarRetiros(destino, default));
        Assert.Empty(destino.Bandeja());
        Assert.Equal(0, destino.Recibir([visita]));
        Assert.Equal((false, true), await enlaceComun.SincronizarRetiros(destino, default));
    }

    [Fact]
    public void UnRetiroInvalidoNoConfirmaElLote()
    {
        using var archivo = new Archivo(":memory:");
        var visita = Visita(Guid.NewGuid());
        archivo.Recibir([visita]);
        Assert.Throws<InvalidDataException>(() => archivo.IntegrarRetiros([new(visita.Id, 1), new(Guid.Empty, 2)], []));
        Assert.Equal(0, archivo.UltimoRetiroRecibido());
        Assert.Single(archivo.Bandeja());
    }

    private sealed class ServidorRetiros : HttpMessageHandler
    {
        internal bool Activo;
        internal HttpStatusCode Escritura = HttpStatusCode.NoContent;
        internal List<RetiroCompartido> Publicados { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancelacion)
        {
            if (!Activo) return new(HttpStatusCode.NotFound);
            if (solicitud.RequestUri!.AbsolutePath.EndsWith("registrar_retiros_consulta"))
            {
                Assert.Equal("credencial-de-prueba", Assert.Single(solicitud.Headers.GetValues("x-gineco-retiro")));
                if (Escritura != HttpStatusCode.NoContent) return new(Escritura);
                var datos = await solicitud.Content!.ReadFromJsonAsync<Dictionary<string, Guid[]>>(cancelacion);
                foreach (var id in datos!["ids"]) Publicados.Add(new(id, Publicados.Count + 1));
                return new(HttpStatusCode.NoContent);
            }
            Assert.False(solicitud.Headers.Contains("x-gineco-retiro"));
            var peticion = await solicitud.Content!.ReadFromJsonAsync<Dictionary<string, long>>(cancelacion);
            var lote = Publicados.Where(retiro => retiro.Orden > peticion!["desde"]).ToArray();
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(lote) };
        }
    }
}
