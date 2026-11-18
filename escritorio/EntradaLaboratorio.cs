namespace Gineco;

internal sealed class EntradaLaboratorio : FlowLayoutPanel
{
    private readonly CheckBox[] habituales;
    private readonly CheckBox otros = Casilla("Otros análisis");
    private readonly TextBox detalle = new()
    {
        Name = "otrosAnalisisDetalle",
        Multiline = true,
        Height = 68,
        Width = 320,
        ScrollBars = ScrollBars.Vertical,
        AccessibleName = "Detalle de otros análisis"
    };

    internal EntradaLaboratorio(string guardado)
    {
        Name = "laboratorios";
        AutoSize = true;
        FlowDirection = FlowDirection.TopDown;
        WrapContents = false;
        Margin = new Padding(0, 0, 0, 12);
        var analisis = AnalisisLaboratorio.Leer(guardado);
        var opciones = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = Padding.Empty };
        opciones.ColumnStyles.Add(new ColumnStyle { SizeType = SizeType.AutoSize });
        opciones.ColumnStyles.Add(new ColumnStyle { SizeType = SizeType.AutoSize });
        habituales = AnalisisLaboratorio.Opciones.Select(nombre => Casilla(nombre)).ToArray();
        foreach (var casilla in habituales)
        {
            casilla.Checked = analisis.Habituales.Contains(casilla.Text);
            opciones.Controls.Add(casilla);
        }

        Controls.Add(opciones);
        Controls.Add(otros);
        detalle.Text = analisis.Otros.ReplaceLineEndings(Environment.NewLine);
        otros.Checked = detalle.Text.Length > 0;
        otros.CheckedChanged += (_, _) => detalle.Visible = otros.Checked;
        detalle.Visible = otros.Checked;
        Controls.Add(detalle);
    }

    internal void AjustarAncho(int ancho) => detalle.Width = ancho;
    internal bool FaltaDetalle => otros.Checked && string.IsNullOrWhiteSpace(detalle.Text);
    internal void EnfocarDetalle() => detalle.Focus();

    internal string Valor() => new AnalisisLaboratorio(
        habituales.Where(casilla => casilla.Checked).Select(casilla => casilla.Text).ToArray(),
        otros.Checked ? detalle.Text : "").Guardar();

    private static CheckBox Casilla(string texto) => new()
    {
        Text = texto,
        AutoSize = true,
        MinimumSize = new Size(0, 30),
        Margin = new Padding(3, 2, 18, 2)
    };
}
