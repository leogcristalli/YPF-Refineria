# Plan de pruebas - Sistema de Gestión de Pesajes (CILP)

## 1. Objetivo y alcance

Verificar que el sistema cumple las reglas de negocio de la especificación (`Docs/CUs.md`) y las condiciones de seguridad, antes de la puesta en marcha. Cubre los módulos de acceso y seguridad, pesadas, planificación logística, maestros, reportes y ayuda. El hardware (básculas y cámaras) se prueba a través del Device Connector simulado.

## 2. Estrategia

| Nivel | Qué verifica | Cómo |
|---|---|---|
| **Unitarias automáticas** | Lógica del doble factor: algoritmo TOTP, Base32, rechazo de códigos reutilizados o vencidos y cifrado del secreto | Proyecto `Backend.Tests` (xUnit): `dotnet test Backend.Tests` |
| **Funcionales manuales** | Reglas de negocio de cada módulo, de punta a punta, desde la interfaz | Casos de la sección 5, sobre los datos de demostración |
| **Seguridad** | Autenticación, doble factor, permisos por rol y aislamiento de los tokens | Casos PS de la sección 4 |
| **Aceptación** | Que cada perfil resuelva su caso práctico con el manual | Evaluación final de cada sesión del cronograma de capacitación |

## 3. Entorno y datos

- Base de datos publicada desde `Database/` con los datos de demostración (`seed_demo.sql`). `reset_demo.sql` devuelve la base al estado inicial antes de cada ronda de pruebas.
- Backend con `dotnet run --launch-profile http` y frontend con `npm run dev`.
- Usuarios de demostración (contraseña `Demo1234!`): `YPF\ADMIN` (Administrador de Sistema, con doble factor obligatorio) y `YPF\OPERADOR` (Gerente de Puesto de Básculas).
- Un teléfono con una aplicación autenticadora para los casos de doble factor.

## 4. Pruebas automáticas

| Clase | Qué comprueba |
|---|---|
| `TotpTests` | Los 6 vectores oficiales del RFC 6238; aceptación del paso actual, anterior y siguiente; rechazo fuera de la ventana, de códigos incorrectos, mal formados y reutilizados; admisión de espacios; Base32 contra el ejemplo del RFC 4648; generación de secretos; dirección `otpauth` |
| `ProtectorTotpTests` | Cifrado y descifrado del secreto; que no quede en claro; resultados distintos por cada cifrado; rechazo con otra clave, con valores alterados o inexistentes; error si falta `Jwt:Key` |

## 5. Casos de prueba funcionales y de seguridad

Las columnas **Resultado obtenido** y **Estado** se completan al ejecutar cada caso.

### Seguridad y acceso (PS)

| ID | Caso | Pasos | Resultado esperado | Resultado obtenido | Estado |
|---|---|---|---|---|---|
| PS-01 | Ingreso correcto sin 2FA | Ingresar con `YPF\OPERADOR` y su contraseña | Entra al sistema y el menú muestra solo Inicio, Reportes y Ayuda | | |
| PS-02 | Credenciales incorrectas | Ingresar con contraseña errada, y con un usuario inexistente | Mismo mensaje en ambos casos: "Usuario o contraseña incorrectos" | | |
| PS-03 | Usuario sin roles | Crear un usuario sin roles e ingresar | El sistema indica que consulte al administrador y no inicia la sesión | | |
| PS-04 | Pantalla sin permiso | Con `YPF\OPERADOR`, escribir en el navegador la dirección de Usuarios | "No tiene permiso para ver esta pantalla"; la API responde 403 | | |
| PS-05 | API sin sesión | Pedir `GET /api/Tanques` sin token | Responde 401 | | |
| PS-06 | Alta del doble factor | Ingresar con `YPF\ADMIN`, escanear el QR y confirmar con un código | Entra al sistema; la lista de Usuarios muestra el 2FA activo | | |
| PS-07 | Ingreso con doble factor | Cerrar sesión e ingresar de nuevo con `YPF\ADMIN` | Tras la contraseña pide el código; con el código correcto entra | | |
| PS-08 | Código incorrecto y bloqueo | Escribir 5 códigos incorrectos seguidos | Cada uno se rechaza; al quinto el ingreso queda bloqueado 5 minutos (429) | | |
| PS-09 | Código reutilizado | Ingresar con un código y volver a usar ese mismo código en otro ingreso | El segundo uso se rechaza | | |
| PS-10 | Desafío vencido | Dejar pasar más de 5 minutos entre la contraseña y el código | Pide volver a iniciar sesión | | |
| PS-11 | Restablecer el 2FA | Con `YPF\ADMIN`, restablecer el doble factor de otro usuario obligado | La persona configura su autenticador en el próximo ingreso | | |
| PS-12 | El desafío no es una sesión | Usar el token de desafío como `Bearer` en `GET /api/Tanques` | Responde 401 | | |

### Pesadas (PF)

| ID | Caso | Pasos | Resultado esperado | Resultado obtenido | Estado |
|---|---|---|---|---|---|
| PF-01 | Descarga completa | Registrar las dos pesadas de una Autorización de Descarga | La primera es Bruto y la segunda Tara; se calcula el neto y la orden se cierra | | |
| PF-02 | Entrega completa | Registrar las dos pesadas de una Orden de Entrega | La primera es Tara y la segunda Bruto; se calcula el neto y la orden se cierra | | |
| PF-03 | Fuera de tolerancia | Registrar un peso cuyo neto se desvíe de lo planificado más que la tolerancia del producto | La pesada se rechaza con el desvío informado y no se guarda | | |
| PF-04 | Bruto menor que Tara | Registrar la segunda pesada con un valor inconsistente | Se rechaza: el Bruto debe ser mayor que la Tara | | |
| PF-05 | Orden ya cerrada | Intentar una tercera pesada sobre una orden completa | Se rechaza | | |
| PF-06 | Pesada automática alterada | Marcar la pesada como automática pero escribir un peso distinto de la lectura de la báscula | Se rechaza: el peso no coincide con una lectura reciente | | |
| PF-07 | Ingreso manual sin motivo | Marcar ingreso manual y dejar el motivo vacío | Se rechaza hasta completar el motivo | | |
| PF-08 | Ticket | Abrir el ticket de una orden completa e imprimirlo | Muestra las dos pesadas, el neto y lo planificado | | |

### Planificación logística (PL)

| ID | Caso | Pasos | Resultado esperado | Resultado obtenido | Estado |
|---|---|---|---|---|---|
| PL-01 | Capacidad insuficiente | Crear una descarga con una cantidad que no entra en el tanque elegido | Se rechaza e informa la capacidad disponible | | |
| PL-02 | Cancelar una descarga abierta | Cancelar una orden sin pesadas | Exige un motivo y libera el volumen reservado en el tanque | | |
| PL-03 | Modificar una orden con pesadas | Intentar modificar o cancelar una orden que ya tiene pesadas | No se permite | | |

### Maestros (PM)

| ID | Caso | Pasos | Resultado esperado | Resultado obtenido | Estado |
|---|---|---|---|---|---|
| PM-01 | Tanque repetido | Crear un tanque con el código de otro activo | Se rechaza | | |
| PM-02 | Tanque con volumen | Reducir el volumen máximo por debajo del ocupado, y dar de baja un tanque con volumen | Ambas operaciones se rechazan | | |
| PM-03 | Dispositivo con IP inválida o repetida | Crear un dispositivo con una IP mal formada, y otro con la IP de uno existente | Ambos se rechazan | | |
| PM-04 | Puesto con órdenes | Dar de baja un puesto que tiene órdenes asociadas | Se rechaza | | |
| PM-05 | Báscula | Cargar una báscula con certificado vencido, y otra con una IP repetida | Ambas se rechazan | | |
| PM-06 | Protecciones de usuarios | Darse de baja a sí mismo, y quitar el rol Administrador de Sistema al último usuario que lo tiene | Ambas operaciones se rechazan | | |

### Reportes y ayuda (PR)

| ID | Caso | Pasos | Resultado esperado | Resultado obtenido | Estado |
|---|---|---|---|---|---|
| PR-01 | Rango de fechas inválido | Pedir el reporte por período con Desde posterior a Hasta | Se rechaza | | |
| PR-02 | Movimientos por período | Filtrar un período con órdenes completadas | Incluye solo órdenes completadas y los totales coinciden con las pesadas de demostración | | |
| PR-03 | Panel de Control | Abrir Inicio después de registrar una pesada | Los indicadores del día reflejan la pesada | | |
| PR-04 | Ayuda y Capacitación | Abrir Ayuda y Capacitación con cualquier usuario | Muestra el manual y el cronograma, cada uno con su índice lateral | | |

## 6. Registro de ejecución

| Fecha | Versión probada (commit) | Responsable | Pruebas automáticas | Casos manuales aprobados | Observaciones |
|---|---|---|---|---|---|
| | | | | | |