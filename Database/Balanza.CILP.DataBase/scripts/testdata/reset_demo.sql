/*
================================================================================
 reset_demo.sql - Deja los datos de demo como recién cargados (NO se publica)
--------------------------------------------------------------------------------
 Build Action = None y NO está referenciado en Script.PostDeployment.sql:
 nunca corre en un publish. Se ejecuta a mano, DESDE LA RAÍZ DEL REPO (el :r del
 final busca seed_demo.sql con una ruta relativa a la carpeta actual):

   sqlcmd -S .\SQLEXPRESS -E -d BalanzaCILP -f 65001 -b -i Database\Balanza.CILP.DataBase\scripts\testdata\reset_demo.sql

 Qué hace:
   1. Borra TODAS las órdenes (orden, detalle, OT, planificación, pesadas y vínculo
      con tanques) salvo las 5 OT que el seed crea ya ejecutadas, que se conservan
      como historial para los reportes. Es decir: borra lo creado durante las
      pruebas y las OT pendientes del seed, aunque se hayan pesado, cancelado o
      editado.
   2. Devuelve a los tanques el volumen que reservaban las autorizaciones de
      descarga borradas que creó la aplicación (las del seed no reservan volumen).
   3. Vuelve a correr seed_demo.sql, que repone las OT pendientes con fechas
      relativas a hoy:
        AF789HJ  descarga de crudo con la primera pesada (BRUTO) hecha
        AD345MN  orden de entrega sin pesadas
        AE123BC  orden de entrega sin pesadas

 No toca maestros (vehículos, conductores, básculas, tanques, usuarios, etc.).
 Es idempotente: se puede correr las veces que haga falta.
================================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT N'reset_demo: borrando órdenes de prueba';

BEGIN TRANSACTION;

DECLARE @Descarga TINYINT = 2;      -- sysdata t_TipoOrden: Autorización de Descarga

-- Los mismos remitos que la tabla @OT de seed_demo.sql. Historial = 1 para las OT que el
-- seed crea ya ejecutadas (se conservan); las demás son las pendientes, que se reponen.
DECLARE @RemitosSeed TABLE (Remito BIGINT PRIMARY KEY, Historial BIT NOT NULL);
INSERT INTO @RemitosSeed VALUES
 (2026001, 1), (2026002, 1), (2026003, 1), (2026004, 1), (2026005, 1)
,(2026006, 0), (2026007, 0), (2026008, 0);

-- Órdenes que se conservan: las del historial del seed.
DECLARE @Conservar TABLE (Id_Orden BIGINT PRIMARY KEY);
INSERT INTO @Conservar
SELECT DISTINCT d.Id_Orden
FROM dbo.t_OrdenDetalle d
INNER JOIN @RemitosSeed s ON s.Remito = d.NroRemito_OrdenDetalle AND s.Historial = 1;

DECLARE @Ordenes TABLE (Id_Orden BIGINT PRIMARY KEY);
INSERT INTO @Ordenes
SELECT o.Id_Orden FROM dbo.t_Orden o
WHERE NOT EXISTS (SELECT 1 FROM @Conservar c WHERE c.Id_Orden = o.Id_Orden);

DECLARE @Detalles TABLE (Id_OrdenDetalle BIGINT PRIMARY KEY);
INSERT INTO @Detalles
SELECT d.Id_OrdenDetalle FROM dbo.t_OrdenDetalle d
INNER JOIN @Ordenes o ON o.Id_Orden = d.Id_Orden;

-- Volumen reservado por las descargas que creó la aplicación (igual que LiberarTanqueAsync
-- en OrdenesTransporteController, pero también para las cerradas, porque la orden desaparece).
UPDATE t
SET VolumenOcupado_Tanque = CASE WHEN t.VolumenOcupado_Tanque > x.Volumen
                                 THEN t.VolumenOcupado_Tanque - x.Volumen ELSE 0 END
FROM dbo.t_Tanque t
INNER JOIN (
    SELECT ta.Id_Tanque, SUM(ta.Volumen_TanqueAlmacenamiento) AS Volumen
    FROM dbo.t_TanqueAlmacenamiento ta
    INNER JOIN @Detalles dd ON dd.Id_OrdenDetalle = ta.Id_OrdenDetalle
    INNER JOIN dbo.t_OrdenDetalle d ON d.Id_OrdenDetalle = ta.Id_OrdenDetalle
    INNER JOIN dbo.t_Orden o ON o.Id_Orden = d.Id_Orden
    WHERE o.Id_TipoOrden = @Descarga
      AND NOT EXISTS (SELECT 1 FROM @RemitosSeed s WHERE s.Remito = d.NroRemito_OrdenDetalle)
    GROUP BY ta.Id_Tanque
) x ON x.Id_Tanque = t.Id_Tanque;
PRINT N'  tanques con volumen liberado: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));

DELETE p FROM dbo.t_Pesada p
INNER JOIN dbo.t_Planificacion pl ON pl.Id_Planificacion = p.Id_Planificacion
INNER JOIN dbo.t_OrdenTransporte ot ON ot.Id_OrdenTransporte = pl.Id_OrdenTransporte
INNER JOIN @Detalles d ON d.Id_OrdenDetalle = ot.Id_OrdenDetalle;
PRINT N'  pesadas borradas: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));

DELETE pl FROM dbo.t_Planificacion pl
INNER JOIN dbo.t_OrdenTransporte ot ON ot.Id_OrdenTransporte = pl.Id_OrdenTransporte
INNER JOIN @Detalles d ON d.Id_OrdenDetalle = ot.Id_OrdenDetalle;
PRINT N'  planificaciones borradas: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));

DELETE ta FROM dbo.t_TanqueAlmacenamiento ta
INNER JOIN @Detalles d ON d.Id_OrdenDetalle = ta.Id_OrdenDetalle;

DELETE ot FROM dbo.t_OrdenTransporte ot
INNER JOIN @Detalles d ON d.Id_OrdenDetalle = ot.Id_OrdenDetalle;
PRINT N'  órdenes de transporte borradas: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));

DELETE d FROM dbo.t_OrdenDetalle d
INNER JOIN @Detalles x ON x.Id_OrdenDetalle = d.Id_OrdenDetalle;

DELETE o FROM dbo.t_Orden o
INNER JOIN @Ordenes x ON x.Id_Orden = o.Id_Orden;
PRINT N'  órdenes borradas: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));

COMMIT TRANSACTION;
GO

PRINT N'reset_demo: recargando seed_demo.sql';
GO
:r Database\Balanza.CILP.DataBase\scripts\testdata\seed_demo.sql
GO

PRINT N'reset_demo OK';
