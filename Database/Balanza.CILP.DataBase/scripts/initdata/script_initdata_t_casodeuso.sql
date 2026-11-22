PRINT N'initdata dbo.t_CasoDeUso'
GO
-- Puntos de menú: uno por pantalla del sistema. Los ids los usan como constantes el backend
-- (Backend/Auth/PuntoMenu.cs) y el frontend (src/auth/puntosMenu.js): no cambiarlos.
--
-- Solo inserta los que faltan (por id): el nombre y la baja se administran desde la pantalla
-- Puntos de Menú (CU03, CU04) y una nueva publicación no los pisa.

-- Migración por única vez: el catálogo anterior tenía 9 puntos que eran los casos de uso de
-- Puntos de Menú y Roles (id 1 = 'Consultar Puntos de Menu'). Se borran con sus asignaciones
-- para cargar el catálogo por pantalla. Este bloque se puede quitar cuando todas las bases
-- estén publicadas con el catálogo nuevo.
IF EXISTS (SELECT 1 FROM dbo.t_CasoDeUso WHERE Id_CasoDeUso = 1 AND Descripcion_CasoDeUso = 'Consultar Puntos de Menu')
BEGIN
	PRINT N'  migración: se borra el catálogo anterior de puntos de menú';
	DELETE FROM dbo.t_RolCasoDeUso WHERE Id_CasoDeUso BETWEEN 1 AND 9;
	DELETE FROM dbo.t_CasoDeUso WHERE Id_CasoDeUso BETWEEN 1 AND 9;
END
GO

SET IDENTITY_INSERT dbo.t_CasoDeUso ON;

INSERT INTO dbo.t_CasoDeUso (Id_CasoDeUso, Descripcion_CasoDeUso, Activo_CasoDeUso)
SELECT v.Id, v.Descripcion, 1
FROM (
	VALUES
	 ( 1, N'Gestión de Pesadas')
	,( 2, N'Órdenes de Transporte')
	,( 3, N'Autorizaciones de Descarga')
	,( 4, N'Órdenes de Entrega')
	,( 5, N'Reportes')
	,( 6, N'Vehículos')
	,( 7, N'Conductores')
	,( 8, N'Productos')
	,( 9, N'Razones Sociales')
	,(10, N'Básculas')
	,(11, N'Tanques')
	,(12, N'Puestos de Expedición y Recepción')
	,(13, N'Dispositivos de Detección')
	,(14, N'Puntos de Menú')
	,(15, N'Roles y Permisos')
	,(16, N'Usuarios')
) v (Id, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM dbo.t_CasoDeUso c WHERE c.Id_CasoDeUso = v.Id);

SET IDENTITY_INSERT dbo.t_CasoDeUso OFF;
GO
