namespace Gineco;

internal sealed class RecuperarCuenta : Form
{
    private readonly TextBox objetivo = new() { Width = 340, MaxLength = 40 };
    private readonly ComboBox administrador = new() { Width = 340, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox claveAdministrador = new() { Width = 340, UseSystemPasswordChar = true };
    private readonly TextBox nueva = new() { Width = 340, UseSystemPasswordChar = true };
    private readonly TextBox confirmacion = new() { Width = 340, UseSystemPasswordChar = true };

    internal RecuperarCuenta(Archivo archivo, string exequatur)
    {
        Estilo.Ventana(this);
        Text = "Restablecer contraseña";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(430, 530);
        objetivo.Text = exequatur;
        administrador.DataSource = archivo.UsuariosActivos().Where(usuario => usuario.Administrador).ToList();
        administrador.DisplayMember = nameof(Medico.Nombre);
        var panel = Estilo.Columna();
        panel.Padding = new Padding(24, 10, 24, 18);
        Estilo.Campo(panel, "Exequátur de la cuenta", objetivo);
        Estilo.Campo(panel, "Administrador que autoriza", administrador);
        Estilo.Campo(panel, "Contraseña del administrador", claveAdministrador);
        Estilo.Campo(panel, $"Nueva contraseña (mínimo {Archivo.MinimoClave} caracteres)", nueva);
        Estilo.Campo(panel, "Confirma la nueva contraseña", confirmacion);
        var guardar = Estilo.Boton("Restablecer", Estilo.Principal);
        guardar.Click += (_, _) => Restablecer(archivo);
        panel.Controls.Add(guardar);
        AcceptButton = guardar;
        Controls.Add(panel);
        Controls.Add(Estilo.Encabezado(Text, "Un administrador de esta PC debe autorizar el cambio."));
    }

    private void Restablecer(Archivo archivo)
    {
        if (nueva.Text != confirmacion.Text || nueva.Text.Length < Archivo.MinimoClave)
        {
            MessageBox.Show(this, $"Las contraseñas deben coincidir y tener al menos {Archivo.MinimoClave} caracteres.");
            return;
        }
        try
        {
            if (administrador.SelectedItem is not Medico autor ||
                !archivo.RestablecerClave(objetivo.Text.Trim(), autor, claveAdministrador.Text, nueva.Text))
            {
                MessageBox.Show(this, "Verifica la cuenta y las credenciales del administrador.");
                return;
            }
            MessageBox.Show(this, "Contraseña actualizada. Ya puedes iniciar sesión.");
            DialogResult = DialogResult.OK;
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            MessageBox.Show(this, "No se pudo guardar la nueva contraseña. Vuelve a intentarlo.");
        }
    }
}
