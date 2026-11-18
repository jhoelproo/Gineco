using System.Security.Cryptography;
using System.Text.Json;
using Velopack;
using Velopack.Exceptions;
using Velopack.Locators;
using Velopack.Sources;
using Xunit;

namespace Gineco.Pruebas;

public sealed class PaquetesDeActualizacion
{
    [Fact]
    public void ElEjecutableDeDesarrolloInformaSuVersionSinConfundirlaConUnaInstalacion()
    {
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        var servicio = new Actualizaciones();
        Assert.False(servicio.Instalada);
        Assert.True(Version.TryParse(servicio.Version.Split('-')[0], out _));
        Assert.DoesNotContain('+', servicio.Version);
    }

    [Fact]
    public async Task LeeElCanalAceptaUnaVersionNuevaYRechazaUnPaqueteCorrupto()
    {
        var carpeta = Directory.CreateTempSubdirectory();
        try
        {
            var origen = carpeta.CreateSubdirectory("release");
            var paquetes = carpeta.CreateSubdirectory("paquetes");
            var locator = new TestVelopackLocator("GinecoQA", "0.1.0-beta.3", paquetes.FullName);
            var servicio = new Actualizaciones(() => new UpdateManager(new SimpleFileSource(origen), locator: locator));
            Assert.True(servicio.Instalada);
            Assert.Throws<InvalidOperationException>(servicio.AplicarAlCerrar);

            string rutaFeed = Path.Combine(origen.FullName, "releases.win.json");
            File.WriteAllText(rutaFeed, """{"Assets":[]}""");
            await servicio.Buscar();
            Assert.Null(servicio.Disponible);

            const string nombre = "GinecoQA-0.1.0-beta.4-full.nupkg";
            string rutaPaquete = Path.Combine(origen.FullName, nombre);
            byte[] contenido = new byte[32];
            await File.WriteAllBytesAsync(rutaPaquete, contenido);
            var asset = new
            {
                PackageId = "GinecoQA",
                Version = "0.1.0-beta.4",
                Type = "Full",
                FileName = nombre,
                SHA256 = Convert.ToHexString(SHA256.HashData(contenido)),
                Size = contenido.Length
            };
            File.WriteAllText(rutaFeed, JsonSerializer.Serialize(new { Assets = new[] { asset } }));
            await servicio.Buscar();
            Assert.Equal("0.1.0-beta.4", servicio.Disponible);
            await File.WriteAllBytesAsync(rutaPaquete, new byte[16]);
            await Assert.ThrowsAsync<ChecksumFailedException>(() => servicio.Descargar(_ => { }, default));
            Assert.False(File.Exists(Path.Combine(paquetes.FullName, nombre)));
        }
        finally { carpeta.Delete(recursive: true); }
    }
}

public sealed partial class Ventanas
{
    [Fact]
    public async Task AlAbrirLaBandejaSeAnunciaLaVersionDisponible()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            var actualizaciones = new ActualizacionesDePrueba();
            using var ventana = new Historias(archivo, new("01", "Médica de prueba", false), actualizaciones: actualizaciones);
            ventana.Show();
            EsperarHasta(() => Descendientes(ventana).OfType<Button>().Any(b => b.Text == "Actualización disponible"));
            Assert.Equal(1, actualizaciones.Consultas);
        });
    }

    [Fact]
    public async Task LaDescargaRequiereConfirmacionYPermiteReintentar()
    {
        await EnVentana(() =>
        {
            var actualizaciones = new ActualizacionesDePrueba { FallaDescarga = true };
            using var ventana = new VentanaActualizaciones(actualizaciones);
            ventana.Show();
            Button Boton(string texto) => Descendientes(ventana).OfType<Button>().Single(b => b.Text == texto);
            var instalar = Boton("Instalar y reiniciar");
            Assert.False(instalar.Enabled);
            Assert.Empty(Descendientes(ventana).OfType<TextBox>());
            Boton("Buscar ahora").PerformClick();
            EsperarHasta(() => instalar.Enabled);
            Assert.Equal(0, actualizaciones.Descargas);
            instalar.PerformClick();
            EsperarHasta(() => Descendientes(ventana).OfType<Label>().Any(l => l.Text.StartsWith("No se pudo instalar")));
            Assert.False(actualizaciones.Aplicada);
            actualizaciones.FallaDescarga = false;
            instalar.PerformClick();
            EsperarHasta(() => ventana.DialogResult == DialogResult.OK);
            Assert.True(actualizaciones.Aplicada);
            Assert.Equal(2, actualizaciones.Descargas);
        });
    }

    [Fact]
    public async Task UnaInstalacionPortableIndicaComoHabilitarLasActualizaciones()
    {
        await EnVentana(() =>
        {
            var actualizaciones = new ActualizacionesDePrueba { Instalada = false };
            using var archivo = new Archivo(":memory:");
            using var bandeja = new Historias(archivo, new("01", "Médica de prueba", false), actualizaciones: actualizaciones);
            bandeja.Show();
            using var ventana = new VentanaActualizaciones(actualizaciones);
            ventana.Show();
            var botones = Descendientes(ventana).OfType<Button>().ToList();
            Assert.False(botones.Single(b => b.Text == "Buscar ahora").Enabled);
            Assert.False(botones.Single(b => b.Text == "Instalar y reiniciar").Enabled);
            Assert.True(botones.Single(b => b.Text == "Abrir GitHub").Enabled);
            Assert.Equal(0, actualizaciones.Consultas);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaBandejaSoloSeCierraCuandoSeOrdenaInstalar(bool instalar)
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            var actualizaciones = new ActualizacionesDePrueba();
            using var bandeja = new Historias(archivo, new("01", "Médica de prueba", false), actualizaciones: actualizaciones);
            bandeja.Show();
            EsperarHasta(() => Descendientes(bandeja).OfType<Button>().Any(b => b.Text == "Actualización disponible"));
            var boton = Descendientes(bandeja).OfType<Button>().Single(b => b.Text == "Actualización disponible");
            using var decidir = new System.Windows.Forms.Timer { Interval = 10 };
            decidir.Tick += (_, _) =>
            {
                if (Application.OpenForms.OfType<VentanaActualizaciones>().FirstOrDefault() is not { } dialogo) return;
                decidir.Stop();
                var nombre = instalar ? "Instalar y reiniciar" : "Cerrar";
                Descendientes(dialogo).OfType<Button>().Single(b => b.Text == nombre).PerformClick();
            };
            decidir.Start();
            boton.PerformClick();
            Assert.Equal(instalar, actualizaciones.Aplicada);
            Assert.Equal(instalar, bandeja.IsDisposed);
        });
    }

    [Fact]
    public async Task PermiteRepetirLaConsultaSinTokenDespuesDeUnFallo()
    {
        await EnVentana(() =>
        {
            var actualizaciones = new ActualizacionesDePrueba { FallaConsulta = true };
            using var ventana = new VentanaActualizaciones(actualizaciones);
            ventana.Show();
            Assert.Empty(Descendientes(ventana).OfType<TextBox>());
            var buscar = Descendientes(ventana).OfType<Button>().Single(b => b.Text == "Buscar ahora");
            buscar.PerformClick();
            EsperarHasta(() => buscar.Enabled);
            Assert.Contains(Descendientes(ventana).OfType<Label>(), l => l.Text.StartsWith("No se pudo consultar"));
            actualizaciones.FallaConsulta = false;
            actualizaciones.HayNovedad = false;
            buscar.PerformClick();
            EsperarHasta(() => Descendientes(ventana).OfType<Label>().Any(l => l.Text == "Ya tienes la última versión."));
        });
    }

    [Fact]
    public async Task CerrarMientrasDescargaCancelaLaActualizacion()
    {
        await EnVentana(() =>
        {
            var descarga = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var actualizaciones = new ActualizacionesDePrueba { DescargaControlada = descarga };
            using var ventana = new VentanaActualizaciones(actualizaciones);
            ventana.Show();
            var botones = Descendientes(ventana).OfType<Button>().ToList();
            botones.Single(b => b.Text == "Buscar ahora").PerformClick();
            var instalar = botones.Single(b => b.Text == "Instalar y reiniciar");
            EsperarHasta(() => instalar.Enabled);
            instalar.PerformClick();
            ventana.Close();
            Assert.True(actualizaciones.Cancelacion.IsCancellationRequested);
            descarga.TrySetResult();
            Application.DoEvents();
            Assert.False(actualizaciones.Aplicada);
        });
    }

    private sealed class ActualizacionesDePrueba : IActualizaciones
    {
        public string Version => "0.1.0-beta.3";
        public bool Instalada { get; init; } = true;
        public string? Disponible { get; private set; }
        public int Consultas { get; private set; }
        public int Descargas { get; private set; }
        public bool FallaDescarga { get; set; }
        public bool FallaConsulta { get; set; }
        public bool HayNovedad { get; set; } = true;
        public TaskCompletionSource? DescargaControlada { get; init; }
        public CancellationToken Cancelacion { get; private set; }
        public bool Aplicada { get; private set; }
        public Task Buscar()
        {
            Consultas++;
            if (FallaConsulta) return Task.FromException(new HttpRequestException());
            Disponible = HayNovedad ? "0.1.0-beta.4" : null;
            return Task.CompletedTask;
        }
        public Task Descargar(Action<int> progreso, CancellationToken cancelacion)
        {
            Descargas++;
            Cancelacion = cancelacion;
            if (DescargaControlada is not null) return DescargaControlada.Task.WaitAsync(cancelacion);
            return FallaDescarga ? Task.FromException(new IOException()) : Task.CompletedTask;
        }
        public void AplicarAlCerrar() => Aplicada = true;
    }
}
