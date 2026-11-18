# Gineco para Windows

Aplicación de escritorio para Windows que permite al personal médico registrar, consultar e imprimir historias clínicas de emergencia ginecológica. Puede recibir las pacientes que Admisión deriva desde SIGEH.

## Probar la beta en Windows

1. Descarga `Gineco-0.1.0-beta.2-win-x64.zip` desde la [segunda beta](https://github.com/jhoelproo/Gineco/releases/tag/v0.1.0-beta.2).
2. Extrae la carpeta completa y abre `Gineco.exe`. El paquete incluye el entorno de ejecución de .NET para Windows de 64 bits.
3. En una instalación nueva, registra al primer administrador. Si ya tienes una cuenta, inicia sesión con ella.

La bandeja utiliza amarillo para las pacientes pendientes, azul para las que están en atención y verde cuando Windows ha aceptado la impresión. La historia se guarda en la estación del consultorio y permite reimprimir la hoja. [Guía del consultorio y conexión de estaciones](escritorio/README.md)

## Integración con SIGEH

La carpeta `integracion-sigeh` incluye el parche de Admisión para la revisión `693fdd6f62a50e3d3dd30e6d6404931048a48e1b` de SIGEH. Desde una copia de esa revisión, ejecuta `git apply --check <ruta-al-parche>` y después `git apply <ruta-al-parche>`. Para preparar el puesto de Admisión, recompila SIGEH siguiendo el procedimiento de su proyecto. Los componentes de base de datos y registro de estaciones están en `supabase/`.

## Resultados y alcance

**Versión preliminar para pruebas.** La segunda beta guarda la derivación aunque la estación aún esté sin vincular y permite vincular cada PC con un archivo preparado por el administrador. El médico completa el motivo de consulta en la historia. La comprobación en los puestos del hospital sigue pendiente. [QA de la segunda beta](qa-integracion/Beta-2.md) · [Informe de la primera beta](qa-integracion/Informe.md) · [Hoja de demostración](qa-integracion/hoja-demostracion.pdf)

El prototipo anterior en Django se conserva en el repositorio. [Documentación del prototipo](clinica/README.md)
