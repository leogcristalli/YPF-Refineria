# Justificación de Tecnologías y Herramientas

## 1. Elección del Lenguaje de Programación y Stack Tecnológico

Para el desarrollo del sistema **CILP** se seleccionó una arquitectura moderna y robusta, con una API REST en el servidor y una aplicación web de página única (SPA) en el cliente. El objetivo es garantizar escalabilidad, seguridad y facilidad de mantenimiento.

### Backend: C# con ASP.NET Core (.NET 10)
Se eligió **C#** sobre el framework **ASP.NET Core** (.NET 10) por las siguientes razones:
*   **Tipado fuerte y seguridad:** al ser un lenguaje fuertemente tipado, reduce los errores en tiempo de ejecución, algo crítico en un sistema que maneja datos sensibles de pesaje y logística.
*   **Rendimiento:** ASP.NET Core es uno de los frameworks web más rápidos del mercado, lo que permite procesar registros de pesaje y consultas de órdenes de transporte con latencia mínima.
*   **Ecosistema empresarial:** ofrece integración nativa con SQL Server y herramientas de depuración de primer nivel (Visual Studio).
*   **Acceso a datos con Entity Framework Core:** el backend usa **Entity Framework Core** (proveedor SQL Server) solo para **mapear** las entidades al esquema existente. El esquema no lo crea ni lo migra el backend: se define y versiona en el proyecto de base de datos (ver sección de Base de Datos).
*   **Seguridad de credenciales:** la cadena de conexión y la clave de firma de los tokens no se guardan en el repositorio: se administran con *user-secrets* de .NET (ver la sección de seguridad).
*   **Documentación de la API:** el backend expone la especificación **OpenAPI**, lo que facilita probar y documentar los endpoints.

### Frontend: React (JavaScript) con Vite y Tailwind CSS
Para la interfaz de usuario se seleccionó la librería **React**, junto con herramientas que aceleran el desarrollo:
*   **Arquitectura basada en componentes:** permite construir elementos reutilizables (tablas de órdenes, formularios de pesaje, menú lateral), lo que acelera el desarrollo y asegura consistencia visual.
*   **Experiencia de usuario (UX):** al ser una *Single Page Application*, la navegación entre "Operaciones", "Maestros" y "Reportes" es instantánea, sin recargas completas de página, simulando una aplicación de escritorio.
*   **Vite:** herramienta de construcción y servidor de desarrollo con recarga inmediata, que acorta el ciclo de prueba.
*   **Tailwind CSS:** estilos mediante clases utilitarias, que dan un diseño uniforme sin mantener hojas de estilo extensas.
*   **Librerías de apoyo:** *React Router* (navegación entre pantallas y rutas protegidas por permiso), *Axios* (comunicación con la API, con el token de sesión en cada petición) y *Lucide* (iconografía). Las validaciones de los formularios usan los atributos nativos de HTML y se repiten en el backend, que es el que decide.

### Base de Datos: Microsoft SQL Server
*   **Integridad de datos:** es un motor relacional que garantiza la estructura estricta que requiere el sistema (relaciones entre vehículos, conductores, empresas, órdenes de transporte y pesadas), con claves foráneas, restricciones y transacciones.
*   **Coherencia con el diseño:** es el motor definido en la especificación del proyecto y en el diseño entidad-relación.
*   **Esquema versionado como código:** la base se define en un **proyecto de base de datos de SQL Server** (`.sqlproj`) incluido en el repositorio. Contiene las tablas, funciones, procedimientos almacenados, triggers de auditoría, los datos de sistema (estados, tipos, unidades), los datos iniciales de roles y casos de uso, y un script de datos de demostración. Así, cualquier integrante puede reconstruir la base completa desde el repositorio.
*   **Auditoría:** los triggers registran fecha y usuario de alta y modificación de cada registro, lo que da trazabilidad sobre las operaciones de pesaje.

### Seguridad y control de acceso
*   **Inicio de sesión con JWT:** el usuario se autentica con su ID de red y contraseña (`POST /api/auth/login`). El backend emite un **JSON Web Token** firmado (HS256) con una vigencia de 8 horas (un turno), que incluye el usuario, sus roles y sus puntos de menú. La clave de firma se guarda en *user-secrets*, nunca en el repositorio. Se eligió JWT porque la API es *stateless*: cada petición se valida sola, sin sesiones en el servidor.
*   **Contraseñas con hash:** se almacenan con `PasswordHasher` de ASP.NET Core Identity (PBKDF2 con SHA-512, 100.000 iteraciones y *salt* aleatorio), nunca en texto plano. El login responde el mismo mensaje si el usuario no existe o si la contraseña no coincide, para no revelar qué usuarios existen.
*   **Control de acceso por roles y puntos de menú (RBAC):** cada pantalla del sistema es un **punto de menú**; cada rol tiene asignados sus puntos y un usuario puede tener varios roles. La asignación inicial sigue la tabla de actores de la especificación y el Administrador de Sistema la modifica desde la aplicación. El sistema impide quedarse sin administrador (no se puede quitar el rol Administrador de Sistema al último usuario que lo tiene ni sacarle a ese rol las pantallas de Roles y Usuarios).
*   **Protección de la API:** todos los endpoints exigen un usuario autenticado salvo el login (política por defecto de ASP.NET Core), y cada controller exige el punto de menú de su pantalla: sin el permiso responde 403. Las restricciones no dependen de lo que muestra la interfaz; el menú y las rutas del frontend solo ocultan lo que el usuario no puede usar.
*   **Sesión en el navegador:** el frontend guarda la sesión en `sessionStorage`, que se borra al cerrar la pestaña (útil en una PC compartida de báscula), envía el token en cada petición y vuelve a la pantalla de login si la API responde 401 (token vencido o inválido).
*   **Auditoría con el usuario de la aplicación:** en cada conexión a la base, un interceptor de Entity Framework ejecuta `dbo.pr_context_save` con el ID de red del usuario logueado. Los valores por defecto y los triggers de auditoría lo leen con `dbo.fn_GetCurrentUser()`, así que cada alta y modificación queda registrada a nombre de la persona que la hizo, no de la cuenta técnica que usa el servidor.
*   **Limitación conocida:** como los permisos viajan en el token, un cambio de roles o la baja de un usuario se aplica cuando vuelve a iniciar sesión. El doble factor de autenticación queda fuera del alcance de esta versión.

---

## 2. Herramientas para la Toma de Decisiones

El sistema no solo registra la operación: transforma los datos de pesaje en información para decidir. Todos los indicadores y reportes se calculan sobre los datos reales registrados (sin datos de ejemplo; si no hay movimientos, muestran ceros).

### Panel de Control (Inicio)
*   **Para quién:** todos los usuarios, especialmente el operador de báscula y el Gerente de Puesto de Básculas.
*   **Qué muestra:** pesajes del día, tonelaje neto de las órdenes cerradas en el día, órdenes de transporte activas, conductores activos, la actividad de pesaje reciente y el estado de cada báscula y cámara.
*   **Decisiones que apoya:** reaccionar durante el turno. Si una báscula aparece sin conexión o con el certificado vencido, se deriva el tránsito a otra báscula o se pasa a ingreso manual; si el tonelaje del día está por debajo de lo esperado o se acumulan órdenes activas, se reasigna personal o se avisa a Logística.

### Reporte de Movimientos por Período (CU080)
*   **Para quién:** Gerente de Puesto de Básculas y responsables de Logística.
*   **Qué muestra:** las órdenes ejecutadas en un rango de fechas, por tipo de movimiento (recepción o despacho), con tara, bruto y neto, totales y promedio diario; permite filtrar las órdenes **replanificadas**.
*   **Decisiones que apoya:** medir el volumen real movido por período para conciliar con remitos y facturación, comparar recepción contra despacho y detectar cuántas órdenes se replanificaron (y por qué), para revisar la planificación o el desempeño de los transportistas.

### Reporte de Movimientos por Producto (CU081)
*   **Para quién:** Gerente de Puesto de Básculas, Logística y quienes planifican el almacenamiento.
*   **Qué muestra:** por producto, la cantidad de órdenes, el total planificado frente al neto real y el promedio por orden.
*   **Decisiones que apoya:** planificar la capacidad de los tanques según la demanda real de cada producto y detectar diferencias sistemáticas entre lo planificado y lo pesado, que pueden indicar errores de planificación o la necesidad de revisar la tolerancia de un producto.

### Reporte de Duración Promedio de Orden de Transporte (CU082)
*   **Para quién:** Gerente de Puesto de Básculas.
*   **Qué muestra:** el tiempo promedio entre la primera y la última pesada de cada orden, con su distribución por rangos y el detalle por orden.
*   **Decisiones que apoya:** encontrar cuellos de botella en la planta (carga, descarga o espera entre pesadas), fijar turnos y franjas horarias de ingreso de camiones y medir el efecto de cambios en el circuito.

### Reporte de Frecuencia de Ocupación por Báscula (CU083)
*   **Para quién:** Gerente de Puesto de Básculas y responsables de mantenimiento.
*   **Qué muestra:** cantidad y porcentaje de pesadas por báscula, incluidas las de ingreso manual sin báscula.
*   **Decisiones que apoya:** balancear la carga entre básculas, programar el mantenimiento y la renovación de certificados en la de menor uso o en horarios de baja demanda, y detectar una báscula con muchos ingresos manuales, señal de fallas de comunicación.

---

## 3. Herramientas de Gestión del Proyecto

La elección de estas herramientas busca maximizar la transparencia, la trazabilidad del trabajo y una comunicación fluida entre los desarrolladores y los interesados (stakeholders).

### Gestión del Código: Git y GitHub
*   **Justificación:** permite un control de versiones distribuido. Es fundamental para trabajar en equipo sin sobrescribir el trabajo de otros, mantener un historial completo de cambios (quién modificó qué y cuándo) y facilitar la incorporación de nuevas funcionalidades mediante *Pull Requests* para revisión de código. Los *issues* registran los problemas detectados y los trabajos pendientes, y cada *Pull Request* los cierra al incorporarse.

### Gestión de Proyectos: Trello / Jira
*   **Justificación:** se utiliza una metodología ágil (Kanban/Scrum). Estas herramientas permiten visualizar el estado real del proyecto (Pendiente > En progreso > Testeo > Terminado).
    *   *Ejemplo:* una tarjeta para "Crear ABM de Conductores" se mueve a "Terminado" solo cuando cumple con los criterios de aceptación, lo que facilita decidir cuándo liberar una nueva versión.

### Enfoque de Desarrollo: Desarrollo Funcional Directo
*   **Justificación:** se optó por prescindir de herramientas de diseño estático (como Figma) para pasar directamente al **desarrollo del sistema funcional**.
*   **Beneficio:** esta estrategia "Code-First" permite validar la lógica de negocio y la experiencia de usuario sobre el software real desde el primer momento, reduciendo los tiempos de entrega y evitando la discrepancia entre el diseño estático y la implementación final.
