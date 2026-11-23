namespace Gineco;

internal sealed class VistaHistorias : Form
{
    private readonly ListBox lista = new() { Dock = DockStyle.Left, Width = 255, AccessibleName = "Historias anteriores" };
    private readonly TextBox detalle = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        BackColor = Estilo.Papel,
        AccessibleName = "Contenido de la historia",
        Name = "detalleHistoriaAnterior"
    };

    internal VistaHistorias(IReadOnlyList<Atencion> historias, Atencion? actual = null)
    {
        Estilo.Ventana(this);
        Text = "Historias anteriores · Vista de solo lectura";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(890, 575);
        MinimumSize = new Size(650, 420);

        var marco = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        marco.Controls.Add(detalle);
        marco.Controls.Add(lista);
        var pie = Estilo.Barra(DockStyle.Bottom);
        var cerrar = Estilo.Boton("Volver a la historia actual", Estilo.Principal);
        cerrar.DialogResult = DialogResult.Cancel;
        pie.Controls.Add(cerrar);
        if (actual is not null)
        {
            var comparar = Estilo.Boton("Comparar con la historia actual");
            comparar.Click += (_, _) =>
            {
                if (lista.SelectedItem is not Atencion previa) return;
                using var comparacion = CrearComparacion(previa, actual);
                comparacion.ShowDialog(this);
            };
            pie.Controls.Add(comparar);
        }
        Controls.Add(marco);
        Controls.Add(pie);
        AcceptButton = CancelButton = cerrar;

        lista.Format += (_, evento) =>
        {
            if (evento.ListItem is Atencion historia)
                evento.Value = $"{historia.Fecha:dd/MM/yyyy HH:mm} · {(historia.Finalizada ? "Finalizada" : "Borrador")}";
        };
        lista.FormattingEnabled = true;
        lista.SelectedIndexChanged += (_, _) => ActualizarDetalle();
        lista.Items.AddRange(historias.ToArray());
        if (lista.Items.Count > 0) lista.SelectedIndex = 0;
    }

    private void ActualizarDetalle()
    {
        if (lista.SelectedItem is not Atencion historia) return;
        detalle.Text = Contenido(historia);
        detalle.Select(0, 0);
    }

    private static string Contenido(Atencion historia)
    {
        var lineas = new List<string> { historia.Nombre, $"Registrada por {historia.Responsable.Nombre}" };
        foreach (var (seccion, campos) in Clinica.Secciones)
        {
            var registrados = campos.Where(campo => !string.IsNullOrWhiteSpace(historia.Valor(campo.Key)))
                .Select(campo => $"{campo.Value}: {historia.Valor(campo.Key)}").ToArray();
            if (registrados.Length == 0) continue;
            lineas.AddRange(["", seccion.ToUpperInvariant(), .. registrados]);
        }
        return string.Join(Environment.NewLine, lineas);
    }

    private static Form CrearComparacion(Atencion anterior, Atencion actual)
    {
        var ventana = new Form
        {
            Text = "Comparar atenciones",
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(950, 590),
            MinimumSize = new Size(670, 420)
        };
        Estilo.Ventana(ventana);
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(16) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        AgregarComparacion(panel, 0, $"Anterior · {anterior.Fecha:dd/MM/yyyy HH:mm}", anterior);
        AgregarComparacion(panel, 1, "Historia actual", actual);
        var pie = Estilo.Barra(DockStyle.Bottom);
        var volver = Estilo.Boton("Volver al historial");
        volver.DialogResult = DialogResult.Cancel;
        pie.Controls.Add(volver);
        ventana.Controls.Add(panel);
        ventana.Controls.Add(pie);
        ventana.CancelButton = volver;
        return ventana;
    }

    private static void AgregarComparacion(TableLayoutPanel panel, int columna, string titulo, Atencion historia)
    {
        panel.Controls.Add(Estilo.Texto(titulo, 12, true), columna, 0);
        panel.Controls.Add(new TextBox
        {
            AccessibleName = titulo,
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Text = Contenido(historia)
        }, columna, 1);
    }
}
