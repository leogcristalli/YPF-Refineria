# Manual de Usuario - Sistema de Gestión de Pesajes (CILP)

Bienvenido al sistema **CILP**. Este manual le guiará paso a paso para utilizar el sistema de manera sencilla y eficiente.

---

## Iniciar sesión

Al abrir la aplicación se muestra la pantalla de inicio de sesión.

1. Escriba su ID de red (ej: `YPF\USUARIO`). No distingue mayúsculas de minúsculas.
2. Escriba su contraseña.
3. Haga clic en **Ingresar**.

Si los datos no son correctos, la pantalla muestra `"Usuario o contraseña incorrectos"`. Si el usuario existe pero no tiene roles asignados, el sistema indica que consulte al administrador.

Si antes de iniciar sesión intentó abrir una pantalla, al ingresar el sistema lo lleva a esa pantalla, siempre que sus roles la permitan; si no, lo lleva a **Inicio**.

## Verificación en dos pasos (2FA)

Los usuarios con roles administrativos (Administrador de Sistema, de Negocio, de Recepción y de Despacho) deben usar un segundo factor: un código de 6 dígitos que genera una aplicación autenticadora en su teléfono (Google Authenticator, Microsoft Authenticator o Authy). Quien ya configuró su autenticador lo usa siempre, cualquiera sea su rol.

### Primera vez (configuración)

1. Escriba su ID de red y su contraseña y haga clic en **Ingresar**.
2. Aparece la pantalla **Configurar doble factor** con un código QR. Instale una aplicación autenticadora en su teléfono, agregue una cuenta y escanee el código QR. Si no puede escanearlo, escriba a mano la clave que se muestra debajo.
3. Escriba el código de 6 dígitos que muestra la aplicación y haga clic en **Verificar e ingresar**.

### Los ingresos siguientes

Después de la contraseña aparece **Verificación en dos pasos**. Escriba el código de 6 dígitos de la cuenta **CILP Refinería** y haga clic en **Verificar e ingresar**.

- Cada código vale 30 segundos y solo se puede usar una vez: si acaba de usarlo, espere al siguiente.
- Tiene 5 minutos, desde que escribió la contraseña, para completar la verificación. Si vence, use **Volver al inicio de sesión** y empiece de nuevo.
- Después de 5 códigos incorrectos, el ingreso se bloquea durante 5 minutos.
- Si perdió o cambió su teléfono, pídale al Administrador de Sistema que restablezca su doble factor (ver la sección *Usuarios*).

## Duración de la sesión

- La sesión dura como máximo 8 horas (un turno). Al vencer, el sistema vuelve a la pantalla de inicio de sesión con el aviso `"La sesión venció"`.
- La sesión se guarda solo en la pestaña del navegador: al cerrar la pestaña, la sesión se cierra. Esto evita que otra persona use una sesión abierta en una PC compartida (por ejemplo, la de la báscula).
- Si un administrador cambia sus roles, los cambios se aplican la próxima vez que inicie sesión.

## Cerrar sesión

En la parte inferior del menú lateral se ven sus iniciales, su nombre y sus roles. Haga clic en el ícono de salida (a la derecha de su nombre) para cerrar la sesión y volver a la pantalla de inicio de sesión.

---

## 2. Pantalla principal, menú y permisos

El **menú lateral** muestra **solo las pantallas que permiten sus roles**. Si un usuario tiene varios roles, ve las pantallas de todos ellos. **Inicio** (Panel de Control) lo ven todos los usuarios.

Si escribe en el navegador la dirección de una pantalla que su rol no permite, el sistema muestra "No tiene permiso para ver esta pantalla".

### Qué ve cada rol

| Rol | Pantallas |
|---|---|
| Administrador de Sistema | Puntos de Menú, Roles y Permisos, Usuarios, Básculas, Dispositivos de Detección |
| Administrador de Negocio | Vehículos, Conductores, Productos, Razones Sociales, Puestos de Expedición, Tanques |
| Administrador de Recepción | Planificación Logística (pestaña de Autorizaciones de Descarga) |
| Administrador de Despacho | Planificación Logística (pestaña de Órdenes de Entrega) |
| Planificador de Descarga | Órdenes de Transporte |
| Planificador de Carga | Órdenes de Transporte |
| Conductor Transporte | Gestión de Pesadas |
| Gerente de Puesto de Básculas | Reportes |

Esta es la asignación inicial (según la especificación del proyecto). El Administrador de Sistema puede cambiarla desde **Roles y Permisos** (ver sección 6).

### Organización del menú
*   **Operaciones:** Inicio, Gestión de Pesadas, Órdenes de Transporte, Planificación Logística y Reportes.
*   **Administración:** Vehículos, Conductores, Productos, Razones Sociales, Básculas, Tanques, Puestos de Expedición, Dispositivos de Detección, Puntos de Menú, Roles y Permisos y Usuarios.

---

## 3. Panel de Control (Inicio)

La pantalla **Inicio** muestra cuatro indicadores: **Pesajes Hoy**, **Órdenes de Transporte** (activas), **Tonelaje Hoy** (peso neto de las órdenes cerradas hoy, en toneladas) y **Conductores Activos**. Debajo se ven la **Actividad de Pesaje Reciente**, el **Estado de Hardware** (básculas y cámaras) y los **Accesos Rápidos**.

---

## 4. Guía paso a paso: una operación completa

Para que un camión pueda pesarse en la planta, primero debe existir una **orden** planificada. El flujo completo es: planificar la orden, registrar las dos pesadas y emitir el ticket.

### Estados de una orden

| Momento | Estado que se muestra |
|---|---|
| Al crearla | **Planificado** |
| Al cambiarle la fecha (replanificar) | **Replanificado** |
| Después de la primera pesada | **Ejecución** |
| Después de la segunda pesada | **Definitivo (cerrada)** |
| Al cancelarla | **Anulado** |

Solo se pueden **modificar**, **replanificar** o **cancelar** las órdenes abiertas que todavía no tienen pesadas (Planificado o Replanificado). En las demás, esos botones no aparecen.

### Paso 1: Planificar la orden (Logística)

#### Desde Planificación Logística
1.  En el menú lateral, haga clic en **Planificación Logística**.
2.  Elija la pestaña (según sus roles puede ver una o las dos):
    *   **Autorizaciones (Crudo)**: recepción de material. El camión llega cargado y se descarga en un tanque.
    *   **Entregas (Refinados)**: despacho de material. El camión llega vacío y se carga.
3.  Haga clic en **Nueva Autorización** o **Nueva Entrega**.
4.  Complete el formulario:
    *   **Fecha planificada**: fecha en la que se espera al camión (no puede ser anterior a hoy).
    *   **Cantidad (KG)**: peso estimado en kilogramos (ej: 30000).
    *   **Centro de recepción** (descargas) o **Puesto de expedición** (entregas).
    *   **Nro. de remito**.
    *   **Producto / Material**.
    *   **Tanque de destino**: obligatorio en las descargas. En las entregas, el **tanque de origen** es opcional. La lista muestra la capacidad disponible de cada tanque.
    *   **Proveedor** (descargas) o **Cliente** (entregas), **Transportista**, **Vehículo** y **Conductor**.
5.  Haga clic en **Crear orden**.

> **Control de capacidad:** en una descarga, el sistema convierte los kilos a litros según la densidad del producto y verifica que el tanque tenga espacio. Si la carga no entra, lo indica en el formulario y no permite guardar. Al crear la descarga, ese volumen queda **reservado** en el tanque.

Si algún dato no es válido (por ejemplo, un vehículo dado de baja), el error se muestra **dentro del formulario**, arriba de los botones.

#### Desde Órdenes de Transporte
Los Planificadores usan la pantalla **Órdenes de Transporte**. Con el botón **+ Nuevo** se abre el mismo formulario, con un campo más al principio: **Tipo de orden** (Autorización de Descarga u Orden de Entrega). La lista muestra el tipo, la fecha y el estado de cada orden.

#### Acciones sobre una orden existente
*   **Ver detalle** (ojo): todos los datos de la orden, incluido el puesto, el tanque, el estado y, si corresponde, la replanificación o la anulación con su motivo. En Órdenes de Transporte también muestra las pesadas registradas.
*   **Modificar** (lápiz): permite corregir la cantidad, el puesto, el producto, el tanque, el cliente o proveedor, el transportista, el vehículo y el conductor. **La fecha no se modifica acá**: se cambia con *Replanificar*. Si se cambia la cantidad o el tanque de una descarga, el sistema recalcula la reserva y vuelve a controlar la capacidad.
*   **Replanificar** (calendario, en Órdenes de Transporte): cambia la fecha planificada. Pide la **nueva fecha** (no anterior a hoy y distinta de la actual) y un **motivo obligatorio**. La orden queda en estado **Replanificado** y el motivo queda registrado.
*   **Cancelar orden** (círculo tachado, en Planificación Logística): pide un **motivo obligatorio**. La orden queda **Anulada**, con la fecha y el motivo de la anulación. En una descarga, se libera el volumen reservado en el tanque.
*   **Eliminar orden** (papelera): elimina la orden, previa confirmación. No se puede eliminar una orden que ya tiene pesadas registradas.

---

### Paso 2: Registrar el pesaje (Báscula)
Esta tarea la realiza el operador de báscula cuando el camión está en la balanza.

1.  En el menú lateral, haga clic en **Gestión de Pesadas**.
2.  **Identificar el camión:**
    *   Escriba la patente en **Consultar Orden Asignada al Vehículo** y haga clic en **Buscar por Patente**, o
    *   haga clic en **Detectar con cámara**: la cámara lee la patente del camión que está en la entrada y muestra su orden.
    *   Aparecen los datos de la orden pendiente de ese camión (si ya hizo la primera pesada, se muestra esa orden). Haga clic en **Registrar Pesada para esta OT →**.
    *   Si el camión no tiene órdenes pendientes, el sistema lo indica.
3.  También puede usar **Nueva Pesada** y elegir la orden en el desplegable. Solo aparecen las órdenes **pendientes de pesaje**.
4.  En la ventana **Registrar Pesada**:
    *   Elija la **Báscula** y haga clic en **Leer de báscula**: el peso se completa solo y la pesada queda como **automática** (AUTO).
    *   Si escribe o corrige el peso a mano, la pesada pasa a ser de **ingreso manual** (la casilla se marca sola) y el **Motivo es obligatorio** (ej: "Báscula sin comunicación: lectura del visor").
    *   Si la báscula no funciona, puede elegir **Sin báscula (solo ingreso manual)** y escribir el peso y el motivo.
5.  Haga clic en **Registrar Peso**.

Si la pesada no se puede registrar (por ejemplo, fuera de tolerancia o con una báscula con el certificado vencido), el motivo se muestra **dentro de la ventana**, sin cerrarla.

**Qué pesada corresponde (la define el sistema):**

| Tipo de orden | Primera pesada | Segunda pesada |
|---|---|---|
| Autorización de Descarga (crudo) | **Bruto** (camión cargado) | **Tara** (camión vacío) |
| Orden de Entrega (refinados) | **Tara** (camión vacío) | **Bruto** (camión cargado) |

**Resultado:**
*   Tras la **primera pesada** aparece, abajo a la derecha, el aviso "✓ Pesada [tipo] registrada. Aguardando pesada [tipo]".
*   Tras la **segunda pesada**, el sistema calcula el **Peso Neto** (Bruto − Tara), **cierra la orden** (estado Definitivo) y muestra "✓ Orden completada. Peso neto: …".

**Controles del sistema:**
*   El peso debe ser mayor que cero.
*   Una pesada automática debe tener exactamente el peso que leyó la báscula; si no, debe registrarse como manual con motivo.
*   La báscula debe estar activa y con el certificado vigente.
*   La orden debe estar abierta y tener pesadas pendientes (admite solo dos).
*   El Bruto debe ser mayor que la Tara.
*   El peso neto debe estar dentro de la **tolerancia** del producto respecto de lo planificado; si no, **la pesada no se registra**.

La lista de **Gestión de Pesadas** muestra el tipo (TARA o BRUTO), producto, vehículo, peso, peso neto, **báscula** (o "Ingreso manual"), fecha y hora, y si la pesada fue **AUTO** o **MANUAL**.

---

### Paso 3: Emitir el ticket de pesada
Cuando la orden tiene las dos pesadas, en su fila aparece el botón **Ticket** (las órdenes que no se completaron muestran "En curso").

1.  Haga clic en **Ticket**.
2.  Se abre el **Comprobante Oficial de Pesada** con los datos de la orden, el remito, el producto, el vehículo, el conductor, el transportista, las dos pesadas con su hora y su báscula (o "Ingreso manual", con el motivo), y el **Peso Neto** frente a lo planificado.
3.  Use **Imprimir Comprobante**: se imprime **solo el ticket**, sin el resto de la pantalla. **Cerrar** vuelve a la lista.

Si el ticket no se puede generar, el motivo se muestra arriba de la lista de pesadas.

---

## 5. Reportes

En **Reportes** hay tres pestañas. **Actualizar** recarga los datos de la pestaña activa y **Exportar / Imprimir** imprime el reporte en pantalla.

*   **Operaciones & Básculas** (CU082 y CU083): duración promedio de las órdenes de transporte (entre la primera y la última pesada) con su distribución, y la frecuencia de uso de cada báscula.
*   **Movimientos por Período** (CU080): complete **Fecha Desde** y **Fecha Hasta** (Desde no puede ser posterior a Hasta). Opcionalmente filtre por **Tipo Movimiento** y por **Replanificado** (Todas, Sí, No; "Sí" muestra las órdenes cuya fecha se cambió con *Replanificar*). Haga clic en **Filtrar Período**.
*   **Por Producto** (CU081): con el rango de fechas, muestra la carga movilizada por cada producto.

Los reportes de movimientos incluyen solo órdenes cerradas (con sus dos pesadas). Si falta un dato o hay un error, el aviso se muestra en la misma pantalla.

---

## 6. Administración

El procedimiento es el mismo en todas las opciones:
1.  Haga clic en la opción del menú y luego en **+ Nuevo**.
2.  Complete el formulario y haga clic en **Guardar**. Si hay un error, se muestra **dentro del formulario**.
3.  En la lista: el **lápiz** edita y la **papelera** da de baja. La baja es lógica: el registro deja de aparecer en las listas, pero se conserva su historial. Si no se puede dar de baja (por ejemplo, porque está en uso), el motivo se muestra arriba de la lista.

### Usuarios (Administrador de Sistema)
*   Datos: **ID de red** (único; tampoco puede repetir el de un usuario dado de baja), nombre, apellido, **tipo de documento** (DNI, CI o Pasaporte), número de documento y contraseña.
*   En el alta la contraseña es obligatoria; al editar, si se deja vacía se conserva la actual. Las contraseñas se guardan cifradas (hash).
*   **Roles asignados:** tilde los roles del usuario. Sin roles, el usuario no puede iniciar sesión. La lista muestra los roles de cada usuario.
*   Nadie puede darse de baja a sí mismo, y no se le puede quitar el rol **Administrador de Sistema** al último usuario que lo tiene.

### Roles y Permisos (Administrador de Sistema)
*   Cada rol tiene una descripción (de 3 a 50 caracteres, sin repetir) y los **puntos de menú** (pantallas) que habilita.
*   No se puede eliminar un rol que tiene usuarios asignados.
*   El rol **Administrador de Sistema** no se puede eliminar ni perder los puntos **Roles y Permisos** y **Usuarios**, para que siempre haya alguien que pueda administrar los permisos.

### Puntos de Menú (Administrador de Sistema)
*   Hay un punto de menú por pantalla. Solo se pueden consultar, ver, modificar (el nombre, que es obligatorio y único) y dar de baja; **no hay alta**, porque los define el sistema.
*   Al dar de baja un punto, se quita de los roles que lo tenían. Los puntos **Roles y Permisos** y **Usuarios** no se pueden dar de baja.

### Reglas de los demás maestros
*   **Conductores:** nombre, apellido, **tipo de documento** (DNI, CI o Pasaporte) y número de documento.
*   **Tanques:** el código es obligatorio, alfanumérico (letras, números y guiones, ej: TK-101) y único entre los tanques activos. El volumen máximo debe ser mayor a cero. El **volumen ocupado** no se edita a mano: lo actualizan las autorizaciones de descarga. No se puede reducir el volumen máximo por debajo del ocupado ni dar de baja un tanque con volumen almacenado.
*   **Básculas:** la fecha de vencimiento del certificado debe ser posterior a hoy. La IP debe ser IPv4 válida y única entre las básculas activas, y la combinación de lote, serie y certificado no puede repetirse.
*   **Vehículos:** los pesos y el volumen deben ser mayores a cero, el peso máximo debe superar al peso sin carga, la patente de chasis no puede repetirse entre vehículos activos y la razón social debe estar activa.
*   **Dispositivos de Detección:** la descripción admite hasta 50 caracteres. La IP es obligatoria, IPv4 válida y única entre los dispositivos activos.
*   **Puestos de Expedición:** la descripción es obligatoria (hasta 50 caracteres). No se puede dar de baja un puesto con órdenes asociadas.

---

## 7. Lectura de báscula y cámara

En esta versión los equipos son **simulados**, pero se usan igual que los reales:

*   **Leer de báscula:** en **Nueva Pesada**, elija la orden y la báscula y haga clic en **Leer de báscula**. Si la báscula no está operativa (por ejemplo, con el certificado vencido), use el ingreso manual con motivo.
*   **Detectar con cámara:** en **Gestión de Pesadas**, lee la patente del camión que está en la entrada y muestra la orden que le corresponde.
*   **Estado de Hardware:** el Panel de Control muestra si cada báscula y cámara está en línea.

---

## 8. Solución de problemas frecuentes

**P: El sistema me devolvió a la pantalla de inicio de sesión.**
> R: La sesión venció (dura 8 horas) o se cerró la pestaña del navegador. Vuelva a iniciar sesión.

**P: No veo una pantalla en el menú, o aparece "No tiene permiso para ver esta pantalla".**
> R: Sus roles no incluyen esa pantalla. Pida al Administrador de Sistema que le asigne el rol correspondiente y vuelva a iniciar sesión para que el cambio se aplique.

**P: El camión está en la báscula pero no aparece su orden.**
> R: La búsqueda muestra solo órdenes pendientes de pesaje. La orden puede no haberse creado (Paso 1), estar ya cerrada (tiene las dos pesadas) o estar anulada.

**P: Al registrar la pesada aparece "Peso fuera de tolerancia".**
> R: El peso neto se desvía de lo planificado más de lo que admite el producto. Verifique la lectura de la báscula. La pesada rechazada no queda registrada. Si la cantidad planificada estaba mal, una orden **en ejecución** ya no se puede modificar: consulte a Logística.

**P: Aparece "El peso no coincide con una lectura reciente de la báscula".**
> R: Se marcó como automática una pesada con un peso escrito o corregido a mano. Vuelva a leer la báscula, o regístrela como ingreso manual indicando el motivo.

**P: No aparecen Modificar, Replanificar o Cancelar en una orden.**
> R: La orden ya tiene pesadas (Ejecución), está cerrada (Definitivo) o anulada. Solo se modifican órdenes abiertas sin pesadas.

**P: Al crear o modificar una Autorización de Descarga aparece "Capacidad insuficiente".**
> R: El tanque elegido no tiene espacio para la cantidad planificada. Elija otro tanque o reduzca la cantidad. La capacidad disponible de cada tanque se ve en la pantalla **Tanques**.

**P: No puedo dar de baja un tanque, un puesto o una orden.**
> R: Es una protección del sistema: un tanque con volumen almacenado, un puesto con órdenes asociadas y una orden con pesadas no pueden eliminarse. El mensaje en pantalla indica el motivo.

**P: Hubo un error al guardar.**
> R: El mensaje aparece dentro del formulario o de la pantalla. Revise los datos indicados. Si el error persiste, contacte a soporte informando el mensaje exacto.
