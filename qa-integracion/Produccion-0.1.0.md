# QA de Gineco 0.1.0

## Implementación

| Archivos | Cambio |
|---|---|
| `escritorio/Informe.cs` | Banda azul de Diagnósticos. Texto en negrita para cada diagnóstico, pruebas y sonografías. Encabezado, cajas, fecha y firmas alineados en papel carta. Se conservan ambos logos y el orden clínico. |
| `escritorio/Historias.Actualizaciones.cs`, `escritorio/Program.cs` | Descarga en segundo plano y aplicación al reabrir. El control manual sigue disponible para la primera actualización desde una beta y para reintentos. |
| `escritorio/Gineco.csproj`, `escritorio/README.md` | Versión estable e instrucciones de uso y actualización. Incluye las mejoras de interfaz y cuentas compartidas documentadas en los informes anteriores. |
| `supabase/migrations/20261123163757_retiros_compartidos_gineco.sql`, `supabase/Registrar-Estacion.ps1`, `supabase/tests/retiros.sql` | Retiros activados en Supabase con credenciales administrativas en una tabla privada. Los vínculos previos de Admisión y Consulta se conservan. |
| `pruebas-escritorio/Laboratorios.cs`, `CambiosClinicos.cs`, `Actualizaciones.cs` | Pruebas de negrita, formato carta, salidas de una y varias páginas, descarga, cancelación y reintento. |

## Quality gates

| Control | Estado | Evidencia |
|---|---|---|
| Funcionalidad, unitarias, regresión y límites | PASS | Pasaron 30 pruebas específicas y después 145 pruebas de escritorio y 7 del servicio. Cero fallos y cero omisiones. |
| Integración | PASS | Las cuatro migraciones se ejecutaron en PostgreSQL 17 aislado y pasaron `bandeja.sql`, `cuentas.sql`, `listado.sql` y `retiros.sql`. Supabase de Ginecología confirmó lectura de retiros y rechazo de escritura con el vínculo normal. |
| Cobertura del cambio | PASS | Coverlet midió 657/675 líneas (97,33 %) y 308/352 ramas (87,50 %) contra Beta 5. El PDF cubrió 32/32 líneas y 24/24 ramas modificadas. El flujo de descarga automática cubrió 19/19 líneas y 5/6 ramas modificadas. Deno midió 98,0 % de líneas y 95,5 % de ramas del servicio. |
| Formato, lint, tipos y análisis estático | PASS | `dotnet format --verify-no-changes`, `git diff --check`, compilación C# con advertencias como errores, `deno fmt --check`, `deno lint` y `deno check`. |
| Complejidad y duplicación | PASS | Lizard midió un máximo de 10 en las funciones C# modificadas. jscpd midió 0,10 % de duplicación y cero duplicaciones nuevas. |
| Build y empaquetado | PASS | `dotnet test pruebas-escritorio/Gineco.Pruebas.csproj -c Release -p:TreatWarningsAsErrors=true` y `./escritorio/Empaquetar.ps1`. SHA-256 e integridad comprobados en los tres archivos de GitHub y en el instalador del ZIP. |
| Smoke del PDF y de impresión | PASS | Poppler renderizó la historia de una página y las tres páginas del caso largo. El PDF exportado tiene texto en Arial Bold para los diagnósticos, laboratorios y sonografías. La regresión de Microsoft Print to PDF confirmó papel carta. |
| Actualización pública | PASS | Se publicó la etiqueta estable `v0.1.0`. Una copia aislada de Beta 5 descargó e instaló 0.1.0 desde GitHub sin token. Otra copia aislada abrió 0.1.0 con un paquete local posterior preparado y lo aplicó al arrancar, sin pulsar Instalar. |
| Seguridad | PASS | NuGet no detectó paquetes vulnerables. ZIP e instalador verificados. El paquete no incluye datos clínicos ni archivos privados. |
| Instalación en el hospital | NO VERIFICADO | Falta la prueba en la PC física, con el acceso autorizado, la conexión de Admisión y la impresora del puesto. El vínculo de un solo uso para activar al primer administrador se renovó en su PC de origen. |

**ESTADO FINAL: NO APROBADO PARA ENTREGA**
