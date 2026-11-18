# Gineco para Windows

Aplicación de escritorio en C# con Windows Forms. Se abre directamente desde Windows. La exportación PDF coloca el logo del Hospital Provincial Dr. Ángel Contreras a la izquierda y el del SNS a la derecha en todas las páginas. Los recursos gráficos provienen de la referencia entregada.

Para compilar y ejecutar desde la raíz del repositorio con el SDK de .NET 10:

```powershell
dotnet run --project escritorio/Gineco.csproj
dotnet test pruebas-escritorio/Gineco.Pruebas.csproj --collect 'XPlat Code Coverage'
dotnet publish escritorio/Gineco.csproj -c Release -r win-x64 --self-contained
```

La primera vez, crea el administrador local e inicia sesión con su nombre, exequátur y contraseña. Desde el historial puedes dar de alta médicos como administrador, registrar historias, buscarlas, guardar borradores, finalizar una atención y exportar a PDF. Una corrección de una historia finalizada requiere un motivo. La base queda en `%LOCALAPPDATA%\Gineco\historias.sqlite3`.

La base de la versión Django se conserva por separado. La aplicación de escritorio aún no migra las cuentas ni las atenciones de Django, y quedan pendientes la desactivación de cuentas desde la interfaz y la reutilización de fichas de pacientes.

En el inicio de sesión se sugieren nombres y exequátures de cuentas activas. El mínimo es de seis caracteres al crear o restablecer una contraseña. En **Olvidé mi contraseña**, un administrador activo debe autorizar el cambio con su propia contraseña. Si se pierde la contraseña del único administrador, este flujo no puede recuperar el acceso.


La interfaz de Windows comparte tipografía, colores y controles mediante `Estilo.cs`. Usa verde para guardar, azul petróleo para acciones principales y botones neutros para navegación, cancelación y exportación. Acompaña siempre el color con una etiqueta visible. Mantén el nombre de la paciente visible en el editor y los campos clínicos en el orden indicado por el formulario. Los signos vitales se introducen sin unidades y el PDF las añade al exportar.

Los perfiles **Paciente embarazada** y **Paciente no embarazada** se eligen en el editor con **Aplicar perfil**. El administrador define sus textos en **Configuración → Perfiles y aprendizaje**. Empiezan vacíos. Los campos de identificación se escriben para cada paciente. Aplicar o cambiar de perfil rellena los campos vacíos y sustituye los predeterminados anteriores que siguen intactos. Los textos ya editados se conservan. Cada historia guarda el perfil y la copia de los valores aplicados en ese momento.

Antes de cada exportación aparece un único resumen de campos vacíos, predeterminados intactos y correcciones, cuando existen. **Revisar historia** abre el editor. **Continuar y exportar** permite generar el PDF.

El aprendizaje recoge localmente los textos clínicos editados al guardar una historia con perfil. Cada historia cuenta una vez por campo y una nueva edición actualiza su propuesta. El administrador ve la frecuencia por perfil y puede usar una propuesta como predeterminado y guardarla. Debe revisar su pertinencia y retirar cualquier dato identificativo escrito dentro del texto clínico antes de incorporarlo a una plantilla compartida.

En **Corrección ortográfica**, el administrador autoriza pares de palabras. El PDF sustituye únicamente palabras completas de los campos clínicos de texto, conserva mayúsculas iniciales y puntuación, y muestra las sustituciones en el resumen previo. La historia guardada conserva el texto original. El diccionario empieza vacío y no detecta faltas desconocidas ni valida terminología médica. No se corrigen identificación, números o signos vitales. Las revisiones de configuración se registran en SQLite; los cambios posteriores de plantilla no reescriben las historias anteriores.

En **Configuración → Sugerencias de medicamentos**, el administrador revisa textos de medicación de historias finalizadas y puede aprobarlos o retirarlos por perfil. Antes de aprobar, revisa que el diagnóstico y el texto se puedan reutilizar y que no incluyan datos de una paciente. El editor muestra los textos aprobados para el perfil aplicado y una coincidencia del diagnóstico completo, ignorando mayúsculas y diferencias de espacios. El médico revisa el texto y pulsa **Añadir sugerencia** para incorporarlo a su nota editable.

En **Datos de la paciente**, escribe una cédula o NSS completo y sal del campo o pulsa **Buscar afiliada**. Gineco completa los datos identificativos vacíos a partir de las historias que el usuario puede consultar. La edad y los datos de la atención actual se escriben para esa visita. Si hay identificadores incompatibles, se muestra un aviso. **Historias anteriores** las abre en una vista de solo lectura con el editor actual abierto.

La última respuesta explícita de alergias de la misma afiliada puede reutilizarse en una historia nueva. El médico puede revisarla y cambiarla. **Sí** abre el detalle y **No** lo oculta. Los signos vitales y la sección de pruebas, incluido hallazgos y sonografías, se llenan en cada atención y están excluidos de los predeterminados del perfil.

## Conectar Admisión y el consultorio

En Admisión se usa SIGEH y en el consultorio se usa Gineco. Copia la carpeta publicada completa de cada aplicación en su PC. Copia también el script `supabase/Registrar-Estacion.ps1`. El proyecto de enlace en Supabase se llama **Ginecología**.

1. Desde la cuenta de Windows que abre SIGEH en Admisión, ejecuta `.\Registrar-Estacion.ps1 -Funcion admision` en PowerShell.
2. Desde la cuenta de Windows compartida por los médicos en la PC del consultorio, ejecuta `.\Registrar-Estacion.ps1 -Funcion consulta`. Cada médico puede iniciar sesión con su propio usuario de Gineco.
3. Como administrador del proyecto **Ginecología**, ejecuta en el SQL Editor la línea que imprime cada estación. La línea contiene la huella de su credencial. La credencial permanece en el perfil de Windows. Cierra la sesión de Windows y vuelve a entrar para que las aplicaciones reciban la configuración.
4. En el consultorio, selecciona la impresora física como predeterminada de Windows. Abre Gineco, inicia sesión y comprueba que aparece **Admisión conectada**. Haz el primer envío con una ficha de demostración y confirma que sale una hoja en esa impresora.

Cuando Admisión confirma una visita de Ginecología, SIGEH la registra y la deja disponible para facturación. Si se interrumpe la conexión, conserva el envío para reintentarlo. En el consultorio, Gineco consulta las novedades cada cinco segundos y muestra un aviso con sonido. Todos los médicos de esa estación comparten la bandeja local.

**Pendiente** (amarillo) indica que aún no se ha abierto la historia. **En atención** (azul) indica que ya se abrió. **Enviada a impresión** (verde) indica que Windows aceptó el trabajo en la cola de impresión. La historia se guarda en la PC del consultorio y se puede reimprimir a partir de los datos guardados. La integración está preparada para un consultorio receptor compartido. La recepción se confirma después de guardar la visita en esa estación.
