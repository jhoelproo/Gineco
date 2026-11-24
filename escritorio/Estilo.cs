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
    internal static Color Resalte => SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(227, 241, 246);
    internal static Color EnPrincipal => SystemInformation.HighContrast ? SystemColors.HighlightText : Color.White;
    internal static Font LetraFuerte { get; } = new("Segoe UI", 11, FontStyle.Bold);
    internal static Font LetraFecha { get; } = new("Segoe UI", 12, FontStyle.Bold);

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
            ForeColor = acento.HasValue ? EnPrincipal : Tinta,
            UseVisualStyleBackColor = false
        };
        boton.FlatAppearance.BorderColor = acento ?? Linea;
        boton.Margin = new Padding(0, 3, 8, 3);
        if (acento is { } activo) boton.EnabledChanged += (_, _) => PintarBoton(boton, activo);
        return boton;
    }

    private static void PintarBoton(Button boton, Color activo)
    {
        boton.BackColor = boton.Enabled ? activo : Fondo;
        boton.ForeColor = boton.Enabled ? EnPrincipal : Secundario;
        boton.FlatAppearance.BorderColor = boton.Enabled ? activo : Linea;
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

    internal static Control Encabezado(string titulo, string? descripcion = null)
    {
        var panel = Barra(DockStyle.Top);
        panel.FlowDirection = FlowDirection.TopDown;
        panel.WrapContents = false;
        panel.BackColor = Fondo;
        panel.Padding = new Padding(22, 13, 22, 12);
        panel.Controls.Add(Texto(titulo, 22, true));
        if (descripcion is not null)
        {
            var nota = Texto(descripcion, 10);
            nota.ForeColor = Secundario;
            panel.Controls.Add(nota);
        }
        return panel;
    }

    internal static DataGridView Tabla(string nombre) => new()
    {
        Dock = DockStyle.Fill,
        AccessibleName = nombre,
        AutoGenerateColumns = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect
    };

    internal static void PresentarTabla(DataGridView tabla)
    {
        tabla.BorderStyle = BorderStyle.None;
        tabla.BackgroundColor = Papel;
        tabla.GridColor = Linea;
        tabla.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        tabla.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        tabla.EnableHeadersVisualStyles = false;
        tabla.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        tabla.ColumnHeadersHeight = 40;
        tabla.RowTemplate.Height = 42;
        tabla.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Font = LetraFuerte,
            BackColor = Fondo,
            ForeColor = Tinta,
            Padding = new Padding(8, 0, 4, 0),
            SelectionBackColor = Fondo,
            SelectionForeColor = Tinta
        };
        tabla.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Papel,
            ForeColor = Tinta,
            Padding = new Padding(8, 5, 4, 5),
            SelectionBackColor = Principal,
            SelectionForeColor = EnPrincipal
        };
        tabla.AlternatingRowsDefaultCellStyle.BackColor = Fondo;
    }

    internal static Label MensajeVacio(string mensaje) => new()
    {
        Dock = DockStyle.Fill,
        Text = mensaje,
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Secundario,
        BackColor = Papel,
        Font = LetraFuerte,
        Padding = new Padding(16)
    };

    internal static FlowLayoutPanel Barra(DockStyle lado) => new()
    {
        Dock = lado,
        AutoSize = true,
        Padding = new Padding(16, 8, 16, 8),
        BackColor = Papel
    };
}
