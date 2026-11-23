using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Gineco.Pruebas;

public sealed class CuentasEntrePuestos
{
    [Fact]
    public async Task LaMismaCuentaAdminAbreConfiguracionEnDosBasesSinCrearCuentaLocal()
    {
        using var transporte = new Servicio();
        using var primero = new Archivo(":memory:");
        using var segundo = new Archivo(":memory:");
        using var cuentas1 = Crear(transporte);
        using var cuentas2 = Crear(transporte);
        var sesion1 = Assert.IsType<SesionCompartida>(await cuentas1.Entrar("QA", "ficticia", default));
        var sesion2 = Assert.IsType<SesionCompartida>(await cuentas2.Entrar("qa", "ficticia", default));
        Assert.Equal(sesion1.Medico, sesion2.Medico);
        foreach (var (archivo, sesion) in new[] { (primero, sesion1), (segundo, sesion2) })
        {
            Assert.True(archivo.SinCuentas());
            archivo.EstablecerSesionCompartida(sesion.Medico);
            archivo.ExigirAdministrador(sesion.Medico);
            Assert.Throws<InvalidOperationException>(() => archivo.ExigirAdministrador(sesion.Medico with { }));
            archivo.EstablecerSesionCompartida(null);
            Assert.Throws<InvalidOperationException>(() => archivo.ExigirAdministrador(sesion.Medico));
        }
    }

    [Fact]
    public async Task ElAltaYElRestablecimientoUsanUnaSesionAdministradora()
    {
        using var transporte = new Servicio();
        using var cuentas = Crear(transporte);
        var admin = Assert.IsType<SesionCompartida>(await cuentas.Entrar("QA", "ficticia", default));
        await cuentas.Registrar("MED", "Médica ficticia", "nueva123", admin, default);
        await cuentas.Restablecer("MED", "modifica123", admin, default);
        await cuentas.Vincular(admin.Medico, "ficticia", "codigo-ficticio", default);
        Assert.Equal(new[] { "entrar", "registrar", "restablecer", "vincular" }, transporte.Acciones);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task DevuelveErroresConUnMensajeSinElCuerpoDelServidor(HttpStatusCode estado)
    {
        using var transporte = new Servicio { Estado = estado };
        using var cuentas = Crear(transporte);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => cuentas.Entrar("QA", "ficticia", default));
        Assert.DoesNotContain("detalle-privado", error.Message);
    }

    [Fact]
    public async Task RechazaCredencialesInvalidasYRespuestasConOtraIdentidad()
    {
        using var transporte = new Servicio { Estado = HttpStatusCode.Unauthorized };
        using var cuentas = Crear(transporte);
        Assert.Null(await cuentas.Entrar("QA", "ficticia", default));
        transporte.Estado = HttpStatusCode.OK;
        await Assert.ThrowsAsync<InvalidDataException>(() => cuentas.Entrar("OTRA", "ficticia", default));
    }

    [Fact]
    public void ReconoceUnaHistoriaLocalDelMismoExequaturConOtraCapitalizacion()
    {
        using var archivo = new Archivo(":memory:");
        var local = new Medico("med-001", "Médica ficticia", false);
        var remoto = local with { Exequatur = "MED-001" };
        var historia = new Atencion(0, local, new(2026, 11, 18), new() { ["nombre"] = "Paciente ficticia" });
        archivo.Guardar(local, historia, null, "");
        var guardada = Assert.Single(archivo.Listar(remoto));
        archivo.Guardar(remoto, guardada with { Finalizada = true }, guardada, "");
        Assert.True(Assert.Single(archivo.Listar(local)).Finalizada);
    }

    internal static CuentasCompartidas Crear(HttpMessageHandler servicio) => new(new HttpClient(servicio, false)
    {
        BaseAddress = new Uri("https://localhost/functions/v1/")
    });

    internal sealed class Servicio : HttpMessageHandler
    {
        internal List<string> Acciones { get; } = [];
        internal HttpStatusCode Estado = HttpStatusCode.OK;
        internal bool Administrador = true;
        internal bool SinRed;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancelacion)
        {
            if (SinRed) throw new HttpRequestException("Error de conexión simulado");
            if (Estado != HttpStatusCode.OK) return new(Estado) { Content = new StringContent("detalle-privado") };
            Assert.Equal("/functions/v1/cuentas-gineco", solicitud.RequestUri!.AbsolutePath);
            using var cuerpo = await solicitud.Content!.ReadFromJsonAsync<JsonDocument>(cancelacion);
            string accion = cuerpo!.RootElement.GetProperty("accion").GetString()!;
            Acciones.Add(accion);
            if (accion == "entrar")
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new InicioCompartido(new("QA", "Administrador ficticio", Administrador), "token-qa")) };
            var autorizacion = solicitud.Headers.Authorization;
            if (accion is "registrar" or "restablecer") Assert.Equal("token-qa", autorizacion?.Parameter);
            else Assert.Null(autorizacion);
            return new(HttpStatusCode.Created);
        }
    }
}

public sealed partial class Ventanas
{
    [Fact]
    public async Task CuentaCompartidaAbreLaBandejaSinNecesitarUnaCuentaLocal()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            using var transporte = new CuentasEntrePuestos.Servicio();
            using var cuentas = CuentasEntrePuestos.Crear(transporte);
            int paso = 0;
            using var reloj = new System.Windows.Forms.Timer { Interval = 25 };
            reloj.Tick += (_, _) =>
            {
                if (paso == 0 && Application.OpenForms.OfType<AccesoCompartido>().FirstOrDefault(f => f.Visible) is { } acceso)
                {
                    paso++;
                    Assert.DoesNotContain(Descendientes(acceso).OfType<Button>(), b => b.Text == "Usar una cuenta de esta PC");
                    EscribirCuentaCompartida(acceso, "Iniciar sesión", "ficticia");
                }
                else if (paso == 1 && Application.OpenForms.OfType<Historias>().FirstOrDefault(f => f.Visible) is { } historias)
                {
                    paso++;
                    Assert.Contains(Descendientes(historias).OfType<Button>(), boton => boton.Text == "Configuración");
                    Descendientes(historias).OfType<Button>().Single(b => b.Text == "Cambiar médico").PerformClick();
                }
                else if (paso == 2 && Application.OpenForms.OfType<AccesoCompartido>().FirstOrDefault(f => f.Visible) is { } siguiente)
                {
                    paso++;
                    siguiente.Close();
                }
            };
            reloj.Start();
            Program.IniciarCompartido(archivo, null, cuentas);
            Assert.Equal(3, paso);
            Assert.True(archivo.SinCuentas());
        });
    }

    [Fact]
    public async Task ElAccesoDeUnaPcAnteriorPuedeVolverAlFormularioLocal()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            archivo.Alta(null, "QA", "Administrador ficticio", "ficticia");
            using var transporte = new CuentasEntrePuestos.Servicio();
            using var cuentas = CuentasEntrePuestos.Crear(transporte);
            using var reloj = new System.Windows.Forms.Timer { Interval = 25 };
            bool vioAccesoLocal = false;
            reloj.Tick += (_, _) =>
            {
                if (Application.OpenForms.OfType<AccesoCompartido>().FirstOrDefault(f => f.Visible) is { } inicio)
                    Descendientes(inicio).OfType<Button>().Single(b => b.Text == "Usar una cuenta de esta PC").PerformClick();
                if (Application.OpenForms.OfType<Cuenta>().FirstOrDefault(f => f.Visible) is not { } local) return;
                vioAccesoLocal = true;
                local.Close();
            };
            reloj.Start();
            Program.IniciarCompartido(archivo, null, cuentas);
            Assert.True(vioAccesoLocal);
            Assert.Empty(transporte.Acciones);
        });
    }

    [Fact]
    public async Task VincularExigeLaClaveLocalYConservaLaIdentidad()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            archivo.Alta(null, "QA", "Administrador ficticio", "ficticia");
            using var transporte = new CuentasEntrePuestos.Servicio();
            using var cuentas = CuentasEntrePuestos.Crear(transporte);
            string vinculo = Path.GetTempFileName();
            try
            {
                File.WriteAllText(vinculo, "codigo-ficticio");
                using var acceso = new AccesoCompartido(cuentas, archivo, vinculo);
                acceso.Show();
                EscribirCuentaCompartida(acceso, "Vincular mi administrador local", "incorrecta");
                Assert.Contains(Descendientes(acceso).OfType<Label>(), etiqueta => etiqueta.Text.Contains("credenciales del administrador de esta PC"));
                Assert.Empty(transporte.Acciones);
                EscribirCuentaCompartida(acceso, "Vincular mi administrador local", "ficticia");
                EsperarHasta(() => acceso.Sesion is not null);
                Assert.True(acceso.Sesion!.Medico.Administrador);
                Assert.False(File.Exists(vinculo));
                Assert.NotNull(archivo.Entrar("QA", "Administrador ficticio", "ficticia"));
                Assert.Equal(new[] { "vincular", "entrar" }, transporte.Acciones);
            }
            finally { File.Delete(vinculo); }
        });
    }

    private static void EscribirCuentaCompartida(AccesoCompartido acceso, string accion, string clave)
    {
        acceso.Controls.Find("exequaturCompartido", true).Single().Text = "QA";
        acceso.Controls.Find("claveCompartida", true).Single().Text = clave;
        Descendientes(acceso).OfType<Button>().Single(boton => boton.Text == accion).PerformClick();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ElBuscadorSeMantieneArribaALaDerecha(bool administrador)
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            using var bandeja = new Historias(archivo, new("QA", "Médica ficticia", administrador));
            bandeja.Show();
            Comprobar("bandeja-buscador-derecha");
            bandeja.Size = bandeja.MinimumSize;
            bandeja.PerformLayout();
            Comprobar("bandeja-buscador-derecha-compacta");

            void Comprobar(string captura)
            {
                var buscar = bandeja.Controls.Find("buscarPaciente", true).Single();
                var acciones = Descendientes(bandeja).OfType<Button>().Single(boton => boton.Text == (administrador ? "Configuración" : "Cambiar médico"));
                var tabla = Descendientes(bandeja).OfType<DataGridView>().Single();
                int separacion = buscar.PointToScreen(Point.Empty).X - acciones.PointToScreen(new(acciones.Width, 0)).X;
                Assert.True(separacion > 0);
                if (administrador) Assert.InRange(separacion, 1, 30);
                Assert.True(buscar.PointToScreen(new(0, buscar.Height)).Y < tabla.PointToScreen(Point.Empty).Y);
                if (administrador) Capturar(bandeja, captura);
            }
        });
    }
}
