CREATE FUNCTION [dbo].[fn_GetCurrentUser]()
RETURNS VARCHAR(50)
AS
BEGIN
	--
	DECLARE @vText VARCHAR(128);
	--
	-- Obtiene el Usuario de CONTEXT_INFO (desde la posición 25: cultura CHAR(5) + sesión CHAR(19),
	-- ver dbo.pr_context_save) y elimina el relleno sobrante. SET CONTEXT_INFO completa los 128
	-- bytes con 0x00, que RTRIM no quita: se eliminan con REPLACE (con collation binaria, porque
	-- las collations de Windows ignoran CHAR(0) al comparar) antes del RTRIM.
	SET @vText = RTRIM(SUBSTRING(REPLACE(CAST(CONTEXT_INFO() AS VARCHAR(128)) COLLATE Latin1_General_BIN, CHAR(0), ''), 25, 103));
    --
    -- Si el contexto no está seteado (o se guardó sin usuario) devuelve el usuario logueado en la base de datos
    IF  @vText IS NULL OR @vText = ''
	BEGIN
		--
		-- Asigna el usuario de logueado en la base de datos
	    SET @vText = SYSTEM_USER;
	    --
	END
	--
	-- Devuelve el valor.
    RETURN @vText;
    --
END;
