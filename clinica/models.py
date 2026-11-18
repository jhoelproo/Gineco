from django.conf import settings
from django.contrib.auth.models import AbstractUser
from django.db import models
from django.utils import timezone


class Medico(AbstractUser):
    username = models.CharField("Exequátur", max_length=40, unique=True)
    REQUIRED_FIELDS = ("first_name", "last_name")

    def __str__(self):
        return self.get_full_name() or self.username


class Paciente(models.Model):
    nombre = models.CharField("Nombre completo", max_length=200, db_index=True)
    cedula = models.CharField(
        "Cédula", max_length=11, unique=True, null=True, blank=True
    )
    sexo = models.CharField(max_length=30, blank=True)
    ars = models.CharField("ARS", max_length=100, blank=True)
    nss = models.CharField("NSS", max_length=9, blank=True)
    telefono = models.CharField("Teléfono", max_length=10, blank=True)
    direccion = models.CharField("Dirección", max_length=250, blank=True)
    nacionalidad = models.CharField(max_length=80, blank=True)

    def __str__(self):
        return self.nombre


class Atencion(models.Model):
    paciente = models.ForeignKey(Paciente, on_delete=models.PROTECT)
    medico = models.ForeignKey(settings.AUTH_USER_MODEL, on_delete=models.PROTECT)
    nombre_medico = models.CharField(max_length=300)
    exequatur = models.CharField(max_length=40)
    identificacion = models.JSONField(default=dict)
    datos = models.JSONField(default=dict, blank=True)
    fecha = models.DateTimeField(default=timezone.now)
    finalizada = models.BooleanField(default=False)
    version = models.PositiveIntegerField(default=0)
    creada = models.DateTimeField(auto_now_add=True)
    modificada = models.DateTimeField(auto_now=True)

    class Meta:
        ordering = ("-fecha", "-pk")


class Revision(models.Model):
    atencion = models.ForeignKey(
        Atencion, on_delete=models.PROTECT, related_name="revisiones"
    )
    autor = models.ForeignKey(settings.AUTH_USER_MODEL, on_delete=models.PROTECT)
    fecha = models.DateTimeField(auto_now_add=True)
    accion = models.CharField(max_length=30)
    motivo = models.TextField(blank=True)
    anterior = models.JSONField(default=dict)
