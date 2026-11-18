using System.Text.RegularExpressions;

namespace Gineco;

internal sealed class EntradaVital : FlowLayoutPanel
{
    private readonly TextBox primera = new() { Width = 76 };
    private readonly TextBox? segunda;
    private readonly string original;
    private readonly bool presion;
    private readonly bool decimalPermitido;

    internal event EventHandler? Editada;

    internal EntradaVital(string clave, string etiqueta, string valor)
    {
        original = valor;
        presion = clave == "ta";
        decimalPermitido = clave is "temperatura" or "spo2";
        AutoSize = true;
        WrapContents = false;
        AccessibleName = etiqueta;
        primera.AccessibleName = presion ? "Presión sistólica" : etiqueta;
        Controls.Add(primera);
        primera.TextChanged += (_, _) => Editada?.Invoke(this, EventArgs.Empty);

        if (!presion || (valor.Length > 0 && !Regex.IsMatch(valor, @"^[0-9]+\s*/\s*[0-9]+$")))
        {
            primera.Text = valor;
            primera.Width = presion ? 170 : 76;
            return;
        }

        Controls.Add(Estilo.Texto("/", 16));
        segunda = new TextBox { Width = 76, AccessibleName = "Presión diastólica" };
        Controls.Add(segunda);
        primera.PlaceholderText = "110";
        segunda.PlaceholderText = "70";
        segunda.TextChanged += (_, _) => Editada?.Invoke(this, EventArgs.Empty);
        primera.KeyPress += (_, tecla) =>
        {
            if (tecla.KeyChar != '/') return;
            tecla.Handled = true;
            segunda.Focus();
        };
        if (valor.Length == 0) return;
        var partes = valor.Split('/');
        primera.Text = partes[0].Trim();
        segunda.Text = partes[1].Trim();
    }

    internal string Valor()
    {
        string valor = primera.Text.Trim();
        if (segunda is not null && (valor.Length > 0 || segunda.Text.Trim().Length > 0))
            valor += "/" + segunda.Text.Trim();
        if (valor == original || valor.Length == 0) return valor;
        string formato = presion ? @"^[0-9]+/[0-9]+$" : decimalPermitido ? @"^[0-9]+([.,][0-9]+)?$" : @"^[0-9]+$";
        if (!Regex.IsMatch(valor, formato))
            throw new ArgumentException(presion ? "Completa la presión arterial con ambos números." : $"Escribe un valor numérico en {AccessibleName}.");
        return valor;
    }
}
