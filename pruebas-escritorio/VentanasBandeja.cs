using Xunit;

namespace Gineco.Pruebas;

public sealed partial class Ventanas
{
    [Fact]
    public async Task AbreEnPendientesYClasificaLasHistoriasSinDuplicarlas()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            var medica = new Medico("01", "Médica ficticia", false);
            var delDia = new DateTime(2026, 11, 18, 10, 30, 0);
            var pendiente = Ficticia(1);
            var abierta = Ficticia(2);
            archivo.Recibir([pendiente, abierta]);
            archivo.AbrirDerivacion(abierta.Id, medica);
            var borrador = new Atencion(0, medica, delDia, new() { ["nombre"] = "Borrador QA" });
            archivo.Guardar(medica, borrador, null, "");
            var final = borrador with { Finalizada = true, Campos = new() { ["nombre"] = "Finalizada QA" } };
            archivo.Guardar(medica, final, null, "");
            var imprimir = final with { Campos = new() { ["nombre"] = "Impresa QA" } };
            long impresa = archivo.Guardar(medica, imprimir, null, "");
            archivo.RegistrarImpresion(archivo.Listar(medica).Single(a => a.Id == impresa));
            archivo.RegistrarSincronizacion(new DateTimeOffset(2026, 11, 18, 14, 42, 0, TimeSpan.Zero));

            using var ventana = new Historias(archivo, medica);
            ventana.Show();
            var tabla = Assert.Single(Descendientes(ventana).OfType<DataGridView>());
            Assert.True(FiltroDeBandeja(ventana, "Pendientes").Checked);
            Assert.Equal("Pendientes (1)", FiltroDeBandeja(ventana, "Pendientes").Text);
            Assert.Equal("Paciente 1", Assert.Single(tabla.Rows.Cast<DataGridViewRow>()).Cells[0].Value);

            ComprobarFiltro("En atención", 2);
            ComprobarFiltro("Finalizadas", 1);
            ComprobarFiltro("Enviadas a impresión", 1);
            ComprobarFiltro("Todas", 5);
            Assert.Contains(Descendientes(ventana).OfType<Label>(), l => l.Text.EndsWith("18/11/2026 10:42"));
            Capturar(ventana, "bandeja-por-estados");

            ventana.Size = ventana.MinimumSize;
            ventana.PerformLayout();
            Capturar(ventana, "bandeja-ventana-reducida");
            Assert.All(Descendientes(ventana).OfType<RadioButton>(),
                boton => Assert.True(boton.Parent!.ClientRectangle.Contains(boton.Bounds), boton.Text));
            var primeraFila = tabla.GetRowDisplayRectangle(0, false);
            Assert.True(primeraFila.Height > 0 && tabla.ClientRectangle.Contains(primeraFila));
            Assert.True(tabla.Columns[0].Width >= 160);

            void ComprobarFiltro(string nombre, int cantidad)
            {
                var filtro = FiltroDeBandeja(ventana, nombre);
                Assert.Equal($"{nombre} ({cantidad})", filtro.Text);
                filtro.Checked = true;
                Assert.Equal(cantidad, tabla.Rows.Count);
            }
        });
    }

    private static RadioButton FiltroDeBandeja(Historias ventana, string nombre) =>
        Descendientes(ventana).OfType<RadioButton>().Single(boton => boton.Text.StartsWith(nombre + " ("));
}
