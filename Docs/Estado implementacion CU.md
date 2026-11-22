# Estado de Implementación de los Casos de Uso

Este documento compara los casos de uso especificados (según el documento de Entrega 3 y `CUs.md`) con lo que está implementado en el sistema.

**Referencias de estado**

| Estado | Significado |
|---|---|
| Implementado | El caso de uso está disponible en la aplicación y cumple sus reglas principales. |
| Parcial | El caso de uso tiene pantalla y backend, pero falta parte de su lógica; el detalle indica qué. |
| Pendiente | No está disponible en la aplicación. |

**Fecha de corte:** 21/11/2026, después de la revisión de los PR #12, #14 y #15 (issue #17) y del inicio de sesión con roles (issue #19). Actualizar la tabla antes de la entrega final.

## 1. Resumen

El documento de Entrega 3 define **78 casos de uso** (CU01 a CU24 y CU030 a CU083; la numeración no incluye CU25 a CU29).

| Estado | Casos de uso | Cantidad |
|---|---|---|
| Implementado | CU01-CU24, CU030-CU059, CU077-CU079, CU081-CU083 | 60 |
| Parcial | CU060-CU076, CU080 | 18 |
| Pendiente | — | 0 |

Todos los casos de uso tienen pantalla y backend. Los parciales tienen huecos de lógica que se detallan abajo, casi todos en la planificación logística (CU060-CU076).

> El conteo por estado debe recalcularse cuando cambie la tabla de la sección 2.

## 2. Detalle por módulo

### Módulo de Maestros

| CU | Nombre | Estado | Observaciones |
|---|---|---|---|
| CU01-CU04 | Puntos de Menú (consultar, ver, modificar, eliminar) | Implementado | `PuntosMenuController` y pantalla `PuntosMenuPage` sobre `t_CasoDeUso`: un punto de menú por pantalla (16), nombre obligatorio y único (CU03) y baja lógica. El post-deploy solo inserta los puntos que faltan, así que los cambios hechos desde la pantalla se conservan. Los puntos Roles y Permisos y Usuarios no se pueden dar de baja. |
| CU05-CU09 | Roles (consultar, crear, ver, modificar, eliminar) | Implementado | `RolesController` y pantalla `RolesPage` sobre `t_Rol` y `t_RolCasoDeUso`: descripción obligatoria de 3 a 50 caracteres y única, puntos de menú existentes y activos, y no se puede eliminar un rol con usuarios. La asignación inicial sigue la tabla de actores de la Entrega 3 y el post-deploy solo inserta lo que falta. El rol Administrador de Sistema no se puede eliminar ni perder los puntos Roles y Permisos y Usuarios. |
| CU10-CU14 | Usuarios y autenticación | Implementado | Alta, consulta, modificación y baja lógica con validación de los datos editables (el backend no acepta id, estado ni hash). Contraseñas con `PasswordHasher`, ID de red único (índice `UX_Usuario_IdRed`) y asignación de roles desde la pantalla (`/api/Usuarios/{id}/roles`). Inicio de sesión con JWT (`/api/auth/login`). No se puede quitar el rol Administrador de Sistema al último usuario que lo tiene, ni darse de baja a uno mismo. |
| CU15-CU19 | Conductores | Implementado | Revisión superficial. |
| CU20-CU24 | Vehículos | Implementado | Reglas de CU021 y CU023: campos obligatorios, pesos y volumen mayores a cero, peso máximo mayor al peso sin carga, patente de chasis única entre vehículos activos y razón social existente y activa. |
| CU030-CU034 | Productos | Implementado | Revisión superficial. |
| CU035-CU039 | Razones Sociales | Implementado | Revisión superficial. |
| CU040-CU044 | Dispositivos de Detección de Vehículo | Implementado | Alta, consulta, modificación y baja lógica (`DispositivosDetectoresController`, pantalla `DispositivosPage`). IP obligatoria con formato IPv4 y única entre dispositivos activos (CU042, CU043). Descripción de hasta 50 caracteres. |
| CU045-CU049 | Puntos de Expedición y Recepción | Implementado | Alta, consulta, modificación y baja lógica (`PuestosController`, pantalla `PuestosPage`). Descripción obligatoria de hasta 50 caracteres. No se puede eliminar un puesto con órdenes asociadas. Las órdenes todavía no permiten elegir el puesto (ver CU060-CU076). |
| CU050-CU054 | Básculas | Implementado | Vencimiento del certificado posterior a la fecha actual, IP IPv4 única entre básculas activas y combinación de lote, serie y certificado única (CU052, CU053). Baja lógica para conservar el historial de pesadas. Una báscula con el certificado vencido no puede usarse para pesar. |
| CU055-CU059 | Tanques de Almacenamiento | Implementado | Alta, consulta, modificación y baja lógica (`TanquesController`, pantalla `TanquesPage`). Código alfanumérico único de hasta 20 caracteres, descripción de 2 a 50, volumen máximo mayor a cero; no se puede reducir por debajo del ocupado ni dar de baja un tanque con volumen. **Limitación:** el volumen ocupado solo aumenta: las autorizaciones de descarga reservan la cantidad planificada (no el neto real) y las órdenes de entrega no descuentan volumen del tanque de origen. |

### Módulo de Gestión (Planificación Logística)

Autorización de Descarga, Orden de Entrega y Orden de Transporte usan el mismo endpoint (`OrdenesTransporteController`) y el mismo formulario, diferenciados por el tipo de orden. Cada alta crea siempre una orden con un detalle, una orden de transporte y una planificación (1 : 1 : 1 : 1).

| CU | Nombre | Estado | Observaciones |
|---|---|---|---|
| CU060-CU065 | Autorización de Descarga: consultar, crear, ver, modificar, cancelar, eliminar | Parcial | Tab de descargas en `AutorizacionesPage`, con tanque de destino y validación de capacidad (CU061, convirtiendo kg a litros con la densidad), cancelación con motivo que libera el volumen reservado y eliminación en cascada. **Falta:** elegir el centro de recepción (CU061 lo pide; hoy se asigna el primer puesto activo); el estado de la orden queda siempre en Borrador (cancelar no la pasa a Anulado); modificar no recalcula la reserva del tanque ni vuelve a validar su capacidad. |
| CU066-CU071 | Orden de Entrega: consultar, crear, ver, modificar, cancelar, eliminar | Parcial | Tab de despachos en `AutorizacionesPage` con alta, detalle, modificación, cancelación y eliminación. **Falta:** una orden de entrega con varios detalles u órdenes de transporte; elegir el puesto de expedición; estados de la orden (igual que CU060-CU065). |
| CU072-CU076 | Orden de Transporte | Parcial | Alta, consulta, detalle, modificación y baja (`OrdenesTransportePage`). La baja de una OT con pesadas devuelve 409 con mensaje claro. **Falta:** la modificación no valida el estado (permite editar una OT ejecutada o cancelada) ni los datos de entrada; no hay replanificación (el estado Replanificado y el motivo de replanificación nunca se cargan). |

### Módulo de Operaciones

| CU | Nombre | Estado | Observaciones |
|---|---|---|---|
| CU077 | Consultar Orden de Transporte asignada a un vehículo | Implementado | `GET /api/OrdenesTransporte/ByPatente/{patente}` devuelve solo OT abiertas con pesadas pendientes: primero la que ya está en ejecución y después la de fecha planificada más antigua. "Detectar con cámara" (Device Connector simulado) muestra la OT que eligió la cámara. |
| CU078 | Registrar Pesada de Transporte | Implementado | Valida orden abierta y pesajes pendientes, determina TARA o BRUTO según el tipo de orden, aplica la tolerancia del producto, calcula el neto y cierra la orden (estado Ejecutado, `Activo = 0` y fecha de cierre). El modal lista solo órdenes pendientes. Una pesada es automática solo si el peso es exactamente la lectura reciente de la báscula; si se tipea o se edita, queda como ingreso manual con motivo obligatorio (validado en frontend y backend). La báscula debe existir, estar activa y tener el certificado vigente; el ingreso manual puede registrarse sin báscula. Índice único `(Id_Planificacion, Id_TipoPesada)` contra pesadas duplicadas. |
| CU079 | Emitir Ticket de Pesada | Implementado | `GET /api/Pesadas/Ticket/{idPlanificacion}` y modal de comprobante con la báscula de cada pesada ("Ingreso manual" si no tiene) y el motivo de los ingresos manuales. La impresión saca solo el ticket. |

### Módulo de Reportes

| CU | Nombre | Estado | Observaciones |
|---|---|---|---|
| CU080 | Reporte de Movimientos por Periodos | Parcial | Solo órdenes en estado Ejecutado (RN01), Desde menor o igual a Hasta (RN02), filtros por fecha de fin de ejecución y tipo de movimiento, tarjetas de totales y tabla. **Falta:** el filtro Replanificado "Sí" siempre devuelve vacío porque el sistema no registra replanificaciones (ver CU072-CU076). |
| CU081 | Reporte de Movimientos por Productos | Implementado | Agrupación por producto con cantidad de OT, total planificado contra neto y promedio por orden. |
| CU082 | Reporte de Duración Promedio de Orden de Transporte | Implementado | Tiempo entre la primera y la última pesada de cada OT, con distribución por rangos y detalle por orden. |
| CU083 | Reporte de Frecuencia de Ocupación por Báscula | Implementado | Cantidad y porcentaje de pesadas por báscula (incluye los ingresos manuales sin báscula). Mide frecuencia de uso, no tiempo de ocupación. |

## 3. Funcionalidades transversales

| Funcionalidad | Estado | Observaciones |
|---|---|---|
| Autenticación (inicio de sesión) | Implementado | `POST /api/auth/login` valida ID de red y contraseña y devuelve un JWT de 8 horas con el usuario, sus roles y sus puntos de menú. Clave de firma en user-secrets. El frontend guarda la sesión en `sessionStorage`, manda el token en cada request y vuelve al login ante un 401. El menú lateral muestra el usuario logueado y permite cerrar sesión. |
| Control de acceso por roles (RBAC) | Implementado | Todos los endpoints exigen usuario logueado (salvo el login) y cada controller exige su punto de menú (`[Permiso]`, 403 si falta). El menú y las rutas muestran solo las pantallas permitidas; en Planificación, cada pestaña según su punto. **Limitación:** los permisos viajan en el token, así que un cambio de roles o una baja se aplica al volver a iniciar sesión. |
| Doble factor de autenticación (2FA) | Pendiente | Fuera del alcance de esta versión. |
| Auditoría por usuario de la aplicación | Implementado | Un interceptor de EF ejecuta `dbo.pr_context_save` con el ID de red del usuario logueado en cada conexión: `Audit_Insert_User` y `Audit_Update_User` registran al usuario de la aplicación. |
| Panel de control (Dashboard) | Implementado | Pesadas del día, OT activas, vehículos, conductores y tonelaje neto de las OT cerradas (hoy y total), más el estado de las básculas y cámaras. |
| Integración con básculas y cámaras (Device Connector, OCR) | Simulado | `IDeviceConnector` con una implementación simulada (`SimulatedDeviceConnector`, `Hardware:Modo = "Simulado"`): la lectura de la báscula se genera a partir de la orden y la cámara "detecta" un camión con orden pendiente. Para hardware real hay que escribir otro conector que implemente la misma interfaz. |
