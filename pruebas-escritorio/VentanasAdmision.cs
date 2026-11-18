using System.Diagnostics;
using System.Drawing.Printing;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Gineco.Pruebas;

public sealed partial class Ventanas
{
    private sealed class BandejaDePrueba : HttpMessageHandler
    {
        internal List<Derivacion> Pendientes = [];
        internal HttpStatusCode Estado = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("leer_cambios_consulta"))
                return Task.FromResult(new HttpResponseMessage(Estado) { Content = JsonContent.Create(Array.Empty<CambioConsulta>()) });
            if (request.RequestUri!.AbsolutePath.EndsWith("confirmar_recepcion")) Pendientes.Clear();
            return Task.FromResult(new HttpResponseMessage(Estado) { Content = JsonContent.Create(Pendientes.ToArray()) });
        }
    }

    private static Derivacion Ficticia(long numero) => new(Guid.NewGuid(), numero, new DateTime(2026, 1, 1).AddMinutes(numero),
        new() { ["nombre"] = $"Paciente {numero}", ["cedula"] = "00100000011" });

    private static void EsperarHasta(Func<bool> condicion)
    {
        var reloj = Stopwatch.StartNew();
        while (!condicion() && reloj.Elapsed < TimeSpan.FromSeconds(5)) Application.DoEvents();
        Assert.True(condicion());
    }

    [Fact]
    public async Task LlegadaDuranteLaConsultaConservaSeleccionYAvisaUnaVez()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            var primera = Ficticia(1);
            using var transporte = new BandejaDePrueba { Pendientes = [primera] };
            using var ventana = new Historias(archivo, new("QA", "Médica de prueba", false),
                () => new HttpClient(transporte) { BaseAddress = new("https://localhost/") });
            ventana.Show();
            bool TieneTexto(string t) => Descendientes(ventana).OfType<Label>().Any(l => l.Text == t);
            EsperarHasta(() => archivo.UltimaSincronizacion().HasValue);
            Assert.Contains(Descendientes(ventana).OfType<Label>(),
                l => l.Text.StartsWith("Admisión conectada · Última sincronización"));
            Assert.True(TieneTexto("Nueva paciente de Admisión"));
            var lista = Assert.Single(Descendientes(ventana).OfType<DataGridView>());
            Assert.Equal("Pendiente", lista.CurrentRow!.Cells[4].FormattedValue);
            var siguiente = Ficticia(2);
            transporte.Pendientes = [siguiente, Ficticia(3)];
            var recibir = ventana.Recibir();
            EsperarHasta(() => recibir.IsCompleted);
            recibir.GetAwaiter().GetResult();
            Assert.Equal(3, lista.Rows.Count);
            Assert.Equal("Paciente 1", lista.CurrentRow!.Cells[0].Value);
            Assert.True(TieneTexto("2 pacientes de Admisión"));
            using var abrir = new System.Windows.Forms.Timer { Interval = 20 };
            abrir.Tick += (_, _) =>
            {
                if (Application.OpenForms.OfType<Editor>().FirstOrDefault() is not { } historia) return;
                abrir.Stop();
                historia.Close();
            };
            abrir.Start();
            Descendientes(ventana).OfType<Button>().Single(b => b.Text == "Abrir historia").PerformClick();
            Assert.Equal(2, lista.Rows.Count);
            FiltroDeBandeja(ventana, "En atención").Checked = true;
            Assert.Equal("En atención", lista.CurrentRow!.Cells[4].FormattedValue);
            Assert.Equal("Paciente 1", lista.CurrentRow.Cells[0].Value);
            FiltroDeBandeja(ventana, "Todas").Checked = true;
            var buscar = Descendientes(ventana).OfType<TextBox>().Single();
            buscar.Text = "001-0000001-1";
            Assert.Equal(3, lista.Rows.Count);
            buscar.Text = "inexistente";
            Assert.True(TieneTexto("No se encontraron pacientes"));
            var marca = archivo.UltimaSincronizacion()!.Value;
            buscar.Clear();
            transporte.Estado = HttpStatusCode.ServiceUnavailable;
            var intento = ventana.Recibir();
            EsperarHasta(() => intento.IsCompleted);
            Assert.True(TieneTexto($"Admisión sin conexión · Reintentando · Última sincronización {Clinica.HoraHospital(marca):dd/MM/yyyy HH:mm}"));
            Assert.Equal(marca, archivo.UltimaSincronizacion());
            Assert.Equal(3, lista.Rows.Count);
            Assert.Equal(3, archivo.Bandeja().Count);
            transporte.Estado = HttpStatusCode.Unauthorized;
            var sinVinculo = ventana.Recibir();
            EsperarHasta(() => sinVinculo.IsCompleted);
            Assert.Contains(Descendientes(ventana).OfType<Label>(),
                l => l.Text.StartsWith("Admisión requiere vincular esta PC de consulta · Última sincronización"));
            ventana.Close();
        });
    }

    [Fact]
    public async Task ConfiguracionIncompletaPermiteAbrirElArchivoLocal()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            using var ventana = new Historias(archivo, new("QA", "Prueba", false),
                () => throw new ArgumentException("Falta configurar la conexión"));
            ventana.Show();
            Assert.Contains(Descendientes(ventana).OfType<Label>(), l => l.Text == "Falta configurar la conexión");
            Assert.True(ventana.Recibir().IsCompletedSuccessfully);
        });
    }

    private sealed class ImpresoraVirtualFactAttribute : FactAttribute
    {
        public ImpresoraVirtualFactAttribute()
        {
            if (!new PrinterSettings { PrinterName = "Microsoft Print to PDF" }.IsValid)
                Skip = "La prueba de integración requiere el controlador Microsoft Print to PDF.";
        }
    }

    [ImpresoraVirtualFact]
    public async Task FinalizarYConfirmarImprimeLaHistoriaVinculada()
    {
        var salida = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        try
        {
            await EnVentana(() =>
            {
                using var archivo = new Archivo(":memory:");
                archivo.Recibir([Ficticia(1)]);
                var impresora = new PrinterSettings { PrinterName = "Microsoft Print to PDF", PrintToFile = true, PrintFileName = salida };
                using var ventana = new Historias(archivo, new("01", "Médica ficticia", false), impresora: impresora);
                ventana.Show();
                int revisiones = 0;
                using var dialogos = new System.Windows.Forms.Timer { Interval = 30 };
                dialogos.Tick += (_, _) =>
                {
                    if (Application.OpenForms.OfType<RevisionExportacion>().FirstOrDefault() is { } revision)
                    {
                        revisiones++;
                        Descendientes(revision).OfType<Button>().Single(b => b.Text == "Confirmar e imprimir").PerformClick();
                    }
                    else if (Application.OpenForms.OfType<Editor>().FirstOrDefault() is { } editor)
                        Descendientes(editor).OfType<Button>().Single(b => b.Text == "Finalizar e imprimir").PerformClick();
                };
                dialogos.Start();
                Descendientes(ventana).OfType<Button>().Single(b => b.Text == "Abrir historia").PerformClick();
                Assert.Equal(1, revisiones);
                Assert.Equal("Enviada a impresión", archivo.Bandeja().Single().Estado);
                FiltroDeBandeja(ventana, "Enviadas a impresión").Checked = true;
                Assert.Equal("Enviada a impresión", Descendientes(ventana).OfType<DataGridView>().Single().CurrentRow!.Cells[4].FormattedValue);
                using var documento = PdfSharp.Pdf.IO.PdfReader.Open(salida, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
                Assert.Equal(1, documento.PageCount);
                Assert.Equal(612, documento.Pages[0].Width.Point, 1);
                Assert.Equal(792, documento.Pages[0].Height.Point, 1);
            });
        }
        finally { File.Delete(salida); }
    }
}
