# QA de Gineco 0.1.0-beta.2

SIGEH conserva las derivaciones de Ginecología sin conexión y sin vínculo inicial. La confirmación de Admisión solo registra la visita. El médico escribe el motivo en la historia. Se excluyen el motivo y la prioridad de las derivaciones nuevas y de los reintentos provenientes de la primera beta.

## Implementación

- `integracion-sigeh/ginecologia.patch` modifica el formulario y la cola de Admisión, e incluye sus pruebas.
- `escritorio/Archivo.Derivaciones.cs`, `EnlaceAdmision.cs`, `Historias.Admision.cs` y `Program.cs` leen el vínculo de Windows, muestran el estado de admisión e ignoran los datos clínicos de los sobres antiguos al crear una historia.
- `supabase/Vincular-Estacion.cmd` y `Vincular-Estacion.ps1` instalan el vínculo de la función correspondiente en el perfil de Windows. Las carpetas privadas se preparan fuera del repositorio.

## Verificación

| Quality gate | Estado | Resultado |
|---|---|---|
| Pruebas de escritorio | PASS | 82 pasaron, 0 fallaron y 0 se omitieron. Incluyen rechazo de credencial, pérdida de conexión, aviso, persistencia y motivo de consulta editable. |
| Pruebas específicas de SIGEH | PASS | 17 pasaron, 0 fallaron y 0 se omitieron. Incluyen registro sin vínculo, reintento sin duplicados, compatibilidad con sobres anteriores y lectura de configuración de Windows. |
| Regresión relacionada de SIGEH | FAIL | Se ejecutaron 47 pruebas con el parche aplicado a la revisión base. Pasaron 46 y falló 1. La prueba de vigencia de turno vuelve a fallar con la restricción de atención duplicada, igual que en la revisión sin integración. La regresión completa de SIGEH de la primera beta está documentada por separado. |
| Integración con Supabase y casos límite | PASS | Se registró una paciente ficticia sin cédula ni NSS en una base aislada con la estación desvinculada. Después de habilitar el vínculo, la API recibió una sola derivación y la función de consulta pudo leerla y acusar recibo. |
| Vinculación | PASS | Ambos paquetes se generaron e instalaron en un perfil de prueba ya vinculado. Se comprobó que el instalador rechaza un archivo ausente y una función inválida. |
| Cobertura de la integración | PASS | Módulo de envío Python 62/62 líneas y 16/16 ramas. Persistencia de la bandeja C# 68/68 líneas y 25/26 ramas. Cliente, avisos, persistencia e impresión C# 201/214 líneas (93,93 %) y 86/92 ramas (93,48 %). |
| Cobertura general de Gineco | FAIL | Coverlet midió 1999/2264 líneas (88,29 %) y 748/944 ramas (79,24 %) en la compilación Release. |
| Formato, lint y tipos | PASS | `dotnet format --verify-no-changes`, Ruff en el módulo y pruebas de SIGEH, mypy aislado en el módulo de envío y compilación de C# con advertencias como errores. |
| Complejidad y duplicación | PASS | Radon registró un máximo de 7 en el módulo de envío y Lizard un máximo de 8 en los tres archivos C# de conexión, recepción y persistencia. jscpd midió 0 % en el módulo de envío y 0,15 % en Gineco. |
| Build y arranque | PASS | `dotnet publish escritorio/Gineco.csproj -c Release -r win-x64 --self-contained true`. `py -3.14 -m PyInstaller --noconfirm --workpath <compilacion> --distpath <salida> build_app.spec`. El autodiagnóstico del paquete SIGEH abrió Admisión, el historial y la configuración. |
| Prueba física en el hospital | NO VERIFICADO | Queda abrir cada aplicación con la cuenta de Windows de su puesto y enviar una visita de demostración entre ambas PC. |

**ESTADO FINAL: NO APROBADO PARA ENTREGA**
