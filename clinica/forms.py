import re
import unicodedata

from django import forms
from django.contrib.auth.forms import AuthenticationForm

from .models import Paciente
from .schema import ESTATUS, IDENTIFICACION, ORIGEN, PROCEDIMIENTOS, SECCIONES


def normalizar_nombre(nombre):
    return unicodedata.normalize("NFKC", " ".join(nombre.split())).casefold()


class AccesoForm(AuthenticationForm):
    nombre = forms.CharField(label="Nombre completo", max_length=300)

    def clean(self):
        datos = super().clean()
        if not self.get_user() or normalizar_nombre(
            datos.get("nombre", "")
        ) != normalizar_nombre(self.get_user().get_full_name()):
            raise self.get_invalid_login_error()
        return datos


class IdentificacionForm(forms.Form):
    edad = forms.IntegerField(label="Edad", min_value=0, max_value=130, required=False)
    fecha = forms.DateTimeField(
        label="Fecha y hora de atención",
        widget=forms.DateTimeInput(
            format="%Y-%m-%dT%H:%M", attrs={"type": "datetime-local"}
        ),
    )

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        for clave, etiqueta in IDENTIFICACION.items():
            if clave == "edad":
                continue
            campo = Paciente._meta.get_field(clave)
            numerico = clave in {"cedula", "nss", "telefono"}
            widget = (
                forms.TextInput(attrs={"inputmode": "numeric"}) if numerico else None
            )
            self.fields[clave] = forms.CharField(
                label=etiqueta,
                required=clave == "nombre",
                widget=widget,
                max_length=campo.max_length + 6 if numerico else campo.max_length,
            )
        self.order_fields([*IDENTIFICACION, "fecha"])

    def clean(self):
        datos = super().clean()
        for clave in ("cedula", "nss", "telefono"):
            valor = re.sub(r"[\s().-]", "", datos.get(clave, ""))
            longitud = Paciente._meta.get_field(clave).max_length
            if valor and not re.fullmatch(rf"[0-9]{{{longitud}}}", valor):
                self.add_error(clave, f"Introduce {longitud} dígitos.")
            elif clave in datos:
                datos[clave] = valor
        return datos


def opciones(valores):
    return [(valor, valor) for valor in valores]


class ClinicaForm(forms.Form):
    fecha = forms.DateTimeField(
        label="Fecha y hora de atención",
        required=False,
        widget=forms.DateTimeInput(
            format="%Y-%m-%dT%H:%M", attrs={"type": "datetime-local"}
        ),
    )

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.fields.update(
            {
                clave: campo_clinico(clave, etiqueta, seccion)
                for seccion, campos in SECCIONES.items()
                for clave, etiqueta in campos
            }
        )
        self.fields["version"] = forms.IntegerField(
            min_value=0, widget=forms.HiddenInput
        )
        self.fields["motivo_correccion"] = forms.CharField(
            label="Motivo de la corrección",
            required=False,
            widget=forms.Textarea(attrs={"rows": 2}),
        )

    def secciones(self):
        return [
            (titulo, [self[clave] for clave, _ in campos])
            for titulo, campos in SECCIONES.items()
        ]


def campo_clinico(clave, etiqueta, seccion):
    seleccion = {
        "previas": ("Sí", "No"),
        "alergias": ("Sí", "No"),
        "estatus": ESTATUS,
        "origen": ORIGEN,
    }
    if clave in seleccion:
        return forms.ChoiceField(
            label=etiqueta,
            required=False,
            choices=[("", "Sin registrar"), *opciones(seleccion[clave])],
        )
    if clave == "procedimientos":
        return forms.MultipleChoiceField(
            label=etiqueta,
            required=False,
            choices=opciones(PROCEDIMIENTOS),
            widget=forms.CheckboxSelectMultiple,
        )
    if seccion == "Signos vitales":
        return forms.CharField(label=etiqueta, required=False, max_length=30)
    return forms.CharField(
        label=etiqueta, required=False, widget=forms.Textarea(attrs={"rows": 3})
    )


def datos_clinicos(limpios):
    datos = {
        clave: limpios[clave] for campos in SECCIONES.values() for clave, _ in campos
    }
    datos["diagnosticos"] = [
        {"texto": linea.strip(), "cie10": None}
        for linea in datos["diagnosticos"].splitlines()
        if linea.strip()
    ]
    return datos
