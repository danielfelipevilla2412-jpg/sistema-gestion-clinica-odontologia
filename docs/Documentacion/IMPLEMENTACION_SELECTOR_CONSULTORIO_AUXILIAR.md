# Implementación: Selector de Consultorio para Auxiliar

**Fecha:** 16 de septiembre de 2026  
**Módulo:** Gestión de Citas - Estado del Consultorio (st-aux-09)  
**Propósito:** Permitir al auxiliar seleccionar y cambiar de consultorio, guardando la preferencia en base de datos.

---

## 🎯 Problema Resuelto

El auxiliar no podía cambiar de consultorio en la vista `st-aux-09-estado-consultorio`. El consultorio se cargaba automáticamente sin opción de cambio, limitando la flexibilidad del auxiliar para gestionar múltiples consultorios.

---

## ✅ Implementación

### 1. **Nueva Entidad: UsuarioPreferenciaConsultorio**

**Ubicación:** `Models/Entities/UsuarioPreferenciaConsultorio.cs`

```csharp
[Table("UsuariosPreferenciasConsultorio")]
public class UsuarioPreferenciaConsultorio
{
    [Key]
    public int IdPreferencia { get; set; }
    
    [Required]
    public int IdUsuario { get; set; }
    
    [Required]
    public int IdConsultorio { get; set; }
    
    public DateTime ActualizadoEn { get; set; }
    
    // Navegación
    public virtual Usuario? Usuario { get; set; }
    public virtual Consultorio? Consultorio { get; set; }
}
```

**Propósito:** Almacena la preferencia del consultorio seleccionado por cada usuario auxiliar.

---

### 2. **Actualización del DbContext**

**Ubicación:** `Data/AppDbContext.cs`

```csharp
public DbSet<UsuarioPreferenciaConsultorio> UsuariosPreferenciasConsultorio => Set<UsuarioPreferenciaConsultorio>();
```

---

### 3. **Nuevos Endpoints API**

**Ubicación:** `Controllers/Api/ConsultoriosApiController.cs`

#### a) GET /api/consultorios
Obtiene la lista de consultorios disponibles.

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "nombre": "Consultorio 1",
      "ubicacion": "Planta baja",
      "estado": "disponible"
    }
  ]
}
```

#### b) POST /api/consultorios/guardar-preferencia
Guarda la preferencia del consultorio seleccionado por el auxiliar.

**Request:**
```json
{
  "consultorioId": 2
}
```

**Response:**
```json
{
  "success": true,
  "message": "Preferencia guardada correctamente."
}
```

---

### 4. **Actualización del Controlador Principal**

**Ubicación:** `Controllers/GestionDeCitas/GestionCitasController.cs`

**Cambio en `Staux09EstadoConsultorio`:**
- Si no se pasa `consultorioId` como parámetro, el sistema busca la preferencia guardada del usuario
- Si existe preferencia, carga automáticamente ese consultorio
- Si no existe preferencia, carga el primer consultorio disponible (comportamiento anterior)

```csharp
if (!consultorioId.HasValue)
{
    int? userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parsedUserId)
        ? parsedUserId
        : null;

    if (userId.HasValue)
    {
        var preferencia = await _context.UsuariosPreferenciasConsultorio
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdUsuario == userId.Value, ct);

        if (preferencia != null)
        {
            consultorioId = preferencia.IdConsultorio;
        }
    }
}
```

---

### 5. **Actualización de la Vista**

**Ubicación:** `Views/Gestion_De_Citas/st-aux-09-estado-consultorio/estado-consultorio.cshtml`

**Cambio en el header:**
Se agregó un selector de consultorio en la parte superior derecha:

```html
<div class="header-right" style="display:flex;align-items:center;gap:12px;">
  <label for="selectConsultorio" class="form-label" style="margin:0;font-weight:500;">Consultorio:</label>
  <select id="selectConsultorio" class="form-select" style="min-width:200px;" aria-label="Seleccionar consultorio">
    <option value="">Cargando...</option>
  </select>
</div>
```

---

### 6. **Actualización del JavaScript**

**Ubicación:** `wwwroot/js/Gestion_De_Citas/st-aux-09-estado-consultorio/estado-consultorio.js`

#### Nuevas funciones:

##### a) `loadConsultorios()`
- Carga la lista de consultorios desde el endpoint `/api/consultorios`
- Puebla el `<select>` con las opciones disponibles
- Selecciona automáticamente el consultorio actual

##### b) `handleConsultorioChange(event)`
- Se ejecuta cuando el auxiliar cambia el consultorio en el selector
- Guarda la preferencia en el backend mediante `/api/consultorios/guardar-preferencia`
- Recarga la página con el nuevo consultorio usando query string: `?consultorioId={id}`

**Flujo:**
1. Usuario selecciona un consultorio diferente
2. POST a `/api/consultorios/guardar-preferencia` → Guarda en BD
3. Redirect a la misma vista con `?consultorioId={nuevoId}`
4. El controlador carga los datos del nuevo consultorio
5. El checklist, estado y historial se actualizan automáticamente

---

## 🗄️ Migración de Base de Datos

**Ubicación:** `Database/MIGRATION_UsuariosPreferenciasConsultorio.sql`

### Tabla creada:
```sql
CREATE TABLE [dbo].[UsuariosPreferenciasConsultorio] (
    [IdPreferencia]   INT           IDENTITY(1,1) PRIMARY KEY,
    [IdUsuario]       INT           NOT NULL,
    [IdConsultorio]   INT           NOT NULL,
    [ActualizadoEn]   DATETIME2(7)  NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT [FK_UsuariosPreferenciasConsultorio_Usuario] 
        FOREIGN KEY ([IdUsuario]) REFERENCES [dbo].[Usuarios]([IdUsuario]) ON DELETE CASCADE,
    
    CONSTRAINT [FK_UsuariosPreferenciasConsultorio_Consultorio] 
        FOREIGN KEY ([IdConsultorio]) REFERENCES [dbo].[Consultorios]([IdConsultorio]) ON DELETE CASCADE,
    
    CONSTRAINT [UQ_UsuariosPreferenciasConsultorio_Usuario] UNIQUE ([IdUsuario])
);
```

### Índice creado:
```sql
CREATE INDEX [IX_UsuariosPreferenciasConsultorio_Usuario] 
    ON [dbo].[UsuariosPreferenciasConsultorio]([IdUsuario]);
```

**Para aplicar la migración:**
```sql
sqlcmd -S localhost -d SmileTrack -i Database/MIGRATION_UsuariosPreferenciasConsultorio.sql
```

---

## 📋 Flujo de Usuario

1. **Primera vez:**
   - Auxiliar accede a `/gestion-de-citas/st-aux-09-estado-consultorio`
   - Sistema carga el primer consultorio disponible
   - Auxiliar ve el selector con todos los consultorios

2. **Cambiar consultorio:**
   - Auxiliar selecciona un consultorio diferente del selector
   - Sistema guarda la preferencia en BD
   - Página se recarga con el nuevo consultorio
   - Checklist, estado e historial se cargan del nuevo consultorio

3. **Visitas posteriores:**
   - Auxiliar accede a la misma vista
   - Sistema carga automáticamente el último consultorio seleccionado
   - Auxiliar puede cambiar de consultorio cuando lo necesite

---

## 🔒 Seguridad

- ✅ Endpoints protegidos con `[Authorize(Roles = "Auxiliar,Administrador")]`
- ✅ Validación de AntiForgery token en POST
- ✅ Validación de que el consultorio existe antes de guardar
- ✅ Solo el usuario autenticado puede guardar su propia preferencia
- ✅ Foreign keys con `ON DELETE CASCADE` para integridad referencial

---

## ✅ Resultados

**Antes:**
- ❌ Auxiliar no podía cambiar de consultorio
- ❌ Sistema cargaba siempre el primer consultorio disponible
- ❌ No había persistencia de preferencias

**Después:**
- ✅ Auxiliar puede seleccionar cualquier consultorio
- ✅ Preferencia se guarda en base de datos
- ✅ Sistema recuerda el último consultorio seleccionado
- ✅ Experiencia fluida con recarga automática de datos

---

## 🎨 Mejoras Futuras (Opcional)

1. **Selector sin recarga:**
   - Cargar datos del nuevo consultorio con AJAX sin recargar la página completa
   - Actualizar solo el checklist, estado e historial dinámicamente

2. **Selector con búsqueda:**
   - Si hay muchos consultorios, agregar búsqueda tipo-ahead

3. **Vista multi-consultorio:**
   - Dashboard que muestre el estado de todos los consultorios simultáneamente

---

## 📝 Archivos Modificados/Creados

### Creados:
- ✨ `Models/Entities/UsuarioPreferenciaConsultorio.cs`
- ✨ `Database/MIGRATION_UsuariosPreferenciasConsultorio.sql`
- ✨ `Documentacion/IMPLEMENTACION_SELECTOR_CONSULTORIO_AUXILIAR.md`

### Modificados:
- 📝 `Controllers/Api/ConsultoriosApiController.cs` (2 nuevos endpoints)
- 📝 `Controllers/GestionDeCitas/GestionCitasController.cs` (carga de preferencia)
- 📝 `Data/AppDbContext.cs` (nuevo DbSet)
- 📝 `Views/Gestion_De_Citas/st-aux-09-estado-consultorio/estado-consultorio.cshtml` (selector en header)
- 📝 `wwwroot/js/Gestion_De_Citas/st-aux-09-estado-consultorio/estado-consultorio.js` (2 nuevas funciones)

---

**Implementación completada exitosamente** ✅
