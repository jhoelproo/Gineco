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
            Assert.True(tabla.Columns[2].DefaultCellStyle.Font?.Bold);
            var buscar = Assert.Single(Descendientes(ventana).OfType<TextBox>());
            buscar.Text = "Finalizada QA";
            Assert.Empty(tabla.Rows.Cast<DataGridViewRow>());
            Assert.Contains(Descendientes(ventana).OfType<Label>(), rotulo => rotulo.Visible && rotulo.Text == "Sin coincidencias en Pendientes");
            var ampliar = Descendientes(ventana).OfType<Button>().Single(boton => boton.Text == "Buscar en todas");
            Assert.True(ampliar.Visible);
            ampliar.PerformClick();
            Assert.True(FiltroDeBandeja(ventana, "Todas").Checked);
            Assert.Equal("Finalizada QA", Assert.Single(tabla.Rows.Cast<DataGridViewRow>()).Cells[0].Value);
            Assert.False(ampliar.Visible);
            buscar.Clear();

            ComprobarFiltro("En atención", 2);
            ComprobarFiltro("Finalizadas", 1);
            ComprobarFiltro("Enviadas a impresión", 1);
            ComprobarFiltro("Todas", 5);
            Assert.Contains(Descendientes(ventana).OfType<Label>(), l => l.Text.EndsWith("18/11/2026 10:42 AM"));
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

    [Fact]
    public async Task CambioDeMedicoSolicitaUnaNuevaSesionYActualizaLosPermisos()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            archivo.Alta(null, "A", "Administradora ficticia", "abcdef");
            var admin = Assert.IsType<Medico>(archivo.Entrar("A", "Administradora ficticia", "abcdef"));
            archivo.Alta(admin, "M", "Médica ficticia", "abcdef");
            int paso = 0;
            using var timer = new System.Windows.Forms.Timer { Interval = 25 };
            timer.Tick += (_, _) =>
            {
                if (paso is 0 or 2 && Application.OpenForms.OfType<Cuenta>().FirstOrDefault(ventana => ventana.Visible) is { } acceso)
                {
                    Assert.Empty(acceso.Clave);
                    Entrar(acceso, paso == 0 ? "A" : "M", paso == 0 ? "Administradora ficticia" : "Médica ficticia");
                    paso++;
                }
                else if (Application.OpenForms.OfType<Historias>().FirstOrDefault(ventana => ventana.Visible) is { } historias)
                {
                    var botones = Descendientes(historias).OfType<Button>().ToArray();
                    Assert.Equal(paso == 1, botones.Any(b => b.Text == "Configuración"));
                    if (paso++ == 1) botones.Single(b => b.Text == "Cambiar médico").PerformClick();
                    else historias.Close();
                }
            };
            timer.Start();
            Program.Iniciar(archivo, null);
            Assert.Equal(4, paso);
        });
    }

    private static void Entrar(Cuenta ventana, string exequatur, string nombre)
    {
        ventana.Controls.Find("nombreCuenta", true).Single().Text = nombre;
        ventana.Controls.Find("exequaturCuenta", true).Single().Text = exequatur;
        ventana.Controls.Find("claveCuenta", true).Single().Text = "abcdef";
        Descendientes(ventana).OfType<Button>().Single(boton => boton.Text == "Continuar").PerformClick();
    }

    [Fact]
    public async Task ElAvisoPermiteEnfocarElCampoDesdeLaNavegacionCompacta()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            var medico = new Medico("M", "Médica ficticia", false);
            var historia = new Atencion(0, medico, new(2026, 11, 18), new()
            {
                ["nombre"] = "Paciente ficticia",
                ["motivo"] = "pediatria"
            });
            using var revision = new RevisionExportacion(archivo.PrepararExportacion(historia));
            revision.Show();
            var arbol = Assert.Single(Descendientes(revision).OfType<TreeView>());
            var correcciones = Assert.Single(arbol.Nodes.Cast<TreeNode>(), nodo => nodo.Text == "Palabras corregidas en el PDF");
            Assert.Equal("pediatria → pediatría", Assert.Single(correcciones.Nodes.Cast<TreeNode>()).Text);
            arbol.SelectedNode = arbol.Nodes.Cast<TreeNode>().SelectMany(nodo => nodo.Nodes.Cast<TreeNode>())
                .Single(nodo => (string)nodo.Tag! == "diagnosticos");
            arbol.SelectedNode.EnsureVisible();
            Descendientes(revision).OfType<Button>().Single(boton => boton.Text == "Ir al campo").PerformClick();
            Assert.Equal(DialogResult.Retry, revision.DialogResult);
            Assert.Equal("diagnosticos", revision.CampoParaRevisar);

            using var editor = new Editor(archivo, medico, historia);
            editor.Show();
            editor.Size = editor.MinimumSize;
            var secciones = Assert.IsType<ComboBox>(editor.Controls.Find("seccionesVentanaReducida", true).Single());
            Assert.True(secciones.Visible);
            editor.EnfocarCampo(revision.CampoParaRevisar!);
            Assert.Equal("Diagnóstico", secciones.SelectedItem);
            Assert.True(editor.Controls.Find("diagnosticos", true).Single().ContainsFocus);
            secciones.SelectedIndex = 3;
            Assert.True(editor.Controls.Find("ta", true).Single().Visible);
            Capturar(editor, "navegacion-compacta");
            editor.EnfocarCampo("pruebas");
            Assert.Equal("Pruebas de laboratorio", secciones.SelectedItem);
            editor.EnfocarCampo("perfilHistoria");
            Assert.True(editor.Controls.Find("perfilHistoria", true).Single().ContainsFocus);
            editor.EnfocarCampo("campo-desconocido");
            Assert.Equal("Pruebas de laboratorio", secciones.SelectedItem);

            using var sinAvisos = new RevisionExportacion(new(historia, [], []), imprimir: true);
            sinAvisos.Show();
            Assert.Equal("La historia está lista para enviar a la impresora.",
                Assert.Single(Assert.Single(Descendientes(sinAvisos).OfType<TreeView>()).Nodes.Cast<TreeNode>()).Text);
        });
    }

    private static RadioButton FiltroDeBandeja(Historias ventana, string nombre) =>
        Descendientes(ventana).OfType<RadioButton>().Single(boton => boton.Text.StartsWith(nombre + " ("));
}
