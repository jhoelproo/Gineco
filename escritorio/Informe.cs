using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace Gineco;

internal static class Informe
{
    private static readonly MigraDoc.DocumentObjectModel.Color Azul = MigraDoc.DocumentObjectModel.Color.FromRgb(18, 49, 73);
    private static readonly MigraDoc.DocumentObjectModel.Color Fondo = MigraDoc.DocumentObjectModel.Color.FromRgb(239, 246, 249);
    private static readonly MigraDoc.DocumentObjectModel.Color Borde = MigraDoc.DocumentObjectModel.Color.FromRgb(207, 222, 230);
    private static readonly MigraDoc.DocumentObjectModel.Color Acento = MigraDoc.DocumentObjectModel.Color.FromRgb(24, 155, 199);
    private static readonly MigraDoc.DocumentObjectModel.Color AzulClinico = MigraDoc.DocumentObjectModel.Color.FromRgb(0, 89, 115);
    private static readonly Unit MargenHorizontal = Unit.FromCentimeter(1.5);
    private static readonly Unit AnchoUtil = Unit.FromPoint(PdfSharp.PageSizeConverter.ToSize(PdfSharp.PageSize.Letter).Width - 2 * MargenHorizontal.Point);

    internal static void Exportar(Atencion atencion, string ruta)
    {
        var renderizador = new PdfDocumentRenderer { Document = CrearDocumento(atencion) };
        renderizador.RenderDocument();
        renderizador.PdfDocument.Save(ruta);
    }

    internal static Document CrearDocumento(Atencion atencion)
    {
        var documento = new Document();
        documento.Info.Title = Clinica.Titulo;
        documento.Styles[StyleNames.Normal]!.Font.Name = "Arial";
        documento.Styles[StyleNames.Normal]!.Font.Size = Unit.FromPoint(9);
        var pagina = documento.AddSection();
        pagina.PageSetup.PageFormat = PageFormat.Letter;
        pagina.PageSetup.TopMargin = Unit.FromCentimeter(3.4);
        pagina.PageSetup.HeaderDistance = Unit.FromCentimeter(0.8);
        pagina.PageSetup.LeftMargin = MargenHorizontal;
        pagina.PageSetup.RightMargin = MargenHorizontal;
        pagina.PageSetup.BottomMargin = Unit.FromCentimeter(1.5);
        AgregarEncabezado(pagina);
        var pie = pagina.Footers.Primary.AddParagraph("Página ");
        pie.Format.Alignment = ParagraphAlignment.Right;
        pie.AddPageField();
        pie.AddText(" / ");
        pie.AddNumPagesField();

        AgregarTitulo(pagina, atencion);
        AgregarBloque(pagina, atencion, "Datos de la paciente",
            ["nombre", "edad", "sexo"], ["ars", "nss", "cedula"], ["telefono", "direccion", "nacionalidad"]);
        AgregarBloque(pagina, atencion, "Motivo de consulta y breve historia clínica", ["motivo"]);
        AgregarAntecedentes(pagina, atencion);
        AgregarBloque(pagina, atencion, "Hallazgos del examen físico", ["abdomen"], ["especuloscopia"], ["tacto"]);
        AgregarBloque(pagina, atencion, "Signos vitales", ["ta", "temperatura", "fc", "fr"], ["pc", "spo2"]);
        AgregarBloque(pagina, atencion, "Pruebas y hallazgos", ["pruebas"], ["hallazgos"], ["sonografias"]);
        AgregarSeccion(pagina, "Diagnósticos", destacada: true);
        AgregarTextoClinico(CrearCuerpo(pagina), atencion.Valor("diagnosticos"), destacado: true);
        AgregarBloque(pagina, atencion, "Manejo en emergencia y disposición",
            ["manejo"], ["estatus"]);
        AgregarBloque(pagina, atencion, "Origen de la enfermedad", ["origen"]);
        AgregarFirmas(pagina, atencion);

        return documento;
    }

    private static void AgregarAntecedentes(Section pagina, Atencion atencion)
    {
        var alergias = AlergiasRegistradas.Leer(atencion.Valor("alergias"), atencion.Valor("detalle_alergias"));
        var datos = new Dictionary<string, string>(atencion.Campos)
        {
            ["alergias"] = alergias.Respuesta,
            ["detalle_alergias"] = alergias.Detalle
        };
        var filas = new List<string[]> { new[] { "antecedentes" }, new[] { "previas", "lugar" }, new[] { "medicamentos", "alergias" } };
        if (alergias.Respuesta == "Sí") filas.Add(["detalle_alergias"]);
        AgregarBloque(pagina, atencion with { Campos = datos }, "Antecedentes, medicamentos y alergias", filas.ToArray());
    }

    private static void AgregarEncabezado(Section pagina)
    {
        var tabla = pagina.Headers.Primary.AddTable();
        tabla.Rows.LeftIndent = Unit.FromPoint(0);
        var columnaSns = Unit.FromCentimeter(2.4);
        tabla.AddColumn(Unit.FromPoint(AnchoUtil.Point - columnaSns.Point));
        tabla.AddColumn(columnaSns);
        var fila = tabla.AddRow();
        fila.Borders.Bottom.Width = Unit.FromPoint(0.5);
        fila.Borders.Bottom.Color = Colors.LightGray;
        fila.BottomPadding = Unit.FromPoint(5);
        var hospital = fila.Cells[0].AddImage(Clinica.Recurso("hospital.png"));
        hospital.Width = Unit.FromCentimeter(8);
        fila.Cells[1].Format.Alignment = ParagraphAlignment.Right;
        var sns = fila.Cells[1].AddImage(Clinica.Recurso("sns.png"));
        sns.Height = Unit.FromCentimeter(1.9);
    }

    private static void AgregarTitulo(Section pagina, Atencion atencion)
    {
        var titulo = pagina.AddParagraph("HISTORIA CLÍNICA DE EMERGENCIA");
        titulo.Format.Font.Size = Unit.FromPoint(15);
        titulo.Format.Font.Bold = true;
        titulo.Format.Font.Color = Azul;
        titulo.Format.KeepWithNext = true;
        var datos = pagina.AddTable();
        datos.Rows.LeftIndent = Unit.FromPoint(0);
        datos.LeftPadding = datos.RightPadding = Unit.FromPoint(0);
        datos.AddColumn(Unit.FromPoint(AnchoUtil.Point * 0.60));
        datos.AddColumn(Unit.FromPoint(AnchoUtil.Point * 0.40));
        var fila = datos.AddRow();
        fila.VerticalAlignment = VerticalAlignment.Center;
        var servicio = fila.Cells[0].AddParagraph("GINECOLOGÍA Y OBSTETRICIA");
        servicio.Format.Font.Bold = true;
        servicio.Format.Font.Color = AzulClinico;
        var atencionLocal = fila.Cells[1].AddParagraph();
        atencionLocal.Format.Alignment = ParagraphAlignment.Right;
        var fecha = atencionLocal.AddFormattedText($"ATENCIÓN  {Clinica.FechaPresentada(atencion.Fecha)}");
        fecha.Size = Unit.FromPoint(12);
        fecha.Bold = true;
        fecha.Color = AzulClinico;
    }

    private static readonly Dictionary<string, string> Etiquetas = Clinica.Secciones.Values
        .SelectMany(seccion => seccion).ToDictionary(campo => campo.Key, campo => campo.Value);

    private static void AgregarBloque(Section pagina, Atencion atencion, string titulo, params string[][] filas)
    {
        AgregarSeccion(pagina, titulo);
        var contenido = CrearCuerpo(pagina);
        foreach (var fila in filas)
        {
            if (contenido.Elements.Count > 0) contenido.AddLineBreak();
            AgregarFila(contenido, atencion, fila);
        }
    }

    private static void AgregarSeccion(Section pagina, string texto, bool destacada = false)
    {
        var titulo = pagina.AddParagraph(texto.ToUpperInvariant());
        titulo.Format.Font.Bold = true;
        titulo.Format.Font.Color = destacada ? Colors.White : Azul;
        titulo.Format.Shading.Color = destacada ? AzulClinico : Fondo;
        titulo.Format.Borders.Left.Color = Acento;
        titulo.Format.Borders.Left.Width = Unit.FromPoint(3);
        titulo.Format.Borders.DistanceFromLeft = Unit.FromPoint(6);
        titulo.Format.LeftIndent = Unit.FromPoint(6);
        titulo.Format.RightIndent = Unit.FromPoint(6);
        titulo.Format.Borders.Top.Color = Borde;
        titulo.Format.Borders.Top.Width = Unit.FromPoint(0.5);
        titulo.Format.Borders.Right.Color = Borde;
        titulo.Format.Borders.Right.Width = Unit.FromPoint(0.5);
        titulo.Format.Borders.DistanceFromRight = Unit.FromPoint(6);
        titulo.Format.Borders.DistanceFromTop = Unit.FromPoint(2);
        titulo.Format.Borders.DistanceFromBottom = Unit.FromPoint(2);
        titulo.Format.SpaceBefore = Unit.FromPoint(4);
        titulo.Format.KeepWithNext = true;
    }

    private static Paragraph CrearCuerpo(Section pagina)
    {
        var fila = pagina.AddParagraph();
        fila.Format.LeftIndent = Unit.FromPoint(7);
        fila.Format.RightIndent = Unit.FromPoint(7);
        var bordes = fila.Format.Borders;
        bordes.Color = Borde;
        bordes.Width = Unit.FromPoint(0.5);
        bordes.DistanceFromLeft = Unit.FromPoint(7);
        bordes.DistanceFromRight = Unit.FromPoint(7);
        bordes.DistanceFromTop = Unit.FromPoint(3);
        bordes.DistanceFromBottom = Unit.FromPoint(3);
        fila.Format.LineSpacingRule = LineSpacingRule.AtLeast;
        fila.Format.LineSpacing = Unit.FromPoint(13);
        return fila;
    }

    private static void AgregarFila(Paragraph fila, Atencion atencion, string[] claves)
    {
        for (int indice = 0; indice < claves.Length; indice++)
        {
            if (indice > 0) fila.AddText("    \u00a0    ");
            var clave = claves[indice];
            if (clave == "pruebas")
            {
                AgregarPruebas(fila, atencion.Valor(clave));
                continue;
            }
            if (clave == "sexo")
            {
                AgregarValor(fila, "Sexo", OpcionesClinicas.Femenino);
                continue;
            }
            var signo = SignoVital.Buscar(clave);
            var valor = atencion.Valor(clave);
            AgregarValor(fila, signo?.RotuloPdf ?? Etiquetas[clave], signo?.FormatoPdf(valor) ?? valor,
                destacado: clave == "sonografias" || Clinica.IdentificacionDestacada(clave));
        }
    }

    internal static (string Etiqueta, string Valor) AplicarUnidad(string clave, string etiqueta, string valor)
    {
        string unidad = clave switch
        {
            "ta" => "mmHg",
            "temperatura" => "°C",
            "fc" => "lpm",
            "fr" => "rpm",
            "spo2" => "%",
            _ => ""
        };
        if (unidad.Length == 0) return (etiqueta, valor);
        string limpia = etiqueta.Replace($" ({unidad})", "", StringComparison.Ordinal);
        string patron = clave == "ta" ? @"^[0-9]+\s*/\s*[0-9]+$" : @"^[0-9]+([.,][0-9]+)?$";
        return (limpia, System.Text.RegularExpressions.Regex.IsMatch(valor, patron) ? $"{valor} {unidad}" : valor);
    }

    private static void AgregarFirmas(Section pagina, Atencion atencion)
    {
        AgregarSeccion(pagina, "Responsable médico y firmas");
        var tabla = pagina.AddTable();
        tabla.Rows.LeftIndent = Unit.FromPoint(0);
        tabla.Borders.Color = Borde;
        tabla.Borders.Width = Unit.FromPoint(0.5);
        tabla.AddColumn(Unit.FromPoint(AnchoUtil.Point / 2));
        tabla.AddColumn(Unit.FromPoint(AnchoUtil.Point / 2));
        var fila = tabla.AddRow();
        AgregarValor(fila.Cells[0].AddParagraph(), "Médico", atencion.Responsable.Nombre, destacado: true);
        AgregarValor(fila.Cells[0].AddParagraph(), "Exequátur", atencion.Responsable.Exequatur, destacado: true);
        var medica = fila.Cells[0].AddParagraph("______________________________\nFirma y sello del médico");
        var paciente = fila.Cells[1].AddParagraph("______________________________\nFirma del paciente o familiar");
        medica.Format.SpaceBefore = Unit.FromPoint(11);
        paciente.Format.SpaceBefore = Unit.FromPoint(11);
        fila.Cells[1].VerticalAlignment = VerticalAlignment.Bottom;
    }

    private static void AgregarPruebas(Paragraph fila, string valor)
    {
        var pruebas = valor.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        AgregarValor(fila, Etiquetas["pruebas"], string.Join(", ", pruebas), destacado: true);
    }

    private static void AgregarValor(Paragraph campo, string etiqueta, string valor, bool destacado = false)
    {
        campo.AddFormattedText(etiqueta + ": ", TextFormat.Bold).Color = Azul;
        AgregarTextoClinico(campo, valor, destacado);
    }

    private static void AgregarTextoClinico(Paragraph campo, string valor, bool destacado)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            campo.AddText("Sin registrar");
            return;
        }
        var lineas = valor.ReplaceLineEndings("\n").Split('\n');
        for (int i = 0; i < lineas.Length; i++)
        {
            if (i > 0) campo.AddLineBreak();
            if (lineas[i].Length == 0) continue;
            if (destacado) campo.AddFormattedText(lineas[i], TextFormat.Bold).Color = Azul;
            else campo.AddText(lineas[i]);
        }
    }
}
