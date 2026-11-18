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

    internal VistaHistorias(IReadOnlyList<Atencion> historias)
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
        var lineas = new List<string> { historia.Nombre, $"Registrada por {historia.Responsable.Nombre}" };
        foreach (var (seccion, campos) in Clinica.Secciones)
        {
            var registrados = campos.Where(campo => !string.IsNullOrWhiteSpace(historia.Valor(campo.Key)))
                .Select(campo => $"{campo.Value}: {historia.Valor(campo.Key)}").ToArray();
            if (registrados.Length == 0) continue;
            lineas.AddRange(["", seccion.ToUpperInvariant(), .. registrados]);
        }
        detalle.Text = string.Join(Environment.NewLine, lineas);
        detalle.Select(0, 0);
    }
}
