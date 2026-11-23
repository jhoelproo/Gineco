# QA de Gineco Beta 6

## Implementación

| Archivos | Cambio |
|---|---|
| `escritorio/Historias.Bandeja.cs`, `escritorio/Program.cs` | El buscador está arriba a la derecha, junto a Configuración en la vista administradora. |
| `escritorio/CuentasCompartidas.cs`, `escritorio/AccesoCompartido.cs`, `escritorio/GestionarCuentaCompartida.cs`, `escritorio/Program.cs` | Inicio de sesión por exequátur, vínculo inicial del administrador, alta médica y recuperación compartidas. Las PC de versiones anteriores pueden abrir su acceso local. |
| `escritorio/Archivo.cs`, `escritorio/Archivo.Perfiles.cs` | La cuenta validada autoriza las acciones de la sesión. La identidad enlaza historias locales por exequátur con la misma regla de mayúsculas que las cuentas. |
| `supabase/config.toml`, `supabase/functions/cuentas-gineco`, `supabase/migrations/20261119023918_cuentas_compartidas_gineco.sql` | Autenticación con Supabase Auth, alias privado por exequátur y activación de un solo uso para el primer administrador. |
| `pruebas-escritorio/CuentasCompartidas.cs`, `pruebas-escritorio/GestionarCuentas.cs`, `supabase/tests/cuentas.sql` | Pruebas de dos puestos, interfaz de cuentas, recuperación, servicio central y privilegios. |
| `escritorio/Gineco.csproj`, `escritorio/README.md` | Versión 0.1.0-beta.6 y uso de las cuentas compartidas. |

## Resultados

| Control | Estado | Evidencia |
|---|---|---|
| Funcionalidad, unitarias, regresión y límites | PASS | 18 pruebas específicas primero. Las 139 pruebas de escritorio y las 7 de la función pasaron con 0 fallos y 0 omitidas. |
| Integración | PASS | Dos archivos SQLite de prueba abrieron la misma identidad. PostgreSQL 17 aislado pasó los cuatro guiones. La función está activa en el proyecto de Ginecología. |
| Cobertura del cambio | PASS | Coverlet midió 606/623 líneas (97,27 %) y 279/322 ramas (86,65 %) en los cambios de escritorio de la beta. Deno midió 98,0 % de líneas y 95,5 % de ramas en el servicio de cuentas. |
| Cobertura crítica | PASS | La validación y el flujo de acceso cubrieron 81/81 líneas y 27/28 ramas en la ventana de cuenta. La comprobación de administrador cubrió todas las ramas nuevas. El servicio central superó 95 % de líneas y 90 % de ramas. |
| Formato, lint, tipos y análisis estático | PASS | `dotnet format --verify-no-changes`, `git diff --check`, compilación C# con `TreatWarningsAsErrors=true`, `deno fmt`, `deno lint` y `deno check`. |
| Complejidad y duplicación | PASS | Lizard midió un máximo de 10 en las funciones C# modificadas. jscpd midió 0,10 % de duplicación y cero duplicaciones nuevas. |
| Build y empaquetado | PASS | `dotnet test pruebas-escritorio/Gineco.Pruebas.csproj -c Release -p:TreatWarningsAsErrors=true` y `./escritorio/Empaquetar.ps1`. Se verificaron el SHA-256 y el contenido del paquete local. |
| Smoke de interfaz | PASS | Buscador a la derecha en los tamaños normal y compacto. Se probaron el cambio de sesión, el acceso local de una PC anterior, el alta y la recuperación en Windows con datos ficticios. |
| Permisos y confidencialidad | PASS | Supabase rechazó acceso desconocido y alta sin autorización. El alias y el código se gestionan con el rol de servicio. NuGet no reportó paquetes vulnerables. El paquete carece de archivos de vínculo y bases clínicas. |
| Activación real y acceso en una segunda PC | NO VERIFICADO | El código de vinculación está listo. El titular debe introducir la contraseña existente en Gineco para activar su cuenta. La beta local puede abrirse en otro puesto para confirmar el acceso. |

**ESTADO FINAL: NO APROBADO PARA ENTREGA**
