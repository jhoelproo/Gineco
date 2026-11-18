from django.contrib.auth.views import LoginView, LogoutView
from django.urls import path

from . import views
from .admin import cuentas
from .forms import AccesoForm

urlpatterns = [
    path("", views.inicio, name="inicio"),
    path(
        "entrar/",
        LoginView.as_view(template_name="entrar.html", authentication_form=AccesoForm),
        name="entrar",
    ),
    path("salir/", LogoutView.as_view(), name="salir"),
    path("cuentas/", cuentas.urls),
    path("atencion/nueva/", views.nueva, name="nueva"),
    path("atencion/nueva/<int:anterior>/", views.nueva, name="reutilizar"),
    path("atencion/<int:pk>/", views.historia, name="historia"),
    path("atencion/<int:pk>/pdf/", views.documento, name="documento"),
]
