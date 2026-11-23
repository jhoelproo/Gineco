namespace Gineco;

internal sealed class AccesoCompartido : Form
{
    private readonly CuentasCompartidas cuentas;
    private readonly Archivo? archivo;
    private readonly string? archivoVinculo;
    private readonly TextBox exequatur = new() { Width = 320, MaxLength = 40, Name = "exequaturCompartido" };
    private readonly TextBox clave = new() { Width = 320, MaxLength = 256, UseSystemPasswordChar = true, Name = "claveCompartida" };
    private readonly Label estado = Estilo.Texto("", 10);
    private readonly FlowLayoutPanel contenido = Estilo.Columna();
    private readonly CancellationTokenSource cierre = new();

    internal SesionCompartida? Sesion { get; private set; }
    internal bool UsarCuentaLocal { get; private set; }

    internal AccesoCompartido(CuentasCompartidas cuentas, Archivo? archivo = null, string? archivoVinculo = null)
    {
        this.cuentas = cuentas;
        this.archivo = archivo;
        this.archivoVinculo = archivoVinculo;
        Estilo.Ventana(this);
        Text = archivo is null ? "Autorizar como administrador" : "Iniciar sesión en Gineco";
        ClientSize = new Size(455, 500);
        StartPosition = FormStartPosition.CenterParent;
        contenido.Controls.Add(Estilo.Texto(Text, 19, true));
        Estilo.Campo(contenido, "Exequátur", exequatur);
        Estilo.Campo(contenido, "Contraseña", clave);
        var entrar = Estilo.Boton(archivo is null ? "Autorizar" : "Iniciar sesión", Estilo.Principal);
        entrar.Click += async (_, _) => await Ejecutar(Iniciar);
        contenido.Controls.Add(entrar);
        estado.MaximumSize = new Size(380, 0);
        estado.AccessibleRole = AccessibleRole.Alert;
        contenido.Controls.Add(estado);
        if (archivo is not null) AgregarOpciones(archivo);
        Controls.Add(contenido);
        AcceptButton = entrar;
        Shown += (_, _) => exequatur.Focus();
        FormClosed += (_, _) => cierre.Cancel();
        Disposed += (_, _) => cierre.Dispose();
    }

    private void AgregarOpciones(Archivo archivo)
    {
        var acciones = Estilo.Barra(DockStyle.None);
        var registrar = Estilo.Boton("Registrarse");
        registrar.Click += async (_, _) => await Ejecutar(async () =>
        {
            if (await GestionarCuentaCompartida.Registrar(this, cuentas, cierre.Token)) estado.Text = "Cuenta registrada. Ya puedes iniciar sesión.";
        });
        var recuperar = Estilo.Boton("Olvidé mi contraseña");
        recuperar.Click += (_, _) =>
        {
            if (GestionarCuentaCompartida.Recuperar(this, cuentas, exequatur.Text.Trim()))
                estado.Text = "Contraseña actualizada. Ya puedes iniciar sesión.";
        };
        acciones.Controls.AddRange([registrar, recuperar]);
        contenido.Controls.Add(acciones);

        if (!archivo.SinCuentas())
        {
            var local = Estilo.Boton("Usar una cuenta de esta PC");
            local.Click += (_, _) => { UsarCuentaLocal = true; DialogResult = DialogResult.Retry; };
            contenido.Controls.Add(local);
        }
        if (File.Exists(archivoVinculo))
        {
            var vincular = Estilo.Boton("Vincular mi administrador local");
            vincular.Click += async (_, _) => await Ejecutar(Vincular);
            contenido.Controls.Add(vincular);
        }
    }

    private async Task Iniciar()
    {
        var sesion = await cuentas.Entrar(exequatur.Text.Trim(), clave.Text, cierre.Token);
        if (sesion is null || archivo is null && !sesion.Medico.Administrador)
        {
            estado.Text = "Verifica el exequátur y la contraseña.";
            return;
        }
        Sesion = sesion;
        DialogResult = DialogResult.OK;
    }

    private async Task Vincular()
    {
        var candidato = archivo!.UsuariosActivos().FirstOrDefault(medico => medico.Administrador &&
            string.Equals(medico.Exequatur, exequatur.Text.Trim(), StringComparison.OrdinalIgnoreCase));
        if (candidato is null || archivo.Entrar(candidato.Exequatur, candidato.Nombre, clave.Text) is not { Administrador: true } admin)
        {
            estado.Text = "Introduce las credenciales del administrador de esta PC.";
            return;
        }
        var codigo = (await File.ReadAllTextAsync(archivoVinculo!, cierre.Token)).Trim();
        await cuentas.Vincular(admin, clave.Text, codigo, cierre.Token);
        File.Delete(archivoVinculo!);
        estado.Text = "Administrador vinculado. Ya puedes iniciar sesión en Gineco desde otra PC.";
        await Iniciar();
    }

    private async Task Ejecutar(Func<Task> accion)
    {
        contenido.Enabled = false;
        estado.Text = "Consultando las cuentas de Gineco…";
        try { await accion(); }
        catch (InvalidOperationException error) { estado.Text = error.Message; }
        catch (Exception error) when (GestionarCuentaCompartida.FalloDeConexion(error))
        {
            if (!IsDisposed) estado.Text = "No se pudo completar la solicitud. Comprueba la conexión e inténtalo de nuevo.";
        }
        finally { if (!IsDisposed) contenido.Enabled = true; }
    }
}
