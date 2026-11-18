from io import BytesIO
from unittest.mock import patch

from django.contrib.auth import get_user_model
from django.db import OperationalError
from django.test import Client, TestCase
from pypdf import PdfReader

from .forms import IdentificacionForm
from .models import Atencion, Paciente
from .pdf import generar_pdf
from .schema import PROCEDIMIENTOS, SECCIONES
from .servicios import VersionEnConflicto, guardar

CLAVE = "Ejemplo-De-Pruebas_8491"
IDENTIDAD = {
    "nombre": "Paciente ficticia",
    "cedula": "000-0000000-0",
    "fecha": "2026-11-15T12:20",
}


class FlujoClinico(TestCase):
    @classmethod
    def setUpTestData(cls):
        usuarios = get_user_model().objects
        cls.medico = usuarios.create_user(
            "001", password=CLAVE, first_name="Ana", last_name="Ejemplo"
        )
        cls.otro = usuarios.create_user(
            "002", password=CLAVE, first_name="Juan", last_name="Ejemplo"
        )
        cls.admin = usuarios.create_superuser(
            "003", password=CLAVE, first_name="Admin", last_name="Ejemplo"
        )

    def setUp(self):
        self.client.force_login(self.medico)

    def crear(self, **cambios):
        respuesta = self.client.post("/atencion/nueva/", dict(IDENTIDAD, **cambios))
        self.assertEqual(respuesta.status_code, 302)
        return Atencion.objects.latest("pk")

    def test_autenticacion(self):
        self.client.logout()
        self.assertEqual(self.client.get("/").status_code, 302)
        datos = {"username": "001", "password": CLAVE, "nombre": "  ANA  ejemplo "}
        self.assertEqual(
            self.client.post("/entrar/", dict(datos, nombre="Otra")).status_code, 200
        )
        self.assertEqual(
            self.client.post("/entrar/", dict(datos, nombre="")).status_code, 200
        )
        self.assertEqual(
            self.client.post("/entrar/", dict(datos, password="errónea")).status_code,
            200,
        )
        self.assertRedirects(self.client.post("/entrar/", datos), "/")
        self.assertNotEqual(self.medico.password, CLAVE)
        self.assertEqual(self.client.get("/salir/").status_code, 405)
        self.assertRedirects(self.client.post("/salir/"), "/entrar/")

    def test_cuenta_medica_y_duplicado(self):
        self.assertEqual(self.client.get("/cuentas/clinica/medico/").status_code, 302)
        self.client.force_login(self.admin)
        self.assertContains(self.client.get("/"), "Cuentas médicas")
        ruta = "/cuentas/clinica/medico/add/"
        datos = {
            "username": "demo-004",
            "first_name": "Rosa",
            "last_name": "Ejemplo",
            "password1": CLAVE,
            "password2": CLAVE,
        }
        self.assertEqual(self.client.post(ruta, datos).status_code, 302)
        creada = get_user_model().objects.get(username="DEMO-004")
        self.assertTrue(creada.check_password(CLAVE))
        self.assertEqual(self.client.post(ruta, datos).status_code, 200)

    def test_identificacion_limites(self):
        for edad, valida in [
            (-1, False),
            (0, True),
            (1, True),
            (130, True),
            (131, False),
            ("", True),
        ]:
            with self.subTest(edad=edad):
                formulario = IdentificacionForm(
                    dict(
                        IDENTIDAD, edad=edad, telefono="(809) 000-0000", nss="123456789"
                    )
                )
                self.assertEqual(formulario.is_valid(), valida)
                if valida:
                    self.assertEqual(formulario.cleaned_data["telefono"], "8090000000")
        formulario = IdentificacionForm(
            dict(IDENTIDAD, cedula="A", telefono="123", nss="123")
        )
        self.assertFalse(formulario.is_valid())
        self.assertEqual(set(formulario.errors), {"cedula", "telefono", "nss"})

    def test_reutilizacion_busqueda_y_conflictos(self):
        primera = self.crear()
        segunda = self.crear()
        self.assertEqual(primera.paciente_id, segunda.paciente_id)
        self.assertContains(
            self.client.get("/?q=000-0000000-0&estado=pendientes"), "Paciente ficticia"
        )
        self.assertContains(self.client.get("/?q=ficticia"), "Paciente ficticia")
        self.assertContains(self.client.get("/?q=inexistente"), "No hay atenciones")
        self.assertEqual(
            self.client.post(
                "/atencion/nueva/", dict(IDENTIDAD, nombre="Otra identidad")
            ).status_code,
            200,
        )
        ruta = f"/atencion/nueva/{primera.pk}/"
        self.assertContains(self.client.get(ruta), "Confirmé la identidad")
        self.assertContains(self.client.post(ruta, IDENTIDAD), "Confirma la identidad")
        datos = dict(IDENTIDAD, identidad="confirmada")
        self.assertEqual(
            self.client.post(ruta, dict(datos, nombre="Otro")).status_code, 200
        )
        self.assertEqual(self.client.post(ruta, datos).status_code, 302)
        self.crear(cedula="")
        self.assertEqual(Paciente.objects.count(), 2)

    def test_borrador_finalizacion_y_correccion(self):
        atencion = self.crear(edad=0)
        ruta = f"/atencion/{atencion.pk}/"
        self.assertContains(self.client.get(ruta), "Guardar borrador")
        datos = {
            clave: "Texto guardado"
            for campos in SECCIONES.values()
            for clave, _ in campos
        }
        datos.update(
            previas="Sí",
            alergias="No",
            estatus="Referido",
            origen="Accidente de tránsito",
            procedimientos=PROCEDIMIENTOS,
            diagnosticos="Uno\n\nDos",
            version=0,
            accion="borrador",
        )
        self.assertEqual(self.client.post(ruta, datos).status_code, 302)
        atencion.refresh_from_db()
        self.assertEqual(len(atencion.datos), len(datos) - 2)
        self.assertEqual(
            atencion.datos["diagnosticos"][1], {"texto": "Dos", "cie10": None}
        )
        self.assertContains(self.client.get(ruta), "Texto guardado")
        self.assertContains(self.client.post(ruta, datos), "La historia cambió")
        self.assertContains(
            self.client.post(ruta, dict(datos, version=1, accion="otra")),
            "acción válida",
        )
        self.assertEqual(
            self.client.post(
                ruta, dict(datos, version=1, accion="finalización")
            ).status_code,
            302,
        )
        self.assertContains(
            self.client.post(ruta, dict(datos, version=2, accion="corrección")),
            "Indica el motivo",
        )
        cambio = dict(
            datos,
            version=2,
            accion="corrección",
            motivo_correccion="Aclaración",
            motivo="Actualizado",
        )
        self.assertEqual(self.client.post(ruta, cambio).status_code, 302)
        atencion.refresh_from_db()
        self.assertTrue(atencion.finalizada)
        self.assertEqual(atencion.datos["motivo"], "Actualizado")
        revision = atencion.revisiones.get(accion="corrección")
        self.assertEqual(revision.autor, self.medico)
        self.assertEqual(revision.anterior["datos"]["motivo"], "Texto guardado")

    def test_control_de_acceso_y_csrf(self):
        atencion = self.crear()
        self.client.force_login(self.otro)
        for ruta in (
            f"/atencion/{atencion.pk}/",
            f"/atencion/{atencion.pk}/pdf/",
            f"/atencion/nueva/{atencion.pk}/",
        ):
            self.assertEqual(self.client.get(ruta).status_code, 404)
        self.assertNotContains(self.client.get("/?q=Paciente"), "Paciente ficticia")
        self.assertEqual(
            self.client.post(f"/atencion/{atencion.pk}/", {"version": 0}).status_code,
            404,
        )
        self.client.force_login(self.admin)
        self.assertContains(
            self.client.get(f"/atencion/{atencion.pk}/"), "Paciente ficticia"
        )
        cliente_csrf = Client(enforce_csrf_checks=True)
        cliente_csrf.force_login(self.medico)
        self.assertEqual(
            cliente_csrf.post("/atencion/nueva/", IDENTIDAD).status_code, 403
        )

    def test_error_al_auditar_revierte_guardado_y_conserva_el_formulario(self):
        atencion = self.crear()
        ruta = f"/atencion/{atencion.pk}/"
        datos = {"version": 0, "accion": "borrador", "motivo": "Texto por conservar"}
        with patch(
            "clinica.servicios.Revision.objects.create", side_effect=OperationalError
        ):
            respuesta = self.client.post(ruta, datos)
            self.assertContains(respuesta, "Texto por conservar")
            self.assertContains(respuesta, "No se pudo guardar la historia")
            respuesta = self.client.post(
                "/atencion/nueva/", dict(IDENTIDAD, cedula="11111111111")
            )
            self.assertContains(respuesta, "No se pudo guardar la identificación")
        atencion.refresh_from_db()
        self.assertEqual(atencion.version, 0)
        self.assertEqual(Paciente.objects.count(), 1)
        self.assertEqual(atencion.revisiones.count(), 1)
        self.assertEqual(
            self.client.post(ruta, dict(datos, fecha="2026-11-15T13:01")).status_code,
            302,
        )
        with self.assertRaises(VersionEnConflicto):
            guardar(atencion, self.medico, 0, {}, "borrador", "", None)
        atencion.refresh_from_db()
        self.assertEqual(atencion.fecha.minute, 1)
        self.assertIn(
            "16:20", atencion.revisiones.get(accion="borrador").anterior["fecha"]
        )

    def test_pdf_completo_y_multipagina(self):
        atencion = self.crear()
        ruta = f"/atencion/{atencion.pk}/pdf/"
        respuesta = self.client.get(ruta)
        self.assertIn("inline", respuesta["Content-Disposition"])
        self.assertIn("no-store", respuesta["Cache-Control"])
        corto = PdfReader(BytesIO(b"".join(respuesta.streaming_content)))
        self.assertIn("Borrador", "".join(p.extract_text() for p in corto.pages))
        self.assertIn(
            "attachment", self.client.get(ruta + "?descargar=1")["Content-Disposition"]
        )
        atencion.datos = {
            "motivo": "Descripción extensa de prueba. " * 1000 + "MARCA FINAL",
            "origen": "Accidente de tránsito",
            "procedimientos": ["Sutura"],
        }
        pdf = PdfReader(generar_pdf(atencion))
        texto = "".join(p.extract_text() for p in pdf.pages)
        self.assertGreater(len(pdf.pages), len(corto.pages))
        for esperado in (
            "MARCA FINAL",
            "Accidente de tránsito",
            "Ana Ejemplo",
            "001",
            "15/11/2026",
            "12:20",
        ):
            self.assertIn(esperado, texto)
        self.assertNotIn("NCH", texto)
        self.assertNotIn("01 Motivo", texto)
        firmas = {}

        def registrar(linea, cm, tm, fuente, tamano):
            for firma in ("Firma y sello del médico", "Firma del paciente o familiar"):
                if firma in linea:
                    firmas[firma] = cm[4] + tm[4]

        pdf.pages[-1].extract_text(visitor_text=registrar)
        self.assertLess(
            firmas["Firma y sello del médico"], firmas["Firma del paciente o familiar"]
        )
