using Gineco;
using Xunit;

namespace Gineco.Pruebas;

public sealed partial class Ventanas
{
    [Fact]
    public async Task AbreElHistorialYGuardaUnBorradorDesdeElEditor()
    {
        await EnVentana(AbrirYGuardar);
    }

    [Fact]
    public async Task InicioDeSesionEsLaOpcionPredeterminada()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            using var acceso = new Cuenta("Iniciar sesión", archivo);
            acceso.Show();
            Assert.Equal("Iniciar sesión", acceso.Text);
            Capturar(acceso, "acceso");
            var panel = Assert.Single(acceso.Controls.OfType<FlowLayoutPanel>());
            var opciones = Assert.Single(panel.Controls.OfType<FlowLayoutPanel>());
            Assert.False(opciones.Controls.OfType<Button>().Single(boton => boton.Text == "Iniciar sesión").Enabled);
            var registro = opciones.Controls.OfType<Button>().Single(boton => boton.Text == "Registrarse");
            using var temporizador = new System.Windows.Forms.Timer { Interval = 100 };
            bool abierto = false;
            temporizador.Tick += (_, _) =>
            {
                temporizador.Stop();
                foreach (var dialogo in Application.OpenForms.OfType<Registro>().ToList())
                {
                    abierto = dialogo.Visible;
                    dialogo.Close();
                }
            };
            temporizador.Start();
            registro.PerformClick();
            Assert.True(abierto);
            Assert.True(acceso.Visible);
            acceso.Close();
        });
    }

    [Theory]
    [InlineData("ta", "", "", "", "")]
    [InlineData("ta", "110/70 mmHg", "110/70 mmHg", "", "110/70 mmHg")]
    [InlineData("ta", "", "", "70", null)]
    [InlineData("temperatura", "", "36,8", "", "36,8")]
    [InlineData("temperatura", "", "36.8", "", "36.8")]
    [InlineData("temperatura", "", "36 °C", "", null)]
    [InlineData("fc", "", "82", "", "82")]
    [InlineData("fc", "", "8.2", "", null)]
    [InlineData("fc", "No medible", "No medible", "", "No medible")]
    public async Task ValidaSignosNuevosYAdmiteDatosAnteriores(string clave, string previo, string primero, string segundo, string? resultado)
    {
        await EnVentana(() =>
        {
            using var entrada = new EntradaVital(clave, clave, previo);
            var cuadros = entrada.Controls.OfType<TextBox>().ToArray();
            cuadros[0].Text = primero;
            if (cuadros.Length > 1) cuadros[1].Text = segundo;
            if (resultado is null) Assert.Throws<ArgumentException>(() => entrada.Valor());
            else Assert.Equal(resultado, entrada.Valor());
        });
    }

    [Fact]
    public async Task LaboratoriosSeGuardanYSeRestauranEnElEditor()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            var medico = new Medico("1", "Médica ficticia", false);
            using var editor = new Editor(archivo, medico, null);
            editor.Show();
            editor.Controls.Find("nombre", true).Single().Text = "Paciente ficticia";
            var (casillas, otros, detalle) = AbrirLaboratorios(editor);
            Assert.Equal(new[] { "Hemograma", "Examen de orina", "Prueba de embarazo", "Glicemia", "Coprológico" },
                casillas.Select(casilla => casilla.Text));
            Assert.All(casillas, casilla => Assert.False(casilla.Checked));
            Assert.False(detalle.Visible);

            casillas[0].Checked = true;
            casillas[2].Checked = true;
            otros.Checked = true;
            Assert.True(detalle.Visible);
            detalle.Text = "Perfil renal";
            otros.Checked = false;
            Assert.False(detalle.Visible);
            var control = Assert.IsType<EntradaLaboratorio>(editor.Controls.Find("laboratorios", true).Single());
            Assert.Equal("Hemograma\nPrueba de embarazo", control.Valor());

            otros.Checked = true;
            Assert.Equal("Perfil renal", detalle.Text);
            Capturar(editor, "laboratorios");
            Descendientes(editor).OfType<Button>().Single(boton => boton.Text == "Guardar borrador").PerformClick();
            var guardada = Assert.Single(archivo.Listar(medico));
            Assert.Equal("Hemograma\nPrueba de embarazo\nPerfil renal", guardada.Valor("pruebas"));

            using var reabierto = new Editor(archivo, medico, guardada);
            reabierto.Show();
            var (guardadas, otrosGuardados, detalleGuardado) = AbrirLaboratorios(reabierto);
            Assert.True(guardadas[0].Checked);
            Assert.False(guardadas[1].Checked);
            Assert.True(guardadas[2].Checked);
            Assert.True(otrosGuardados.Checked);
            Assert.True(detalleGuardado.Visible);
            Assert.Equal("Perfil renal", detalleGuardado.Text);
        });
    }

    private static (CheckBox[] Habituales, CheckBox Otros, TextBox Detalle) AbrirLaboratorios(Editor editor)
    {
        var secciones = Assert.IsType<ListBox>(editor.Controls.Find("secciones", true).Single());
        secciones.SelectedItem = "Pruebas de laboratorio";
        var control = Assert.IsType<EntradaLaboratorio>(editor.Controls.Find("laboratorios", true).Single());
        var casillas = Descendientes(control).OfType<CheckBox>().ToArray();
        var otros = casillas.Single(casilla => casilla.Text == "Otros análisis");
        return (casillas.Where(casilla => casilla != otros).ToArray(), otros, Assert.Single(Descendientes(control).OfType<TextBox>()));
    }

    [Fact]
    public async Task PerfilesConfiguracionYRevisionFuncionanEnWindows()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            archivo.Alta(null, "admin", "Administrador de prueba", "abcdef");
            var admin = Assert.IsType<Medico>(archivo.Entrar("admin", "Administrador de prueba", "abcdef"));
            archivo.Alta(admin, "med", "Médica de prueba", "abcdef");
            var medica = Assert.IsType<Medico>(archivo.Entrar("med", "Médica de prueba", "abcdef"));
            var perfil = archivo.LeerPerfiles()[0] with
            {
                Campos = new() { ["motivo"] = "Texto de ejemplo del administrador" }
            };
            archivo.GuardarPerfil(admin, perfil);
            Assert.Throws<InvalidOperationException>(() => new Configuracion(archivo, medica));
            using (var configuracion = new Configuracion(archivo, admin))
            {
                configuracion.Show();
                Assert.True(configuracion.Visible);
                var tabla = Assert.IsType<DataGridView>(configuracion.Controls.Find("predeterminados", true).Single());
                var abdomen = tabla.Rows.Cast<DataGridViewRow>().Single(fila => fila.Tag as string == "abdomen");
                abdomen.Cells[1].Value = "Predeterminado del administrador";
                Descendientes(configuracion).OfType<Button>().Single(boton => boton.Text == "Guardar predeterminados").PerformClick();
                Assert.Equal("Predeterminado del administrador", archivo.LeerPerfiles()[0].Campos["abdomen"]);
                Capturar(configuracion, "configuracion-perfiles");
                var pestanas = Assert.Single(configuracion.Controls.OfType<TabControl>());
                pestanas.SelectedIndex = 1;
                var diccionario = Assert.IsType<DataGridView>(configuracion.Controls.Find("diccionario", true).Single());
                diccionario.Rows.Add("dolro", "dolor");
                Descendientes(configuracion).OfType<Button>().Single(boton => boton.Text == "Guardar diccionario").PerformClick();
                Assert.Equal(new Correccion("dolro", "dolor"), Assert.Single(archivo.ConsultarCorrecciones(admin)));
                Capturar(configuracion, "configuracion-ortografia");
                pestanas.SelectedIndex = 0;
                var selectorPerfil = Assert.IsType<ComboBox>(configuracion.Controls.Find("perfilConfiguracion", true).Single());
                selectorPerfil.SelectedIndex = 1;
                Assert.All(tabla.Rows.Cast<DataGridViewRow>(), fila => Assert.Equal("", fila.Cells[1].Value));
                selectorPerfil.SelectedIndex = 0;
                Assert.Equal("Predeterminado del administrador", tabla.Rows.Cast<DataGridViewRow>()
                    .Single(fila => fila.Tag as string == "abdomen").Cells[1].Value);
            }
            using (var historias = new Historias(archivo, medica))
            {
                historias.Show();
                Assert.DoesNotContain(Descendientes(historias).OfType<Button>(), boton => boton.Text == "Configuración");
            }
            using var editor = new Editor(archivo, medica, null);
            editor.Show();
            editor.Controls.Find("nombre", true).Single().Text = "Paciente de prueba";
            editor.Controls.Find("abdomen", true).Single().Text = "Observación escrita por la médica";
            var selector = Assert.IsType<ComboBox>(editor.Controls.Find("perfilHistoria", true).Single());
            Assert.Equal(-1, selector.SelectedIndex);
            selector.SelectedIndex = 0;
            Descendientes(editor).OfType<Button>().Single(boton => boton.Text == "Aplicar perfil").PerformClick();
            Assert.Equal("Observación escrita por la médica", editor.Controls.Find("abdomen", true).Single().Text);
            Assert.Equal("Texto de ejemplo del administrador", editor.Controls.Find("motivo", true).Single().Text);
            var laboratorios = Assert.IsType<EntradaLaboratorio>(editor.Controls.Find("laboratorios", true).Single());
            Assert.Empty(laboratorios.Valor());
            Capturar(editor, "perfil-aplicado");
            Descendientes(editor).OfType<Button>().Single(boton => boton.Text == "Guardar borrador").PerformClick();
            var historia = Assert.Single(archivo.Listar(medica));
            Assert.Equal("embarazada", historia.Perfil!.Id);
            Assert.False(Perfiles.EsPredeterminado(historia, "pruebas"));
            Assert.Single(archivo.LeerPropuestas(admin, "embarazada"));
            using (var aprendizaje = new Configuracion(archivo, admin))
            {
                aprendizaje.Show();
                Descendientes(aprendizaje).OfType<Button>().Single(boton => boton.Text == "Usar propuesta como predeterminado").PerformClick();
                Descendientes(aprendizaje).OfType<Button>().Single(boton => boton.Text == "Guardar predeterminados").PerformClick();
                Assert.Equal("Observación escrita por la médica", archivo.LeerPerfiles()[0].Campos["abdomen"]);
            }
            using var revision = new RevisionExportacion(archivo.PrepararExportacion(historia));
            revision.Show();
            Capturar(revision, "revision-exportacion");
            Descendientes(revision).OfType<Button>().Single(boton => boton.Text == "Continuar y exportar").PerformClick();
            Assert.Equal(DialogResult.OK, revision.DialogResult);
        });
    }

    [Fact]
    public async Task OpcionesYSugerenciasSoloSeIncorporanPorAccionDelMedico()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            archivo.Alta(null, "a", "Administrador", "abcdef");
            var admin = Assert.IsType<Medico>(archivo.Entrar("a", "Administrador", "abcdef"));
            archivo.Alta(admin, "m", "Médica", "abcdef");
            var medico = Assert.IsType<Medico>(archivo.Entrar("m", "Médica", "abcdef"));
            const string diagnostico = "Diagnóstico ficticio";
            const string sugerencia = "Texto de medicación ficticia";
            archivo.Guardar(medico, OpcionesYSugerencias.Historia(diagnostico, sugerencia), null, "");
            using (var configuracion = new Configuracion(archivo, admin))
            {
                configuracion.Show();
                Assert.Single(configuracion.Controls.OfType<TabControl>()).SelectedIndex = 2;
                var aprobar = Descendientes(configuracion).OfType<Button>().Single(boton => boton.Text == "Aprobar");
                Assert.True(aprobar.Enabled);
                Capturar(configuracion, "sugerencias-admin");
                aprobar.PerformClick();
                Assert.Single(archivo.SugerenciasDisponibles("embarazada", diagnostico));
            }

            using var editor = new Editor(archivo, medico, null);
            editor.Show();
            Assert.Empty(editor.Controls.Find("sexo", true));
            Assert.Empty(editor.Controls.Find("procedimientos", true));
            Assert.Empty(editor.Controls.Find("observaciones", true));
            editor.Controls.Find("nombre", true).Single().Text = "Otra paciente ficticia";
            var secciones = Assert.IsType<ListBox>(editor.Controls.Find("secciones", true).Single());
            secciones.SelectedIndex = 1;
            var detalle = editor.Controls.Find("lugar", true).Single();
            Assert.False(detalle.Visible);
            ElegirCasilla(editor, "previas", "Sí");
            Assert.True(detalle.Visible);
            ElegirCasilla(editor, "previas", "No");
            Assert.False(detalle.Visible);
            Assert.Equal("No", Assert.IsType<SeleccionClinica>(editor.Controls.Find("previas", true).Single()).Valor);
            ElegirCasilla(editor, "previas", "Sí");
            detalle.Text = "Detalle de ejemplo";
            Capturar(editor, "atenciones-previas");

            secciones.SelectedIndex = 7;
            var origen = Assert.IsType<SeleccionClinica>(editor.Controls.Find("origen", true).Single());
            Assert.Equal("Maternidad", origen.Valor);
            ElegirCasilla(editor, "origen", "Otros");
            Assert.Equal("Otros", origen.Valor);

            editor.Controls.Find("diagnosticos", true).Single().Text = diagnostico;
            var perfil = Assert.IsType<ComboBox>(editor.Controls.Find("perfilHistoria", true).Single());
            perfil.SelectedIndex = 0;
            Descendientes(editor).OfType<Button>().Single(boton => boton.Text == "Aplicar perfil").PerformClick();
            secciones.SelectedIndex = 6;
            var manejo = editor.Controls.Find("manejo", true).Single();
            Assert.Equal("", manejo.Text);
            manejo.Text = "Nota de la médica";
            var botonAnadir = Descendientes(editor).OfType<Button>().Single(boton => boton.Text == "Añadir sugerencia");
            Assert.True(botonAnadir.Visible);
            Capturar(editor, "sugerencias-medico");
            botonAnadir.PerformClick();
            Assert.Equal("Nota de la médica" + Environment.NewLine + sugerencia, manejo.Text);
            ElegirCasilla(editor, "estatus", "Alta");
            ElegirCasilla(editor, "estatus", "Admitida");
            Descendientes(editor).OfType<Button>().Single(boton => boton.Text == "Guardar borrador").PerformClick();
            var guardada = Assert.Single(archivo.Listar(medico), historia => historia.Nombre == "Otra paciente ficticia");
            Assert.Equal("Admitida", guardada.Valor("estatus"));
            Assert.Equal("Otros", guardada.Valor("origen"));
            Assert.Equal("Detalle de ejemplo", guardada.Valor("lugar"));
            Assert.Contains(sugerencia, guardada.Valor("manejo"));
        });
    }

    private static void ElegirCasilla(Editor editor, string clave, string texto)
    {
        var control = Assert.IsType<SeleccionClinica>(editor.Controls.Find(clave, true).Single());
        control.Controls.OfType<CheckBox>().Single(casilla => casilla.Text == texto).Checked = true;
    }

    [Fact]
    public async Task AfiliadaAutocompletaPermiteVistaPreviaYGuardaAlergiaEditable()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            archivo.Alta(null, "a", "Administradora", "abcdef");
            var admin = Assert.IsType<Medico>(archivo.Entrar("a", "Administradora", "abcdef"));
            var previa = HistoriasDeAfiliadas.Historia(1, "00123456789", "001234567", "Sí", "Látex");
            previa.Campos["diagnosticos"] = "Diagnóstico anterior ficticio";
            archivo.Guardar(admin, previa, null, "");
            using var editor = new Editor(archivo, admin, null);
            editor.Show();
            var secciones = Assert.IsType<ListBox>(editor.Controls.Find("secciones", true).Single());
            var nss = editor.Controls.Find("nss", true).Single();
            nss.Text = "001-234-567";
            var botones = Descendientes(editor).OfType<Button>().ToArray();
            botones.Single(boton => boton.Text == "Buscar afiliada").PerformClick();
            Assert.Equal("Paciente ficticia", editor.Controls.Find("nombre", true).Single().Text);
            Assert.Equal("00123456789", editor.Controls.Find("cedula", true).Single().Text);
            Assert.Empty(editor.Controls.Find("edad", true).Single().Text);
            editor.Controls.Find("motivo", true).Single().Text = "Borrador actual";

            string? textoVista = null;
            bool soloLectura = false;
            using var temporizador = new System.Windows.Forms.Timer { Interval = 100 };
            temporizador.Tick += (_, _) =>
            {
                var anterior = Application.OpenForms.OfType<VistaHistorias>().FirstOrDefault();
                if (anterior is null) return;
                temporizador.Stop();
                var contenido = anterior.Controls.Find("detalleHistoriaAnterior", true).OfType<TextBox>().Single();
                textoVista = contenido.Text;
                soloLectura = contenido.ReadOnly;
                Capturar(anterior, "vista-afiliada");
                anterior.Close();
            };
            temporizador.Start();
            botones.Single(boton => boton.Text == "Historias anteriores").PerformClick();
            Assert.True(soloLectura);
            Assert.Contains("Diagnóstico anterior ficticio", textoVista);
            Assert.Equal("Borrador actual", editor.Controls.Find("motivo", true).Single().Text);

            secciones.SelectedIndex = 1;
            var alergias = Assert.IsType<SeleccionClinica>(editor.Controls.Find("alergias", true).Single());
            var detalle = editor.Controls.Find("detalle_alergias", true).Single();
            Assert.Equal("Sí", alergias.Valor);
            Assert.True(detalle.Visible);
            Assert.Equal("Látex", detalle.Text);
            var pagina = Assert.IsAssignableFrom<ScrollableControl>(editor.Controls.Find("motivo", true).Single().Parent);
            var rueda = new RuedaDeSeccion(editor, () => pagina);
            pagina.AutoScrollPosition = Point.Empty;
            rueda.Desplazar(pagina, -12000);
            Assert.True(pagina.AutoScrollPosition.Y < 0);
            Assert.True(detalle.RectangleToScreen(detalle.ClientRectangle).Bottom <= pagina.RectangleToScreen(pagina.ClientRectangle).Bottom);
            Capturar(editor, "alergias-afiliada");
            rueda.Desplazar(pagina, 12000);
            Assert.Equal(0, pagina.AutoScrollPosition.Y);

            ElegirCasilla(editor, "alergias", "No");
            Assert.False(detalle.Visible);
            botones.Single(boton => boton.Text == "Guardar borrador").PerformClick();
            var guardada = Assert.Single(archivo.Listar(admin), historia => historia.Valor("motivo") == "Borrador actual");
            Assert.Equal("No", guardada.Valor("alergias"));
            Assert.Empty(guardada.Valor("detalle_alergias"));
        });
    }

    [Fact]
    public async Task CambiarIdentificadorRetiraLosDatosImportadosDeOtraAfiliada()
    {
        await EnVentana(() =>
        {
            using var archivo = new Archivo(":memory:");
            archivo.Alta(null, "a", "Administradora", "abcdef");
            var admin = Assert.IsType<Medico>(archivo.Entrar("a", "Administradora", "abcdef"));
            archivo.Guardar(admin, HistoriasDeAfiliadas.Historia(1, "00123456789", "001234567", "Sí", "Látex"), null, "");
            using var editor = new Editor(archivo, admin, null);
            editor.Show();
            var nss = editor.Controls.Find("nss", true).Single();
            nss.Text = "001234567";
            Descendientes(editor).OfType<Button>().Single(boton => boton.Text == "Buscar afiliada").PerformClick();
            Assert.Equal("00123456789", editor.Controls.Find("cedula", true).Single().Text);
            nss.Text = "098765432";
            Assert.Empty(editor.Controls.Find("nombre", true).Single().Text);
            Assert.Empty(editor.Controls.Find("cedula", true).Single().Text);
            Assert.Empty(editor.Controls.Find("detalle_alergias", true).Single().Text);
            Assert.Empty(Assert.IsType<SeleccionClinica>(editor.Controls.Find("alergias", true).Single()).Valor);
        });
    }

    private static async Task EnVentana(Action accion)
    {
        var resultado = new TaskCompletionSource();
        var interfaz = new Thread(() =>
        {
            try
            {
                accion();
                resultado.SetResult();
            }
            catch (Exception error) { resultado.SetException(error); }
        })
        { IsBackground = true };
        interfaz.SetApartmentState(ApartmentState.STA);
        interfaz.Start();
        await resultado.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }

    private static void AbrirYGuardar()
    {
        using var archivo = new Archivo(":memory:");
        var medico = new Medico("0001", "Médica ficticia", false);
        using (var historial = new Historias(archivo, medico))
        {
            historial.Show();
            Assert.True(historial.Visible);
            historial.Close();
        }
        using (var editor = new Editor(archivo, medico, null))
        {
            editor.Show();
            var navegacion = Assert.IsType<ListBox>(editor.Controls.Find("secciones", true).Single());
            Assert.Equal(new[] { "Datos de la paciente", "Consulta y antecedentes", "Examen físico",
                "Signos vitales", "Pruebas de laboratorio", "Diagnóstico", "Manejo de medicamentos",
                "Origen de la enfermedad" }, navegacion.Items.Cast<string>());
            Assert.Equal(0, navegacion.SelectedIndex);
            var nombre = Assert.IsType<TextBox>(editor.Controls.Find("nombre", true).Single());
            nombre.Text = "Elena Martínez · paciente ficticia";
            Capturar(editor, "paciente");
            navegacion.SelectedIndex = 1;
            var motivo = Assert.IsType<TextBox>(editor.Controls.Find("motivo", true).Single());
            motivo.Text = "Motivo ficticio para comprobar el guardado.";
            Assert.True(motivo.Visible);
            Assert.True(editor.Controls.Find("antecedentes", true).Single().Visible);
            Capturar(editor, "consulta");
            navegacion.SelectedIndex = 3;
            var presion = Assert.IsType<EntradaVital>(editor.Controls.Find("ta", true).Single());
            var valores = presion.Controls.OfType<TextBox>().ToArray();
            valores[0].Text = "110";
            var boton = Descendientes(editor).OfType<Button>().Single(boton => boton.Text == "Guardar borrador");
            boton.PerformClick();
            Assert.NotEqual(DialogResult.OK, editor.DialogResult);
            Assert.Empty(archivo.Listar(medico));
            valores[1].Text = "70";
            Capturar(editor, "signos");
            boton.PerformClick();
            Assert.Equal(DialogResult.OK, editor.DialogResult);
        }
        var atencion = Assert.Single(archivo.Listar(medico));
        Assert.Equal("Elena Martínez · paciente ficticia", atencion.Nombre);
        Assert.Equal("110/70", atencion.Valor("ta"));
        Assert.Equal("Motivo ficticio para comprobar el guardado.", atencion.Valor("motivo"));
        using var historialGuardado = new Historias(archivo, medico);
        historialGuardado.Show();
        var tabla = Assert.Single(Descendientes(historialGuardado).OfType<DataGridView>());
        Assert.Equal(atencion.Nombre, tabla.Rows[0].Cells[0].Value);
        Capturar(historialGuardado, "historial");
        var busqueda = Assert.Single(Descendientes(historialGuardado).OfType<TextBox>());
        busqueda.Text = "sin coincidencias";
        Assert.Empty(tabla.Rows.Cast<DataGridViewRow>());
        Assert.False(Descendientes(historialGuardado).OfType<Button>().Single(boton => boton.Text == "Abrir historia").Enabled);
        busqueda.Text = "martínez";
        Assert.Single(tabla.Rows.Cast<DataGridViewRow>());

        using var reabierto = new Editor(archivo, medico, atencion);
        reabierto.Size = reabierto.MinimumSize;
        reabierto.Show();
        var navegacionReducida = Assert.IsType<ListBox>(reabierto.Controls.Find("secciones", true).Single());
        navegacionReducida.SelectedIndex = 3;
        var presionGuardada = Assert.IsType<EntradaVital>(reabierto.Controls.Find("ta", true).Single());
        Assert.Equal("110/70", presionGuardada.Valor());
        Capturar(reabierto, "ventana-reducida");
        Assert.False(atencion.Finalizada);
    }

    private static IEnumerable<Control> Descendientes(Control control) =>
        control.Controls.Cast<Control>().SelectMany(hijo => Descendientes(hijo).Prepend(hijo));

    private static void Capturar(Form ventana, string nombre)
    {
        var carpeta = Environment.GetEnvironmentVariable("GINECO_CAPTURAS_QA");
        if (carpeta is null) return;
        ventana.Refresh();
        using var imagen = new Bitmap(ventana.Width, ventana.Height);
        ventana.DrawToBitmap(imagen, new Rectangle(Point.Empty, ventana.Size));
        imagen.Save(Path.Combine(carpeta, nombre + ".png"));
    }
}
