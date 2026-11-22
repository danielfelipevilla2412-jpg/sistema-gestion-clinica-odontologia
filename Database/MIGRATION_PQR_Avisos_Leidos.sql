IF OBJECT_ID(N'dbo.PQR_Aviso_Leido', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PQR_Aviso_Leido (
        id_usuario int NOT NULL,
        clave nvarchar(64) NOT NULL,
        CONSTRAINT PK_PQR_Aviso_Leido PRIMARY KEY (id_usuario, clave),
        CONSTRAINT FK_PQR_Aviso_Leido_Usuario FOREIGN KEY (id_usuario)
            REFERENCES dbo.Usuario (id_usuario) ON DELETE CASCADE
    );
END;
GO
