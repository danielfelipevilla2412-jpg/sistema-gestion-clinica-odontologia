-- Run after MIGRATION_NotaClinica_Cita.sql; then run SEED_CIE10_SISPRO.sql.
IF OBJECT_ID(N'dbo.Historia_Clinica', N'U') IS NULL
    THROW 50000, 'No existe la tabla dbo.Historia_Clinica.', 1;
IF OBJECT_ID(N'dbo.Cita', N'U') IS NULL
    THROW 50000, 'No existe la tabla dbo.Cita.', 1;
IF OBJECT_ID(N'dbo.Nota_Clinica', N'U') IS NULL
    THROW 50000, 'No existe la tabla dbo.Nota_Clinica; ejecuta primero MIGRATION_NotaClinica_Cita.sql.', 1;
IF OBJECT_ID(N'dbo.Profesional', N'U') IS NULL
    THROW 50000, 'No existe la tabla dbo.Profesional.', 1;
IF OBJECT_ID(N'dbo.Servicio', N'U') IS NULL
    THROW 50000, 'No existe la tabla dbo.Servicio.', 1;
GO

IF OBJECT_ID(N'dbo.Consulta_Clinica', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Consulta_Clinica
    (
        id_consulta_clinica INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Consulta_Clinica PRIMARY KEY,
        id_historia INT NOT NULL,
        id_cita INT NULL,
        id_nota INT NULL,
        id_proxima_cita INT NULL,
        id_profesional INT NULL,
        fecha_consulta DATETIME2 NULL,
        motivo_consulta NVARCHAR(MAX) NULL,
        enfermedad_actual NVARCHAR(MAX) NULL,
        antecedentes_medicos NVARCHAR(MAX) NULL,
        habitos NVARCHAR(MAX) NULL,
        alergias NVARCHAR(MAX) NULL,
        hallazgos NVARCHAR(MAX) NULL,
        odontograma_observaciones NVARCHAR(MAX) NULL,
        examenes_complementarios NVARCHAR(MAX) NULL,
        evolucion_clinica NVARCHAR(MAX) NULL,
        prescripcion NVARCHAR(MAX) NULL,
        origen NVARCHAR(30) NOT NULL CONSTRAINT DF_Consulta_Clinica_origen DEFAULT N'captura',
        fecha_registro DATETIME2 NOT NULL CONSTRAINT DF_Consulta_Clinica_fecha_registro DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Consulta_Clinica_Historia FOREIGN KEY (id_historia)
            REFERENCES dbo.Historia_Clinica(id_historia) ON DELETE CASCADE,
        CONSTRAINT FK_Consulta_Clinica_Cita FOREIGN KEY (id_cita)
            REFERENCES dbo.Cita(id_cita) ON DELETE SET NULL,
        CONSTRAINT FK_Consulta_Clinica_Nota FOREIGN KEY (id_nota)
            -- Yeray: Evita una ruta de cascada múltiple porque la nota ya se elimina en cascada desde la historia clínica.
            REFERENCES dbo.Nota_Clinica(id_nota) ON DELETE NO ACTION,
        CONSTRAINT FK_Consulta_Clinica_ProximaCita FOREIGN KEY (id_proxima_cita)
            REFERENCES dbo.Cita(id_cita),
        CONSTRAINT FK_Consulta_Clinica_Profesional FOREIGN KEY (id_profesional)
            REFERENCES dbo.Profesional(id_profesional) ON DELETE SET NULL
    );
END;
GO

IF COL_LENGTH(N'dbo.Consulta_Clinica', N'id_proxima_cita') IS NULL
    ALTER TABLE dbo.Consulta_Clinica ADD id_proxima_cita INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Consulta_Clinica_ProximaCita' AND parent_object_id = OBJECT_ID(N'dbo.Consulta_Clinica'))
    ALTER TABLE dbo.Consulta_Clinica ADD CONSTRAINT FK_Consulta_Clinica_ProximaCita
        FOREIGN KEY (id_proxima_cita) REFERENCES dbo.Cita(id_cita);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Consulta_Clinica_Cita' AND object_id = OBJECT_ID(N'dbo.Consulta_Clinica'))
    CREATE UNIQUE INDEX UX_Consulta_Clinica_Cita ON dbo.Consulta_Clinica(id_cita) WHERE id_cita IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Consulta_Clinica_Nota' AND object_id = OBJECT_ID(N'dbo.Consulta_Clinica'))
    CREATE UNIQUE INDEX UX_Consulta_Clinica_Nota ON dbo.Consulta_Clinica(id_nota) WHERE id_nota IS NOT NULL;
GO

IF OBJECT_ID(N'dbo.Diagnostico_CIE10', N'U') IS NULL
BEGIN
    -- This table intentionally starts empty. Load only a verified official Colombian CIE-10 source.
    CREATE TABLE dbo.Diagnostico_CIE10
    (
        codigo NVARCHAR(10) NOT NULL CONSTRAINT PK_Diagnostico_CIE10 PRIMARY KEY,
        descripcion NVARCHAR(500) NOT NULL,
        version_catalogo NVARCHAR(50) NOT NULL,
        fuente NVARCHAR(500) NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.Consulta_Clinica_Diagnostico', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Consulta_Clinica_Diagnostico
    (
        id_diagnostico INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Consulta_Clinica_Diagnostico PRIMARY KEY,
        id_consulta_clinica INT NOT NULL,
        codigo_cie10 NVARCHAR(10) NULL,
        descripcion NVARCHAR(500) NOT NULL,
        es_principal BIT NOT NULL,
        CONSTRAINT FK_Consulta_Clinica_Diagnostico_Consulta FOREIGN KEY (id_consulta_clinica)
            REFERENCES dbo.Consulta_Clinica(id_consulta_clinica) ON DELETE CASCADE,
        CONSTRAINT FK_Consulta_Clinica_Diagnostico_CIE10 FOREIGN KEY (codigo_cie10)
            REFERENCES dbo.Diagnostico_CIE10(codigo) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Consulta_Clinica_Diagnostico_Consulta' AND object_id = OBJECT_ID(N'dbo.Consulta_Clinica_Diagnostico'))
    CREATE INDEX IX_Consulta_Clinica_Diagnostico_Consulta ON dbo.Consulta_Clinica_Diagnostico(id_consulta_clinica);
GO

IF OBJECT_ID(N'dbo.Consulta_Clinica_Sintoma', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Consulta_Clinica_Sintoma
    (
        id_sintoma INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Consulta_Clinica_Sintoma PRIMARY KEY,
        id_consulta_clinica INT NOT NULL,
        descripcion NVARCHAR(250) NOT NULL,
        orden INT NOT NULL,
        CONSTRAINT FK_Consulta_Clinica_Sintoma_Consulta FOREIGN KEY (id_consulta_clinica)
            REFERENCES dbo.Consulta_Clinica(id_consulta_clinica) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Consulta_Clinica_Sintoma_Consulta' AND object_id = OBJECT_ID(N'dbo.Consulta_Clinica_Sintoma'))
    CREATE INDEX IX_Consulta_Clinica_Sintoma_Consulta ON dbo.Consulta_Clinica_Sintoma(id_consulta_clinica);
GO

IF OBJECT_ID(N'dbo.Plan_Tratamiento', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Plan_Tratamiento
    (
        id_plan INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Plan_Tratamiento PRIMARY KEY,
        id_historia INT NOT NULL,
        id_servicio INT NOT NULL,
        id_profesional INT NULL,
        numero_fdi NVARCHAR(5) NULL,
        sesiones_estimadas INT NOT NULL CONSTRAINT DF_Plan_Tratamiento_sesiones_estimadas DEFAULT 0,
        estado NVARCHAR(20) NOT NULL CONSTRAINT DF_Plan_Tratamiento_estado DEFAULT N'propuesto',
        observacion NVARCHAR(1000) NULL,
        fecha_registro DATETIME2 NOT NULL CONSTRAINT DF_Plan_Tratamiento_fecha_registro DEFAULT SYSUTCDATETIME(),
        actualizado_en DATETIME2 NOT NULL CONSTRAINT DF_Plan_Tratamiento_actualizado_en DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Plan_Tratamiento_Historia FOREIGN KEY (id_historia)
            REFERENCES dbo.Historia_Clinica(id_historia) ON DELETE CASCADE,
        CONSTRAINT FK_Plan_Tratamiento_Servicio FOREIGN KEY (id_servicio)
            REFERENCES dbo.Servicio(id_servicio),
        CONSTRAINT FK_Plan_Tratamiento_Profesional FOREIGN KEY (id_profesional)
            REFERENCES dbo.Profesional(id_profesional) ON DELETE SET NULL,
        CONSTRAINT CK_Plan_Tratamiento_Estado CHECK (estado IN (N'propuesto', N'aprobado', N'en-curso', N'pausado', N'completado', N'cancelado')),
        CONSTRAINT CK_Plan_Tratamiento_SesionesEstimadas CHECK (sesiones_estimadas >= 0),
        CONSTRAINT CK_Plan_Tratamiento_NumeroFdi CHECK
        (
            numero_fdi IS NULL
            OR
            (
                LEN(numero_fdi) = 2
                AND LEFT(numero_fdi, 1) BETWEEN N'1' AND N'4'
                AND RIGHT(numero_fdi, 1) BETWEEN N'1' AND N'8'
            )
        )
    );
END;
GO

-- Yeray: Agrega la estimación a planes ya existentes y valida que no sea negativa.
IF COL_LENGTH(N'dbo.Plan_Tratamiento', N'sesiones_estimadas') IS NULL
    ALTER TABLE dbo.Plan_Tratamiento ADD sesiones_estimadas INT NOT NULL
        CONSTRAINT DF_Plan_Tratamiento_sesiones_estimadas DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Plan_Tratamiento_SesionesEstimadas' AND parent_object_id = OBJECT_ID(N'dbo.Plan_Tratamiento'))
    ALTER TABLE dbo.Plan_Tratamiento ADD CONSTRAINT CK_Plan_Tratamiento_SesionesEstimadas CHECK (sesiones_estimadas >= 0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Plan_Tratamiento_Historia' AND object_id = OBJECT_ID(N'dbo.Plan_Tratamiento'))
    CREATE INDEX IX_Plan_Tratamiento_Historia ON dbo.Plan_Tratamiento(id_historia);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Plan_Tratamiento_Profesional' AND object_id = OBJECT_ID(N'dbo.Plan_Tratamiento'))
    CREATE INDEX IX_Plan_Tratamiento_Profesional ON dbo.Plan_Tratamiento(id_profesional);
GO

-- Yeray: Las sesiones realizadas se registran como eventos anulables, no como un contador editable, para conservar trazabilidad clínica.
IF OBJECT_ID(N'dbo.Plan_Sesion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Plan_Sesion
    (
        id_sesion INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Plan_Sesion PRIMARY KEY,
        id_plan INT NOT NULL,
        fecha_sesion DATETIME2 NOT NULL CONSTRAINT DF_Plan_Sesion_fecha DEFAULT SYSUTCDATETIME(),
        id_profesional_registro INT NOT NULL,
        id_cita INT NULL,
        id_nota INT NULL,
        registrada_en DATETIME2 NOT NULL CONSTRAINT DF_Plan_Sesion_registrada DEFAULT SYSUTCDATETIME(),
        anulada BIT NOT NULL CONSTRAINT DF_Plan_Sesion_anulada DEFAULT 0,
        motivo_anulacion NVARCHAR(500) NULL,
        anulada_en DATETIME2 NULL,
        id_profesional_anula INT NULL,
        CONSTRAINT FK_Plan_Sesion_Plan FOREIGN KEY (id_plan)
            REFERENCES dbo.Plan_Tratamiento(id_plan) ON DELETE CASCADE,
        CONSTRAINT FK_Plan_Sesion_ProfesionalRegistro FOREIGN KEY (id_profesional_registro)
            REFERENCES dbo.Profesional(id_profesional),
        CONSTRAINT FK_Plan_Sesion_ProfesionalAnula FOREIGN KEY (id_profesional_anula)
            REFERENCES dbo.Profesional(id_profesional),
        CONSTRAINT FK_Plan_Sesion_Cita FOREIGN KEY (id_cita)
            REFERENCES dbo.Cita(id_cita) ON DELETE SET NULL,
        CONSTRAINT FK_Plan_Sesion_Nota FOREIGN KEY (id_nota)
            REFERENCES dbo.Nota_Clinica(id_nota) ON DELETE NO ACTION,
        CONSTRAINT CK_Plan_Sesion_Anulacion CHECK
        (
            (anulada = 0 AND motivo_anulacion IS NULL AND anulada_en IS NULL AND id_profesional_anula IS NULL)
            OR
            (anulada = 1 AND NULLIF(LTRIM(RTRIM(motivo_anulacion)), N'') IS NOT NULL
                AND anulada_en IS NOT NULL AND id_profesional_anula IS NOT NULL)
        )
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Plan_Sesion_Plan_Activa' AND object_id = OBJECT_ID(N'dbo.Plan_Sesion'))
    CREATE INDEX IX_Plan_Sesion_Plan_Activa ON dbo.Plan_Sesion(id_plan) WHERE anulada = 0;
GO

;WITH HistoriasConFormulario AS
(
    SELECT h.id_historia, f.Formulario
    FROM dbo.Historia_Clinica AS h
    CROSS APPLY
    (
        SELECT COALESCE(
            JSON_QUERY(CASE WHEN ISJSON(h.observaciones_generales) = 1 THEN h.observaciones_generales END, '$.formularioHistoria'),
            JSON_QUERY(CASE WHEN ISJSON(h.observaciones_generales) = 1 THEN h.observaciones_generales END, '$.FormularioHistoria')
        ) AS Formulario
    ) AS f
    WHERE f.Formulario IS NOT NULL
)
INSERT INTO dbo.Consulta_Clinica
(
    id_historia, fecha_consulta, motivo_consulta, enfermedad_actual,
    antecedentes_medicos, habitos, alergias, hallazgos,
    odontograma_observaciones, examenes_complementarios,
    evolucion_clinica, prescripcion, origen
)
SELECT
    h.id_historia,
    NULL,
    COALESCE(campos.MotivoConsulta, campos.MotivoConsultaCamel),
    COALESCE(campos.EnfermedadActual, campos.EnfermedadActualCamel),
    COALESCE(campos.AntecedentesMedicos, campos.AntecedentesMedicosCamel),
    COALESCE(campos.Habitos, campos.HabitosCamel),
    COALESCE(campos.Alergias, campos.AlergiasCamel),
    COALESCE(campos.Hallazgos, campos.HallazgosCamel),
    COALESCE(campos.OdontogramaObservaciones, campos.OdontogramaObservacionesCamel),
    COALESCE(campos.ExamenesComplementarios, campos.ExamenesComplementariosCamel),
    COALESCE(campos.EvolucionClinica, campos.EvolucionClinicaCamel),
    COALESCE(campos.Prescripcion, campos.PrescripcionCamel),
    N'migracion-json'
FROM HistoriasConFormulario AS h
CROSS APPLY OPENJSON(h.Formulario)
WITH
(
    MotivoConsulta NVARCHAR(MAX) '$.MotivoConsulta',
    MotivoConsultaCamel NVARCHAR(MAX) '$.motivoConsulta',
    EnfermedadActual NVARCHAR(MAX) '$.EnfermedadActual',
    EnfermedadActualCamel NVARCHAR(MAX) '$.enfermedadActual',
    AntecedentesMedicos NVARCHAR(MAX) '$.AntecedentesMedicos',
    AntecedentesMedicosCamel NVARCHAR(MAX) '$.antecedentesMedicos',
    Habitos NVARCHAR(MAX) '$.Habitos',
    HabitosCamel NVARCHAR(MAX) '$.habitos',
    Alergias NVARCHAR(MAX) '$.Alergias',
    AlergiasCamel NVARCHAR(MAX) '$.alergias',
    Hallazgos NVARCHAR(MAX) '$.Hallazgos',
    HallazgosCamel NVARCHAR(MAX) '$.hallazgos',
    OdontogramaObservaciones NVARCHAR(MAX) '$.OdontogramaObservaciones',
    OdontogramaObservacionesCamel NVARCHAR(MAX) '$.odontogramaObservaciones',
    ExamenesComplementarios NVARCHAR(MAX) '$.ExamenesComplementarios',
    ExamenesComplementariosCamel NVARCHAR(MAX) '$.examenesComplementarios',
    EvolucionClinica NVARCHAR(MAX) '$.EvolucionClinica',
    EvolucionClinicaCamel NVARCHAR(MAX) '$.evolucionClinica',
    Prescripcion NVARCHAR(MAX) '$.Prescripcion',
    PrescripcionCamel NVARCHAR(MAX) '$.prescripcion'
) AS campos
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.Consulta_Clinica AS cc
    WHERE cc.id_historia = h.id_historia AND cc.origen = N'migracion-json'
);
GO

;WITH Formularios AS
(
    SELECT h.id_historia, f.Formulario
    FROM dbo.Historia_Clinica AS h
    CROSS APPLY
    (
        SELECT COALESCE(
            JSON_QUERY(CASE WHEN ISJSON(h.observaciones_generales) = 1 THEN h.observaciones_generales END, '$.formularioHistoria'),
            JSON_QUERY(CASE WHEN ISJSON(h.observaciones_generales) = 1 THEN h.observaciones_generales END, '$.FormularioHistoria')
        ) AS Formulario
    ) AS f
    WHERE f.Formulario IS NOT NULL
),
Diagnosticos AS
(
    SELECT cc.id_consulta_clinica, d.Descripcion, d.EsPrincipal
    FROM Formularios AS f
    INNER JOIN dbo.Consulta_Clinica AS cc ON cc.id_historia = f.id_historia AND cc.origen = N'migracion-json'
    CROSS APPLY OPENJSON(f.Formulario)
    WITH
    (
        DiagnosticoPrincipal NVARCHAR(MAX) '$.DiagnosticoPrincipal',
        DiagnosticoPrincipalCamel NVARCHAR(MAX) '$.diagnosticoPrincipal',
        DiagnosticoSecundario NVARCHAR(MAX) '$.DiagnosticoSecundario',
        DiagnosticoSecundarioCamel NVARCHAR(MAX) '$.diagnosticoSecundario'
    ) AS campos
    CROSS APPLY
    (
        VALUES
            (COALESCE(campos.DiagnosticoPrincipal, campos.DiagnosticoPrincipalCamel), CONVERT(bit, 1)),
            (COALESCE(campos.DiagnosticoSecundario, campos.DiagnosticoSecundarioCamel), CONVERT(bit, 0))
    ) AS d(Descripcion, EsPrincipal)
    WHERE NULLIF(LTRIM(RTRIM(d.Descripcion)), N'') IS NOT NULL
)
INSERT INTO dbo.Consulta_Clinica_Diagnostico (id_consulta_clinica, descripcion, es_principal)
SELECT d.id_consulta_clinica, LEFT(d.Descripcion, 500), d.EsPrincipal
FROM Diagnosticos AS d
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.Consulta_Clinica_Diagnostico AS actual
    WHERE actual.id_consulta_clinica = d.id_consulta_clinica
      AND actual.es_principal = d.EsPrincipal
      AND actual.descripcion = LEFT(d.Descripcion, 500)
);
GO

;WITH Formularios AS
(
    SELECT h.id_historia,
        COALESCE(
            JSON_QUERY(CASE WHEN ISJSON(h.observaciones_generales) = 1 THEN h.observaciones_generales END, '$.formularioHistoria.SintomasSignos'),
            JSON_QUERY(CASE WHEN ISJSON(h.observaciones_generales) = 1 THEN h.observaciones_generales END, '$.formularioHistoria.sintomasSignos'),
            JSON_QUERY(CASE WHEN ISJSON(h.observaciones_generales) = 1 THEN h.observaciones_generales END, '$.FormularioHistoria.SintomasSignos'),
            JSON_QUERY(CASE WHEN ISJSON(h.observaciones_generales) = 1 THEN h.observaciones_generales END, '$.FormularioHistoria.sintomasSignos')
        ) AS Sintomas
    FROM dbo.Historia_Clinica AS h
)
INSERT INTO dbo.Consulta_Clinica_Sintoma (id_consulta_clinica, descripcion, orden)
SELECT cc.id_consulta_clinica, LEFT(LTRIM(RTRIM(s.value)), 250), TRY_CONVERT(int, s.[key])
FROM Formularios AS f
INNER JOIN dbo.Consulta_Clinica AS cc ON cc.id_historia = f.id_historia AND cc.origen = N'migracion-json'
CROSS APPLY OPENJSON(CASE WHEN ISJSON(f.Sintomas) = 1 THEN f.Sintomas ELSE N'[]' END) AS s
WHERE NULLIF(LTRIM(RTRIM(s.value)), N'') IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.Consulta_Clinica_Sintoma AS actual
      WHERE actual.id_consulta_clinica = cc.id_consulta_clinica
        AND actual.orden = TRY_CONVERT(int, s.[key])
  );
GO
