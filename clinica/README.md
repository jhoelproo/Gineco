# Historia clínica de emergencia ginecológica

Aplicación web en español para registrar atenciones del Hospital Provincial Dr. Ángel Contreras. Usa Django, SQLite y ReportLab.

## Instalación local en Windows

Ejecutar desde la carpeta del proyecto. Se recomienda Python 3.13.

```powershell
python -m venv .venv
$pythonClinica = ".\.venv\Scripts\python.exe"
& $pythonClinica -m pip install -r requirements.txt
$env:CLINICA_LOCAL = "1"
& $pythonClinica manage.py migrate
& $pythonClinica manage.py createsuperuser
& $pythonClinica manage.py runserver 127.0.0.1:8000
```

Abrir http://127.0.0.1:8000. Al crear el primer administrador, usar su exequátur como identificador, escribir su nombre y apellidos y elegir una contraseña única. Luego iniciar sesión con el nombre completo, el exequátur y la contraseña. En **Cuentas médicas** se pueden dar de alta, desactivar y restablecer las contraseñas de los médicos.

## Flujo de atención

1. Seleccionar **Nueva historia clínica**. El sistema propone fecha y hora del hospital en la zona America/Santo_Domingo.
2. Completar la identificación. El nombre es obligatorio. Cédula, teléfono y NSS aceptan separadores y se guardan como dígitos. Al reutilizar una paciente desde el historial, confirmar su identidad.
3. Completar las secciones clínicas y guardar un borrador o finalizar. Introducir un diagnóstico por línea. Las ediciones posteriores a la finalización requieren un motivo. La revisión conserva datos anteriores, autor y fecha de registro.
4. Abrir **Vista previa del PDF** para revisar la versión guardada en el visor del navegador, que también ofrece impresión. **Descargar PDF** guarda una copia.

Los médicos ven sus propias atenciones. Los administradores pueden gestionar cuentas y consultar o corregir atenciones. La identificación profesional que figura en el PDF corresponde al responsable registrado al crear la atención. Una corrección mantiene ese dato y registra a su autor en la auditoría. El PDF adjunto de ejemplo contiene datos ficticios.

## Configuración y respaldo

- `CLINICA_LOCAL=1` activa el modo de desarrollo. Usarlo solo en loopback con datos ficticios.
- Para un despliegue institucional, configurar `CLINICA_SECRET` con un secreto único, `CLINICA_HOSTS` con los dominios autorizados y `CLINICA_DB` con una ruta absoluta en almacenamiento privado. Servir mediante WSGI y HTTPS. Ejecutar `collectstatic` para publicar los recursos estáticos.
- Con la aplicación detenida y las conexiones a la base cerradas, copiar la base SQLite a una ubicación cifrada con acceso restringido. Si existen archivos auxiliares de SQLite, usar su API de respaldo en línea en vez de copiar archivos activos. Para comprobar la recuperación, restaurar en una ruta distinta y arrancar una instancia aislada apuntando `CLINICA_DB` a esa copia.
- La instalación del proyecto es local. El despliegue, el cifrado de almacenamiento, los límites de intentos de inicio de sesión y la operación de respaldos se configuran en el entorno institucional.

## Arquitectura

- `clinica/models.py` y `clinica/migrations/` definen el esquema y la migración inicial.
- `clinica/forms.py` y `clinica/schema.py` definen campos, secciones y validaciones.
- `clinica/servicios.py` controla visibilidad, identidad, cambios de estado y revisiones.
- `clinica/views.py`, `clinica/templates/` y `clinica/static/` implementan el flujo web.
- `clinica/pdf.py` genera el documento A4.

El azul identifica acciones primarias y títulos, mientras el fondo neutro sostiene la lectura. La navegación por secciones acorta el desplazamiento y el espaciado compacto mantiene los controles cerca del texto que editan. La tipografía del sistema aporta legibilidad sin descargas externas. Los estados interactivos usan foco y cambios de color.

## Validación

```powershell
$env:CLINICA_LOCAL = "1"
& $pythonClinica -m pip install -r requirements-dev.txt
& $pythonClinica manage.py test clinica
& $pythonClinica -m coverage run --branch --source=clinica --omit='*/tests.py' manage.py test
& $pythonClinica -m coverage report -m
& $pythonClinica -m ruff check .
& $pythonClinica -m ruff format --check .
& $pythonClinica -m radon cc clinica -s -a
& $pythonClinica manage.py makemigrations --check --dry-run
```
