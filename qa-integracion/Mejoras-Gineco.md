# QA de las mejoras de Gineco

## Implementación

| Archivos | Cambio |
|---|---|
| `escritorio/Historias.Bandeja.cs`, `escritorio/Program.cs` | La búsqueda ofrece ampliar a todos los estados. Cambiar médico vuelve a solicitar credenciales y carga los permisos de la nueva cuenta. |
| `escritorio/Editor.cs`, `escritorio/Editor.Afiliada.cs`, `escritorio/VistaHistorias.cs` | Navegación compacta, estado del perfil, referencia de alergias sin preselección y comparación en solo lectura entre la historia actual y una anterior. |
| `escritorio/Configuracion.cs`, `escritorio/Perfiles.cs`, `escritorio/Historias.Admision.cs` | Avisos agrupados por sección con acceso al campo, editor de plantillas y vista previa de las propuestas. |
| `escritorio/OrtografiaAutomatica.cs`, `escritorio/Gineco.csproj`, `escritorio/Recursos/Ortografia`, `escritorio/Archivo.Perfiles.cs` | Corrector ortográfico local para texto narrativo del PDF y aprendizaje a partir de historias finalizadas. |
| `escritorio/Archivo.Derivaciones.cs`, `escritorio/Archivo.Retiros.cs`, `escritorio/EnlaceAdmision.cs`, `supabase/Registrar-Estacion.ps1`, `supabase/migrations/20261119010712_retiros_compartidos_gineco.sql` | Retiro de visitas de Admisión entre los puestos vinculados. La estación publicadora necesita credencial administrativa. El retiro conserva la historia clínica. |
| `pruebas-escritorio/Listado.cs`, `PerfilesClinicos.cs`, `Ventanas.cs`, `VentanasAdmision.cs`, `VentanasBandeja.cs`, `OrtografiaAutomatica.cs`, `RetirosCompartidos.cs`, `supabase/tests/retiros.sql` | Cobertura de las reglas nuevas, regresión de WinForms, dos archivos SQLite de prueba y pruebas de PostgreSQL. |
| `escritorio/README.md` | Uso de las pantallas, alcance de las correcciones y configuración del puesto autorizado para publicar retiros. |

## Resultados

| Control | Estado | Evidencia |
|---|---|---|
| Funcionalidad, unitarias y regresión | PASS | Primero pasaron 59 pruebas relacionadas. La suite completa pasó 121 pruebas con 0 fallos y 0 omisiones. |
| Campos y casos límite | PASS | Probados términos ambiguos, números y signos preservados, borradores excluidos del aprendizaje, paciente equivocada sin referencia de alergias y retiro inválido que revierte el lote. |
| Integración | PASS | Pruebas de WinForms, SQLite y HTTP simulado. Las tres migraciones se aplicaron en PostgreSQL 17 aislado. Pasaron `bandeja.sql`, `listado.sql` y `retiros.sql`. |
| Cobertura del cambio | PASS | Coverlet y el diff midieron 354/360 líneas (98,33 %) y 188/218 ramas (86,24 %). |
| Cobertura crítica | PASS | Persistencia de retiros con 40/40 líneas y 10/10 ramas cambiadas. El enlace cubrió 23/24 líneas y 21/22 ramas. El corrector cubrió 28/28 líneas y 40/42 ramas. |
| Formato, lint, tipos y análisis estático | PASS | `dotnet format --verify-no-changes` para los archivos C# modificados, `git diff --check` y compilación con `TreatWarningsAsErrors=true`. |
| Complejidad y duplicación | PASS | Lizard midió un máximo de 10 en las funciones modificadas. jscpd registró 0,12 % de duplicación en escritorio y cero duplicaciones nuevas. |
| Build y empaquetado | PASS | `dotnet test pruebas-escritorio/Gineco.Pruebas.csproj -c Release -p:TreatWarningsAsErrors=true` y `./escritorio/Empaquetar.ps1`. Se verificaron el contenido y el SHA-256 del paquete local. |
| Smoke de Windows | PASS | Apertura, cambio de cuenta, campo enfocado desde la revisión, comparación, configuración y papel carta con la impresora virtual de Windows. Se usaron datos ficticios. |
| Seguridad | PASS | PostgreSQL rechazó publicaciones con el vínculo normal. NuGet no reportó paquetes vulnerables. El paquete no contiene bases de datos ni archivos de vínculo. |
| Publicación y QA en el hospital | NO VERIFICADO | La migración se probó localmente. Falta aplicarla en Supabase, actualizar los puestos y comprobar el retiro en los equipos del hospital. El paquete local está destinado a QA. |

**ESTADO FINAL: NO APROBADO PARA ENTREGA**
