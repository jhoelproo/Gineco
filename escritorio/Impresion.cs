using System.Drawing.Printing;
using MigraDoc.Rendering;
using PdfSharp.Drawing;

namespace Gineco;

internal static class Impresion
{
    internal static void Enviar(Atencion historia, PrinterSettings? impresora = null)
    {
        using var trabajo = new PrintDocument { DocumentName = $"Historia clínica {historia.Id}", PrintController = new StandardPrintController() };
        if (impresora is not null) trabajo.PrinterSettings = impresora;
        if (!trabajo.PrinterSettings.IsValid)
            throw new InvalidOperationException("Elige una impresora predeterminada en Windows y vuelve a intentar.");
        trabajo.DefaultPageSettings.PaperSize = PapelCarta(trabajo.PrinterSettings.PaperSizes.Cast<PaperSize>());
        var documento = new DocumentRenderer(Informe.CrearDocumento(historia));
        documento.PrepareDocument();
        int pagina = 1;
        trabajo.PrintPage += (_, e) =>
        {
            using var imagen = DibujarPagina(documento, pagina++);
            var area = e.PageSettings.PrintableArea;
            float escala = Math.Min(area.Width / imagen.Width, area.Height / imagen.Height);
            float ancho = imagen.Width * escala;
            e.Graphics!.DrawImage(imagen, (area.Width - ancho) / 2, 0, ancho, imagen.Height * escala);
            e.HasMorePages = pagina <= documento.FormattedDocument!.PageCount;
        };
        trabajo.Print();
    }

    internal static Bitmap DibujarPagina(DocumentRenderer documento, int pagina)
    {
        const int dpi = 240;
        var formato = PdfSharp.PageSizeConverter.ToSize(PdfSharp.PageSize.Letter);
        var imagen = new Bitmap((int)Math.Ceiling(formato.Width * dpi / 72), (int)Math.Ceiling(formato.Height * dpi / 72));
        try
        {
            imagen.SetResolution(dpi, dpi);
            using var superficie = Graphics.FromImage(imagen);
            superficie.Clear(Color.White);
            using var lienzo = XGraphics.FromGraphics(superficie, formato);
            lienzo.ScaleTransform(dpi / 72d);
            documento.RenderPage(lienzo, pagina);
            return imagen;
        }
        catch
        {
            imagen.Dispose();
            throw;
        }
    }

    internal static PaperSize PapelCarta(IEnumerable<PaperSize> tamaños) =>
        tamaños.FirstOrDefault(papel => papel.Kind == PaperKind.Letter)
        ?? new PaperSize("Carta", 850, 1100) { RawKind = (int)PaperKind.Letter };
}
