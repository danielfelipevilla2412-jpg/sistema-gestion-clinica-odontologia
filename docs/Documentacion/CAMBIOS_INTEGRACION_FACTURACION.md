# Cambios de Integración de Facturación

**Sistema:** SmileTrack - Gestión Clínica Odontológica  
**Fecha:** 22 de septiembre de 2026  
**Alcance:** Integración entre Gestión de Citas, Facturación y Pagos, y Gestión de Profesionales.

## 1. Objetivo

Se implementó el flujo principal:

```text
Cita atendida -> Factura -> Pago -> Comisión del profesional
```

La integración permite conservar el origen clínico de una factura, registrar pagos parciales y completos, generar notificaciones y calcular comisiones usando facturas pagadas.

## 2. Modelo de datos

### Factura

La entidad `Factura` ahora puede asociarse con:

- Paciente.
- Cita de origen mediante `IdCita`.
- Profesional responsable mediante `IdProfesional`.
- Usuario que generó la factura.
- Detalles de servicios.
- Historial de pagos.

`IdCita` e `IdProfesional` son opcionales para conservar compatibilidad con facturas manuales existentes.

### Pago

Se agregó la entidad [Pago.cs](../Models/Entities/Pago.cs), con los siguientes datos:

- `IdPago`
- `IdFactura`
- `Monto`
- `FechaPago`
- `MetodoPago`
- `Referencia`
- `RegistradoPor`
- `Observaciones`

Una factura puede tener múltiples pagos.

### Restricción de duplicidad

Se agregó el índice único filtrado `UX_Factura_Cita_Activa`.

Esto permite:

- Una sola factura activa por cita.
- Conservar facturas anuladas históricas.
- Evitar facturación duplicada bajo concurrencia en SQL Server.

## 3. Migración SQL

Los cambios están incluidos en:

[Database/SCRIPT_SQL_UNICO_SMILETRACK.sql](../Database/SCRIPT_SQL_UNICO_SMILETRACK.sql)

El script agrega de forma idempotente:

- `Factura.id_cita`
- `Factura.id_profesional`
- Relaciones con `Cita` y `Profesional`.
- Índice único para facturas activas por cita.
- Tabla `Pago`.
- Relaciones de `Pago` con `Factura` y `Usuario`.

El proyecto SQL se validó con una compilación correcta del DACPAC.

## 4. Endpoints implementados

Todos los endpoints de facturación requieren los roles `Administrador` o `Recepcionista`, salvo las restricciones específicas indicadas.

### Listar facturas

```http
GET /api/facturas
```

Parámetros disponibles:

- `buscar`
- `estado`
- `desde`
- `hasta`
- `idCita`
- `idProfesional`

### Consultar factura

```http
GET /api/facturas/{id}
```

Devuelve:

- Datos del paciente.
- Cita y profesional asociados.
- Totales.
- Saldo pendiente.
- Detalles de servicios.
- Historial de pagos.

### Consultar contexto de una cita

```http
GET /api/facturas/desde-cita/{idCita}
```

Devuelve:

- Paciente.
- Servicio.
- Precio actual del servicio.
- Profesional.
- Estado de la cita.
- Factura asociada, si existe.
- Indicador `PuedeFacturar`.

### Crear factura desde una cita

```http
POST /api/facturas/desde-cita/{idCita}
```

Reglas aplicadas:

- La cita debe existir.
- El estado debe ser `Atendida`, `Completada` o `Finalizada`.
- Debe tener paciente activo.
- Debe tener servicio asignado.
- No debe existir otra factura activa para la cita.
- El precio del servicio se guarda como valor histórico en el detalle.
- La respuesta `409 Conflict` se utiliza cuando ya existe una factura activa.

### Crear factura manual

```http
POST /api/facturas
```

Las facturas manuales continúan permitidas. El profesional puede enviarse mediante `IdProfesional` y la cita mediante `IdCita` cuando corresponda.

### Actualizar factura

```http
PUT /api/facturas/{id}
```

Solo se pueden editar facturas pendientes o parciales. Las facturas pagadas o anuladas no se pueden modificar.

### Eliminar factura

```http
DELETE /api/facturas/{id}
```

Solo disponible para administradores y únicamente para facturas pendientes sin pagos.

### Registrar pago

```http
POST /api/facturas/{id}/pagos
```

Reglas:

- No se permiten pagos sobre facturas anuladas.
- No se permiten pagos sobre facturas ya pagadas.
- El pago no puede superar el saldo pendiente.
- Se registra el usuario, método, referencia y fecha.
- El estado cambia automáticamente a `parcial` o `pagada`.

### Consultar pagos

```http
GET /api/facturas/{id}/pagos
```

Devuelve el historial de abonos registrados para una factura.

### Anular factura

```http
POST /api/facturas/{id}/anulacion
```

Solo disponible para administradores. La anulación registra motivo, notificación y auditoría.

### Catálogos

```http
GET /api/facturas/catalogos/pacientes
GET /api/facturas/catalogos/servicios
GET /api/facturas/catalogos/profesionales
```

## 5. Integración con Gestión de Citas

Se agregó la acción **Facturar** en:

- Gestión de citas de recepción:
  `/gestion-de-citas/st-rec-03-gestion-citas`
- Gestión administrativa de citas:
  `/gestion-de-citas/st-adm-09-citas`

El botón aparece para citas con estado:

- `Atendida`
- `Completada`
- `Finalizada`

Al usarlo:

1. Se llama a `POST /api/facturas/desde-cita/{idCita}`.
2. Se crea la factura con paciente, servicio y profesional.
3. Se muestra un mensaje de resultado.
4. Se actualiza la tabla de citas.

Archivos principales:

- [app.js de recepción](../wwwroot/js/Gestion_De_Citas/st-rec-03-gestion-citas/app.js)
- [gestionintegral.js administrativo](../wwwroot/js/Gestion_De_Citas/st-adm-09-citas/gestionintegral.js)

## 6. Integración con Facturación

La pantalla principal está disponible en:

```text
/facturacion-y-pagos/st-adm-12-facturacion
```

Se agregaron filtros por:

- Número de factura o paciente.
- Estado.
- Mes.
- Profesional.
- ID de cita.

También se corrigió el cálculo del saldo pendiente. Ahora se calcula como:

```text
Saldo pendiente = Total - MontoPagado
```

El formulario manual de generación de factura conserva el profesional seleccionado mediante `IdProfesional`.

Archivos principales:

- [Vista de facturación](../Views/Facturacion_Y_Pagos/st-adm-12-facturacion/gestionfacturacion.cshtml)
- [JavaScript de facturación](../wwwroot/js/Facturacion_Y_Pagos/st-adm-12-facturacion/gestionfacturacion.js)
- [Controlador SSR](../Controllers/FacturacionPagosController.cs)

## 7. Integración con Profesionales y comisiones

El reporte de comisiones ahora consulta facturas asociadas al profesional:

```http
GET /api/profesionales/{id}/comisiones
```

Se excluyen facturas anuladas.

La comisión se calcula sobre facturas pagadas y no anuladas, de acuerdo con la decisión funcional aprobada.

El reporte incluye:

- Profesional.
- Periodo.
- Citas facturadas.
- Servicios realizados.
- Total facturado.
- Total recaudado.
- Porcentaje de comisión.
- Honorarios generados.
- Comisión pendiente.

## 8. Auditoría

Las operaciones de facturación registran filas en `Auditoria`:

- `INSERT`: creación de factura.
- `UPDATE`: modificación de factura.
- `PAYMENT`: registro de pago.
- `VOID`: anulación.
- `DELETE`: eliminación.

Cada registro conserva:

- Usuario operador.
- Tabla afectada.
- ID del registro.
- Acción.
- Descripción.
- Fecha.

## 9. Notificaciones

Se crean notificaciones internas para el paciente cuando:

- Se crea una factura.
- Se registra un pago.
- La factura queda pagada.
- Se anula una factura.

Estas notificaciones utilizan el tipo `factura` y pueden visualizarse desde:

```text
/gestion-de-citas/st-pac-03-notificaciones
```

## 10. Pruebas implementadas

Se agregó:

[FacturacionServiceTests.cs](../SmileTrack_MVC.Tests/Services/FacturacionServiceTests.cs)

Las pruebas cubren:

- Creación de factura desde cita atendida.
- Conservación del precio histórico.
- Rechazo de citas no atendidas.
- Prevención de factura activa duplicada.
- Pagos parciales.
- Pago final y cambio a estado `pagada`.
- Historial de dos pagos.

Comando utilizado:

```powershell
dotnet test SmileTrack_MVC.Tests\SmileTrack_MVC.Tests.csproj --no-restore --filter FullyQualifiedName~FacturacionServiceTests
```

Resultado validado:

```text
Total: 4
Errores: 0
Correctas: 4
```

## 11. Validaciones realizadas

Se validó correctamente:

- Compilación del proyecto MVC.
- Compilación del proyecto SQL.
- Compilación de entidades y servicios.
- Pruebas focalizadas de facturación.
- Diagnósticos de controlador, servicio, vistas y JavaScript.

## 12. Pendientes

Quedan como mejoras posteriores:

- Pruebas HTTP completas con autenticación real.
- Pruebas de concurrencia contra SQL Server real.
- Consulta visual detallada de pagos dentro del drawer de factura.
- Filtros avanzados directamente contra API con paginación remota.
- Persistir `DatosAnteriores` y `DatosNuevos` completos en auditoría.
- Reemplazar la generación de número basada en `COUNT + loop` por una secuencia o mecanismo transaccional más robusto.
- Confirmar y ejecutar la migración SQL sobre la base de datos productiva.

## 13. Flujo de uso recomendado

1. Abrir la agenda de recepción o administrativa.
2. Buscar una cita atendida, completada o finalizada.
3. Seleccionar **Facturar**.
4. Verificar paciente, servicio, profesional y total.
5. Registrar un pago total o parcial.
6. Consultar el estado desde la pantalla de facturación.
7. Revisar la notificación del paciente.
8. Consultar las comisiones del profesional.
