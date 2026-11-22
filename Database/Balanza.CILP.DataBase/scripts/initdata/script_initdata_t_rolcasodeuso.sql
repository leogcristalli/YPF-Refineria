PRINT N'initdata dbo.t_RolCasoDeUso'
GO
-- Asignación inicial de puntos de menú a los roles base, según la tabla de actores y casos de
-- uso de la especificación. Ids de rol: script_initdata_t_rol.sql; ids de punto de menú:
-- script_initdata_t_casodeuso.sql.
--
-- Solo inserta los pares (rol, punto) que no existen. Si desde la pantalla Roles y Permisos se
-- quita una asignación, la fila queda inactiva y no se vuelve a insertar: una nueva publicación
-- no pisa los cambios hechos desde la pantalla.
INSERT INTO dbo.t_RolCasoDeUso (Id_Rol, Id_CasoDeUso)
SELECT v.IdRol, v.IdPunto
FROM (
	VALUES
	-- 1 Administrador de Sistema: Puntos de Menú, Roles, Usuarios, Básculas, Dispositivos
	 (1, 14), (1, 15), (1, 16), (1, 10), (1, 13)
	-- 2 Administrador de Negocio: Conductores, Vehículos, Productos, Razones Sociales, Puestos, Tanques
	,(2, 7), (2, 6), (2, 8), (2, 9), (2, 12), (2, 11)
	-- 3 Administrador de Recepción: Autorizaciones de Descarga
	,(3, 3)
	-- 4 Planificador de Descarga: Órdenes de Transporte
	,(4, 2)
	-- 5 Administrador de Despacho: Órdenes de Entrega
	,(5, 4)
	-- 6 Planificador de Carga: Órdenes de Transporte
	,(6, 2)
	-- 7 Conductor Transporte: Gestión de Pesadas (CU077 a CU079)
	,(7, 1)
	-- 8 Gerente de Puesto de Básculas: Reportes (CU080 a CU083)
	,(8, 5)
) v (IdRol, IdPunto)
WHERE EXISTS (SELECT 1 FROM dbo.t_Rol r WHERE r.Id_Rol = v.IdRol)
  AND EXISTS (SELECT 1 FROM dbo.t_CasoDeUso c WHERE c.Id_CasoDeUso = v.IdPunto)
  AND NOT EXISTS (SELECT 1 FROM dbo.t_RolCasoDeUso x WHERE x.Id_Rol = v.IdRol AND x.Id_CasoDeUso = v.IdPunto);
GO
