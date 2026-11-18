using Microsoft.Data.Sqlite;

namespace Gineco;

internal sealed partial class Editor : Form
{
    internal static readonly string[] Secciones =
    [
        "Datos de la paciente", "Consulta y antecedentes", "Examen físico", "Signos vitales",
        "Pruebas de laboratorio", "Diagnóstico", "Manejo de medicamentos", "Origen de la enfermedad"
    ];

    private EntradaLaboratorio laboratorios;
    private PerfilAplicado? perfilAplicado;
    private readonly ComboBox perfil = new()
    {
        Name = "perfilHistoria",
        Width = 245,
        DropDownStyle = ComboBoxStyle.DropDownList,
        AccessibleName = "Perfil de la paciente"
    };
    private readonly Dictionary<string, string> valoresIniciales;
    private readonly Archivo archivo;
    private readonly Medico usuario;
    private readonly Atencion? anterior;
    private readonly Dictionary<string, TextBox> campos = [];
    private readonly Dictionary<string, SeleccionClinica> selecciones = [];
    private SugerenciasMedicacion? sugerencias;
    private readonly Dictionary<string, EntradaVital> vitales = [];
    private readonly List<Control> paginas = [];
    private readonly Panel contenido = new() { Dock = DockStyle.Fill, BackColor = Estilo.Papel };
    private readonly ListBox navegacion = new()
    {
        Name = "secciones",
        Dock = DockStyle.Fill,
        AccessibleName = "Secciones de la historia",
        IntegralHeight = false,
        BorderStyle = BorderStyle.None,
        DrawMode = DrawMode.OwnerDrawFixed
    };
    private readonly Label paciente = Estilo.Texto("", 23, true);
    private readonly Label aviso = Estilo.Texto("", 10);
    private readonly DateTimePicker fecha = new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "dd/MM/yyyy HH:mm",
        Width = 220
    };
    private readonly TextBox motivo = new() { Width = 235 };
    private readonly Button retroceder = Estilo.Boton("Anterior");
    private readonly Button avanzar = Estilo.Boton("Siguiente");

    internal long HistoriaGuardada { get; private set; }
    internal bool SolicitaImpresion { get; private set; }
    private readonly bool imprimirAlFinalizar;

    internal Editor(Archivo archivo, Medico usuario, Atencion? anterior, bool imprimirAlFinalizar = false)
    {
        this.archivo = archivo;
        this.imprimirAlFinalizar = imprimirAlFinalizar;
        this.usuario = usuario;
        this.anterior = anterior;
        perfilAplicado = anterior?.Perfil;
        valoresIniciales = new Dictionary<string, string>(anterior?.Campos ?? []);
        if (anterior is null) valoresIniciales["origen"] = OpcionesClinicas.Maternidad;
        perfil.Items.AddRange(archivo.LeerPerfiles().ToArray());
        perfil.SelectedIndex = -1;
        if (perfilAplicado is not null)
            perfil.SelectedItem = perfil.Items.Cast<PerfilClinico>().FirstOrDefault(opcion => opcion.Id == perfilAplicado.Id);
        laboratorios = new EntradaLaboratorio(anterior?.Valor("pruebas") ?? "");
        Estilo.Ventana(this);
        Text = "Historia clínica";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1040, 660);
        MinimumSize = new Size(770, 505);
        fecha.Value = anterior?.Fecha ?? Clinica.HoraHospital();
        CrearPaginas();

        var cuerpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 0, 14, 0) };
        cuerpo.Controls.Add(contenido);
        cuerpo.Controls.Add(CrearNavegacion());
        Controls.Add(cuerpo);
        Controls.Add(CrearAcciones());
        Controls.Add(CrearCabecera());
        campos["nombre"].TextChanged += (_, _) => ActualizarPaciente();
        ActualizarPaciente();
        navegacion.SelectedIndex = 0;
        var rueda = new RuedaDeSeccion(this, () => paginas.FirstOrDefault(pagina => pagina.Visible));
        Application.AddMessageFilter(rueda);
        Disposed += (_, _) => Application.RemoveMessageFilter(rueda);
    }

    private Control CrearCabecera()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(20, 12, 20, 13)
        };
        string estado = anterior is null ? "Nueva historia" : anterior.Finalizada ? "Historia finalizada" : "Borrador";
        var contexto = Estilo.Texto("Historia clínica  /  " + estado);
        contexto.ForeColor = Estilo.Secundario;
        paciente.AutoSize = false;
        paciente.Height = 42;
        paciente.AutoEllipsis = true;
        paciente.AccessibleName = "Nombre de la paciente";
        panel.SizeChanged += (_, _) => paciente.Width = Math.Max(100, panel.ClientSize.Width - panel.Padding.Horizontal - 10);
        panel.Controls.Add(contexto);
        panel.Controls.Add(paciente);
        var opciones = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        opciones.Controls.Add(Estilo.Texto("Perfil", 11, true));
        opciones.Controls.Add(perfil);
        var aplicar = Estilo.Boton("Aplicar perfil");
        aplicar.Click += (_, _) => AplicarPerfil();
        opciones.Controls.Add(aplicar);
        panel.Controls.Add(opciones);
        return panel;
    }

    private void ActualizarPaciente() => paciente.Text = string.IsNullOrWhiteSpace(campos["nombre"].Text)
        ? "Paciente sin identificar" : campos["nombre"].Text.Trim();

    private Control CrearNavegacion()
    {
        var lateral = new Panel { Dock = DockStyle.Left, Width = 244, Padding = new Padding(8, 0, 10, 0) };
        var titulo = Estilo.Texto("Secciones", 10, true);
        titulo.Dock = DockStyle.Top;
        titulo.Padding = new Padding(5, 0, 0, 7);
        navegacion.BackColor = Estilo.Fondo;
        navegacion.ItemHeight = 34;
        navegacion.Items.AddRange(Secciones);
        navegacion.DrawItem += DibujarSeccion;
        navegacion.SelectedIndexChanged += (_, _) => MostrarSeccion();
        lateral.Controls.Add(navegacion);
        lateral.Controls.Add(titulo);
        return lateral;
    }

    private static void DibujarSeccion(object? remitente, DrawItemEventArgs evento)
    {
        if (evento.Index < 0) return;
        bool seleccionada = evento.State.HasFlag(DrawItemState.Selected);
        using var fondo = new SolidBrush(seleccionada ? Estilo.Principal : Estilo.Fondo);
        evento.Graphics.FillRectangle(fondo, evento.Bounds);
        var texto = new Rectangle(evento.Bounds.X + 9, evento.Bounds.Y, evento.Bounds.Width - 18, evento.Bounds.Height);
        var color = seleccionada ? (SystemInformation.HighContrast ? SystemColors.HighlightText : Color.White) : Estilo.Tinta;
        TextRenderer.DrawText(evento.Graphics, Secciones[evento.Index], evento.Font, texto, color,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        evento.DrawFocusRectangle();
    }

    private void MostrarSeccion()
    {
        int indice = navegacion.SelectedIndex;
        if (indice < 0) return;
        foreach (var pagina in paginas) pagina.Visible = pagina == paginas[indice];
        paginas[indice].BringToFront();
        if (Secciones[indice] == "Manejo de medicamentos") sugerencias?.Actualizar();
        retroceder.Enabled = indice > 0;
        avanzar.Enabled = indice < paginas.Count - 1;
    }

    private void AplicarPerfil()
    {
        if (perfil.SelectedItem is not PerfilClinico elegido)
        {
            MostrarAviso("Elige paciente embarazada o paciente no embarazada.");
            return;
        }
        try
        {
            var datos = LeerDatos(false);
            perfilAplicado = Perfiles.Aplicar(elegido, datos, perfilAplicado);
            valoresIniciales.Clear();
            foreach (var dato in datos) valoresIniciales.Add(dato.Key, dato.Value);
            int seccion = navegacion.SelectedIndex;
            contenido.SuspendLayout();
            fecha.Parent?.Controls.Remove(fecha);
            foreach (var pagina in paginas) pagina.Dispose();
            paginas.Clear();
            campos.Clear();
            selecciones.Clear();
            vitales.Clear();
            laboratorios = new EntradaLaboratorio(datos.GetValueOrDefault("pruebas", ""));
            CrearPaginas();
            campos["nombre"].TextChanged += (_, _) => ActualizarPaciente();
            ActualizarPaciente();
            contenido.ResumeLayout(true);
            navegacion.SelectedIndex = seccion;
            MostrarSeccion();
            aviso.ForeColor = Estilo.Guardar;
            aviso.Text = $"Perfil aplicado: {elegido.Nombre}. Puedes editar todos los campos.";
            aviso.Visible = true;
        }
        catch (ArgumentException error) { MostrarAviso(error.Message); }
    }

    private void CrearPaginas()
    {
        CrearDatos();
        var consulta = Pagina(Secciones[1]);
        AgregarGrupo(consulta, "Motivo de consulta y breve historia clínica", "motivo");
        AgregarGrupo(consulta, "Antecedentes");
        CrearAtencionesPrevias(consulta);
        AgregarCampos(consulta, "antecedentes", "medicamentos");
        CrearAlergias(consulta);
        AgregarCampos(Pagina(Secciones[2]), "abdomen", "especuloscopia", "tacto");
        CrearVitales();
        CrearLaboratorios();
        AgregarCampos(Pagina(Secciones[5]), "diagnosticos");
        CrearManejo();
        var origen = Pagina(Secciones[7]);
        AgregarSeleccion(origen, "origen", "Selecciona el origen", OpcionesClinicas.Origen);
        AgregarCampo(origen, "observaciones_origen");
        foreach (var pagina in paginas) pagina.Controls.Add(new Panel { Width = 1, Height = 16 });
    }

    private void CrearAtencionesPrevias(FlowLayoutPanel pagina)
    {
        var previas = AgregarSeleccion(pagina, "previas", "Atenciones previas", OpcionesClinicas.Atenciones);
        previas.FlowDirection = FlowDirection.LeftToRight;
        var detalle = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        pagina.Controls.Add(detalle);
        AgregarCampo(detalle, "lugar");
        AdaptarAncho(pagina, detalle);
        detalle.Visible = previas.Valor == OpcionesClinicas.No;
        previas.Editada += (_, _) => detalle.Visible = previas.Valor == OpcionesClinicas.No;
    }

    private SeleccionClinica AgregarSeleccion(FlowLayoutPanel pagina, string clave, string etiqueta, string[] opciones)
    {
        var seleccion = new SeleccionClinica(clave, opciones, valoresIniciales.GetValueOrDefault(clave, ""));
        selecciones.Add(clave, seleccion);
        Estilo.Campo(pagina, etiqueta, seleccion);
        return seleccion;
    }

    private void CrearManejo()
    {
        var pagina = Pagina(Secciones[6]);
        AgregarCampo(pagina, "manejo");
        sugerencias = new SugerenciasMedicacion(
            () => archivo.SugerenciasDisponibles(perfilAplicado?.Id, campos["diagnosticos"].Text),
            texto => campos["manejo"].Text = RepertorioMedicacion.Anadir(campos["manejo"].Text, texto));
        pagina.Controls.Add(sugerencias);
        AdaptarAncho(pagina, sugerencias);
        AgregarSeleccion(pagina, "estatus", "Estatus del paciente", OpcionesClinicas.Estatus);
    }

    private void CrearDatos()
    {
        var pagina = Pagina(Secciones[0]);
        AgregarCampos(pagina, "nombre");
        var identificadores = Cuadricula(pagina, 2);
        AgregarCampo(Celda(identificadores), "cedula");
        AgregarCampo(Celda(identificadores), "nss");
        CrearAccionesAfiliada(pagina);
        var columnas = Cuadricula(pagina, 2);
        foreach (string clave in new[] { "edad", "telefono", "ars", "direccion", "nacionalidad" })
            AgregarCampo(Celda(columnas), clave);
        Estilo.Campo(pagina, "Fecha y hora de atención · Santo Domingo", fecha);
    }

    private void CrearLaboratorios()
    {
        var pagina = Pagina(Secciones[4]);
        Estilo.Campo(pagina, "Pruebas de laboratorio indicadas", laboratorios);
        pagina.ClientSizeChanged += (_, _) =>
            laboratorios.AjustarAncho(Math.Max(160, pagina.ClientSize.Width - pagina.Padding.Horizontal - 20));
        AgregarCampos(pagina, "hallazgos", "sonografias");
    }

    private void CrearVitales()
    {
        var pagina = Pagina(Secciones[3]);
        var columnas = Cuadricula(pagina, 2);
        AgregarVital(columnas, "ta", "Presión arterial");
        AgregarVital(columnas, "temperatura", "Temperatura");
        AgregarVital(columnas, "fc", "Frecuencia cardíaca");
        AgregarVital(columnas, "fr", "Frecuencia respiratoria");
        AgregarVital(columnas, "spo2", "Saturación de oxígeno");
        AgregarVital(columnas, "pc", "PC en 10 minutos");
    }

    private void AgregarVital(TableLayoutPanel columnas, string clave, string etiqueta)
    {
        var entrada = new EntradaVital(clave, etiqueta, valoresIniciales.GetValueOrDefault(clave, "")) { Name = clave };
        vitales.Add(clave, entrada);
        entrada.Editada += (_, _) => aviso.Visible = false;
        Estilo.Campo(Celda(columnas), etiqueta, entrada);
    }

    private FlowLayoutPanel Pagina(string titulo)
    {
        var pagina = Estilo.Columna();
        pagina.AutoScrollMargin = new Size(0, 20);
        pagina.Visible = false;
        var encabezado = Estilo.Texto(titulo, 18, true);
        encabezado.Margin = new Padding(0, 0, 0, 10);
        pagina.Controls.Add(encabezado);
        paginas.Add(pagina);
        contenido.Controls.Add(pagina);
        return pagina;
    }

    private static TableLayoutPanel Cuadricula(FlowLayoutPanel pagina, int columnas)
    {
        var tabla = new TableLayoutPanel { AutoSize = true, ColumnCount = columnas, Margin = Padding.Empty };
        for (int indice = 0; indice < columnas; indice++)
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columnas));
        pagina.ClientSizeChanged += (_, _) =>
            tabla.MinimumSize = new Size(Math.Max(0, pagina.ClientSize.Width - pagina.Padding.Horizontal - 20), 0);
        pagina.Controls.Add(tabla);
        return tabla;
    }

    private static FlowLayoutPanel Celda(TableLayoutPanel tabla)
    {
        var celda = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 14, 0)
        };
        tabla.Controls.Add(celda);
        return celda;
    }

    private void AgregarGrupo(FlowLayoutPanel pagina, string titulo, params string[] claves)
    {
        var encabezado = Estilo.Texto(titulo, 12, true);
        encabezado.ForeColor = Estilo.Principal;
        encabezado.Margin = new Padding(0, 12, 0, 2);
        pagina.Controls.Add(encabezado);
        AgregarCampos(pagina, claves);
    }

    private void AgregarCampos(FlowLayoutPanel pagina, params string[] claves)
    {
        foreach (var clave in claves) AgregarCampo(pagina, clave);
    }

    private void AgregarCampo(FlowLayoutPanel panel, string clave)
    {
        bool identificacion = Clinica.Secciones["Datos de la paciente"].ContainsKey(clave);
        bool larga = !identificacion && clave is not ("previas" or "lugar" or "alergias" or "estatus" or "origen");
        string etiqueta = Clinica.Secciones.Values.First(seccion => seccion.ContainsKey(clave))[clave];
        if (clave == "nombre") etiqueta += " (obligatorio)";
        if (clave == "motivo") etiqueta = "Motivo y evolución";
        var entrada = new TextBox
        {
            Name = clave,
            Text = valoresIniciales.GetValueOrDefault(clave, ""),
            Width = 185,
            Multiline = larga,
            ScrollBars = larga ? ScrollBars.Vertical : ScrollBars.None
        };
        if (larga) entrada.Height = clave is "motivo" or "manejo" or "diagnosticos" ? 100 : 64;
        if (panel.Parent is TableLayoutPanel tabla)
            tabla.ClientSizeChanged += (_, _) => entrada.Width = Math.Max(110, tabla.Width / tabla.ColumnCount - 27);
        else
            AdaptarAncho(panel, entrada);
        entrada.TextChanged += (_, _) => aviso.Visible = false;
        campos.Add(clave, entrada);
        Estilo.Campo(panel, etiqueta, entrada);
    }

    private static void AdaptarAncho(FlowLayoutPanel panel, Control control)
    {
        panel.ClientSizeChanged += (_, _) =>
            control.Width = Math.Max(125, panel.ClientSize.Width - panel.Padding.Horizontal - 20);
    }

    private Control CrearAcciones()
    {
        var pie = new Panel { Dock = DockStyle.Bottom, AutoSize = true, BackColor = Estilo.Papel };
        aviso.Dock = DockStyle.Top;
        aviso.Padding = new Padding(18, 3, 0, 0);
        aviso.ForeColor = SystemInformation.HighContrast ? Estilo.Tinta : Color.FromArgb(155, 47, 34);
        aviso.Visible = false;
        var acciones = Estilo.Barra(DockStyle.Bottom);
        acciones.FlowDirection = FlowDirection.RightToLeft;
        SetAccion(acciones, "Cancelar", Close);
        if (anterior?.Finalizada == true)
        {
            SetAccion(acciones, "Guardar corrección", () => Guardar(true), Estilo.Guardar);
            motivo.PlaceholderText = "Motivo de la corrección";
            motivo.AccessibleName = "Motivo de la corrección";
            acciones.Controls.Add(motivo);
        }
        else
        {
            SetAccion(acciones, imprimirAlFinalizar ? "Finalizar e imprimir" : "Finalizar", () => Guardar(true), Estilo.Principal);
            SetAccion(acciones, "Guardar borrador", () => Guardar(false), Estilo.Guardar);
        }
        retroceder.Click += (_, _) => navegacion.SelectedIndex--;
        avanzar.Click += (_, _) => navegacion.SelectedIndex++;
        acciones.Controls.Add(avanzar);
        acciones.Controls.Add(retroceder);
        pie.Controls.Add(acciones);
        pie.Controls.Add(aviso);
        return pie;
    }

    private static void SetAccion(Control barra, string texto, Action ejecutar, Color? color = null)
    {
        var boton = Estilo.Boton(texto, color);
        boton.Click += (_, _) => ejecutar();
        barra.Controls.Add(boton);
    }

    private Dictionary<string, string> LeerDatos(bool exigirNombre = true)
    {
        var datos = new Dictionary<string, string>(valoresIniciales);
        foreach (var (clave, campo) in campos) datos[clave] = campo.Text.Trim();
        foreach (var (clave, seleccion) in selecciones) datos[clave] = seleccion.Valor;
        if (datos["alergias"] != "Sí") datos["detalle_alergias"] = "";
        datos["pruebas"] = laboratorios.Valor();
        if (exigirNombre && datos["nombre"].Length == 0)
        {
            navegacion.SelectedIndex = 0;
            campos["nombre"].Focus();
            throw new ArgumentException("Escribe el nombre completo de la paciente.");
        }
        foreach (var (clave, entrada) in vitales)
        {
            try { datos[clave] = entrada.Valor(); }
            catch (ArgumentException)
            {
                navegacion.SelectedIndex = 3;
                entrada.SelectNextControl(null, true, true, true, false);
                throw;
            }
        }
        return datos;
    }

    private void Guardar(bool finalizada)
    {
        try
        {
            var actual = new Atencion(anterior?.Id ?? 0, anterior?.Responsable ?? usuario, fecha.Value, LeerDatos(), finalizada) { Perfil = perfilAplicado };
            HistoriaGuardada = archivo.Guardar(usuario, actual, anterior, motivo.Text.Trim());
            SolicitaImpresion = finalizada && imprimirAlFinalizar && anterior?.Finalizada != true;
            DialogResult = DialogResult.OK;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            MostrarAviso(error.Message);
        }
        catch (SqliteException)
        {
            MostrarAviso("No se pudo guardar la historia. Vuelve a intentarlo.");
        }
    }

    private void MostrarAviso(string mensaje)
    {
        aviso.ForeColor = SystemInformation.HighContrast ? Estilo.Tinta : Color.FromArgb(155, 47, 34);
        aviso.Text = mensaje;
        aviso.AccessibleRole = AccessibleRole.Alert;
        aviso.Visible = true;
    }
}
