namespace Gineco;

internal static class OpcionesClinicas
{
    internal const string Maternidad = "Maternidad";
    internal const string Femenino = "Femenino";
    internal const string No = "No";
    internal static readonly string[] Atenciones = ["Sí", No];
    internal static readonly string[] Estatus = ["Alta", "Alta a petición", "Admitida", "Referida", "Fallecida"];
    internal static readonly string[] Origen =
        ["Enfermedad general", Maternidad, "Patología quirúrgica", "Accidente de tránsito", "Laboral", "Otros"];
}

internal sealed class SeleccionClinica : FlowLayoutPanel
{
    private readonly CheckBox[] casillas;

    internal SeleccionClinica(string clave, string[] opciones, string previo)
    {
        Name = clave;
        FlowDirection = FlowDirection.TopDown;
        WrapContents = false;
        AutoSize = true;
        string[] mostradas = previo.Length > 0 && !opciones.Contains(previo, StringComparer.OrdinalIgnoreCase)
            ? [.. opciones, previo] : opciones;
        casillas = mostradas.Select(opcion => new CheckBox
        {
            Text = opcion,
            AutoSize = true,
            MaximumSize = new Size(375, 0),
            Checked = string.Equals(opcion, previo, StringComparison.OrdinalIgnoreCase)
        }).ToArray();
        foreach (var casilla in casillas) casilla.CheckedChanged += AlSeleccionar;
        Controls.AddRange(casillas);
    }

    internal event EventHandler? Editada;
    internal string Valor => casillas.FirstOrDefault(casilla => casilla.Checked)?.Text ?? "";

    internal void Elegir(string valor)
    {
        foreach (var casilla in casillas)
        {
            casilla.CheckedChanged -= AlSeleccionar;
            casilla.Checked = casilla.Text == valor;
            casilla.CheckedChanged += AlSeleccionar;
        }
        Editada?.Invoke(this, EventArgs.Empty);
    }

    private void AlSeleccionar(object? remitente, EventArgs evento)
    {
        var elegida = (CheckBox)remitente!;
        if (elegida.Checked)
        {
            foreach (var casilla in casillas.Where(casilla => casilla != elegida && casilla.Checked))
            {
                casilla.CheckedChanged -= AlSeleccionar;
                casilla.Checked = false;
                casilla.CheckedChanged += AlSeleccionar;
            }
        }
        Editada?.Invoke(this, EventArgs.Empty);
    }
}
