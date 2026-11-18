# QA de Gineco 0.1.0-beta.3

Gineco anuncia nuevas versiones desde **Historias clínicas**. La instalación se inicia desde **Actualización disponible → Instalar y reiniciar**. Un administrador configura el acceso al repositorio privado y Windows protege el token en su perfil. Las historias, las cuentas y el vínculo de Admisión siguen en el perfil existente de Windows.

## Archivos de la actualización

| Archivos | Función |
|---|---|
| `escritorio/Actualizaciones.cs`, `Historias.Actualizaciones.cs`, `Program.cs` | Consulta del repositorio, almacenamiento local del token, avisos, descarga y cierre para aplicar una versión. |
| `escritorio/Gineco.csproj`, `Empaquetar.ps1`, `.config/dotnet-tools.json` | Crea el instalador y el canal de Velopack con las mismas versiones del SDK y de la herramienta. |
| `pruebas-escritorio/Actualizaciones.cs` | Cubre aviso, búsqueda, permiso de configuración, cierre, cancelación, fallos de red y rechazo de un paquete corrupto. |
| `README.md` y `escritorio/README.md` | Instalación inicial, configuración de acceso y proceso para publicar una versión nueva. |

## Resultados de verificación

| Control | Estado | Evidencia |
|---|---|---|
| Pruebas unitarias, de interfaz y regresión | PASS | 96 ejecutadas, 96 aprobadas, 0 fallidas y 0 omitidas. |
| Integración y casos límite | PASS | Se aplicó la actualización de control desde una instalación Windows aislada con Velopack. Se verificaron la versión, los binarios instalados y el SHA-256 de la base ficticia externa antes y después. Se probaron credencial ausente e inválida, canal vacío, descarga cancelada y paquete con tamaño incorrecto. |
| Cobertura de los archivos nuevos del actualizador | PASS | Coverlet midió 145/153 líneas (94,77 %) y 44/50 ramas (88 %). El servicio y el almacenamiento del acceso cubrieron 36/39 líneas y 8/8 ramas. La aplicación completa cubrió 2145/2419 líneas y 800/1004 ramas. |
| Formato, lint, análisis y tipos | PASS | `dotnet format --verify-no-changes` en el código de la actualización y las pruebas, `git diff --check` y compilación con advertencias como errores. |
| Complejidad y duplicación | PASS | Máximo de 7 en el código nuevo, medido con Lizard. jscpd detectó 0,14 % de líneas duplicadas en escritorio, en los formularios previos de registro y recuperación. |
| Build e instalador | PASS | `./escritorio/Empaquetar.ps1` compiló el ejecutable Windows x64 autocontenido y generó el instalador, el paquete de actualización y el manifiesto de Velopack. |
| Seguridad | PASS | Se comprobó que la credencial no se guarda como texto y que se rechaza la descarga corrupta. `dotnet list package --vulnerable --include-transitive` no notificó vulnerabilidades. |
| Operación en el hospital | NO VERIFICADO | Falta ejecutar el instalador con la cuenta del consultorio y configurar su acceso de solo lectura. |
| Cambios en SIGEH | N/A | La actualización aplica a Gineco. La anulación de admisiones es una tarea independiente. |

**ESTADO FINAL: NO APROBADO PARA ENTREGA** para despliegue general. Paquete beta para pruebas de instalación.
