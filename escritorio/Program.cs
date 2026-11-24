using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Velopack;

namespace Gineco;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        VelopackApp.Build().Run();
        ApplicationConfiguration.Initialize();
        try
        {
            var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gineco");
            Directory.CreateDirectory(carpeta);
            using var archivo = new Archivo(Path.Combine(carpeta, "historias.sqlite3"));
            using var cuentas = CuentasCompartidas.DelHospital();
            IniciarCompartido(archivo, new Actualizaciones(), cuentas, Path.Combine(carpeta, "vinculo-cuentas.dat"), EnlaceAdmision.DesdeEstacion);
        }
        catch (Exception error) when (error is IOException or SqliteException or UnauthorizedAccessException)
        {
            MessageBox.Show("No se pudo abrir el archivo clínico local.", Clinica.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    internal static void IniciarCompartido(Archivo archivo, IActualizaciones? actualizaciones, CuentasCompartidas cuentas,
        string? vinculo = null, Func<HttpClient?>? conectar = null)
    {
        while (true)
        {
            using var acceso = new AccesoCompartido(cuentas, archivo, vinculo);
            acceso.ShowDialog();
            if (acceso.UsarCuentaLocal) { Iniciar(archivo, actualizaciones, conectar); return; }
            if (acceso.Sesion is not { } sesion) return;
            archivo.EstablecerSesionCompartida(sesion.Medico);
            try
            {
                using var historias = new Historias(archivo, sesion.Medico, conectar, actualizaciones: actualizaciones, cuentasCompartidas: cuentas);
                Application.Run(historias);
                if (!historias.CambioDeMedico) return;
            }
            finally { archivo.EstablecerSesionCompartida(null); }
        }
    }

    internal static void Iniciar(Archivo archivo, IActualizaciones? actualizaciones, Func<HttpClient?>? conectar = null)
    {
        if (archivo.SinCuentas())
        {
            using var alta = new Cuenta("Crear administrador");
            if (alta.ShowDialog() != DialogResult.OK) return;
            archivo.Alta(null, alta.Exequatur, alta.Nombre, alta.Clave);
        }
        while (true)
        {
            using var acceso = new Cuenta("Iniciar sesión", archivo);
            if (acceso.ShowDialog() != DialogResult.OK) return;
            var usuario = archivo.Entrar(acceso.Exequatur, acceso.Nombre, acceso.Clave);
            if (usuario is not null)
            {
                using var historias = new Historias(archivo, usuario, conectar, actualizaciones: actualizaciones);
                Application.Run(historias);
                if (!historias.CambioDeMedico) return;
                continue;
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
        ClientSize = new Size(445, 470);
        nombre.Name = "nombreCuenta";
        exequatur.Name = "exequaturCuenta";
        clave.Name = "claveCuenta";
        var campos = Estilo.Columna();
        campos.Padding = new Padding(24, 10, 24, 18);
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
        Controls.Add(Estilo.Encabezado(titulo, archivo is null ? "Registra el exequátur del médico." : "Emergencia ginecológica"));
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
    private readonly CuentasCompartidas? cuentasCompartidas;
    private readonly DataGridView lista = Estilo.Tabla("Historias clínicas de pacientes");
    private readonly Label bandejaVacia = Estilo.MensajeVacio("");
    private readonly Label resultados = Estilo.Texto("", 10);
    private readonly Button abrir = Estilo.Boton("Abrir historia", Estilo.Principal);
    private readonly Button exportar = Estilo.Boton("Exportar PDF");
    private readonly Button imprimir = Estilo.Boton("Imprimir");
    private readonly Button retirar = Estilo.Boton("Eliminar del listado");
    private readonly TextBox buscar = new() { PlaceholderText = "Buscar por nombre o cédula", Width = 240 };
    private List<Atencion> atenciones = [];
    internal bool CambioDeMedico { get; private set; }

    internal Historias(Archivo archivo, Medico usuario, Func<HttpClient?>? conectar = null,
        System.Drawing.Printing.PrinterSettings? impresora = null, IActualizaciones? actualizaciones = null,
        CuentasCompartidas? cuentasCompartidas = null)
    {
        this.archivo = archivo;
        this.usuario = usuario;
        this.cuentasCompartidas = cuentasCompartidas;
        this.impresora = impresora;
        Estilo.Ventana(this);
        Text = "Historias clínicas";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1020, 620);
        MinimumSize = new Size(780, 540);

        var cabecera = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(18, 11, 18, 10),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        var servicio = Estilo.Texto("Emergencia ginecológica", 10, true);
        servicio.ForeColor = Estilo.Principal;
        cabecera.Controls.Add(servicio);
        cabecera.Controls.Add(Estilo.Texto("Historias clínicas", 26, true));
        var acciones = Estilo.Barra(DockStyle.Top);
        Accion(acciones, "Nueva historia", () => Editar(null), Estilo.Principal);
        Accion(acciones, "Cambiar médico", () => { CambioDeMedico = true; Close(); });
        Button? configuracion = null;
        if (usuario.Administrador)
        {
            var alta = Estilo.Boton("Registrar médico");
            alta.Click += async (_, _) => await Alta(alta);
            acciones.Controls.Add(alta);
            configuracion = Estilo.Boton("Configuración");
            configuracion.Click += (_, _) => Configurar();
        }
        var barraSuperior = CrearBarraSuperior(acciones, configuracion);
        var pie = Estilo.Barra(DockStyle.Bottom);
        resultados.Margin = new Padding(0, 12, 18, 0);
        pie.Controls.Add(resultados);
        pie.Controls.Add(abrir);
        pie.Controls.Add(imprimir);
        pie.Controls.Add(exportar);
        pie.Controls.Add(retirar);
        retirar.Click += (_, _) => RetirarSeleccion();
        if (actualizaciones is not null) PrepararActualizaciones(pie, actualizaciones);
        abrir.Click += (_, _) => AbrirSeleccion();
        imprimir.Click += (_, _) => { if (Seleccion?.Atencion is { } a) Imprimir(a); };
        exportar.Click += (_, _) => { if (Seleccion?.Atencion is { } a) Exportar(a); };
        ConfigurarLista();
        PrepararMenuHistoria();
        lista.CellDoubleClick += (_, evento) => { if (evento.RowIndex >= 0) AbrirSeleccion(); };
        lista.KeyDown += AbrirConTeclado;
        lista.SelectionChanged += (_, _) => ActualizarAcciones();
        buscar.TextChanged += (_, _) => Filtrar();

        var marco = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 10, 18, 10) };
        marco.Controls.Add(lista);
        marco.Controls.Add(bandejaVacia);
        Controls.Add(marco);
        Controls.Add(CrearFiltros());
        Controls.Add(pie);
        Controls.Add(barraSuperior);
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
        Estilo.PresentarTabla(lista);
        lista.ReadOnly = true;
        lista.RowTemplate.Height = 46;
        lista.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        var paciente = new DataGridViewTextBoxColumn { HeaderText = "Paciente", DataPropertyName = nameof(FilaHistoria.Nombre), FillWeight = 49, MinimumWidth = 160 };
        paciente.DefaultCellStyle.Font = Estilo.LetraFecha;
        lista.Columns.Add(paciente);
        lista.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Cédula", DataPropertyName = nameof(FilaHistoria.Cedula), FillWeight = 17, MinimumWidth = 120 });
        var fecha = new DataGridViewTextBoxColumn { HeaderText = "Fecha y hora", DataPropertyName = nameof(FilaHistoria.Fecha), FillWeight = 25, MinimumWidth = 184 };
        fecha.DefaultCellStyle.Font = Estilo.LetraFecha;
        lista.Columns.Add(fecha);
        lista.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Admisión", DataPropertyName = nameof(FilaHistoria.Visita), FillWeight = 12, MinimumWidth = 87 });
        lista.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Estado", DataPropertyName = nameof(FilaHistoria.Estado), FillWeight = 24, MinimumWidth = 163 });
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
        abrir.Enabled = Seleccion is { } fila && fila.Admision?.SoloRemota != true;
        exportar.Enabled = Seleccion?.Atencion is not null;
        imprimir.Enabled = Seleccion?.Atencion?.Finalizada == true;
        retirar.Enabled = Seleccion is not null;
    }

    private void AbrirSeleccion()
    {
        var fila = Seleccion;
        if (fila is null || fila.Admision?.SoloRemota == true) return;
        try
        {
            Editar(fila.Atencion ?? archivo.AbrirDerivacion(fila.Admision!.Id, usuario));
        }
        catch (Exception error) when (error is SqliteException or InvalidOperationException)
        {
            MessageBox.Show(this, "No se pudo abrir esta atención. Actualiza la bandeja y vuelve a intentarlo.");
        }
    }

    private void RetirarSeleccion()
    {
        if (Seleccion is not { } fila) return;
        if (MessageBox.Show(this, $"¿Quitar a {fila.Nombre} del listado de Ginecología? La historia y su registro de auditoría se conservan.",
            "Eliminar del listado", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        try
        {
            if (AutorizarYRetirar(fila)) Recargar();
        }
        catch (Exception error) when (error is SqliteException or InvalidOperationException or ArgumentException)
        {
            MessageBox.Show(this, "No se pudo retirar la atención. Comprueba que siga en el listado.", Text);
        }
    }

    private bool AutorizarYRetirar(FilaHistoria fila)
    {
        long? historia = fila.Atencion?.Id;
        Guid? derivacion = fila.Admision?.Id;
        if (cuentasCompartidas is { } cuentas)
        {
            if (GestionarCuentaCompartida.Autorizar(this, cuentas) is not { } autorizacion) return false;
            archivo.Retirar(usuario, historia, derivacion, autorizacion);
        }
        else
        {
            using var permiso = new AutorizarRetiroLocal(archivo);
            if (permiso.ShowDialog(this) != DialogResult.OK || permiso.Administrador is not { } administrador) return false;
            archivo.Retirar(administrador, historia, derivacion);
        }
        return true;
    }

    private void Editar(Atencion? atencion, string? revisarCampo = null)
    {
        using var editor = new Editor(archivo, usuario, atencion, imprimirAlFinalizar: true);
        if (revisarCampo is not null) editor.Shown += (_, _) => editor.EnfocarCampo(revisarCampo);
        var resultado = editor.ShowDialog(this);
        Recargar();
        if (resultado == DialogResult.OK && editor.SolicitaImpresion &&
            atenciones.FirstOrDefault(a => a.Id == editor.HistoriaGuardada) is { } guardada)
            Imprimir(guardada);
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

    private async Task Alta(Button boton)
    {
        boton.Enabled = false;
        try
        {
            if (cuentasCompartidas is null) AltaLocal();
            else if (await GestionarCuentaCompartida.Registrar(this, cuentasCompartidas, cierre.Token) && !IsDisposed)
                resultados.Text = "Cuenta registrada. Ya se puede usar en otra computadora.";
        }
        catch (InvalidOperationException error) { MessageBox.Show(this, error.Message, Text); }
        catch (Exception error) when (GestionarCuentaCompartida.FalloDeConexion(error))
        { if (!IsDisposed) MessageBox.Show(this, "Comprueba la conexión y vuelve a intentarlo.", Text); }
        finally { if (!IsDisposed) boton.Enabled = true; }
    }

    private void AltaLocal()
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
        if (preparada.Revisiones.Count > 0 || preparada.Correcciones.Count > 0)
        {
            using var revision = new RevisionExportacion(preparada);
            var respuesta = revision.ShowDialog(this);
            if (respuesta == DialogResult.Retry) Editar(atencion, revision.CampoParaRevisar);
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
