from html import escape
from io import BytesIO

from django.utils import timezone
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import (
    KeepTogether,
    Paragraph,
    SimpleDocTemplate,
    Table,
    TableStyle,
)

from .schema import IDENTIFICACION, SECCIONES

AZUL = colors.HexColor("#173854")
CUERPO = ParagraphStyle(
    "Cuerpo", fontName="Helvetica", fontSize=10, leading=14, spaceAfter=4
)
TITULO = ParagraphStyle(
    "Título",
    parent=CUERPO,
    fontName="Helvetica-Bold",
    fontSize=16,
    leading=19,
    textColor=AZUL,
)
SECCION = ParagraphStyle(
    "Sección",
    parent=CUERPO,
    fontName="Helvetica-Bold",
    textColor=AZUL,
    spaceBefore=9,
    borderPadding=4,
    backColor=colors.HexColor("#eaf0f5"),
    keepWithNext=True,
)
FECHA = ParagraphStyle(
    "Fecha", parent=CUERPO, fontName="Helvetica-Bold", fontSize=12, leading=16
)


def texto(valor):
    if isinstance(valor, list):
        valor = "\n".join(
            elemento["texto"] if isinstance(elemento, dict) else elemento
            for elemento in valor
        )
    if valor is None or valor == "":
        return "Sin registrar"
    return escape(str(valor)).replace("\n", "<br/>") or "Sin registrar"


def linea(etiqueta, valor):
    rotulo = escape(etiqueta).replace("₂", "<sub>2</sub>")
    return Paragraph(f"<b>{rotulo}:</b> {texto(valor)}", CUERPO)


def identificacion(atencion, ancho):
    filas = [
        linea(etiqueta, atencion.identificacion.get(clave))
        for clave, etiqueta in IDENTIFICACION.items()
    ]
    return Table(
        [filas[i : i + 2] for i in range(0, len(filas), 2)], colWidths=[ancho / 2] * 2
    )


def firmas(atencion, ancho):
    medico = [
        Paragraph("<b>MÉDICO TRATANTE</b>", CUERPO),
        linea("Nombre", atencion.nombre_medico),
        linea("Exequátur", atencion.exequatur),
    ]
    tabla = Table(
        [
            [medico, ""],
            [
                Paragraph("Firma y sello del médico", CUERPO),
                Paragraph("Firma del paciente o familiar", CUERPO),
            ],
        ],
        colWidths=[ancho / 2] * 2,
    )
    tabla.setStyle(
        TableStyle(
            [
                ("BOTTOMPADDING", (0, 0), (-1, 0), 48),
                ("LINEABOVE", (0, 1), (-1, 1), 0.5, colors.grey),
            ]
        )
    )
    return KeepTogether([Paragraph("Responsable médico y firmas", SECCION), tabla])


def pie(canvas, documento):
    canvas.setFont("Helvetica", 8)
    canvas.setFillColor(colors.grey)
    canvas.drawRightString(
        A4[0] - documento.rightMargin, 25, f"Página {documento.page}"
    )


def generar_pdf(atencion):
    salida = BytesIO()
    documento = SimpleDocTemplate(
        salida,
        pagesize=A4,
        leftMargin=42,
        rightMargin=42,
        topMargin=36,
        bottomMargin=43,
        title="Historia clínica de emergencia ginecológica",
    )
    fecha = timezone.localtime(atencion.fecha).strftime("%d/%m/%Y · %H:%M")
    estado = "Historia finalizada" if atencion.finalizada else "Borrador"
    contenido = [
        Paragraph("Hospital Provincial Dr. Ángel Contreras", CUERPO),
        Paragraph("HISTORIA CLÍNICA DE EMERGENCIA GINECOLÓGICA", TITULO),
        Paragraph(f"Atención · {fecha}", FECHA),
        Paragraph(estado, CUERPO),
        Paragraph("Datos de la paciente", SECCION),
        identificacion(atencion, documento.width),
    ]
    for titulo, campos in SECCIONES.items():
        contenido.append(Paragraph(titulo, SECCION))
        contenido.extend(
            linea(etiqueta, atencion.datos.get(clave)) for clave, etiqueta in campos
        )
    contenido.append(firmas(atencion, documento.width))
    documento.build(contenido, onFirstPage=pie, onLaterPages=pie)
    salida.seek(0)
    return salida
