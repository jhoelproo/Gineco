using System.Text.Json;

namespace Gineco;

internal static class GestionarCuentaCompartida
{
    internal static async Task<bool> Registrar(Form origen, CuentasCompartidas cuentas, CancellationToken cancelacion)
    {
        var autorizacion = Autorizar(origen, cuentas);
        if (autorizacion is null) return false;
        using var cuenta = new Cuenta("Registrar cuenta compartida");
        if (cuenta.ShowDialog(origen) != DialogResult.OK) return false;
        await cuentas.Registrar(cuenta.Exequatur, cuenta.Nombre, cuenta.Clave, autorizacion, cancelacion);
        return true;
    }

    internal static bool Recuperar(Form origen, CuentasCompartidas cuentas, string exequatur)
    {
        if (Autorizar(origen, cuentas) is not { } autorizacion) return false;
        using var recuperacion = new RecuperarCuentaCompartida(cuentas, autorizacion, exequatur);
        return recuperacion.ShowDialog(origen) == DialogResult.OK;
    }

    internal static SesionCompartida? Autorizar(Form origen, CuentasCompartidas cuentas)
    {
        using var acceso = new AccesoCompartido(cuentas);
        return acceso.ShowDialog(origen) == DialogResult.OK ? acceso.Sesion : null;
    }

    internal static bool FalloDeConexion(Exception error) =>
        error is HttpRequestException or OperationCanceledException or IOException or JsonException;
}

internal sealed class RecuperarCuentaCompartida : Form
{
    private readonly TextBox objetivo = new() { Width = 320, MaxLength = 40 };
    private readonly TextBox nueva = new() { Width = 320, MaxLength = 256, UseSystemPasswordChar = true };
    private readonly TextBox confirmacion = new() { Width = 320, MaxLength = 256, UseSystemPasswordChar = true };
    private readonly Label estado = Estilo.Texto("", 10);
    private readonly CancellationTokenSource cierre = new();

    internal RecuperarCuentaCompartida(CuentasCompartidas cuentas, SesionCompartida administrador, string exequatur)
    {
        Estilo.Ventana(this);
        Text = "Restablecer contraseña";
        ClientSize = new Size(455, 430);
        StartPosition = FormStartPosition.CenterParent;
        objetivo.Text = exequatur;
        var panel = Estilo.Columna();
        panel.Padding = new Padding(24, 10, 24, 18);
        Estilo.Campo(panel, "Exequátur de la cuenta", objetivo);
        Estilo.Campo(panel, "Nueva contraseña", nueva);
        Estilo.Campo(panel, "Confirma la nueva contraseña", confirmacion);
        var guardar = Estilo.Boton("Restablecer", Estilo.Principal);
        guardar.Click += async (_, _) =>
        {
            panel.Enabled = false;
            try { await Guardar(cuentas, administrador); }
            finally { if (!IsDisposed) panel.Enabled = true; }
        };
        panel.Controls.Add(guardar);
        estado.MaximumSize = new Size(380, 0);
        estado.AccessibleRole = AccessibleRole.Alert;
        panel.Controls.Add(estado);
        Controls.Add(panel);
        Controls.Add(Estilo.Encabezado(Text, "El cambio se aplicará en todos los puestos vinculados."));
        AcceptButton = guardar;
        FormClosed += (_, _) => cierre.Cancel();
        Disposed += (_, _) => cierre.Dispose();
    }

    private async Task Guardar(CuentasCompartidas cuentas, SesionCompartida administrador)
    {
        if (nueva.Text.Length < Archivo.MinimoClave || nueva.Text != confirmacion.Text)
        {
            estado.Text = $"Escribe la misma contraseña en ambos campos. Mínimo {Archivo.MinimoClave} caracteres.";
            return;
        }
        try
        {
            await cuentas.Restablecer(objetivo.Text.Trim(), nueva.Text, administrador, cierre.Token);
            if (IsDisposed) return;
            DialogResult = DialogResult.OK;
        }
        catch (InvalidOperationException error) { estado.Text = error.Message; }
        catch (Exception error) when (GestionarCuentaCompartida.FalloDeConexion(error))
        { if (!IsDisposed) estado.Text = "Comprueba la conexión y vuelve a intentarlo."; }
    }
}
