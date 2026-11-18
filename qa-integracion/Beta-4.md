# QA de Gineco 0.1.0-beta.4

| Componente | Cambio |
|---|---|
| `Archivo.Listado.cs`, `Archivo.Derivaciones.cs`, `EnlaceAdmision.cs`, `Historias.Admision.cs` | Publican los cambios del listado de admisiones y reciben el listado actualizado en las PC vinculadas a Gineco. El identificador de la visita conserva los reingresos como filas distintas. |
| `Archivo.Retiros.cs`, `Archivo.cs`, `Program.cs` | El administrador retira una atención del listado de su estación. Se conserva la historia y se registra cuenta y fecha. |
| `Clinica.cs`, `Afiliadas.cs`, `Editor.cs`, `EntradaLaboratorio.cs` | El NSS admite la longitud recibida. Al finalizar se exige detalle en alergias, atenciones previas y otros análisis cuando corresponda. |
| `Impresion.cs`, `Informe.cs` | Papel carta en el PDF y en el trabajo enviado al controlador de Windows. |
| `Actualizaciones.cs`, `Historias.Actualizaciones.cs` | Consulta pública de las versiones de GitHub sin token. |
| `supabase/migrations/20261118100017_sincronizar_listado_gineco.sql` y `supabase/tests/listado.sql` | Activa el listado compartido con el rol de consulta y prueba permisos, cursores y reintentos. |

| Control | Estado | Evidencia |
|---|---|---|
| Funcionalidad, unitarias, regresión, límites y GUI | PASS | 104 de 104 pruebas de Gineco pasaron. Cero fallos y cero omisiones. Incluye edición tras recepción, intentos sin permiso y reingresos. |
| Integración del listado | PASS | Dos archivos SQLite de prueba compartieron cambios. PostgreSQL aislado aceptó ambas migraciones y pasó los guiones `bandeja.sql` y `listado.sql`, que revierten los datos de prueba. |
| Impresión en Windows | PASS | Microsoft Print to PDF imprimió una sola página de 612 × 792 puntos desde el flujo **Finalizar e imprimir**. Los PDF de varias páginas conservaron ambos logos. |
| Cobertura de persistencia y enlace | PASS | Coverlet midió 64/64 líneas y 22/24 ramas en `Archivo.Listado.cs`, 31/31 y 8/8 en `Archivo.Retiros.cs`, y 38/42 y 25/28 en `EnlaceAdmision.cs`. |
| Cobertura general | FAIL | La aplicación completa obtuvo 2282/2575 líneas (88,62 %) y 891/1116 ramas (79,84 %). |
| Formato, lint y análisis estático | PASS | Comprobación de formato .NET sobre los archivos modificados, `git diff --check` y compilación de pruebas con advertencias como errores. |
| Complejidad y duplicación | PASS | Máximo 9 en la validación clínica y 7 en la persistencia del listado. jscpd obtuvo 0,13 % de líneas duplicadas en escritorio. |
| Build y empaquetado | PASS | `dotnet test pruebas-escritorio/Gineco.Pruebas.csproj -c Release -p:TreatWarningsAsErrors=true` y `./escritorio/Empaquetar.ps1` finalizaron correctamente. |
| Permisos y confidencialidad | PASS | Las pruebas SQL rechazaron el acceso sin credencial y el rol de Admisión en el listado de Gineco. El historial de Git se comprobó antes de publicar. NuGet no notificó dependencias vulnerables. |
| Supabase del hospital y QA físico | NO VERIFICADO | El conector carece de permisos para aplicar la migración al proyecto del hospital. Falta probar las PC y la impresora físicas. |

**ESTADO FINAL: NO APROBADO PARA ENTREGA**
