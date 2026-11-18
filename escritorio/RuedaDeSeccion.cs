namespace Gineco;

internal sealed class RuedaDeSeccion(Form editor, Func<Control?> seccion) : IMessageFilter
{
    private const int MensajeRueda = 0x020A;
    private const int UnidadRueda = 120;
    private int acumulado;
    private ScrollableControl? anterior;

    public bool PreFilterMessage(ref Message mensaje)
    {
        if (mensaje.Msg != MensajeRueda || Form.ActiveForm != editor) return false;
        if (seccion() is not ScrollableControl panel || !panel.VerticalScroll.Visible) return false;
        if (!panel.RectangleToScreen(panel.ClientRectangle).Contains(Control.MousePosition)) return false;
        if (editor.ActiveControl is ComboBox { DroppedDown: true }) return false;
        Desplazar(panel, unchecked((short)(mensaje.WParam.ToInt64() >> 16)));
        return true;
    }

    internal void Desplazar(ScrollableControl panel, int delta)
    {
        if (panel != anterior) acumulado = 0;
        anterior = panel;
        acumulado += delta;
        int pasos = acumulado / UnidadRueda;
        acumulado %= UnidadRueda;
        if (pasos == 0) return;
        int lineas = SystemInformation.MouseWheelScrollLines;
        int avance = lineas < 0 ? panel.ClientSize.Height : panel.Font.Height * lineas;
        int maximo = Math.Max(0, panel.VerticalScroll.Maximum - panel.VerticalScroll.LargeChange + 1);
        int posicion = Math.Clamp(-panel.AutoScrollPosition.Y - pasos * avance, 0, maximo);
        panel.AutoScrollPosition = new Point(-panel.AutoScrollPosition.X, posicion);
    }
}
