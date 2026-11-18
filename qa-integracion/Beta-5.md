# QA de Gineco 0.1.0-beta.5

## Implementación

| Archivos | Cambio |
|---|---|
| `escritorio/Historias.Bandeja.cs`, `escritorio/Program.cs` | Bandeja con cinco filtros y contadores. Abre en Pendientes y combina la búsqueda con el filtro activo. En atención incluye borradores. |
| `escritorio/Historias.Admision.cs`, `escritorio/Clinica.cs`, `escritorio/Archivo.Listado.cs`, `escritorio/EnlaceAdmision.cs` | Guarda la hora de sincronización completa en SQLite y la muestra también al reabrir sin conexión. |
| `escritorio/Archivo.cs`, `escritorio/Archivo.Derivaciones.cs` | Registra las impresiones de historias manuales por versión. Una nueva revisión deja de contar como enviada hasta volverse a imprimir. |
| `escritorio/Gineco.csproj`, `escritorio/README.md` | Versión beta.5 e instrucciones de la bandeja. |
| `pruebas-escritorio/VentanasBandeja.cs`, `pruebas-escritorio/PersistenciaBandeja.cs`, `pruebas-escritorio/Listado.cs`, `pruebas-escritorio/Ventanas.cs`, `pruebas-escritorio/VentanasAdmision.cs` | Pruebas de filtros, reanudación, marcas de impresión, continuidad fuera de línea y sincronización paginada. |

Las historias anteriores se abren con el mismo almacén SQLite. Las impresiones manuales quedan registradas desde esta versión. Los reingresos continúan identificándose por visita. El filtro de anulaciones queda pendiente del aviso de SIGEH.

## Resultados y controles

| Control | Estado | Evidencia |
|---|---|---|
| Funcionalidad, unitarias, regresión y límites | PASS | Primero pasaron las 5 pruebas específicas. Suite completa con 109 aprobadas, 0 fallos y 0 omitidas. Se verificaron los límites de 99 y 100 entradas de una página de sincronización. |
| Integración | PASS | Pruebas de WinForms, SQLite y respuestas HTTP simuladas verificaron persistencia al reabrir, transición de estados y conservación de la última hora ante una pérdida de conexión. |
| Cobertura del código cambiado | PASS | Coverlet y el diff midieron 167/168 líneas (99,40 %) y 91/97 ramas (93,81 %). La persistencia del envío a impresión cubrió 11/11 líneas y 4/4 ramas cambiadas. |
| Cobertura general | N/A | Los umbrales se aplican al código nuevo o modificado. Como referencia, Coverlet midió 2391/2685 líneas (89,05 %) y 931/1149 ramas (81,03 %) en el escritorio completo. |
| Formato, lint, análisis estático y compilación de tipos | PASS | `dotnet format --verify-no-changes` sobre los archivos modificados y `git diff --check`. C# compilado con `TreatWarningsAsErrors=true`. |
| Complejidad y duplicación | PASS | Lizard midió complejidad máxima de 9 en las funciones modificadas. jscpd midió 0,13 % de duplicación en escritorio y cero duplicaciones nuevas. |
| Build y empaquetado | PASS | `dotnet test pruebas-escritorio/Gineco.Pruebas.csproj -c Release -p:TreatWarningsAsErrors=true` y `./escritorio/Empaquetar.ps1`. El SHA-256 del paquete coincidió con el manifiesto. |
| Smoke de escritorio | PASS | Se abrió y probó la bandeja en Windows a tamaño normal y reducido con datos ficticios. La regresión de impresión comprobó una página de tamaño carta a través de Microsoft Print to PDF. |
| Seguridad aplicable | PASS | NuGet no informó paquetes vulnerables. Se comprobó la integridad del paquete y la ausencia de archivos de vinculación, bases de datos y configuración privada. |
| QA en el hospital | NO VERIFICADO | Queda pendiente una prueba en la PC de los médicos y en la impresora del hospital. |

**ESTADO FINAL: NO APROBADO PARA ENTREGA**
