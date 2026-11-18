namespace Gineco;

internal sealed class Registro : Form
{
    private readonly ComboBox administrador = new() { Width = 320, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox autorizacion = new() { Width = 320, UseSystemPasswordChar = true };

    internal Registro(Archivo archivo)
    {
        Estilo.Ventana(this);
        Text = "Registro de cuenta médica";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(430, 300);
        administrador.DataSource = archivo.UsuariosActivos().Where(usuario => usuario.Administrador).ToList();
        administrador.DisplayMember = nameof(Medico.Nombre);
        var panel = Estilo.Columna();
        panel.Controls.Add(Estilo.Texto(Text, 20, true));
        Estilo.Campo(panel, "Administrador que autoriza el registro", administrador);
        Estilo.Campo(panel, "Contraseña del administrador", autorizacion);
        var continuar = Estilo.Boton("Continuar al registro", Estilo.Principal);
        continuar.Click += (_, _) => Registrar(archivo);
        panel.Controls.Add(continuar);
        AcceptButton = continuar;
        Controls.Add(panel);
    }

    private void Registrar(Archivo archivo)
    {
        if (administrador.SelectedItem is not Medico elegido ||
            archivo.Entrar(elegido.Exequatur, elegido.Nombre, autorizacion.Text) is not { Administrador: true } autor)
        {
            MessageBox.Show(this, "Verifica las credenciales del administrador.");
            return;
        }
        autorizacion.Clear();
        using var cuenta = new Cuenta("Registrar cuenta médica");
        if (cuenta.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            archivo.Alta(autor, cuenta.Exequatur, cuenta.Nombre, cuenta.Clave);
            DialogResult = DialogResult.OK;
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            MessageBox.Show(this, "No se pudo registrar la cuenta. Comprueba si el exequátur ya existe.");
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(this, error.Message);
        }
    }
}
