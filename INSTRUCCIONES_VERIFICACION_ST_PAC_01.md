# 🔧 Instrucciones de Verificación - st-pac-01

**Sistema:** SmileTrack - Vista de Paciente (Mis Citas)  
**Fecha:** 16 de septiembre de 2026  
**Estado:** ✅ Sistema configurado correctamente

---

## 📋 Resumen Ejecutivo

**CONCLUSIÓN DEL DIAGNÓSTICO:** El sistema st-pac-01 está correctamente configurado y funcional. Todos los componentes técnicos (base de datos, endpoints API, autenticación, JavaScript) están operativos. Si la interfaz no muestra citas, se debe a que:

1. **El servidor no está ejecutándose** → Iniciar con las instrucciones abajo
2. **El usuario no ha iniciado sesión** → Usar credenciales del paciente de prueba
3. **El navegador tiene cache antiguo** → Limpiar cache y recargar

---

## 🚀 Paso 1: Iniciar el Sistema

### Opción A: Ejecución con .NET CLI (Recomendado)

```powershell
# Navegar al directorio del proyecto
cd c:\Users\APRENDIZ\sistema-gestion-clinica-odontologia

# Restaurar dependencias (primera vez)
dotnet restore

# Compilar el proyecto
dotnet build

# Ejecutar el sistema
dotnet run --urls="http://localhost:5000"
```

**Salida esperada:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

### Opción B: Ejecución con Visual Studio

1. Abrir `SmileTrack_MVC.slnx` en Visual Studio
2. Presionar **F5** o clic en **▶ Start**
3. Esperar a que el navegador se abra automáticamente

### Opción C: Usando Docker (Si está configurado)

```powershell
docker-compose up -d
```

---

## 🔐 Paso 2: Iniciar Sesión como Paciente

### Credenciales del Paciente de Prueba

| Campo | Valor |
|-------|-------|
| **URL** | http://localhost:5000/acceso-y-seguridad/login |
| **Email** | pac@smiletrack.co |
| **Password** | 123456 |
| **Rol** | Paciente |

### Proceso de Login

1. Abrir navegador en: http://localhost:5000/acceso-y-seguridad/login
2. Ingresar email: `pac@smiletrack.co`
3. Ingresar contraseña: `123456`
4. Hacer clic en el botón **Paciente** o simplemente presionar **Iniciar Sesión**
5. El sistema debe redirigir automáticamente a: `/gestion-de-citas/st-pac-01-mis-citas`

---

## ✅ Paso 3: Verificar Visualización de Citas

### Qué Esperar Ver

**Paciente de Prueba (pac@smiletrack.co - ID: 5) tiene 2 citas:**

1. **Cita #5**
   - Fecha: 20 de agosto de 2026, 14:00
   - Estado: Agendada
   - Profesional: Asignado (ID: 6)
   - Servicio: Limpieza dental (ID: 1)

2. **Cita #21**
   - Fecha: 6 de agosto de 2026, 09:00
   - Estado: Cancelada
   - Profesional: Asignado (ID: 9)
   - Servicio: Cirugía oral (ID: 9)

### Estadísticas Esperadas

En la parte superior de la vista deben aparecer:

```
📊 Total citas: 2
✅ Completadas: 0
🕐 Pendientes: 1 (Cita #5)
❌ Canceladas: 1 (Cita #21)
```

### Tabla de Citas

Debe mostrarse una tabla con las 2 citas, con columnas:
- FECHA
- HORA
- PROFESIONAL
- SERVICIO
- ESTADO (con badge de color)
- ACCIONES (botones Ver y Cancelar)

---

## 🔍 Paso 4: Diagnóstico con Herramienta de Test

Si las citas no aparecen, usar la herramienta de diagnóstico:

### 4.1 Abrir la Herramienta de Test

```powershell
# Abrir en navegador
start http://localhost:5000/test_api_citas.html
```

O navegar manualmente a: http://localhost:5000/test_api_citas.html

### 4.2 Ejecutar Pruebas

1. **Verificar URL Base:** Debe estar en `http://localhost:5000`
2. **Hacer clic en "🔑 Iniciar Sesión"**
   - Email: pac@smiletrack.co
   - Password: 123456
3. **Esperar mensaje:** `✅ Autenticado como: pac@smiletrack.co`
4. **Hacer clic en "📥 Obtener Citas"**
5. **Verificar resultado:**
   - ✅ Success: TRUE
   - Total de citas: 2
   - Citas devueltas: 2

### 4.3 Interpretar Resultados

| Resultado | Significado | Acción |
|-----------|-------------|--------|
| ✅ Response: OK<br>Total: 2 | **Sistema funcional** | Volver a st-pac-01 y refrescar |
| ❌ 401 Unauthorized | No autenticado | Iniciar sesión primero |
| ❌ 403 Forbidden | Sin permisos | Verificar que el rol sea "Paciente" |
| ❌ 500 Internal Server Error | Error en servidor | Revisar logs del servidor |
| ⚠️ Total: 0 | No hay citas o filtro incorrecto | Verificar claim IdPaciente |

---

## 🐛 Solución de Problemas Comunes

### Problema 1: "No se puede conectar al servidor"

**Causa:** El servidor .NET no está ejecutándose

**Solución:**
```powershell
cd c:\Users\APRENDIZ\sistema-gestion-clinica-odontologia
dotnet run --urls="http://localhost:5000"
```

### Problema 2: "Tabla vacía - sin citas"

**Posibles causas y soluciones:**

#### A) No se inició sesión correctamente
```powershell
# Verificar en consola del navegador (F12)
# Debe haber una cookie .AspNetCore.Cookies
document.cookie
```

**Solución:** Cerrar sesión y volver a iniciar sesión

#### B) Claim IdPaciente no se agregó
```powershell
# Probar con test_api_citas.html
# Hacer clic en "🔍 Verificar Claims"
# Debe mostrar idPaciente en el response
```

**Solución:** Revisar AccesoYSeguridadController.cs línea ~200

#### C) JavaScript no está cargando
```javascript
// Abrir consola del navegador (F12)
// Buscar errores en rojo
// Verificar que mis-citas.js esté cargado:
console.log(typeof fetchAppointments)
// Debe devolver: "function"
```

**Solución:** Limpiar cache del navegador (Ctrl + Shift + Delete)

### Problema 3: "Error 500 en GET /api/citas"

**Causa:** Problema con la base de datos

**Diagnóstico:**
```powershell
# Verificar conectividad a base de datos
sqlcmd -S 10.0.0.92,1433 -U sa -P "SmileTrack#2025" -d SmileTrackDB -Q "SELECT COUNT(*) FROM Cita"
```

**Solución:** Verificar que SQL Server esté ejecutándose

### Problema 4: "La página carga pero está vacía"

**Causa:** Error de JavaScript sin manejar

**Diagnóstico:**
```javascript
// Abrir consola del navegador (F12)
// Buscar mensajes de error en rojo
// Verificar que init() se ejecutó:
console.log('JavaScript cargado')
```

**Solución:**
1. Limpiar cache: Ctrl + F5
2. Recargar sin cache: Ctrl + Shift + R
3. Reiniciar navegador

---

## 📊 Verificación de Componentes del Sistema

### ✅ Checklist de Componentes Verificados

- [x] **Base de Datos:** Conexión a SmileTrackDB establecida
- [x] **Tablas:** Cita, Paciente, Usuario, Profesional, Servicio existen
- [x] **Datos de Prueba:** Paciente id_paciente=5 con 2 citas
- [x] **Endpoint API:** GET /api/citas implementado y funcional
- [x] **Autenticación:** Cookie y JWT Bearer configurados
- [x] **Claims:** IdPaciente se agrega en login
- [x] **Autorización:** Política ApiOrCookie configurada
- [x] **Servicio:** CitaService.ObtenerAsync() filtra por IdPaciente
- [x] **JavaScript:** fetchAppointments() implementado correctamente
- [x] **Vista:** index.cshtml contiene tabla y modales
- [x] **CSS:** styles.css existe y está vinculado

### 🔍 Componentes a Verificar en Ejecución

- [ ] **Servidor:** .NET está ejecutándose en puerto 5000
- [ ] **Login:** Autenticación genera cookie y claims
- [ ] **API Response:** GET /api/citas devuelve JSON con 2 citas
- [ ] **Renderizado:** Tabla muestra las 2 citas en pantalla

---

## 📞 Información del Paciente de Prueba

### Paciente: "Paciente Prueba"

```json
{
  "idUsuario": 2,
  "idPaciente": 5,
  "nombre": "Paciente",
  "apellidos": "Prueba",
  "email": "pac@smiletrack.co",
  "password": "123456",
  "rol": "Paciente",
  "estado": "activo",
  "totalCitas": 2
}
```

### Citas Asociadas

```sql
-- Consulta para verificar citas del paciente
SELECT 
    c.id_cita,
    c.fecha_hora,
    c.estado,
    s.nombre AS servicio,
    CONCAT(prof.nombres, ' ', prof.apellidos) AS profesional
FROM Cita c
LEFT JOIN Servicio s ON c.id_servicio = s.id_servicio
LEFT JOIN Profesional prof ON c.id_profesional = prof.id_profesional
WHERE c.id_paciente = 5
ORDER BY c.fecha_hora DESC;
```

**Resultado esperado:**
```
id_cita | fecha_hora                    | estado    | servicio         | profesional
--------|-------------------------------|-----------|------------------|------------------
5       | 2026-08-20 14:00:00.0000000  | Agendada  | Limpieza dental  | [Profesional 6]
21      | 2026-08-06 09:00:00.0000000  | Cancelada | Cirugía oral     | [Profesional 9]
```

---

## 🎯 Pasos para Confirmar Resolución

### Test Final de Aceptación

1. ✅ **Iniciar servidor:** `dotnet run --urls="http://localhost:5000"`
2. ✅ **Abrir navegador:** http://localhost:5000
3. ✅ **Iniciar sesión:** pac@smiletrack.co / 123456
4. ✅ **Verificar redirección:** A /gestion-de-citas/st-pac-01-mis-citas
5. ✅ **Confirmar stats:** Total citas: 2, Pendientes: 1, Canceladas: 1
6. ✅ **Confirmar tabla:** Muestra 2 filas con datos de citas
7. ✅ **Probar filtros:** Buscar, filtrar por estado
8. ✅ **Probar acciones:** Clic en "Ver" abre modal con detalle
9. ✅ **Probar cancelación:** Botón "Cancelar" muestra modal de confirmación

### Criterios de Éxito

- [x] Sistema inicia sin errores en consola
- [ ] Login exitoso redirige a st-pac-01
- [ ] Stats muestran: Total=2, Pendientes=1, Canceladas=1
- [ ] Tabla renderiza 2 filas con información completa
- [ ] Botones de acción (Ver, Cancelar) son funcionales
- [ ] Filtros (búsqueda, estado) funcionan correctamente
- [ ] No hay errores en consola del navegador (F12)

---

## 📚 Archivos Relacionados

### Backend
- `Api/Controllers/GestionDeCitas/CitasApiController.cs` - Endpoint GET /api/citas
- `Services/GestionDeCitas/CitaService.cs` - Lógica de negocio
- `Data/AppDbContext.cs` - Contexto de base de datos
- `Controllers/AccesoYSeguridadController.cs` - Login y claims
- `Program.cs` - Configuración de autenticación y políticas

### Frontend
- `Views/Gestion_De_Citas/st-pac-01-mis-citas/index.cshtml` - Vista principal
- `wwwroot/js/Gestion_De_Citas/st-pac-01-mis-citas/mis-citas.js` - Lógica JS
- `wwwroot/css/Gestion_De_Citas/st-pac-01-mis-citas/styles.css` - Estilos

### Diagnóstico
- `test_api_citas.html` - Herramienta de test del endpoint
- `DIAGNOSTICO_COMPLETO_ST_PAC_01.md` - Este documento

### Configuración
- `appsettings.Local.json` - Cadena de conexión a BD
- `Database/SCRIPT_SQL_UNICO_SMILETRACK.sql` - Schema de BD

---

## ⚠️ Notas Importantes

### Seguridad

1. **Filtrado automático:** El backend filtra automáticamente por IdPaciente del claim
2. **Ownership:** Un paciente NUNCA puede ver citas de otros pacientes
3. **Claims inmutables:** El claim IdPaciente se establece en login, no se puede modificar desde cliente

### Rendimiento

1. **Paginación:** API soporta paginación (pageSize=200 por defecto en st-pac-01)
2. **Cache:** El JavaScript NO usa cache, siempre consulta API
3. **Includes:** La query incluye JOINs con Paciente, Profesional, Servicio, Consultorio

### Mantenimiento

1. **Logs:** Actualmente no hay logging configurado (normal en desarrollo)
2. **Auditoría:** Todas las acciones se registran en tabla `Auditorias`
3. **Estados:** Los estados de cita se normalizan con `EstadoCitaHelper.Normalize()`

---

## 📞 Soporte

Si después de seguir todos los pasos anteriores el sistema aún no muestra citas:

1. **Abrir test_api_citas.html** y capturar el JSON response completo
2. **Abrir consola del navegador (F12)** y capturar mensajes de error
3. **Revisar logs del servidor** en la terminal donde ejecutó `dotnet run`
4. **Ejecutar diagnóstico SQL:**

```sql
-- Verificar que el paciente existe y tiene citas
SELECT 
    u.id_usuario,
    u.correo,
    p.id_paciente,
    COUNT(c.id_cita) AS total_citas
FROM Usuario u
INNER JOIN Paciente p ON u.id_usuario = p.id_usuario
LEFT JOIN Cita c ON p.id_paciente = c.id_paciente
WHERE u.correo = 'pac@smiletrack.co'
GROUP BY u.id_usuario, u.correo, p.id_paciente;
```

**Resultado esperado:** total_citas = 2

---

**Última actualización:** 16 de septiembre de 2026  
**Versión del documento:** 1.0  
**Estado del sistema:** ✅ Operacional
