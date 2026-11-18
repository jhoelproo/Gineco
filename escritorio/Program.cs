using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace Gineco;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gineco");
            Directory.CreateDirectory(carpeta);
            using var archivo = new Archivo(Path.Combine(carpeta, "historias.sqlite3"));
            Iniciar(archivo);
        }
        catch (Exception error) when (error is IOException or SqliteException or UnauthorizedAccessException)
        {
            MessageBox.Show("No se pudo abrir el archivo clínico local.", Clinica.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void Iniciar(Archivo archivo)
    {
        if (archivo.SinCuentas())
        {
            using var alta = new Cuenta("Crear administrador");
            if (alta.ShowDialog() != DialogResult.OK) return;
            archivo.Alta(null, alta.Exequatur, alta.Nombre, alta.Clave);
        }
        using var acceso = new Cuenta("Iniciar sesión", archivo);
        while (acceso.ShowDialog() == DialogResult.OK)
        {
            var usuario = archivo.Entrar(acceso.Exequatur, acceso.Nombre, acceso.Clave);
            if (usuario is not null)
            {
                Application.Run(new Historias(archivo, usuario, EnlaceAdmision.DesdeEstacion));
                return;
            }
            MessageBox.Show("El nombre, exequátur o contraseña no coincide.", Clinica.Titulo);
        }
    }
}

internal sealed class Cuenta : Form
{
    private readonly TextBox exequatur = new() { MaxLength = 40, Width = 320 };
    private readonly TextBox nombre = new() { MaxLength = 300, Width = 320 };
    private readonly TextBox clave = new() { UseSystemPasswordChar = true, Width = 320 };
    private List<Medico> sugerenciasActivas = [];
    internal string Exequatur => exequatur.Text.Trim();
    internal string Nombre => nombre.Text.Trim();
    internal string Clave => clave.Text;

    internal Cuenta(string titulo, Archivo? archivo = null)
    {
        Estilo.Ventana(this);
        Text = titulo;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(430, 445);
        var campos = Estilo.Columna();
        campos.Controls.Add(Estilo.Texto(titulo, 23, true));
        FlowLayoutPanel? navegacion = archivo is null ? null : CrearNavegacion(archivo);
        if (navegacion is not null) campos.Controls.Add(navegacion);
        Estilo.Campo(campos, "Nombre completo", nombre);
        Estilo.Campo(campos, "Exequátur", exequatur);
        Estilo.Campo(campos, "Contraseña", clave);
        var aceptar = Estilo.Boton(archivo is null ? "Guardar cuenta" : "Continuar", Estilo.Principal);
        aceptar.Click += (_, _) =>
        {
            if (Nombre.Length > 0 && Exequatur.Length > 0 && Clave.Length >= Archivo.MinimoClave)
                DialogResult = DialogResult.OK;
            else MessageBox.Show(this, $"Completa los campos. La contraseña debe tener al menos {Archivo.MinimoClave} caracteres.");
        };
        campos.Controls.Add(aceptar);
        if (archivo is not null)
        {
            ConfigurarSugerencias(archivo.UsuariosActivos());
            nombre.Validated += (_, _) => CompletarExequatur();
            exequatur.Validated += (_, _) => CompletarNombre();
            var recuperar = Estilo.Boton("Olvidé mi contraseña");
            recuperar.Click += (_, _) =>
            {
                using var dialogo = new RecuperarCuenta(archivo, Exequatur);
                dialogo.ShowDialog(this);
            };
            campos.Controls.Add(recuperar);
        }
        AcceptButton = aceptar;
        Shown += (_, _) => nombre.Focus();
        Controls.Add(campos);
    }

    private FlowLayoutPanel CrearNavegacion(Archivo archivo)
    {
        var opciones = new FlowLayoutPanel { AutoSize = true };
        opciones.Controls.Add(NuevaOpcionActiva());
        var registro = Estilo.Boton("Registrarse");
        registro.Click += (_, _) =>
        {
            using var dialogo = new Registro(archivo);
            if (dialogo.ShowDialog(this) != DialogResult.OK) return;
            ConfigurarSugerencias(archivo.UsuariosActivos());
            clave.Clear();
            MessageBox.Show(this, "Cuenta registrada. Ya puedes iniciar sesión.");
            nombre.Focus();
        };
        opciones.Controls.Add(registro);
        return opciones;
    }

    private void ConfigurarSugerencias(List<Medico> usuarios)
    {
        sugerenciasActivas = usuarios;
        Sugerir(nombre, usuarios.Select(usuario => usuario.Nombre).Distinct(StringComparer.OrdinalIgnoreCase));
        Sugerir(exequatur, usuarios.Select(usuario => usuario.Exequatur));
    }

    private void CompletarExequatur()
    {
        var coincidencias = sugerenciasActivas.Where(usuario =>
            string.Equals(usuario.Nombre, Nombre, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        if (coincidencias.Count == 1) exequatur.Text = coincidencias[0].Exequatur;
    }

    private void CompletarNombre()
    {
        var usuario = sugerenciasActivas.FirstOrDefault(cuenta =>
            string.Equals(cuenta.Exequatur, Exequatur, StringComparison.OrdinalIgnoreCase));
        if (usuario is not null) nombre.Text = usuario.Nombre;
    }

    private static void Sugerir(TextBox campo, IEnumerable<string> opciones)
    {
        var sugerencias = new AutoCompleteStringCollection();
        sugerencias.AddRange(opciones.ToArray());
        campo.AutoCompleteCustomSource = sugerencias;
        campo.AutoCompleteSource = AutoCompleteSource.CustomSource;
        campo.AutoCompleteMode = AutoCompleteMode.Suggest;
    }

    private static Button NuevaOpcionActiva()
    {
        var boton = Estilo.Boton("Iniciar sesión");
        boton.Enabled = false;
        return boton;
    }
}

internal sealed partial class Historias : Form
{
    private readonly Archivo archivo;
    private readonly Medico usuario;
    private readonly DataGridView lista = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AllowUserToAddRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        AutoGenerateColumns = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        AccessibleName = "Historias clínicas de pacientes"
    };
    private readonly Label resultados = Estilo.Texto("", 11);
    private readonly Button abrir = Estilo.Boton("Abrir historia", Estilo.Principal);
    private readonly Button exportar = Estilo.Boton("Exportar PDF");
    private readonly Button imprimir = Estilo.Boton("Imprimir");
    private readonly TextBox buscar = new() { PlaceholderText = "Buscar por nombre o cédula", Width = 240 };
    private List<Atencion> atenciones = [];

    internal Historias(Archivo archivo, Medico usuario, Func<HttpClient?>? conectar = null,
        System.Drawing.Printing.PrinterSettings? impresora = null)
    {
        this.archivo = archivo;
        this.usuario = usuario;
        this.impresora = impresora;
        Estilo.Ventana(this);
        Text = "Historias clínicas";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1020, 620);
        MinimumSize = new Size(695, 430);

        var cabecera = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 108,
            Padding = new Padding(18, 11, 18, 8),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        var servicio = Estilo.Texto("Emergencia ginecológica", 11, true);
        servicio.ForeColor = Estilo.Principal;
        cabecera.Controls.Add(servicio);
        cabecera.Controls.Add(Estilo.Texto("Historias clínicas", 26, true));
        var acciones = Estilo.Barra(DockStyle.Top);
        Accion(acciones, "Nueva historia", () => Editar(null), Estilo.Principal);
        if (usuario.Administrador)
        {
            Accion(acciones, "Alta médica", Alta);
            Accion(acciones, "Configuración", Configurar);
        }
        buscar.Width = 245;
        buscar.AccessibleName = "Buscar paciente por nombre o cédula";
        var rotuloBusqueda = Estilo.Texto("Buscar paciente", 11, true);
        rotuloBusqueda.Margin = new Padding(18, 12, 4, 0);
        var busqueda = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        busqueda.Controls.Add(rotuloBusqueda);
        buscar.Margin = new Padding(2, 10, 0, 0);
        busqueda.Controls.Add(buscar);
        acciones.Controls.Add(busqueda);
        var pie = Estilo.Barra(DockStyle.Bottom);
        resultados.Margin = new Padding(0, 12, 18, 0);
        pie.Controls.Add(resultados);
        pie.Controls.Add(abrir);
        pie.Controls.Add(imprimir);
        pie.Controls.Add(exportar);
        abrir.Click += (_, _) => AbrirSeleccion();
        imprimir.Click += (_, _) => { if (Seleccion?.Atencion is { } a) Imprimir(a); };
        exportar.Click += (_, _) => { if (Seleccion?.Atencion is { } a) Exportar(a); };
        ConfigurarLista();
        lista.CellDoubleClick += (_, evento) => { if (evento.RowIndex >= 0) AbrirSeleccion(); };
        lista.KeyDown += AbrirConTeclado;
        lista.SelectionChanged += (_, _) => ActualizarAcciones();
        buscar.TextChanged += (_, _) => Filtrar();

        var marco = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 10, 18, 10) };
        marco.Controls.Add(lista);
        Controls.Add(marco);
        Controls.Add(pie);
        Controls.Add(acciones);
        Controls.Add(cabecera);
        PrepararRecepcion(cabecera, conectar);
        Recargar();
    }

    private void AbrirConTeclado(object? remitente, KeyEventArgs evento)
    {
        if (evento.KeyCode != Keys.Enter) return;
        evento.Handled = true;
        evento.SuppressKeyPress = true;
        AbrirSeleccion();
    }

    private void ConfigurarLista()
    {
        lista.BorderStyle = BorderStyle.None;
        lista.BackgroundColor = Estilo.Papel;
        lista.GridColor = Estilo.Linea;
        lista.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        lista.EnableHeadersVisualStyles = false;
        lista.ColumnHeadersHeight = 39;
        lista.RowTemplate.Height = 46;
        lista.ColumnHeadersDefaultCellStyle.BackColor = Estilo.Fondo;
        lista.ColumnHeadersDefaultCellStyle.ForeColor = Estilo.Tinta;
        lista.ColumnHeadersDefaultCellStyle.SelectionBackColor = Estilo.Fondo;
        lista.ColumnHeadersDefaultCellStyle.SelectionForeColor = Estilo.Tinta;
        lista.DefaultCellStyle.ForeColor = Estilo.Tinta;
        lista.DefaultCellStyle.SelectionBackColor = Estilo.Principal;
        lista.DefaultCellStyle.SelectionForeColor = SystemInformation.HighContrast ? SystemColors.HighlightText : Color.White;
        lista.DefaultCellStyle.Padding = new Padding(8, 0, 4, 0);
        var paciente = new DataGridViewTextBoxColumn { HeaderText = "Paciente", DataPropertyName = nameof(FilaHistoria.Nombre), FillWeight = 49 };
        paciente.DefaultCellStyle.Font = new Font("Segoe UI", 12, FontStyle.Bold);
        lista.Columns.Add(paciente);
        lista.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Cédula", DataPropertyName = nameof(FilaHistoria.Cedula), FillWeight = 15 });
        lista.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Fecha y hora", DataPropertyName = nameof(FilaHistoria.Fecha), FillWeight = 21 });
        lista.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Admisión", DataPropertyName = nameof(FilaHistoria.Visita), FillWeight = 23 });
        lista.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Estado", DataPropertyName = nameof(FilaHistoria.Estado), FillWeight = 24 });
        lista.CellFormatting += ColorearEstado;
    }

    private static void Accion(Control panel, string texto, Action ejecutar, Color? color = null)
    {
        var boton = Estilo.Boton(texto, color);
        boton.Click += (_, _) => ejecutar();
        panel.Controls.Add(boton);
    }

    private FilaHistoria? Seleccion => lista.CurrentRow?.DataBoundItem as FilaHistoria;

    private void ActualizarAcciones()
    {
        abrir.Enabled = Seleccion is not null;
        exportar.Enabled = Seleccion?.Atencion is not null;
        imprimir.Enabled = Seleccion?.Atencion?.Finalizada == true;
    }

    private sealed record FilaHistoria(Atencion? Atencion, FilaAdmision? Admision)
    {
        public string Nombre => Atencion?.Nombre ?? Admision!.Nombre;
        public string Cedula => Atencion?.Valor("cedula") ?? Admision!.Cedula;
        public string Fecha => (Admision?.Llegada ?? Atencion!.Fecha).ToString("dd/MM/yyyy HH:mm");
        public string Visita => Admision is null ? "" : $"#{Admision.Visita}  {Admision.Prioridad}";
        public string Estado => Admision?.Estado ?? (Atencion!.Finalizada ? "Finalizada" : "Borrador");
    }

    private void Recargar()
    {
        atenciones = archivo.Listar(usuario);
        admisiones = archivo.Bandeja();
        Filtrar();
    }

    private void Filtrar()
    {
        var elegida = Seleccion;
        var consulta = buscar.Text.Trim();
        string cedula = consulta.Replace("-", "").Replace(" ", "");
        var vinculadas = admisiones.Where(a => a.Historia.HasValue).ToDictionary(a => a.Historia!.Value);
        var visibles = admisiones.Where(a => !a.Historia.HasValue).Select(a => new FilaHistoria(null, a))
            .Concat(atenciones.Select(a => new FilaHistoria(a, vinculadas.GetValueOrDefault(a.Id))))
            .Where(fila => fila.Nombre.Contains(consulta, StringComparison.CurrentCultureIgnoreCase)
                || (cedula.Length > 0 && fila.Cedula.Contains(cedula, StringComparison.Ordinal)))
            .ToList();
        lista.DataSource = visibles;
        if (elegida is not null)
        {
            int indice = visibles.FindIndex(fila => elegida.Admision is { } d
                ? fila.Admision?.Id == d.Id : fila.Atencion?.Id == elegida.Atencion!.Id);
            if (indice >= 0) lista.CurrentCell = lista.Rows[indice].Cells[0];
        }
        resultados.Text = visibles.Count switch
        {
            0 when atenciones.Count == 0 && admisiones.Count == 0 => "Todavía no hay historias",
            0 => "No se encontraron pacientes",
            1 => "1 historia",
            _ => $"{visibles.Count} historias"
        };
        ActualizarAcciones();
    }

    private void AbrirSeleccion()
    {
        var fila = Seleccion;
        if (fila is null) return;
        try
        {
            Editar(fila.Atencion ?? archivo.AbrirDerivacion(fila.Admision!.Id, usuario));
        }
        catch (Exception error) when (error is SqliteException or InvalidOperationException)
        {
            MessageBox.Show(this, "No se pudo abrir esta atención. Actualiza la bandeja y vuelve a intentarlo.");
        }
    }

    private void Editar(Atencion? atencion)
    {
        using var editor = new Editor(archivo, usuario, atencion, imprimirAlFinalizar: true);
        var resultado = editor.ShowDialog(this);
        Recargar();
        if (resultado == DialogResult.OK && editor.SolicitaImpresion)
            Imprimir(atenciones.Single(a => a.Id == editor.HistoriaGuardada));
    }

    private void Configurar()
    {
        try
        {
            using var configuracion = new Configuracion(archivo, usuario);
            configuracion.ShowDialog(this);
        }
        catch (InvalidOperationException error) { MessageBox.Show(this, error.Message); }
    }

    private void Alta()
    {
        using var alta = new Cuenta("Alta de médico");
        if (alta.ShowDialog(this) != DialogResult.OK) return;
        try { archivo.Alta(usuario, alta.Exequatur, alta.Nombre, alta.Clave); }
        catch (SqliteException) { MessageBox.Show(this, "No se pudo crear la cuenta. Verifica si el exequátur ya existe."); }
        catch (InvalidOperationException error) { MessageBox.Show(this, error.Message); }
    }

    private void Exportar(Atencion atencion)
    {
        var preparada = archivo.PrepararExportacion(atencion);
        if (preparada.Avisos.Count > 0 || preparada.Correcciones.Count > 0)
        {
            using var revision = new RevisionExportacion(preparada);
            var respuesta = revision.ShowDialog(this);
            if (respuesta == DialogResult.Retry) Editar(atencion);
            if (respuesta != DialogResult.OK) return;
        }
        using var selector = new SaveFileDialog { Filter = "Documento PDF (*.pdf)|*.pdf", FileName = $"Historia-{atencion.Id}.pdf" };
        if (selector.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            Informe.Exportar(preparada.Atencion, selector.FileName);
            if (MessageBox.Show(this, "PDF guardado. ¿Deseas abrirlo?", Text, MessageBoxButtons.YesNo) == DialogResult.Yes)
                Process.Start(new ProcessStartInfo(selector.FileName) { UseShellExecute = true });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(this, "Comprueba que la carpeta permita escribir y que Windows tenga un visor PDF asociado.");
        }
    }
}
