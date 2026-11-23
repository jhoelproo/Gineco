using System.Net;
using Xunit;

namespace Gineco.Pruebas;

public sealed partial class Ventanas
{
    [Fact]
    public async Task ElAltaCompartidaSolicitaAutorizacionYPermiteCancelar()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            using var transporte = new CuentasEntrePuestos.Servicio();
            using var cuentas = CuentasEntrePuestos.Crear(transporte);
            using var ventana = new Historias(archivo, new("QA", "Administrador ficticio", true), cuentasCompartidas: cuentas);
            ventana.Show();
            using var inicio = new AccesoCompartido(cuentas, archivo);
            inicio.Show();
            var registroInicial = Descendientes(inicio).OfType<Button>().Single(boton => boton.Text == "Registrarse");
            var registrar = Descendientes(ventana).OfType<Button>().Single(boton => boton.Text == "Registrar médico");
            using var temporizador = new System.Windows.Forms.Timer { Interval = 20 };
            bool cancelar = true;
            temporizador.Tick += (_, _) =>
            {
                if (Application.OpenForms.OfType<AccesoCompartido>().FirstOrDefault(f => f.Visible && f != inicio) is { } autorizacion)
                    EscribirCuentaCompartida(autorizacion, "Autorizar", "ficticia");
                if (Application.OpenForms.OfType<Cuenta>().FirstOrDefault(f => f.Visible) is not { } crear) return;
                temporizador.Stop();
                if (cancelar) { crear.Close(); return; }
                crear.Controls.Find("nombreCuenta", true).Single().Text = "Médica de prueba";
                crear.Controls.Find("exequaturCuenta", true).Single().Text = "MED-003";
                crear.Controls.Find("claveCuenta", true).Single().Text = "ficticia";
                Descendientes(crear).OfType<Button>().Single(boton => boton.Text == "Guardar cuenta").PerformClick();
            };
            temporizador.Start();
            registroInicial.PerformClick();
            EsperarHasta(() => registroInicial.Enabled);
            Assert.Equal("entrar", Assert.Single(transporte.Acciones));

            cancelar = false;
            temporizador.Start();
            registrar.PerformClick();
            EsperarHasta(() => Descendientes(ventana).OfType<Label>().Any(l => l.Text.Contains("Cuenta registrada")));
            Assert.Equal(new[] { "entrar", "entrar", "registrar" }, transporte.Acciones);
        });
    }

    [Fact]
    public async Task RestablecerUnaCuentaCompruebaLaConfirmacionYPermiteReintentar()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            using var transporte = new CuentasEntrePuestos.Servicio();
            using var cuentas = CuentasEntrePuestos.Crear(transporte);
            using var inicio = new AccesoCompartido(cuentas, archivo);
            inicio.Show();
            inicio.Controls.Find("exequaturCompartido", true).Single().Text = "MED-003";
            using var temporizador = new System.Windows.Forms.Timer { Interval = 20 };
            temporizador.Tick += (_, _) =>
            {
                if (Application.OpenForms.OfType<AccesoCompartido>().FirstOrDefault(f => f.Visible && f != inicio) is { } permiso)
                {
                    transporte.Administrador = false;
                    EscribirCuentaCompartida(permiso, "Autorizar", "ficticia");
                    EsperarHasta(() => Descendientes(permiso).OfType<Label>().Any(l => l.Text.StartsWith("Verifica el exequátur")));
                    transporte.Administrador = true;
                    EscribirCuentaCompartida(permiso, "Autorizar", "ficticia");
                }
                if (Application.OpenForms.OfType<RecuperarCuentaCompartida>().FirstOrDefault(f => f.Visible) is not { } recuperar) return;
                temporizador.Stop();
                var claves = Descendientes(recuperar).OfType<TextBox>().Where(campo => campo.UseSystemPasswordChar).ToArray();
                var guardar = Descendientes(recuperar).OfType<Button>().Single(boton => boton.Text == "Restablecer");
                claves[0].Text = "nueva123";
                guardar.PerformClick();
                Assert.Contains(Descendientes(recuperar).OfType<Label>(), l => l.Text.Contains("ambos campos"));
                claves[1].Text = claves[0].Text;
                transporte.Estado = HttpStatusCode.Forbidden;
                guardar.PerformClick();
                EsperarHasta(() => Descendientes(recuperar).OfType<Label>().Any(l => l.Text.Contains("Verifica la autorización")));
                transporte.SinRed = true;
                guardar.PerformClick();
                EsperarHasta(() => Descendientes(recuperar).OfType<Label>().Any(l => l.Text.Contains("Comprueba la conexión")));
                transporte.SinRed = false;
                transporte.Estado = HttpStatusCode.OK;
                guardar.PerformClick();
            };
            temporizador.Start();
            Descendientes(inicio).OfType<Button>().Single(boton => boton.Text == "Olvidé mi contraseña").PerformClick();
            Assert.Contains(Descendientes(inicio).OfType<Label>(), l => l.Text.Contains("Contraseña actualizada"));
            Assert.Equal(new[] { "entrar", "entrar", "restablecer" }, transporte.Acciones);
        });
    }

    [Fact]
    public async Task ElAccesoConservaLaOpcionLocalYMuestraFallosDelServicio()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            archivo.Alta(null, "QA", "Administrador ficticio", "ficticia");
            using var transporte = new CuentasEntrePuestos.Servicio { Estado = HttpStatusCode.Unauthorized };
            using var cuentas = CuentasEntrePuestos.Crear(transporte);
            using var inicio = new AccesoCompartido(cuentas, archivo);
            inicio.Show();
            EscribirCuentaCompartida(inicio, "Iniciar sesión", "ficticia");
            EsperarHasta(() => Descendientes(inicio).OfType<Label>().Any(l => l.Text.StartsWith("Verifica el exequátur")));
            transporte.Estado = HttpStatusCode.TooManyRequests;
            EscribirCuentaCompartida(inicio, "Iniciar sesión", "ficticia");
            EsperarHasta(() => Descendientes(inicio).OfType<Label>().Any(l => l.Text.StartsWith("Hubo varios intentos")));
            transporte.SinRed = true;
            EscribirCuentaCompartida(inicio, "Iniciar sesión", "ficticia");
            EsperarHasta(() => Descendientes(inicio).OfType<Label>().Any(l => l.Text.StartsWith("No se pudo completar")));
            Descendientes(inicio).OfType<Button>().Single(boton => boton.Text == "Usar una cuenta de esta PC").PerformClick();
            Assert.True(inicio.UsarCuentaLocal);
        });
    }
}
