using System.ComponentModel;

namespace Gineco;

internal sealed class EntradaFecha : FlowLayoutPanel
{
    private const int HorasPorPeriodo = 12;
    private readonly DateTimePicker fecha = new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "dd/MM/yyyy hh:mm",
        Font = Estilo.LetraFecha,
        Width = 190,
        Margin = new Padding(0, 0, 6, 0),
        AccessibleName = "Fecha y hora de atención"
    };
    private readonly ComboBox periodo = new()
    {
        Name = "periodoAtencion",
        AccessibleName = "Período de la hora de atención",
        DropDownStyle = ComboBoxStyle.DropDownList,
        Font = Estilo.LetraFecha,
        Width = 63,
        Margin = Padding.Empty
    };

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal DateTime Value
    {
        get => fecha.Value;
        set => fecha.Value = value;
    }

    internal EntradaFecha()
    {
        AutoSize = true;
        WrapContents = false;
        Controls.AddRange([fecha, periodo]);
        periodo.Items.AddRange(["AM", "PM"]);
        fecha.ValueChanged += (_, _) => ActualizarPeriodo();
        periodo.SelectedIndexChanged += (_, _) => CambiarPeriodo();
        ActualizarPeriodo();
    }

    private void ActualizarPeriodo() => periodo.SelectedIndex = fecha.Value.Hour / HorasPorPeriodo;

    private void CambiarPeriodo()
    {
        int periodoActual = fecha.Value.Hour / HorasPorPeriodo;
        int diferencia = (periodo.SelectedIndex - periodoActual) * HorasPorPeriodo;
        var actualizada = fecha.Value.AddHours(diferencia);
        if (actualizada >= fecha.MinDate && actualizada <= fecha.MaxDate) fecha.Value = actualizada;
        else ActualizarPeriodo();
    }
}
