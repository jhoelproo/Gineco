namespace Gineco;

internal static class Apariencia
{
    internal static readonly Color Papel = Color.White;
    internal static readonly Color Fondo = Color.FromArgb(238, 244, 247);
    internal static readonly Color Tinta = Color.FromArgb(20, 47, 66);
    internal static readonly Color Secundario = Color.FromArgb(64, 86, 102);
    internal static readonly Color Azul = Color.FromArgb(0, 92, 133);
    internal static readonly Color Verde = Color.FromArgb(0, 100, 78);
    internal static readonly Color Borde = Color.FromArgb(187, 204, 213);
    internal static readonly Font Texto = new("Segoe UI", 11);
    internal static readonly Font Etiqueta = new("Segoe UI", 11, FontStyle.Bold);
    internal static readonly Font Titulo = new("Segoe UI", 23, FontStyle.Bold);
    internal static readonly Font Subtitulo = new("Segoe UI", 17, FontStyle.Bold);

    internal static void Preparar(Form ventana)
    {
        ventana.Font = Texto;
        ventana.AutoScaleMode = AutoScaleMode.Dpi;
        ventana.BackColor = Fondo;
        ventana.ForeColor = Tinta;
    }

    internal static Label Rotulo(string texto, Font? fuente = null) => new()
    {
        Text = texto,
        AutoSize = true,
        Font = fuente ?? Texto,
        ForeColor = Tinta
    };

    internal static Button Boton(string texto, Action ejecutar, Color? tono = null)
    {
        var boton = new Button
        {
            Text = texto,
            AutoSize = true,
            MinimumSize = new Size(0, 40),
            Padding = new Padding(12, 0, 12, 0),
            FlatStyle = FlatStyle.Flat,
            Font = Etiqueta,
            BackColor = tono ?? Papel,
            ForeColor = tono.HasValue ? Color.White : Tinta,
            UseVisualStyleBackColor = false
        };
        boton.FlatAppearance.BorderColor = tono ?? Borde;
        boton.Click += (_, _) => ejecutar();
        return boton;
    }

    internal static FlowLayoutPanel Barra(DockStyle lugar) => new()
    {
        Dock = lugar,
        AutoSize = true,
        Padding = new Padding(16, 8, 16, 8),
        BackColor = Papel
    };
}
