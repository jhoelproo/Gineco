SECCIONES = {
    "Motivo de consulta y breve historia clínica": [
        ("motivo", "Motivo de consulta y evolución"),
    ],
    "Antecedentes clínicos": [
        ("antecedentes", "Antecedentes patológicos"),
        ("medicamentos", "Medicamentos que utiliza"),
        ("previas", "Atenciones previas"),
        ("lugar", "Lugar de atención previa"),
        ("alergias", "Alergias"),
        ("detalle_alergias", "Especificación de alergias"),
    ],
    "Hallazgos positivos al examen físico": [
        ("abdomen", "Abdomen"),
        ("especuloscopia", "Especuloscopia"),
        ("tacto", "Tacto vaginal"),
    ],
    "Signos vitales": [
        ("ta", "TA (mmHg)"),
        ("temperatura", "Temperatura (°C)"),
        ("fc", "FC (lpm)"),
        ("fr", "FR (rpm)"),
        ("spo2", "SpO₂ (%)"),
        ("pc", "PC/10 MIN"),
    ],
    "Pruebas diagnósticas": [
        ("pruebas", "Pruebas diagnósticas indicadas"),
        ("hallazgos", "Hallazgos positivos"),
        ("sonografias", "Sonografías"),
    ],
    "Diagnósticos": [("diagnosticos", "Diagnósticos (uno por línea)")],
    "Manejo y medicación en emergencia": [
        ("manejo", "Manejo y medicación administrada"),
        ("procedimientos", "Procedimientos"),
        ("observaciones", "Observaciones de procedimientos"),
    ],
    "Estatus del paciente": [("estatus", "Estatus del paciente")],
    "Origen de la enfermedad": [
        ("origen", "Origen de la enfermedad"),
        ("observaciones_origen", "Observaciones del origen"),
    ],
}
PROCEDIMIENTOS = (
    "Sutura",
    "Inmovilización",
    "Reanimación cardiopulmonar",
    "Nebulizaciones",
    "Otros procedimientos",
)
ESTATUS = ("Dado de alta", "Alta a petición", "Admitido", "Referido", "Fallecido")
ORIGEN = (
    "Enfermedad general",
    "Maternidad",
    "Patología quirúrgica",
    "Accidente de tránsito",
    "Accidente de trabajo o enfermedad profesional",
    "Otros",
)
IDENTIFICACION = {
    "nombre": "Nombre completo",
    "edad": "Edad",
    "sexo": "Sexo",
    "cedula": "Cédula",
    "ars": "ARS",
    "nss": "NSS",
    "telefono": "Teléfono",
    "direccion": "Dirección",
    "nacionalidad": "Nacionalidad",
}
