from django.db import transaction
from django.db.models import Q
from django.utils import timezone

from .models import Atencion, Paciente, Revision


class IdentidadEnConflicto(Exception):
    pass


class VersionEnConflicto(Exception):
    pass


def atenciones_visibles(usuario):
    atenciones = Atencion.objects.select_related("paciente", "medico")
    return atenciones if usuario.is_superuser else atenciones.filter(medico=usuario)


def buscar_atenciones(usuario, consulta):
    atenciones = atenciones_visibles(usuario)
    if consulta:
        cedula = consulta.replace("-", "").replace(" ", "")
        atenciones = atenciones.filter(
            Q(paciente__nombre__icontains=consulta)
            | Q(paciente__cedula=cedula)
            | Q(identificacion__nombre__icontains=consulta)
        )
    return atenciones


def paciente_por_identidad(identificacion, seleccionado):
    cedula = identificacion["cedula"] or None
    datos = {
        campo: valor
        for campo, valor in identificacion.items()
        if campo not in {"edad", "cedula"}
    }
    if seleccionado:
        if (
            seleccionado.cedula != cedula
            or seleccionado.nombre.casefold() != datos["nombre"].casefold()
        ):
            raise IdentidadEnConflicto
        return seleccionado
    if cedula:
        paciente, creado = Paciente.objects.get_or_create(cedula=cedula, defaults=datos)
        if not creado and paciente.nombre.casefold() != datos["nombre"].casefold():
            raise IdentidadEnConflicto
        return paciente
    return Paciente.objects.create(**datos)


@transaction.atomic
def crear_atencion(usuario, limpiezas, seleccionado=None):
    identificacion = {
        clave: valor for clave, valor in limpiezas.items() if clave != "fecha"
    }
    paciente = paciente_por_identidad(identificacion, seleccionado)
    atencion = Atencion.objects.create(
        paciente=paciente,
        medico=usuario,
        nombre_medico=usuario.get_full_name(),
        exequatur=usuario.username,
        identificacion=identificacion,
        fecha=limpiezas["fecha"],
    )
    Revision.objects.create(atencion=atencion, autor=usuario, accion="creación")
    return atencion


@transaction.atomic
def guardar(atencion, usuario, version, datos, accion, motivo, fecha):
    if version != atencion.version:
        raise VersionEnConflicto
    actualizada = Atencion.objects.filter(
        pk=atencion.pk, version=version, finalizada=atencion.finalizada
    ).update(
        version=version + 1,
        datos=datos,
        fecha=fecha or atencion.fecha,
        modificada=timezone.now(),
        finalizada=atencion.finalizada or accion == "finalización",
    )
    if not actualizada:
        raise VersionEnConflicto
    Revision.objects.create(
        atencion=atencion,
        autor=usuario,
        accion=accion,
        motivo=motivo,
        anterior={
            "datos": atencion.datos,
            "finalizada": atencion.finalizada,
            "fecha": atencion.fecha.isoformat(),
        },
    )
