from django import forms
from django.contrib import admin
from django.contrib.auth.admin import UserAdmin
from django.contrib.auth.forms import UserChangeForm, UserCreationForm
from django.shortcuts import redirect
from django.urls import reverse

from .models import Medico


class CuentasAdmin(admin.AdminSite):
    site_header = "Administración de cuentas médicas"

    def has_permission(self, request):
        return request.user.is_active and request.user.is_superuser

    def login(self, request, extra_context=None):
        return redirect(reverse("entrar", query={"next": request.GET.get("next", "/")}))


cuentas = CuentasAdmin()


class AltaMedico(UserCreationForm):
    class Meta:
        model = Medico
        fields = ("username", "first_name", "last_name")

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        for clave in self.Meta.fields:
            self.fields[clave].required = True

    def clean_username(self):
        exequatur = self.cleaned_data["username"].upper()
        if Medico.objects.filter(username__iexact=exequatur).exists():
            raise forms.ValidationError("Ya existe una cuenta con este exequátur.")
        return exequatur


class CambioMedico(UserChangeForm):
    class Meta:
        model = Medico
        fields = "__all__"


@admin.register(Medico, site=cuentas)
class MedicoAdmin(UserAdmin):
    add_form = AltaMedico
    form = CambioMedico
    add_fieldsets = (
        (
            None,
            {
                "fields": (
                    "username",
                    "first_name",
                    "last_name",
                    "password1",
                    "password2",
                )
            },
        ),
    )
    fieldsets = (
        ("Identidad profesional", {"fields": ("username", "first_name", "last_name")}),
        ("Cuenta", {"fields": ("password", "is_active", "is_staff", "is_superuser")}),
    )
    list_display = ("username", "first_name", "last_name", "is_active", "is_superuser")
    search_fields = ("username", "first_name", "last_name")
    list_filter = ("is_active", "is_superuser")
    readonly_fields = ("username", "first_name", "last_name")

    def get_readonly_fields(self, request, obj=None):
        return self.readonly_fields if obj else ()

    def has_delete_permission(self, request, obj=None):
        return False
