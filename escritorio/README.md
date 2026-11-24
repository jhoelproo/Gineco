# Gineco para Windows

Aplicación de escritorio en C# con Windows Forms. Se abre directamente desde Windows. La exportación PDF coloca el logo del Hospital Provincial Dr. Ángel Contreras a la izquierda y el del SNS a la derecha en todas las páginas. Los recursos gráficos provienen de la referencia entregada.

Para compilar y ejecutar desde la raíz del repositorio con el SDK de .NET 10:

```powershell
dotnet run --project escritorio/Gineco.csproj
dotnet test pruebas-escritorio/Gineco.Pruebas.csproj --collect 'XPlat Code Coverage'
dotnet publish escritorio/Gineco.csproj -c Release -r win-x64 --self-contained
```

Desde la versión 0.1.0, inicia sesión con tu exequátur y contraseña de Gineco. Las cuentas de la organización están en Supabase Auth y se pueden usar en cualquier PC con Gineco e Internet. Los permisos se toman de la cuenta compartida. Desde el historial puedes registrar historias, buscarlas, guardar borradores, finalizar una atención y exportar a PDF. Una corrección de una historia finalizada requiere un motivo. La base clínica de cada PC queda en `%LOCALAPPDATA%\Gineco\historias.sqlite3`.

La base de la versión Django se conserva por separado. La aplicación de escritorio aún no migra las cuentas ni las atenciones de Django, y quedan pendientes la desactivación de cuentas desde la interfaz y la reutilización de fichas de pacientes.

En la versión 0.1.1, el editor incluye un selector AM/PM y la bandeja, el historial y el PDF muestran la hora en formato de 12 horas. El PDF destaca en negrita el nombre, NSS, teléfono y ARS de la paciente, además del nombre y exequátur del médico. La sección de origen muestra la opción seleccionada. Las observaciones del origen que se registraron antes se conservan en las historias guardadas.

**Registrarse**, **Registrar médico** y **Olvidé mi contraseña** requieren la autorización de un administrador con cuenta compartida. El mínimo es de seis caracteres al crear o restablecer una contraseña. En una PC con cuentas de la versión anterior, **Usar una cuenta de esta PC** abre el acceso local y sus sugerencias de usuarios. Las cuentas médicas antiguas se pueden registrar como compartidas conservando su exequátur.

El primer administrador se vincula en el equipo donde ya tiene su cuenta local. Un operador del proyecto Ginecología prepara un archivo privado `%LOCALAPPDATA%\Gineco\vinculo-cuentas.dat` con un código aleatorio de 32 bytes en hexadecimal minúsculo. Supabase guarda su SHA-256, el exequátur autorizado en minúsculas ASCII y su vencimiento en `gineco_privado.vinculo_administrador`. En Gineco, el administrador escribe su exequátur y contraseña local, y pulsa **Vincular mi administrador local**. El código se consume una vez. A partir de entonces puede usar **Iniciar sesión** desde otra PC.


La interfaz de Windows comparte tipografía, colores y controles mediante `Estilo.cs`. Usa verde para guardar, azul petróleo para acciones principales y botones neutros para navegación, cancelación y exportación. Acompaña siempre el color con una etiqueta visible. Mantén el nombre de la paciente visible en el editor y los campos clínicos en el orden indicado por el formulario. Los signos vitales se introducen sin unidades y el PDF las añade al exportar.

Los perfiles **Paciente embarazada** y **Paciente no embarazada** se eligen en el editor con **Aplicar perfil**. El administrador define sus textos en **Configuración → Perfiles y aprendizaje**. Empiezan vacíos. Los campos de identificación se escriben para cada paciente. Aplicar o cambiar de perfil rellena los campos vacíos y sustituye los predeterminados anteriores que siguen intactos. Los textos ya editados se conservan. Cada historia guarda el perfil y la copia de los valores aplicados en ese momento.

Antes de imprimir o exportar, la revisión reúne los campos vacíos, los predeterminados intactos y las palabras corregidas. Selecciona un campo y pulsa **Ir al campo** para abrir esa parte del editor. La acción **Confirmar e imprimir** o **Continuar y exportar** permite generar el documento desde el mismo aviso.

El aprendizaje recoge localmente los textos clínicos editados de historias finalizadas con perfil. Cada historia cuenta una vez por campo y una corrección posterior actualiza su propuesta. En **Configuración → Perfiles y aprendizaje**, selecciona un campo para editar su texto en una ventana amplia o compara una propuesta con el texto actual antes de aplicarla. Revisa el texto y retira datos identificativos antes de guardarlo como predeterminado.

Gineco corrige automáticamente faltas claras de palabras en los campos narrativos del PDF. El corrector funciona localmente, conserva la puntuación y muestra las sustituciones antes de imprimir o exportar. Los campos de identificación, diagnósticos, medicación, alergias y signos vitales conservan sus valores. Las reglas autorizadas en versiones anteriores siguen disponibles para las historias guardadas. El léxico dominicano es [RLA-ES v2.6](https://github.com/sbosio/rla-es/releases/tag/v2.6) y se ejecuta con [WeCantSpell.Hunspell](https://github.com/aarondandy/WeCantSpell.Hunspell). Sus avisos de licencia están en `Recursos/Ortografia`.

En **Configuración → Sugerencias de medicamentos**, el administrador revisa textos de medicación de historias finalizadas y puede aprobarlos o retirarlos por perfil. Antes de aprobar, revisa que el diagnóstico y el texto se puedan reutilizar y que no incluyan datos de una paciente. El editor muestra los textos aprobados para el perfil aplicado y una coincidencia del diagnóstico completo, ignorando mayúsculas y diferencias de espacios. El médico revisa el texto y pulsa **Añadir sugerencia** para incorporarlo a su nota editable.

En **Datos de la paciente**, escribe una cédula o NSS completo y sal del campo o pulsa **Buscar afiliada**. Gineco completa los datos identificativos vacíos a partir de las historias que el usuario puede consultar. La edad y los datos de la atención actual se escriben para esa visita. Si hay identificadores incompatibles, se muestra un aviso. Desde **Historias anteriores**, selecciona una visita y pulsa **Comparar con la historia actual** para ver las dos versiones en paralelo.

La última respuesta explícita de alergias de la afiliada aparece con la fecha como referencia. El médico decide qué marcar en la atención actual. **Sí** abre el detalle y **No** lo oculta. Los signos vitales y la sección de pruebas, incluidos hallazgos y sonografías, se llenan en cada atención.

## Conectar Admisión y el consultorio

En Admisión se usa SIGEH y en el consultorio se usa Gineco. Instala los archivos de programa de la beta en cada puesto y conserva su carpeta de datos. En una instalación portable de SIGEH, los datos se guardan en `_internal/data` salvo que el puesto tenga configurada otra ruta.

1. En la PC de Admisión, extrae la carpeta privada `Vinculo-Gineco-admision` y abre **Vincular-Estacion.cmd** con la cuenta de Windows que usa SIGEH.
2. En la PC del consultorio, repite con `Vinculo-Gineco-consulta` y la cuenta de Windows que abre Gineco. Cada médico inicia sesión con su propio usuario de Gineco.
3. Cierra y vuelve a abrir las aplicaciones. En el consultorio debe aparecer **Admisión conectada**. Selecciona la impresora predeterminada de Windows y prueba una derivación de demostración.

Cuando Admisión confirma una visita de Ginecología, SIGEH guarda la derivación y deja el registro disponible para facturación. Si a Admisión le falta el vínculo o la conexión, la visita permanece en su cola local hasta que pueda enviarla. Si el consultorio está desconectado, recibe las visitas al reconectarse. Gineco consulta las novedades cada cinco segundos y muestra un aviso con sonido. El médico escribe el motivo en la historia.

**Pendiente** (amarillo) indica que aún no se ha abierto la historia. **En atención** (azul) indica que ya se abrió. **Enviada a impresión** (verde) indica que Windows aceptó el trabajo en la cola de impresión. La historia se guarda en la PC del consultorio y se puede reimprimir a partir de los datos guardados. La integración está preparada para un consultorio receptor compartido. La recepción se confirma después de guardar la visita en esa estación.

### Preparar un vínculo como administrador

Desde una cuenta de Windows que ya tenga configurado ese rol, ejecuta `./supabase/Vincular-Estacion.ps1 -Funcion admision -ExportarA <carpeta-privada>`. Para el consultorio usa `-Funcion consulta`. Pasa cada carpeta completa solo al puesto correspondiente. El archivo de vínculo contiene la credencial de esa función. Para registrar una credencial nueva, usa `supabase/Registrar-Estacion.ps1` y ejecuta la sentencia de huella que devuelve en el SQL Editor del proyecto **Ginecología**.

## Actualizaciones de Gineco

Cierra la copia anterior de Gineco. Descarga y ejecuta una vez **HospitalContreras.Gineco-win-Setup.exe** con la cuenta de Windows que usa el consultorio. Abre **Gineco** desde el menú Inicio. Se conservan las cuentas y las historias de esa cuenta de Windows en `%LOCALAPPDATA%\Gineco` y el vínculo de Admisión existente. [Publicaciones en GitHub](https://github.com/jhoelproo/Gineco/releases)

Desde la beta 4, Gineco consulta el repositorio público sin token. Si usas una beta anterior, ejecuta el instalador de la beta 4 una vez para activar esa consulta.

Al abrir **Historias clínicas**, Gineco consulta GitHub y descarga las actualizaciones en segundo plano. Cuando aparezca **Actualización lista al reabrir**, podrás cerrar y abrir Gineco al terminar la atención para que se instale. También puedes usar **Actualizaciones** para buscar una versión o instalarla en el momento. Desde una beta anterior, usa **Actualización disponible → Instalar y reiniciar** una vez para pasar a la versión con descarga automática.

### Publicar una versión nueva

Sube el número de versión en `escritorio/Gineco.csproj` y ejecuta `./escritorio/Empaquetar.ps1` con el SDK de .NET 10. En la publicación de GitHub de esa versión, adjunta `HospitalContreras.Gineco-win-Setup.exe`, `releases.win.json` y todos los `.nupkg` referenciados por el manifiesto que generó Velopack. Publica el instalador y el manifiesto juntos en la misma etiqueta.

## Cambios en la beta 4

- El NSS admite los valores recibidos de Admisión, incluidos los de menos o más de nueve caracteres. El médico puede editarlo.
- Al finalizar, **Sí** en Alergias o Atenciones previas requiere su detalle. Seleccionar **Otros análisis** requiere escribir cuáles. El borrador admite campos incompletos.
- El PDF y el trabajo enviado a Windows usan papel carta (8,5 × 11 pulgadas).
- **Eliminar del listado** permite al administrador retirar la atención seleccionada. Se conserva la historia con el registro de quién la retiró.

### Sincronizar el listado entre consultorios

El administrador de Supabase aplica una vez [la migración del listado](../supabase/migrations/20261118100017_sincronizar_listado_gineco.sql) en el proyecto **Ginecología**, después de la migración de Admisión. Si todavía no está aplicada, Gineco indica **Admisión conectada · Solo listado local** y conserva los cambios por enviar.

En las estaciones vinculadas se comparten el nombre, la cédula, la visita y el estado de las admisiones que el médico abre o modifica. Las historias que se crean directamente en Gineco y el contenido clínico de cada historia se guardan en la PC de origen. **Eliminar del listado** es una acción del administrador sobre la visita seleccionada.

Para compartir los retiros de visitas de Admisión, aplica [la migración de retiros](../supabase/migrations/20261123163757_retiros_compartidos_gineco.sql) y actualiza los puestos de Gineco. En el puesto de consulta autorizado para publicar retiros, ejecuta `./supabase/Registrar-Estacion.ps1 -Funcion consulta_admin` y registra en Supabase la huella que devuelve. Los puestos de consulta pueden recibir el retiro con su vínculo actual. Cuando falta Internet o esa credencial administrativa, Gineco conserva la solicitud de publicación en el puesto que hizo el retiro. Los retiros de historias creadas manualmente corresponden al puesto donde se guardó esa historia.

### Revisar la bandeja

Gineco abre en **Pendientes**. Selecciona **En atención** para retomar un borrador, **Finalizadas** para ver historias listas para imprimir o **Enviadas a impresión** para revisar las que Windows aceptó en la cola. **Todas** permite buscar por nombre o cédula sin elegir un estado. Los contadores muestran el total de cada grupo.

Haz clic derecho sobre una fila para abrir la historia, imprimirla o exportar el PDF según su estado. Para quitar una atención, elige **Eliminar del listado**, confirma y solicita la contraseña de una cuenta de administrador. La autorización se usa para ese retiro. La historia y su registro de auditoría se conservan.

Si la búsqueda queda vacía con un filtro activo, pulsa **Buscar en todas**. En ventanas reducidas, el editor muestra un selector de secciones arriba del formulario. El estado junto al selector de perfil indica cuál se aplicó y si hay otro por aplicar. Desde la bandeja, **Cambiar médico** vuelve a solicitar la cuenta de acceso.

La última sincronización muestra la fecha y hora del hospital. Si se pierde la conexión, Gineco conserva en pantalla el listado disponible y esa hora. Cuando recibe un lote completo, muestra **Sincronizando listado…** hasta que termine de ponerse al día.
