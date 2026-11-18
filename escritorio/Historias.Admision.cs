using System.ComponentModel;
using System.Drawing.Printing;
using System.Media;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Gineco;

internal sealed partial class Historias
{
    private List<FilaAdmision> admisiones = [];
    private readonly Label conexion = Estilo.Texto("", 9);
    private readonly System.Windows.Forms.Timer consulta = new() { Interval = 5000 };
    private readonly CancellationTokenSource cierre = new();
    private EnlaceAdmision? enlace;
    private HttpClient? http;
    private bool recibiendo;
    private readonly PrinterSettings? impresora;

    private void PrepararRecepcion(Control cabecera, Func<HttpClient?>? conectar)
    {
        conexion.AutoSize = true;
        cabecera.Controls.Add(conexion);
        try
        {
            http = conectar?.Invoke();
            if (http is not null)
            {
                enlace = new(http);
                conexion.Text = "Conectando con Admisión…";
                consulta.Tick += async (_, _) => await Recibir();
                Shown += async (_, _) => { consulta.Start(); await Recibir(); };
            }
            else conexion.Text = "Admisión sin configurar";
        }
        catch (ArgumentException error) { conexion.Text = error.Message; }
        Disposed += LiberarRecepcion;
    }

    private void LiberarRecepcion(object? sender, EventArgs args)
    {
        Disposed -= LiberarRecepcion;
        consulta.Dispose();
        cierre.Cancel();
        http?.Dispose();
        cierre.Dispose();
    }

    internal async Task Recibir()
    {
        if (recibiendo || enlace is null) return;
        recibiendo = true;
        try
        {
            await enlace.Sincronizar(archivo, n =>
            {
                Recargar();
                Avisar(n);
            }, cierre.Token);
            if (!IsDisposed) conexion.Text = "Admisión conectada";
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException
            or InvalidDataException or SqliteException or IOException)
        {
            if (!IsDisposed) conexion.Text = "Admisión sin conexión · Reintentando";
        }
        finally { recibiendo = false; }
    }

    private void Avisar(int cantidad)
    {
        var ventana = ActiveForm ?? this;
        var aviso = new Label
        {
            Text = cantidad == 1 ? "Nueva paciente de Admisión" : $"{cantidad} pacientes de Admisión",
            AutoSize = true,
            Padding = new Padding(14, 10, 14, 10),
            BackColor = Color.FromArgb(255, 239, 182),
            ForeColor = Color.FromArgb(78, 52, 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AccessibleRole = AccessibleRole.Alert
        };
        ventana.Controls.Add(aviso);
        aviso.Location = new Point(Math.Max(0, ventana.ClientSize.Width - aviso.Width - 14), 12);
        aviso.BringToFront();
        SystemSounds.Asterisk.Play();
        var ocultar = new System.Windows.Forms.Timer { Interval = 6500 };
        ocultar.Tick += (_, _) => aviso.Dispose();
        aviso.Disposed += (_, _) => ocultar.Dispose();
        ocultar.Start();
    }

    private void ColorearEstado(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || lista.Columns[e.ColumnIndex].DataPropertyName != nameof(FilaHistoria.Estado) ||
            lista.Rows[e.RowIndex].DataBoundItem is not FilaHistoria { Admision: { } a } || SystemInformation.HighContrast) return;
        var fondo = a.Impresa ? Color.Honeydew : a.Historia.HasValue ? Color.AliceBlue : Color.LemonChiffon;
        e.CellStyle!.SelectionBackColor = e.CellStyle.BackColor = fondo;
        e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor = Estilo.Tinta;
    }

    private void Imprimir(Atencion historia)
    {
        var preparada = archivo.PrepararExportacion(historia);
        using var revision = new RevisionExportacion(preparada, imprimir: true);
        var respuesta = revision.ShowDialog(this);
        if (respuesta == DialogResult.Retry) Editar(historia);
        if (respuesta != DialogResult.OK) return;
        try { Impresion.Enviar(preparada.Atencion, impresora); }
        catch (Exception error) when (error is InvalidPrinterException or InvalidOperationException or Win32Exception or ExternalException or IOException)
        {
            MessageBox.Show(this, "La historia está guardada. Comprueba la impresora y pulsa Imprimir para reintentar.", "Impresión");
            return;
        }
        try
        {
            if (admisiones.Any(a => a.Historia == historia.Id)) archivo.RegistrarImpresion(historia);
            Recargar();
            resultados.Text = "Enviada a la impresora";
        }
        catch (Exception error) when (error is SqliteException or InvalidOperationException)
        {
            MessageBox.Show(this, "Windows aceptó la impresión. No se pudo actualizar el estado. Comprueba la cola antes de reimprimir.");
        }
    }
}
