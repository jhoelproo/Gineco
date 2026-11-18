# Verificación de SIGEH y Ginecología

Se probó el recorrido de una visita ficticia desde Admisión, pasando por el proyecto gratuito **Ginecología** en Supabase, hasta la impresión en Windows. Gineco también reimprimió la visita a partir de la historia guardada. El equipo de prueba usó Microsoft Print to PDF. La instalación en los puestos del hospital y la salida en papel quedan pendientes.

## Implementación

| Componente | Archivos principales | Comportamiento comprobado |
|---|---|---|
| Admisión en SIGEH | `ADMISION_PYSIDE6_V15/derivaciones_gineco.py`, `facturacion_tabs_pyside6.py`, `build_app.spec` | Guarda el envío con la visita, admite cédula y NSS vacíos, conserva los pendientes de red y reintenta. Una derivación omite la impresión local y permite continuar con facturación. Una visita sin derivar conserva la salida ordinaria. |
| Enlace en Supabase | `supabase/migrations/20261117233407_bandeja_ginecologia.sql`, `supabase/Registrar-Estacion.ps1` | Credenciales por estación y función. Consulta confirma la recepción después de guardar localmente. Los reintentos conservan una sola visita. |
| Consultorio en Windows | `escritorio/Archivo.Derivaciones.cs`, `EnlaceAdmision.cs`, `Historias.Admision.cs`, `Program.cs`, `Impresion.cs` | Bandeja compartida en la PC, aviso lateral con sonido, conservación de la fila seleccionada, estados según apertura y envío a la impresora. Impresión A4 con ambos logos y reimpresión desde la historia. |
| Pruebas y operación | `tests/test_derivaciones_gineco.py` y el autodiagnóstico de `CALCULOS_QT.py` en SIGEH. `pruebas-escritorio/Derivaciones.cs`, `Ventanas.cs`, `VentanasAdmision.cs`, `supabase/tests/bandeja.sql` y `escritorio/README.md` en Gineco. | Incluye pruebas de regresión de inicialización de la bandeja, entrega de dos visitas, avisos, cambio de médico, escala de impresión y cierre del paquete de SIGEH. |

## Pruebas y resultados

| Alcance | Ejecutadas | Pasaron | Fallaron | Omitidas |
|---|---:|---:|---:|---:|
| Suite de escritorio C# | 81 | 81 | 0 | 0 |
| Pruebas específicas de derivaciones en SIGEH | 18 | 18 | 0 | 0 |
| Derivaciones, eventos de Admisión, facturación y sincronización sin conexión | 48 | 47 | 1 | 0 |
| Suite completa de SIGEH, incluidos los grupos anteriores | 659 | 615 | 43 | 1 |

La suite de SIGEH se ejecutó por grupos y, cuando Qt bloqueó el proceso compartido, por pruebas individuales. Se cotejaron los resultados JUnit con los 659 identificadores recogidos por pytest. Los 43 fallos se reprodujeron en una copia de la revisión anterior `693fdd6`. Afectan a transición de estación principal (19), listado mensual (11), papelera y duplicados de recibos (6), historial de acciones (5), facturación por ARS (1) y vigencia de un turno de 24 horas (1). La prueba omitida exige activar expresamente la prueba de capacidad de PostgreSQL con datos operativos. [Resultados por prueba](regresion-sigeh.csv)

**Integración y casos límite — PASS.** Se probaron alta sin documentos, bandeja vacía, hora de mediodía y medianoche, identificadores inválidos, cancelación, persistencia al reabrir, cambio de médico, error de conexión, repetición de entrega y fallo de impresora. El guion SQL ejecutó sus comprobaciones de roles y restricciones dentro de una transacción con rollback en PostgreSQL local. En Supabase, la lectura de consulta respondió HTTP 200 y los intentos de lectura con credencial de Admisión o sin credencial respondieron HTTP 401.

**Impresión virtual y revisión visual — PASS.** Desde la ventana del consultorio se confirmó la impresión y se verificó el cambio a verde. Se corrigió la escala del documento, se comprobó la posición del logo derecho y del folio en la prueba de regresión y se inspeccionó la hoja reimpresa. [Hoja de demostración en A4](hoja-demostracion.pdf)

## Cobertura medida

| Alcance | Líneas | Ramas |
|---|---:|---:|
| Módulo de envío `derivaciones_gineco.py` | 51/51 (100 %) | 14/14 (100 %) |
| Líneas modificadas de la integración en `facturacion_tabs_pyside6.py` | 72/77 (93,51 %) | 24/30 (80 %) |
| Líneas modificadas del autodiagnóstico en `CALCULOS_QT.py` | 3/4 (75 %) | 1/2 (50 %) |
| Recepción y persistencia C#, `Archivo.Derivaciones.cs` | 76/76 (100 %) | 25/26 (96,15 %) |
| Cliente, persistencia, bandeja e impresión C#, cuatro archivos completos | 224/238 (94,12 %) | 83/88 (94,32 %) |
| Aplicación C# completa, incluido código previo | 2260/2591 (87,22 %) | 748/940 (79,57 %) |

El informe usa coverage.py y Coverlet. En Python se cotejaron las líneas ejecutables y el origen de cada rama con los fragmentos modificados respecto de la revisión de control. La medición de cuatro archivos C# corresponde a `Archivo.Derivaciones.cs`, `EnlaceAdmision.cs`, `Historias.Admision.cs` e `Impresion.cs`.

## Calidad, build y QA

| Comprobación | Estado | Evidencia |
|---|---|---|
| Funcionalidad en la estación de prueba, integración, casos límite y pruebas unitarias de la derivación | PASS | Recorrido de ida por Supabase, registros persistidos y reimpresión de la paciente ficticia. |
| Regresión general de SIGEH | FAIL | 43 fallos también presentes en la revisión de control. |
| Cobertura | FAIL | El adaptador de SIGEH tiene un 80 % de ramas cubiertas frente al mínimo del 85 %. |
| Formato y lint del código comprobado | PASS | Ruff en el módulo de envío y sus pruebas. `dotnet format --verify-no-changes` en los archivos C# de la integración y sus pruebas. `git diff --check` en SIGEH. |
| Tipos y análisis estático del módulo de envío y aplicación C# | PASS | mypy con comprobación de funciones sin anotaciones sobre el módulo de envío aislado. Compilación .NET con advertencias tratadas como errores, cero advertencias y cero errores. |
| Tipado completo de SIGEH | FAIL | Al cargar las dependencias heredadas, mypy informa errores de tipos y módulos importados bajo dos nombres. |
| Complejidad de los módulos de la integración | PASS | Máximo 9 en el módulo de envío con Radon y máximo 7 en los cuatro archivos C# con Lizard. |
| Duplicación en el alcance medido | PASS | jscpd detectó 0 % en el módulo de envío y 0,15 % en el proyecto C#. Las cinco líneas coincidentes están en los formularios de registro y recuperación. |
| Build de Gineco | PASS | `dotnet publish escritorio/Gineco.csproj -c Release -r win-x64 --self-contained true -p:TreatWarningsAsErrors=true`. El ejecutable publicado abrió **Iniciar sesión**. |
| Build y arranque de SIGEH | PASS | `py -3.14 -m PyInstaller --noconfirm --workpath <compilacion> --distpath <salida> build_app.spec`. En el paquete generado, `CALCULOS_QT.exe --check-v15-package <informe.json>` abrió Admisión, historial y configuración. |
| Seguridad comprobada | PASS | Separación de roles en el servicio real, pruebas SQL de permisos, reintentos idempotentes y análisis de dependencias NuGet directas y transitivas sin vulnerabilidades notificadas. |
| Instalación en las dos PC físicas e impresión en papel | NO VERIFICADO | Se necesita acceso a los puestos de Admisión y consulta y a la impresora del hospital. |
| QA final para despliegue general | FAIL | Quedan pendientes la regresión general, la cobertura indicada y la comprobación en los puestos físicos. |

**ESTADO FINAL: NO APROBADO PARA ENTREGA**
