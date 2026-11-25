# Instalación y configuración

Guía completa para levantar el sistema en un entorno local: base de datos, datos de demostración, backend y frontend. El resumen de cuatro pasos está en el [README](../README.md#inicio-rápido).

## Requisitos previos

- **SQL Server**: SQL Server Express (instancia `.\SQLEXPRESS`) o SQL Server LocalDB (instancia `(localdb)\MSSQLLocalDB`, incluida con Visual Studio).
- **Visual Studio 2022 o posterior** con la carga de trabajo *Almacenamiento y procesamiento de datos* (SQL Server Data Tools), para publicar el proyecto de base de datos. Como alternativa sin interfaz gráfica se puede usar MSBuild y `sqlpackage` (ver más abajo).
- **.NET SDK 10**.
- **Node.js** (versión LTS) y npm.
- **sqlcmd** (incluido con SQL Server o instalable por separado), para cargar los datos de demostración.

Los pasos se ejecutan en este orden: base de datos, datos de demostración, backend y frontend.

## 1. Crear la base de datos

El esquema se define en el proyecto `Database/Balanza.CILP.DataBase` (`.sqlproj`). El backend no crea ni migra tablas.

### Con Visual Studio

1. Abrir `Database/Balanza.CILP.DB.sln`.
2. Clic derecho sobre el proyecto `Balanza.CILP.DataBase` y elegir **Publicar**.
3. Cargar el perfil `Balanza.CILP.DataBase.publish.xml`. Por defecto apunta a la instancia `.\SQLEXPRESS` y crea la base `BalanzaCILP`.
4. Para usar otra instancia, cambiar la conexión de destino con el botón **Editar**:
   - **SQL Server Express:** servidor `.\SQLEXPRESS`.
   - **LocalDB:** servidor `(localdb)\MSSQLLocalDB`, autenticación de Windows y **Cifrar** en *Opcional* (o marcar *Certificado de servidor de confianza*).
5. Verificar que el **Nombre de la base de datos de destino** sea `BalanzaCILP`.
6. Publicar.

### Desde la línea de comandos

Desde la Developer PowerShell de Visual Studio, en la raíz del repositorio:

```powershell
msbuild Database\Balanza.CILP.DataBase\Balanza.CILP.DataBase.sqlproj /p:Configuration=Debug /v:minimal /nologo

# Una sola vez
dotnet tool install -g microsoft.sqlpackage

sqlpackage /Action:Publish `
  /SourceFile:"Database\Balanza.CILP.DataBase\bin\Debug\Balanza.CILP.DataBase.dacpac" `
  /Profile:"Database\Balanza.CILP.DataBase\Balanza.CILP.DataBase.publish.xml"
```

Si `sqlpackage` no está en el PATH, se encuentra en `$env:USERPROFILE\.dotnet\tools\sqlpackage.exe`.

### Qué hace la publicación

Crea las tablas, funciones, procedimientos almacenados y triggers de auditoría, y carga los datos de sistema (estados, tipos de pesada, unidades de medida) y los datos iniciales de roles y puntos de menú.

> **Si la base todavía no existe**, al probar la conexión Visual Studio puede indicar que "la base de datos especificada no existe". En ese cuadro, ingresar `master` en *Nombre de la base de datos* **solo para conectarse**: la base de destino sigue siendo `BalanzaCILP`. Las tablas no deben publicarse dentro de `master`.

> **Atención:** el perfil de publicación tiene activada la opción `DropObjectsNotInSource`. Al volver a publicar se eliminan de la base los objetos que no estén en el proyecto. Todo objeto que deba conservarse tiene que agregarse al proyecto, no directamente a la base.

Con LocalDB, si la instancia está detenida:

```
sqllocaldb start MSSQLLocalDB
```

## 2. Cargar los datos de demostración

El script `seed_demo.sql` **no se ejecuta al publicar**; se corre manualmente. Carga vehículos, conductores, empresas, productos, básculas, tanques, puestos, dispositivos, órdenes de transporte con pesadas recientes y dos usuarios de prueba. Todos los datos son ficticios.

Desde la raíz del repositorio, con SQL Server Express:

```
sqlcmd -S .\SQLEXPRESS -E -d BalanzaCILP -f 65001 -b -i Database\Balanza.CILP.DataBase\scripts\testdata\seed_demo.sql
```

Con LocalDB (el servidor va entre comillas):

```
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -d BalanzaCILP -f 65001 -b -i Database\Balanza.CILP.DataBase\scripts\testdata\seed_demo.sql
```

El parámetro `-f 65001` es necesario para que los acentos se carguen correctamente. Si aparece un error de certificado con el driver ODBC 17, agregar `-C`. Al terminar correctamente, el script imprime `seed_demo OK`.

Es idempotente: se puede ejecutar varias veces sin duplicar datos. Las fechas de las pesadas se calculan respecto del día de ejecución, para que el Panel de Control muestre actividad reciente.

### Usuarios de demostración

Contraseña de ambos: `Demo1234!`

| Usuario | Roles | Pantallas |
|---|---|---|
| `YPF\ADMIN` | Los 8 roles | Todas |
| `YPF\OPERADOR` | Conductor Transporte y Gerente de Puesto de Básculas | Inicio, Gestión de Pesadas y Reportes |

> Son solo para demostración; en un entorno real deben eliminarse o cambiarse sus contraseñas.

### Restablecer la demostración

Al probar el sistema (pesar, crear o cancelar órdenes) se consumen las órdenes pendientes. El script `reset_demo.sql` las repone. Tampoco se ejecuta al publicar y debe correrse **desde la raíz del repositorio**, porque incluye `seed_demo.sql` con una ruta relativa a esa carpeta:

```
sqlcmd -S .\SQLEXPRESS -E -d BalanzaCILP -f 65001 -b -i Database\Balanza.CILP.DataBase\scripts\testdata\reset_demo.sql
```

Con LocalDB, reemplazar el servidor por `"(localdb)\MSSQLLocalDB"`. Al terminar correctamente imprime `reset_demo OK`.

El script:

- Borra **todas** las órdenes de transporte con sus pesadas y planificaciones, salvo las 5 órdenes ya ejecutadas que carga el seed (el historial que usan los reportes). Esto incluye las órdenes creadas desde la aplicación.
- Devuelve a los tanques el volumen que reservaban las autorizaciones de descarga borradas.
- Vuelve a ejecutar `seed_demo.sql`, que deja tres camiones con órdenes pendientes y fecha del día:

| Patente | Orden | Pesadas |
|---|---|---|
| `AF789HJ` | Autorización de descarga (crudo) | BRUTO registrada, falta la TARA |
| `AD345MN` | Orden de entrega (gasoil) | Ninguna |
| `AE123BC` | Orden de entrega (nafta) | Ninguna |

No modifica los maestros (vehículos, conductores, básculas, tanques, usuarios, etc.) y puede ejecutarse las veces que sea necesario.

## 3. Configurar y ejecutar el backend

La cadena de conexión **no está en el repositorio**: se configura con *user-secrets* de .NET. Desde la carpeta `Backend/`:

```
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=BalanzaCILP;Trusted_Connection=True;TrustServerCertificate=True"
```

Con LocalDB:

```
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=BalanzaCILP;Trusted_Connection=True;TrustServerCertificate=True"
```

El valor de `Database=` debe coincidir con el nombre usado al publicar.

La **clave de firma de los tokens** (JWT) tampoco está en el repositorio. Cada entorno genera la suya, de al menos 32 caracteres, y la guarda en *user-secrets*. Desde PowerShell (5.1 o 7), en `Backend/`:

```powershell
$b = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); dotnet user-secrets set "Jwt:Key" ([Convert]::ToBase64String($b))
```

Luego, desde `Backend/`:

```
dotnet run --launch-profile http
```

La API queda disponible en `http://localhost:5033`. En modo desarrollo expone la especificación OpenAPI. La primera compilación puede demorar un par de minutos.

Todos los endpoints exigen sesión iniciada: `http://localhost:5033/api/Tanques` abierto en el navegador debe responder **401**, lo que confirma que la API está en ejecución. La conexión con la base se comprueba iniciando sesión desde el frontend con un usuario de demostración.

> Debe usarse el perfil `http`. El frontend apunta a `http://localhost:5033`, y con el perfil `https` la redirección a HTTPS hace que el navegador bloquee las peticiones (CORS).

> El backend solo **mapea** las tablas existentes. No se usan migrations de Entity Framework: todo cambio de estructura se hace en el proyecto `Database/`.

## 4. Ejecutar el frontend

Desde la carpeta `Frontend/`:

```
npm install
npm run dev
```

La aplicación queda disponible en `http://localhost:5173`.

Antes de `npm run dev` y `npm run build` se ejecuta `scripts/sync-docs.mjs` (scripts `predev` y `prebuild`), que copia `Docs/Manual_Usuario_CILP.md` y `Docs/Cronograma_Capacitacion.md` a `Frontend/src/content/`. Son los textos que muestra la pantalla **Ayuda y Capacitación**. Para cambiar el manual o el cronograma se edita el archivo de `Docs/`; la carpeta `src/content/` es generada y no se versiona.

El frontend se comunica con la API en `http://localhost:5033/api` (definido en `Frontend/src/api/agent.js`), y el backend acepta peticiones únicamente desde `http://localhost:5173` (CORS en `Backend/Program.cs`). Si se cambia alguno de los puertos, deben actualizarse ambos lugares.

## Inicio de sesión y permisos

- **Login:** `POST /api/auth/login` con ID de red y contraseña. Devuelve un token JWT (vigencia de 8 horas, `Jwt:HorasVigencia`) con el usuario, sus roles y sus puntos de menú. El frontend lo guarda en `sessionStorage` (se borra al cerrar la pestaña), lo envía en cada petición y vuelve al login si la API responde 401.
- **Doble factor (2FA):** TOTP según RFC 6238 (6 dígitos, paso de 30 s), compatible con Google Authenticator, Microsoft Authenticator y Authy. Los usuarios con alguno de los roles de `Auth:DobleFactor:RolesObligatorios` (en `appsettings.json`: Administrador de Sistema, de Negocio, de Recepción y de Despacho) deben usarlo, y quien ya lo configuró lo usa siempre. Con `Auth:DobleFactor:Habilitado = false` se desactiva. Con contraseña correcta, el login no devuelve la sesión sino un desafío de 5 minutos (otra clave y otra audiencia: el resto de la API no lo acepta) que solo sirve para `POST /api/auth/2fa/enrolar/iniciar`, `/2fa/enrolar/confirmar` y `/2fa/verificar`. El secreto se guarda cifrado (AES-GCM, clave derivada de `Jwt:Key`) en `t_Usuario`, un código no puede usarse dos veces y tras 5 intentos fallidos el usuario queda bloqueado 5 minutos. El Administrador de Sistema puede restablecerlo desde la pantalla Usuarios (`POST /api/usuarios/{id}/reiniciar-2fa`). Requiere publicar la versión actual del proyecto de base de datos, que agrega tres columnas a `t_Usuario`.
- **Puntos de menú:** hay uno por pantalla (`t_CasoDeUso`). Cada rol tiene puntos de menú asignados y un usuario ve las pantallas de todos sus roles. Inicio es visible para cualquier usuario con sesión iniciada. Planificación Logística tiene dos puntos: Autorizaciones de Descarga y Órdenes de Entrega (una pestaña cada uno).
- **API:** todos los endpoints exigen sesión iniciada, salvo el login. Cada controller exige su punto de menú (atributo `[Permiso]`; sin el punto responde 403). Las consultas de maestros, el Panel de Control y las pesadas del panel quedan abiertas a cualquier usuario con sesión porque alimentan otras pantallas.
- **Auditoría:** cada conexión a la base registra al usuario de la aplicación (`dbo.pr_context_save`), de modo que `Audit_Insert_User` y `Audit_Update_User` guardan su ID de red. Los scripts ejecutados con `sqlcmd` quedan a nombre del usuario de Windows.
- **Asignación inicial:** según la tabla de actores de la especificación. Administrador de Sistema → Puntos de Menú, Roles, Usuarios, Básculas y Dispositivos; Administrador de Negocio → Conductores, Vehículos, Productos, Razones Sociales, Puestos y Tanques; Administrador de Recepción → Autorizaciones de Descarga; Planificador de Descarga y Planificador de Carga → Órdenes de Transporte; Administrador de Despacho → Órdenes de Entrega; Conductor Transporte → Gestión de Pesadas; Gerente de Puesto de Básculas → Reportes.
- **Script frente a pantalla:** la publicación de la base solo **inserta lo que falta** (puntos de menú, los 8 roles base y la asignación inicial). Los nombres, las bajas, los roles nuevos y las asignaciones se administran desde las pantallas Puntos de Menú, Roles y Permisos y Usuarios, y una nueva publicación no los sobrescribe.
- **Protecciones:** el rol Administrador de Sistema no se puede eliminar ni perder los puntos Roles y Permisos y Usuarios; esos dos puntos no se pueden dar de baja; no se puede quitar ese rol al último usuario que lo tiene, y ningún usuario puede darse de baja a sí mismo.

## Pruebas automáticas

El proyecto `Backend.Tests` (xUnit, 34 pruebas unitarias) prueba el doble factor: el algoritmo TOTP contra los vectores oficiales del RFC 6238, la codificación Base32 contra el ejemplo del RFC 4648, el rechazo de códigos reutilizados, vencidos o mal formados, y el cifrado del secreto:

```
dotnet test Backend.Tests
```

El proyecto `Backend.IntegrationTests` (xUnit + `WebApplicationFactory<Program>`, 111 pruebas) levanta la API real y la consulta por HTTP contra la base de demostración (seguridad, doble factor de punta a punta, báscula, planificación, maestros y reportes). **Atención:** ejecuta `reset_demo.sql` antes de cada clase, por lo que debe apuntar únicamente a la base de demostración:

```powershell
$env:CILP_TEST_CONNECTION = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"
dotnet test Backend.IntegrationTests
```

Las pruebas funcionales manuales desde la interfaz están descritas en `Docs/Plan_de_Pruebas.md`.

## Limitaciones conocidas

- **Permisos en el token:** los permisos viajan dentro del JWT. Un cambio de roles, de los puntos de menú de un rol o la baja de un usuario se aplica cuando el usuario vuelve a iniciar sesión (como máximo, al vencer el token a las 8 horas).
- **Doble factor de autenticación (2FA):** no hay códigos de recuperación (si se pierde el teléfono, el Administrador de Sistema lo restablece desde Usuarios). Los intentos fallidos se cuentan en memoria y se reinician con el backend. El secreto se cifra con una clave derivada de `Jwt:Key`: si esa clave cambia, los secretos guardados dejan de poder leerse y cada usuario debe configurar su autenticador de nuevo. Quien aún no lo configuró lo hace con solo su contraseña en el primer ingreso, por lo que conviene que los administradores lo configuren apenas se instala el sistema.
- **Hardware simulado:** básculas y cámaras OCR se simulan con `SimulatedDeviceConnector` (`Hardware:Modo = "Simulado"`). La lectura de báscula se genera a partir de la orden y la cámara "detecta" un camión con orden pendiente. Para equipos reales se implementa otro conector con la interfaz `IDeviceConnector`.
- **Volumen de los tanques:** representa reservas, no el stock real. Al cerrarse una descarga queda reservada la cantidad planificada (no el neto pesado), y las órdenes de entrega no descuentan volumen del tanque de origen.
- **Órdenes de entrega:** tienen un solo detalle y una sola orden de transporte.
- **Unicidad de documentos:** no se controla que el número de documento de los conductores ni el CUIT de las razones sociales sean únicos.
- **Ocupación por báscula (CU083):** mide la frecuencia de uso (cantidad de pesadas), no el tiempo de ocupación.

## Solución de problemas

**El backend arranca con el error "Falta ConnectionStrings:DefaultConnection".**
No se configuró el secreto. Ejecutar el comando `dotnet user-secrets set "ConnectionStrings:DefaultConnection" ...` del paso 3 desde la carpeta `Backend/`.

**El backend arranca con el error "Falta Jwt:Key".**
No se configuró la clave de firma de los tokens. Ejecutar el comando `dotnet user-secrets set "Jwt:Key" ...` del paso 3 desde la carpeta `Backend/`.

**El login indica "El usuario no tiene roles asignados".**
El usuario existe pero no tiene roles activos. Un administrador puede asignarlos desde la pantalla Usuarios; para los usuarios de demostración, ejecutar `seed_demo.sql`.

**El backend no se conecta a la base ("Cannot open database" o "Invalid object name").**
Verificar que la instancia de SQL Server esté en ejecución y que el `Server` de la cadena de conexión coincida con la instancia, y que `Database=` sea la base en la que se publicó el proyecto. Si las tablas quedaron en `master` y la cadena apunta a `BalanzaCILP` (o al revés), la API falla en todas las pantallas. Para ver las bases existentes:

```
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "SELECT name FROM sys.databases"
```

**`sqlcmd` indica "Error Locating Server/Instance Specified".**
El servidor indicado no existe en el equipo. Si solo está instalado LocalDB, usar `-S "(localdb)\MSSQLLocalDB"` en lugar de `.\SQLEXPRESS`.

**`dotnet run` permanece mucho tiempo en "Compilando...".**
Cancelar con `Ctrl+C`, ejecutar `dotnet build-server shutdown` y luego `dotnet run --no-build --launch-profile http`. Si el repositorio está dentro de OneDrive, moverlo a otra carpeta (por ejemplo `C:\dev`): OneDrive puede bloquear los archivos de `bin` y `obj`.

**El frontend carga pero las listas aparecen vacías o hay errores de red.**
Confirmar que el backend esté en ejecución en el puerto 5033 con el perfil `http`, y que el frontend corra exactamente en `http://localhost:5173`, porque el backend rechaza otros orígenes (CORS).

**Las listas están vacías aunque la conexión funciona.**
Faltan los datos de demostración (paso 2), o crear los registros desde la sección Administración.

**La lectura de báscula indica que no está operativa.**
El simulador marca una báscula como no operativa cuando tiene el certificado vencido. Usar otra báscula, actualizar el vencimiento desde Administración → Básculas o cargar la pesada con Ingreso Manual.