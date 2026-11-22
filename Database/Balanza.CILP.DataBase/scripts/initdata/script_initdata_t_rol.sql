PRINT N'initdata dbo.t_rol'
GO
-- Roles base de la especificación. Solo inserta los que faltan (por id): el nombre,
-- la baja y los roles nuevos se administran desde la pantalla Roles y Permisos (CU05 a CU09)
-- y una nueva publicación no los pisa.
SET IDENTITY_INSERT dbo.t_rol ON;

INSERT INTO dbo.t_rol (id_rol, descripcion_rol, activo_rol)
SELECT v.Id, v.Descripcion, 1
FROM (
	VALUES
	 (1, N'Administrador de Sistema')
	,(2, N'Administrador de Negocio')
	,(3, N'Administrador de Recepción')
	,(4, N'Planificador de Descarga')
	,(5, N'Administrador de Despacho')
	,(6, N'Planificador de Carga')
	,(7, N'Conductor Transporte')
	,(8, N'Gerente de Puesto de Básculas')
) v (Id, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM dbo.t_rol r WHERE r.id_rol = v.Id);

SET IDENTITY_INSERT dbo.t_rol OFF;
GO
