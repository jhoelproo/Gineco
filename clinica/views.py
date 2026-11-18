from django.contrib.auth.decorators import login_required
from django.core.paginator import Paginator
from django.db import DatabaseError
from django.http import FileResponse
from django.shortcuts import get_object_or_404, redirect, render
from django.utils import timezone
from django.views.decorators.cache import never_cache
from django.views.decorators.http import require_http_methods, require_safe

from .forms import ClinicaForm, IdentificacionForm, datos_clinicos
from .pdf import generar_pdf
from .schema import IDENTIFICACION
from .servicios import (
    IdentidadEnConflicto,
    VersionEnConflicto,
    atenciones_visibles,
    buscar_atenciones,
    crear_atencion,
    guardar,
)


@never_cache
@login_required
@require_safe
def inicio(request):
    consulta = request.GET.get("q", "").strip()[:200]
    pendientes = request.GET.get("estado") == "pendientes"
    historias = buscar_atenciones(request.user, consulta)
    if pendientes:
        historias = historias.filter(finalizada=False)
    pagina = Paginator(historias, 20).get_page(request.GET.get("p"))
    return render(
        request,
        "inicio.html",
        {"pagina": pagina, "consulta": consulta, "pendientes": pendientes},
    )


@never_cache
@login_required
@require_http_methods(["GET", "HEAD", "POST"])
def nueva(request, anterior=None):
    paciente = paciente_anterior(request.user, anterior)
    datos = {"fecha": timezone.localtime()}
    if paciente:
        datos.update(
            {
                clave: getattr(paciente, clave)
                for clave in IDENTIFICACION
                if clave != "edad"
            }
        )
    formulario = IdentificacionForm(
        request.POST if request.method == "POST" else None, initial=datos
    )
    if request.method == "POST" and formulario.is_valid():
        atencion = registrar_identificacion(request, formulario, paciente)
        if atencion:
            return redirect("historia", pk=atencion.pk)
    return render(
        request, "nueva.html", {"formulario": formulario, "paciente": paciente}
    )


def paciente_anterior(usuario, anterior):
    if anterior is None:
        return None
    return get_object_or_404(atenciones_visibles(usuario), pk=anterior).paciente


def registrar_identificacion(request, formulario, paciente):
    if paciente and request.POST.get("identidad") != "confirmada":
        formulario.add_error(
            None, "Confirma la identidad de la paciente antes de continuar."
        )
        return None
    try:
        return crear_atencion(request.user, formulario.cleaned_data, paciente)
    except IdentidadEnConflicto:
        formulario.add_error(
            "cedula", "Verifica el nombre y la cédula antes de continuar."
        )
        return None
    except DatabaseError:
        formulario.add_error(
            None, "No se pudo guardar la identificación. Intenta nuevamente."
        )
        return None


def inicial_clinico(atencion):
    datos = dict(
        atencion.datos,
        version=atencion.version,
        fecha=timezone.localtime(atencion.fecha),
    )
    datos["diagnosticos"] = "\n".join(
        item["texto"] for item in datos.get("diagnosticos", [])
    )
    return datos


def aplicar_cambio(formulario, atencion, usuario, accion):
    limpios = formulario.cleaned_data
    motivo = limpios["motivo_correccion"]
    if atencion.finalizada and (accion != "corrección" or not motivo):
        formulario.add_error(
            "motivo_correccion",
            "Indica el motivo para corregir la historia finalizada.",
        )
        return False
    if not atencion.finalizada and accion not in {"borrador", "finalización"}:
        formulario.add_error(None, "Selecciona una acción válida.")
        return False
    try:
        guardar(
            atencion,
            usuario,
            limpios["version"],
            datos_clinicos(limpios),
            accion,
            motivo,
            limpios["fecha"],
        )
    except VersionEnConflicto:
        formulario.add_error(
            None,
            "La historia cambió. Conserva tu texto antes de abrir la versión actual en otra pestaña.",
        )
        return False
    except DatabaseError:
        formulario.add_error(
            None, "No se pudo guardar la historia. Intenta nuevamente."
        )
        return False
    return True


@never_cache
@login_required
@require_http_methods(["GET", "HEAD", "POST"])
def historia(request, pk):
    atencion = get_object_or_404(atenciones_visibles(request.user), pk=pk)
    formulario = ClinicaForm(
        request.POST if request.method == "POST" else None,
        initial=inicial_clinico(atencion),
    )
    if (
        request.method == "POST"
        and formulario.is_valid()
        and aplicar_cambio(
            formulario, atencion, request.user, request.POST.get("accion")
        )
    ):
        return redirect("historia", pk=pk)
    return render(
        request, "historia.html", {"atencion": atencion, "formulario": formulario}
    )


@never_cache
@login_required
@require_safe
def documento(request, pk):
    atencion = get_object_or_404(atenciones_visibles(request.user), pk=pk)
    return FileResponse(
        generar_pdf(atencion),
        content_type="application/pdf",
        as_attachment=request.GET.get("descargar") == "1",
        filename=f"atencion-{pk}.pdf",
    )
