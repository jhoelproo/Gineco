namespace Gineco;

internal sealed partial class Historias
{
    private void PrepararActualizaciones(Control pie, IActualizaciones actualizaciones)
    {
        var boton = Estilo.Boton("Actualizaciones");
        pie.Controls.Add(boton);
        boton.Click += (_, _) =>
        {
            using var ventana = new VentanaActualizaciones(actualizaciones, usuario.Administrador);
            if (ventana.ShowDialog(this) == DialogResult.OK) Close();
            else boton.Text = TituloActualizaciones(actualizaciones);
        };
        Shown += async (_, _) =>
        {
            if (!actualizaciones.Configurada || !actualizaciones.Instalada) return;
            boton.Enabled = false;
            try
            {
                await actualizaciones.Buscar();
                if (!IsDisposed) boton.Text = TituloActualizaciones(actualizaciones);
            }
            catch (Exception) { }
            finally { if (!IsDisposed) boton.Enabled = true; }
        };
    }

    private static string TituloActualizaciones(IActualizaciones actualizaciones) =>
        actualizaciones.Disponible is null ? "Actualizaciones" : "Actualización disponible";
}

internal sealed class VentanaActualizaciones : Form
{
    private readonly IActualizaciones actualizaciones;
    private readonly Label estado = Estilo.Texto("", 11);
    private readonly Button buscar = Estilo.Boton("Buscar ahora");
    private readonly Button instalar = Estilo.Boton("Instalar y reiniciar", Estilo.Principal);
    private readonly Button guardar = Estilo.Boton("Guardar acceso");
    private readonly TextBox token = new() { UseSystemPasswordChar = true, MaxLength = 255, Width = 360 };
    private readonly CancellationTokenSource cierre = new();

    internal VentanaActualizaciones(IActualizaciones actualizaciones, bool administrador)
    {
        this.actualizaciones = actualizaciones;
        Estilo.Ventana(this);
        Text = "Actualizaciones";
        ClientSize = new Size(530, 285);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = MaximizeBox = false;

        var contenido = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(18),
            AutoScroll = true
        };
        contenido.Controls.Add(Estilo.Texto($"Versión {actualizaciones.Version}", 16, true));
        estado.MaximumSize = new Size(470, 0);
        contenido.Controls.Add(estado);
        if (administrador && actualizaciones.Instalada)
        {
            Estilo.Campo(contenido, "Token de lectura de GitHub", token);
            contenido.Controls.Add(guardar);
            guardar.Click += async (_, _) => await GuardarYBuscar();
        }
        var acciones = Estilo.Barra(DockStyle.None);
        acciones.AutoSize = true;
        var publicaciones = Estilo.Boton("Abrir GitHub");
        var cerrar = Estilo.Boton("Cerrar");
        cerrar.DialogResult = DialogResult.Cancel;
        acciones.Controls.AddRange([buscar, instalar, publicaciones, cerrar]);
        contenido.Controls.Add(acciones);
        Controls.Add(contenido);
        CancelButton = cerrar;
        buscar.Click += async (_, _) => await Buscar();
        instalar.Click += async (_, _) => await Instalar();
        publicaciones.Click += (_, _) => AbrirPublicaciones();
        FormClosed += (_, _) => cierre.Cancel();
        Disposed += (_, _) => cierre.Dispose();
        MostrarEstado();
    }

    private void MostrarEstado()
    {
        estado.Text = actualizaciones is { Instalada: false } ? "Instala Gineco desde GitHub para habilitar las actualizaciones."
            : actualizaciones is { Configurada: false } ? "El administrador debe configurar el acceso a las actualizaciones."
            : actualizaciones.Disponible is { } version ? $"Disponible la versión {version}."
            : "Busca la última versión publicada.";
        HabilitarAcciones(true);
    }

    private void HabilitarAcciones(bool habilitar)
    {
        buscar.Enabled = habilitar && actualizaciones.Instalada && actualizaciones.Configurada;
        instalar.Enabled = habilitar && actualizaciones.Disponible is not null;
        guardar.Enabled = token.Enabled = habilitar;
    }

    private async Task GuardarYBuscar()
    {
        try { actualizaciones.GuardarAcceso(token.Text); }
        catch (ArgumentException error) { estado.Text = error.Message; return; }
        catch (Exception) { estado.Text = "Windows no pudo guardar el acceso para esta cuenta."; return; }
        token.Clear();
        await Buscar();
    }

    private async Task Buscar()
    {
        HabilitarAcciones(false);
        estado.Text = "Consultando GitHub…";
        try
        {
            await actualizaciones.Buscar();
            if (IsDisposed) return;
            estado.Text = actualizaciones.Disponible is { } version
                ? $"Disponible la versión {version}." : "Ya tienes la última versión.";
        }
        catch (Exception)
        {
            if (!IsDisposed) estado.Text = "No se pudo consultar GitHub. Comprueba Internet y el acceso de lectura.";
        }
        finally { if (!IsDisposed) HabilitarAcciones(true); }
    }

    private async Task Instalar()
    {
        HabilitarAcciones(false);
        estado.Text = "Descargando actualización…";
        var progreso = new Progress<int>(valor =>
        {
            if (!IsDisposed) estado.Text = $"Descargando actualización… {valor} %";
        });
        try
        {
            await actualizaciones.Descargar(((IProgress<int>)progreso).Report, cierre.Token);
            if (IsDisposed || cierre.IsCancellationRequested) return;
            actualizaciones.AplicarAlCerrar();
            DialogResult = DialogResult.OK;
        }
        catch (Exception)
        {
            if (!IsDisposed) estado.Text = "No se pudo instalar la actualización. Puedes volver a intentarlo.";
        }
        finally { if (!IsDisposed) HabilitarAcciones(true); }
    }

    private void AbrirPublicaciones()
    {
        try { Actualizaciones.AbrirPublicaciones(); }
        catch (System.ComponentModel.Win32Exception) { estado.Text = "Windows no pudo abrir GitHub en el navegador."; }
    }
}
