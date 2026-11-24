using Microsoft.Data.Sqlite;

namespace Gineco;

internal sealed class AutorizarRetiroLocal : Form
{
    private readonly ComboBox cuenta = new() { Width = 320, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox clave = new() { Width = 320, MaxLength = 256, UseSystemPasswordChar = true };
    private readonly Label estado = Estilo.Texto("", 10);

    internal Medico? Administrador { get; private set; }

    internal AutorizarRetiroLocal(Archivo archivo)
    {
        Estilo.Ventana(this);
        Text = "Autorizar el retiro";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(455, 335);
        cuenta.DisplayMember = nameof(Medico.Nombre);
        cuenta.DataSource = archivo.UsuariosActivos().Where(usuario => usuario.Administrador).ToList();
        var datos = Estilo.Columna();
        Estilo.Campo(datos, "Cuenta de administrador", cuenta);
        Estilo.Campo(datos, "Contraseña del administrador", clave);
        estado.AccessibleRole = AccessibleRole.Alert;
        estado.MaximumSize = new Size(380, 0);
        datos.Controls.Add(estado);

        var botones = Estilo.Barra(DockStyle.Bottom);
        var autorizar = Estilo.Boton("Autorizar retiro", Estilo.Principal);
        autorizar.Click += (_, _) => Autorizar(archivo);
        var cancelar = Estilo.Boton("Cancelar");
        cancelar.DialogResult = DialogResult.Cancel;
        botones.Controls.AddRange([autorizar, cancelar]);
        Controls.Add(datos);
        Controls.Add(botones);
        Controls.Add(Estilo.Encabezado(Text, "El administrador autoriza el retiro de esta atención."));
        AcceptButton = autorizar;
        CancelButton = cancelar;
        ActiveControl = clave;
    }

    private void Autorizar(Archivo archivo)
    {
        try
        {
            if (cuenta.SelectedItem is Medico elegido &&
                archivo.Entrar(elegido.Exequatur, elegido.Nombre, clave.Text) is { Administrador: true } administrador)
            {
                Administrador = administrador;
                DialogResult = DialogResult.OK;
                return;
            }
            estado.Text = "Verifica la cuenta y la contraseña del administrador.";
            clave.Focus();
        }
        catch (SqliteException)
        {
            estado.Text = "No se pudo verificar la cuenta. Vuelve a intentarlo.";
        }
        finally
        {
            clave.Clear();
        }
    }
}
