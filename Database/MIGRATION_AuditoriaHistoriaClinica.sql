IF OBJECT_ID(N'dbo.Auditoria', N'U') IS NULL
    THROW 50000, 'No existe la tabla dbo.Auditoria.', 1;
GO

DECLARE @constraintName SYSNAME;
DECLARE @dropConstraintSql NVARCHAR(MAX);

SELECT TOP (1) @constraintName = cc.name
FROM sys.check_constraints AS cc
WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Auditoria')
  AND cc.definition LIKE N'%INSERT%'
  AND cc.definition LIKE N'%UPDATE%'
  AND cc.definition LIKE N'%DELETE%'
  AND cc.definition NOT LIKE N'%SELECT%';

IF @constraintName IS NOT NULL
BEGIN
    SET @dropConstraintSql =
        N'ALTER TABLE dbo.Auditoria DROP CONSTRAINT ' + QUOTENAME(@constraintName) + N';';
    EXEC sys.sp_executesql @dropConstraintSql;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_Auditoria_Accion'
      AND parent_object_id = OBJECT_ID(N'dbo.Auditoria')
)
    ALTER TABLE dbo.Auditoria
        ADD CONSTRAINT CK_Auditoria_Accion
        CHECK (accion IN ('INSERT', 'UPDATE', 'DELETE', 'SELECT'));
GO
