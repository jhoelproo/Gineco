using Xunit;

namespace Gineco.Pruebas;

public sealed partial class Ventanas
{
    [Theory]
    [InlineData("")]
    [InlineData("5")]
    [InlineData("001234567890")]
    public async Task LosDatosDestacadosMantienenElNssEditable(string nss)
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            var medico = new Medico("01", "Médico ficticio", false);
            using var editor = new Editor(archivo, medico, null);
            editor.Show();
            foreach (string clave in new[] { "nombre", "nss", "telefono", "ars" })
            {
                var campo = Assert.IsType<TextBox>(Assert.Single(editor.Controls.Find(clave, true)));
                Assert.True(campo.Font.Bold, clave);
            }

            var fecha = Assert.Single(Descendientes(editor).OfType<DateTimePicker>());
            Assert.Equal("dd/MM/yyyy hh:mm", fecha.CustomFormat);
            Assert.True(fecha.Font.Bold);
            Assert.True(fecha.Font.Size >= 12);
            var periodo = Assert.IsType<ComboBox>(Assert.Single(editor.Controls.Find("periodoAtencion", true)));
            fecha.Value = new DateTime(2026, 11, 24, 15, 30, 0);
            Assert.Equal("PM", periodo.SelectedItem);
            periodo.SelectedItem = "AM";
            Assert.Equal(new DateTime(2026, 11, 24, 3, 30, 0), fecha.Value);
            fecha.Value = new DateTime(2026, 11, 24, 0, 0, 0);
            Assert.Equal("AM", periodo.SelectedItem);
            periodo.SelectedItem = "PM";
            Assert.Equal(new DateTime(2026, 11, 24, 12, 0, 0), fecha.Value);
            Assert.IsType<TextBox>(Assert.Single(editor.Controls.Find("nombre", true))).Text = "Paciente ficticia";
            Assert.IsType<TextBox>(Assert.Single(editor.Controls.Find("nss", true))).Text = nss;
            Descendientes(editor).OfType<Button>().Single(boton => boton.Text == "Guardar borrador").PerformClick();
            var historia = Assert.Single(archivo.Listar(medico));
            Assert.Equal(nss, historia.Valor("nss"));
            Assert.Equal(fecha.Value, historia.Fecha);
        });
    }

    [Fact]
    public async Task ElPeriodoRespetaLosLimitesDelCalendario()
    {
        await EnVentana(() =>
        {
            using var entrada = new EntradaFecha();
            var selector = Assert.Single(Descendientes(entrada).OfType<DateTimePicker>());
            var periodo = Assert.Single(Descendientes(entrada).OfType<ComboBox>());
            selector.MinDate = new DateTime(2025, 1, 1, 12, 0, 0);
            selector.MaxDate = new DateTime(2027, 1, 1, 9, 0, 0);

            entrada.Value = selector.MinDate;
            periodo.SelectedItem = "AM";
            Assert.Equal(selector.MinDate, entrada.Value);
            Assert.Equal("PM", periodo.SelectedItem);

            entrada.Value = selector.MaxDate;
            periodo.SelectedItem = "PM";
            Assert.Equal(selector.MaxDate, entrada.Value);
            Assert.Equal("AM", periodo.SelectedItem);
        });
    }

    [Fact]
    public async Task ElEditorRetiraLaObservacionDelOrigenYConservaLosDatosGuardados()
    {
        await EnVentana(() =>
        {
            const string observacion = "Observación ficticia de una historia anterior";
            using var archivo = new Archivo(":memory:");
            var medico = new Medico("01", "Médico ficticio", false);
            var visita = new Atencion(0, medico, new DateTime(2026, 11, 24),
                new() { ["nombre"] = "Paciente ficticia", ["origen"] = "Maternidad", ["observaciones_origen"] = observacion });
            archivo.Guardar(medico, visita, null, "");
            using var editor = new Editor(archivo, medico, Assert.Single(archivo.Listar(medico)));
            editor.Show();
            var secciones = Assert.IsType<ListBox>(Assert.Single(editor.Controls.Find("secciones", true)));
            secciones.SelectedItem = "Origen de la enfermedad";
            Assert.Empty(editor.Controls.Find("observaciones_origen", true));
            var origen = Assert.IsType<SeleccionClinica>(Assert.Single(editor.Controls.Find("origen", true)));
            Assert.True(origen.Visible);
            Assert.Equal("Maternidad", origen.Valor);
            ElegirCasilla(editor, "origen", "Enfermedad general");
            Capturar(editor, "origen-sin-observacion");
            Descendientes(editor).OfType<Button>().Single(boton => boton.Text == "Guardar borrador").PerformClick();

            var guardada = Assert.Single(archivo.Listar(medico));
            Assert.Equal("Enfermedad general", guardada.Valor("origen"));
            Assert.Equal(observacion, guardada.Valor("observaciones_origen"));
            var sinObservacion = guardada with { Campos = new(guardada.Campos) { ["observaciones_origen"] = "" } };
            Assert.DoesNotContain(Perfiles.Revisiones(sinObservacion).SelectMany(aviso => aviso.Campos),
                campo => campo.Key == "observaciones_origen");
        });
    }

    [Fact]
    public async Task ElHistorialMuestraElEstadoVacioYLaFechaDeUnaVisitaAnterior()
    {
        await EnVentana(() =>
        {
            using var vacio = new VistaHistorias([]);
            vacio.Show();
            var detalleVacio = Assert.IsType<TextBox>(Assert.Single(vacio.Controls.Find("detalleHistoriaAnterior", true)));
            Assert.Equal("Todavía no hay historias anteriores de esta paciente.", detalleVacio.Text);

            var visita = new Atencion(1, new Medico("01", "Médico ficticio", false), new DateTime(2026, 11, 24, 13, 30, 0),
                new() { ["nombre"] = "Paciente ficticia", ["origen"] = "Maternidad", ["observaciones_origen"] = "Observación ficticia" }, true);
            using var historial = new VistaHistorias([visita]);
            historial.Show();
            var lista = Assert.Single(Descendientes(historial).OfType<ListBox>());
            Assert.Equal("24/11/2026 01:30 PM · Finalizada", lista.GetItemText(lista.Items[0]));
            Assert.Contains(Descendientes(historial).OfType<Label>(), rotulo => rotulo.Visible && rotulo.Text == visita.Nombre);
            var detalle = Assert.IsType<TextBox>(Assert.Single(historial.Controls.Find("detalleHistoriaAnterior", true)));
            Assert.Contains("Maternidad", detalle.Text);
            Assert.DoesNotContain("Observación ficticia", detalle.Text);
        });
    }

    [Fact]
    public async Task LaRecuperacionLocalConservaLaCuentaSeleccionada()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            archivo.Alta(null, "A", "Administradora ficticia", "abcdef");
            using var recuperar = new RecuperarCuenta(archivo, "M-002");
            recuperar.Show();
            var campos = Descendientes(recuperar).OfType<TextBox>().ToArray();
            Assert.Contains(campos, campo => campo.AccessibleName == "Exequátur de la cuenta" && campo.Text == "M-002");
            Assert.Contains(Descendientes(recuperar).OfType<Label>(), titulo => titulo.Visible && titulo.Text == "Restablecer contraseña");
            Capturar(recuperar, "recuperar-cuenta");
        });
    }

    [Fact]
    public async Task LaConfiguracionVaciaExplicaCadaPerfilYDesactivaLasAcciones()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            archivo.Alta(null, "A", "Administradora ficticia", "abcdef");
            var admin = Assert.IsType<Medico>(archivo.Entrar("A", "Administradora ficticia", "abcdef"));
            using var configuracion = new Configuracion(archivo, admin);
            configuracion.Show();
            var pestanas = Assert.Single(Descendientes(configuracion).OfType<TabControl>());
            var perfiles = pestanas.TabPages[0];
            var predeterminados = Assert.IsType<DataGridView>(Assert.Single(perfiles.Controls.Find("predeterminados", true)));
            Assert.DoesNotContain(predeterminados.Rows.Cast<DataGridViewRow>(), fila => fila.Tag as string == "observaciones_origen");
            var usarPropuesta = Descendientes(perfiles).OfType<Button>().Single(boton => boton.Text == "Usar propuesta como predeterminado");
            Assert.False(usarPropuesta.Enabled);
            Assert.Contains(Descendientes(perfiles).OfType<Label>(), rotulo => rotulo.Visible && rotulo.Text == "Todavía no hay propuestas para este perfil.");

            pestanas.SelectedIndex = 2;
            var medicacion = pestanas.SelectedTab!;
            var selector = Assert.Single(Descendientes(medicacion).OfType<ComboBox>());
            foreach (int indice in new[] { 0, 1 })
            {
                selector.SelectedIndex = indice;
                Assert.Contains(Descendientes(medicacion).OfType<Label>(),
                    rotulo => rotulo.Visible && rotulo.Text == "Todavía no hay sugerencias para este perfil.");
                Assert.All(Descendientes(medicacion).OfType<Button>(), boton => Assert.False(boton.Enabled));
            }

            Capturar(configuracion, "configuracion-sugerencias-vacia");
            configuracion.Size = configuracion.MinimumSize;
            configuracion.PerformLayout();
            Assert.All(Enumerable.Range(0, pestanas.TabCount),
                indice => Assert.True(pestanas.ClientRectangle.Contains(pestanas.GetTabRect(indice)), pestanas.TabPages[indice].Text));
            Capturar(configuracion, "configuracion-compacta");
        });
    }
}
