namespace Gineco;

internal static class Estilo
{
    internal static Color Fondo => SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(242, 247, 249);
    internal static Color Papel => SystemColors.Window;
    internal static Color Tinta => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(23, 46, 60);
    internal static Color Secundario => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(68, 93, 107);
    internal static Color Principal => SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(0, 94, 113);
    internal static Color Guardar => SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(24, 107, 76);
    internal static Color Linea => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(190, 207, 215);

    internal static void Ventana(Form ventana)
    {
        ventana.AutoScaleMode = AutoScaleMode.Dpi;
        ventana.Font = new Font("Segoe UI", 11);
        ventana.BackColor = Fondo;
        ventana.ForeColor = Tinta;
    }

    internal static Label Texto(string texto, float tamano = 11, bool fuerte = false) => new()
    {
        Text = texto,
        AutoSize = true,
        Font = new Font("Segoe UI", tamano, fuerte ? FontStyle.Bold : FontStyle.Regular)
    };

    internal static Button Boton(string texto, Color? acento = null)
    {
        var boton = new Button
        {
            Text = texto,
            AutoSize = true,
            MinimumSize = new Size(0, 42),
            Padding = new Padding(12, 0, 12, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = acento ?? Papel,
            ForeColor = acento.HasValue ? (SystemInformation.HighContrast ? SystemColors.HighlightText : Color.White) : Tinta,
            UseVisualStyleBackColor = false
        };
        boton.FlatAppearance.BorderColor = acento ?? Linea;
        return boton;
    }

    internal static FlowLayoutPanel Columna() => new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        BackColor = Papel,
        Padding = new Padding(24)
    };

    internal static void Campo(Control panel, string etiqueta, Control entrada)
    {
        entrada.AccessibleName = etiqueta;
        var rotulo = Texto(etiqueta, fuerte: true);
        rotulo.Margin = new Padding(0, 7, 0, 2);
        entrada.Margin = new Padding(0, 0, 0, 8);
        panel.Controls.Add(rotulo);
        panel.Controls.Add(entrada);
    }

    internal static FlowLayoutPanel Barra(DockStyle lado) => new()
    {
        Dock = lado,
        AutoSize = true,
        Padding = new Padding(16, 8, 16, 8),
        BackColor = Papel
    };
}
