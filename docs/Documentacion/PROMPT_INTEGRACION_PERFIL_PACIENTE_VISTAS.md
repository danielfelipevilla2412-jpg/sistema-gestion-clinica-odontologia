# 📋 Prompt de Implementación: Integración de Perfil de Paciente en Vistas Principales

**Sistema:** SmileTrack - Gestión Clínica Odontológica  
**Módulo:** Integración Perfil-Vistas de Paciente  
**Fecha de Creación:** 16 de septiembre de 2026  
**Versión:** 1.0  
**Prioridad:** ALTA

---

## 🎯 Objetivo General

Implementar la sincronización y visualización de información del perfil de usuario del paciente en todas las vistas principales dedicadas a pacientes, garantizando consistencia de datos, seguridad de información sensible, y una experiencia de usuario fluida y accesible.

---

## 📐 Alcance del Proyecto

### Vistas Afectadas

#### Vistas Principales de Paciente
1. **st-pac-01-mis-citas** - Gestión de citas odontológicas
2. **st-pac-02-historial** - Historial clínico del paciente
3. **st-pac-03-notificaciones** - Centro de notificaciones
4. **st-pac-04-perfil-paciente** - Perfil completo y editable

#### Componentes de UI a Implementar
1. **Header de Usuario** - Barra superior con información básica
2. **Panel de Perfil Lateral** - Sidebar con datos relevantes
3. **Cards de Información** - Widgets contextuales
4. **Modales de Confirmación** - Verificación de identidad
5. **Breadcrumbs Personalizados** - Navegación con nombre del usuario

---

## 🗂️ Información del Paciente a Sincronizar

### 1. Datos Básicos de Identificación (Público)

```typescript
interface DatosBasicosPaciente {
  // Identificación
  idPaciente: number;
  idUsuario: number;
  
  // Información Personal
  nombreCompleto: string;        // "{Nombres} {Apellidos}"
  nombres: string;
  apellidos: string;
  tipoDocumento: string;         // CC, TI, CE, Pasaporte
  documento: string;             // Parcialmente oculto: ***1234
  
  // Contacto
  correo: string;
  telefono: string;              // Formato: (###) ###-####
  
  // Demografía
  fechaNacimiento: Date;
  edad: number;                  // Calculada
  genero: string;                // Masculino, Femenino, Otro
  estadoCivil?: string;
  
  // Ubicación
  direccion?: string;
  ciudad?: string;
  departamento?: string;
  
  // Sistema
  estado: string;                // activo, inactivo
  fechaRegistro: Date;
}
```

### 2. Información Médica (Restringida)

```typescript
interface InformacionMedicaPaciente {
  // Salud General
  grupoSanguineo?: string;       // O+, A+, B+, AB+, etc.
  alergias?: string;             // Lista separada por comas
  medicamentosActuales?: string;
  antecedentesMedicos?: string;
  antecedentesFamiliares?: string;
  
  // Seguro de Salud
  epsAseguradora?: string;
  tipoAfiliacion?: string;       // Contributivo, Subsidiado
  numeroPoliza?: string;
  
  // Contacto de Emergencia
  contactoEmergencia?: string;
  telefonoEmergencia?: string;
  parentescoEmergencia?: string;
  
  // Odontológico
  profesionalAsignado?: {
    idProfesional: number;
    nombreCompleto: string;
    especialidad: string;
    telefono?: string;
  };
}
```

### 3. Estadísticas y Métricas (Calculadas)

```typescript
interface EstadisticasPaciente {
  // Citas
  totalCitas: number;
  citasPendientes: number;
  citasCompletadas: number;
  citasCanceladas: number;
  proximaCita?: {
    idCita: number;
    fechaHora: Date;
    profesional: string;
    servicio: string;
    consultorio: string;
  };
  
  // Historial Clínico
  tieneHistoriaClinica: boolean;
  fechaUltimaConsulta?: Date;
  totalTratamientos: number;
  
  // Financiero
  saldoPendiente: number;
  ultimoPago?: {
    fecha: Date;
    monto: number;
    concepto: string;
  };
  
  // Fidelización
  antiguedad: number;            // Años como paciente
  frecuenciaVisitas: string;     // Baja, Media, Alta
}
```

### 4. Preferencias y Configuración (Personalización)

```typescript
interface PreferenciasPaciente {
  // Notificaciones
  notificacionesEmail: boolean;
  notificacionesSMS: boolean;
  recordatoriosCitas: boolean;
  
  // Visualización
  tema?: 'light' | 'dark' | 'auto';
  idioma?: string;               // es, en
  
  // Privacidad
  compartirDatosEstadisticos: boolean;
  aceptaPublicidad: boolean;
}
```

---

## 🏗️ Arquitectura de Implementación

### Capa 1: Backend (API y Servicios)

#### 1.1 Endpoint de API REST

**Archivo:** `Api/Controllers/PerfilesApiController.cs`

```csharp
[ApiController]
[Route("api/perfil-paciente")]
[Authorize(Policy = "ApiOrCookie")]
public class PerfilPacienteApiController : ControllerBase
{
    private readonly IPerfilPacienteService _service;
    private readonly ILogger<PerfilPacienteApiController> _logger;

    [HttpGet]
    [Route("info-basica")]
    public async Task<IActionResult> ObtenerInfoBasica(CancellationToken ct = default)
    {
        // Extrae IdPaciente del claim
        if (!int.TryParse(User.FindFirstValue("IdPaciente"), out int idPaciente) || idPaciente <= 0)
            return Forbid();

        var info = await _service.ObtenerInfoBasicaAsync(idPaciente, ct);
        
        if (info == null)
            return NotFound(new { success = false, message = "Perfil no encontrado." });

        // Ocultar documento parcialmente
        info.DocumentoOculto = OcultarDocumento(info.Documento);
        
        return Ok(new { success = true, data = info });
    }

    [HttpGet]
    [Route("info-medica")]
    public async Task<IActionResult> ObtenerInfoMedica(CancellationToken ct = default)
    {
        if (!int.TryParse(User.FindFirstValue("IdPaciente"), out int idPaciente) || idPaciente <= 0)
            return Forbid();

        var info = await _service.ObtenerInfoMedicaAsync(idPaciente, ct);
        
        return Ok(new { success = true, data = info });
    }

    [HttpGet]
    [Route("estadisticas")]
    public async Task<IActionResult> ObtenerEstadisticas(CancellationToken ct = default)
    {
        if (!int.TryParse(User.FindFirstValue("IdPaciente"), out int idPaciente) || idPaciente <= 0)
            return Forbid();

        var stats = await _service.ObtenerEstadisticasAsync(idPaciente, ct);
        
        return Ok(new { success = true, data = stats });
    }

    [HttpGet]
    [Route("resumen-completo")]
    public async Task<IActionResult> ObtenerResumenCompleto(CancellationToken ct = default)
    {
        if (!int.TryParse(User.FindFirstValue("IdPaciente"), out int idPaciente) || idPaciente <= 0)
            return Forbid();

        var resumen = await _service.ObtenerResumenCompletoAsync(idPaciente, ct);
        
        return Ok(new { success = true, data = resumen });
    }

    private static string OcultarDocumento(string? documento)
    {
        if (string.IsNullOrWhiteSpace(documento) || documento.Length < 4)
            return "****";
        
        return $"***{documento.Substring(documento.Length - 4)}";
    }
}
```

#### 1.2 Servicio de Negocio

**Archivo:** `Services/Perfiles/IPerfilPacienteService.cs`

```csharp
public interface IPerfilPacienteService
{
    Task<InfoBasicaPacienteDto?> ObtenerInfoBasicaAsync(
        int idPaciente, 
        CancellationToken ct = default);

    Task<InfoMedicaPacienteDto?> ObtenerInfoMedicaAsync(
        int idPaciente, 
        CancellationToken ct = default);

    Task<EstadisticasPacienteDto> ObtenerEstadisticasAsync(
        int idPaciente, 
        CancellationToken ct = default);

    Task<ResumenCompletoPacienteDto> ObtenerResumenCompletoAsync(
        int idPaciente, 
        CancellationToken ct = default);
}
```

**Archivo:** `Services/Perfiles/PerfilPacienteService.cs`

```csharp
public class PerfilPacienteService : IPerfilPacienteService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PerfilPacienteService> _logger;

    public async Task<InfoBasicaPacienteDto?> ObtenerInfoBasicaAsync(
        int idPaciente, 
        CancellationToken ct = default)
    {
        var paciente = await _context.Pacientes
            .AsNoTracking()
            .Include(p => p.ProfesionalAsignado)
            .ThenInclude(prof => prof.Usuario)
            .FirstOrDefaultAsync(p => p.IdPaciente == idPaciente, ct);

        if (paciente == null)
            return null;

        var edad = CalcularEdad(paciente.FechaNacimiento);

        return new InfoBasicaPacienteDto
        {
            IdPaciente = paciente.IdPaciente,
            IdUsuario = paciente.IdUsuario ?? 0,
            NombreCompleto = $"{paciente.Nombres} {paciente.Apellidos}".Trim(),
            Nombres = paciente.Nombres,
            Apellidos = paciente.Apellidos,
            TipoDocumento = paciente.TipoDocumento ?? "CC",
            Documento = paciente.Documento,
            Correo = paciente.Correo,
            Telefono = paciente.Telefono,
            FechaNacimiento = paciente.FechaNacimiento,
            Edad = edad,
            Genero = paciente.Genero ?? "No especificado",
            EstadoCivil = paciente.EstadoCivil,
            Direccion = paciente.Direccion,
            Ciudad = paciente.Ciudad,
            Departamento = paciente.Departamento,
            Estado = paciente.Estado ?? "activo",
            FechaRegistro = paciente.FechaRegistro,
            ProfesionalAsignado = paciente.ProfesionalAsignado == null ? null : new ProfesionalAsignadoDto
            {
                IdProfesional = paciente.ProfesionalAsignado.IdProfesional,
                NombreCompleto = paciente.ProfesionalAsignado.Usuario != null
                    ? $"{paciente.ProfesionalAsignado.Usuario.Nombre} {paciente.ProfesionalAsignado.Usuario.Apellidos}".Trim()
                    : $"{paciente.ProfesionalAsignado.Nombres} {paciente.ProfesionalAsignado.Apellidos}".Trim(),
                Telefono = paciente.ProfesionalAsignado.Telefono
            }
        };
    }

    public async Task<EstadisticasPacienteDto> ObtenerEstadisticasAsync(
        int idPaciente, 
        CancellationToken ct = default)
    {
        var citas = await _context.Citas
            .AsNoTracking()
            .Where(c => c.IdPaciente == idPaciente)
            .Select(c => new { c.IdCita, c.FechaHora, c.Estado })
            .ToListAsync(ct);

        var totalCitas = citas.Count;
        var citasPendientes = citas.Count(c => 
            c.Estado == "Agendada" || c.Estado == "Confirmada");
        var citasCompletadas = citas.Count(c => 
            c.Estado == "Completada" || c.Estado == "Atendida");
        var citasCanceladas = citas.Count(c => 
            c.Estado == "Cancelada" || c.Estado == "No asistió");

        // Próxima cita
        var proximaCita = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Profesional)
            .ThenInclude(p => p.Usuario)
            .Include(c => c.Servicio)
            .Include(c => c.Consultorio)
            .Where(c => c.IdPaciente == idPaciente && 
                        (c.Estado == "Agendada" || c.Estado == "Confirmada") &&
                        c.FechaHora >= DateTime.Now)
            .OrderBy(c => c.FechaHora)
            .FirstOrDefaultAsync(ct);

        // Historia clínica
        var tieneHistoria = await _context.HistoriasClinicas
            .AsNoTracking()
            .AnyAsync(h => h.IdPaciente == idPaciente, ct);

        var fechaUltimaConsulta = await _context.Citas
            .AsNoTracking()
            .Where(c => c.IdPaciente == idPaciente && 
                        (c.Estado == "Completada" || c.Estado == "Atendida"))
            .OrderByDescending(c => c.FechaHora)
            .Select(c => c.FechaHora)
            .FirstOrDefaultAsync(ct);

        // Antigüedad
        var fechaRegistro = await _context.Pacientes
            .AsNoTracking()
            .Where(p => p.IdPaciente == idPaciente)
            .Select(p => p.FechaRegistro)
            .FirstOrDefaultAsync(ct);

        var antiguedad = fechaRegistro != DateTime.MinValue
            ? (DateTime.Now - fechaRegistro).Days / 365
            : 0;

        return new EstadisticasPacienteDto
        {
            TotalCitas = totalCitas,
            CitasPendientes = citasPendientes,
            CitasCompletadas = citasCompletadas,
            CitasCanceladas = citasCanceladas,
            ProximaCita = proximaCita == null ? null : new ProximaCitaDto
            {
                IdCita = proximaCita.IdCita,
                FechaHora = proximaCita.FechaHora,
                Profesional = proximaCita.Profesional?.Usuario != null
                    ? $"{proximaCita.Profesional.Usuario.Nombre} {proximaCita.Profesional.Usuario.Apellidos}".Trim()
                    : "Por asignar",
                Servicio = proximaCita.Servicio?.Nombre ?? "Consulta general",
                Consultorio = proximaCita.Consultorio?.Nombre ?? "Por asignar"
            },
            TieneHistoriaClinica = tieneHistoria,
            FechaUltimaConsulta = fechaUltimaConsulta != DateTime.MinValue ? fechaUltimaConsulta : null,
            Antiguedad = antiguedad,
            FrecuenciaVisitas = DeterminarFrecuenciaVisitas(totalCitas, antiguedad)
        };
    }

    private static int CalcularEdad(DateTime? fechaNacimiento)
    {
        if (!fechaNacimiento.HasValue)
            return 0;

        var hoy = DateTime.Today;
        var edad = hoy.Year - fechaNacimiento.Value.Year;
        
        if (fechaNacimiento.Value.Date > hoy.AddYears(-edad))
            edad--;

        return edad;
    }

    private static string DeterminarFrecuenciaVisitas(int totalCitas, int antiguedad)
    {
        if (antiguedad == 0 || totalCitas == 0)
            return "Nueva";

        var citasPorAno = (double)totalCitas / antiguedad;

        return citasPorAno switch
        {
            >= 4 => "Alta",
            >= 2 => "Media",
            _ => "Baja"
        };
    }
}
```

#### 1.3 DTOs (Data Transfer Objects)

**Archivo:** `Models/DTOs/PerfilPacienteDtos.cs`

```csharp
public class InfoBasicaPacienteDto
{
    public int IdPaciente { get; set; }
    public int IdUsuario { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string? DocumentoOculto { get; set; }
    public string Correo { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public DateTime? FechaNacimiento { get; set; }
    public int Edad { get; set; }
    public string Genero { get; set; } = string.Empty;
    public string? EstadoCivil { get; set; }
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public string? Departamento { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public ProfesionalAsignadoDto? ProfesionalAsignado { get; set; }
}

public class ProfesionalAsignadoDto
{
    public int IdProfesional { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Telefono { get; set; }
}

public class InfoMedicaPacienteDto
{
    public string? GrupoSanguineo { get; set; }
    public string? Alergias { get; set; }
    public string? MedicamentosActuales { get; set; }
    public string? AntecedentesMedicos { get; set; }
    public string? EpsAseguradora { get; set; }
    public string? TipoAfiliacion { get; set; }
    public string? ContactoEmergencia { get; set; }
    public string? TelefonoEmergencia { get; set; }
    public string? ParentescoEmergencia { get; set; }
}

public class EstadisticasPacienteDto
{
    public int TotalCitas { get; set; }
    public int CitasPendientes { get; set; }
    public int CitasCompletadas { get; set; }
    public int CitasCanceladas { get; set; }
    public ProximaCitaDto? ProximaCita { get; set; }
    public bool TieneHistoriaClinica { get; set; }
    public DateTime? FechaUltimaConsulta { get; set; }
    public int Antiguedad { get; set; }
    public string FrecuenciaVisitas { get; set; } = string.Empty;
}

public class ProximaCitaDto
{
    public int IdCita { get; set; }
    public DateTime FechaHora { get; set; }
    public string Profesional { get; set; } = string.Empty;
    public string Servicio { get; set; } = string.Empty;
    public string Consultorio { get; set; } = string.Empty;
}

public class ResumenCompletoPacienteDto
{
    public InfoBasicaPacienteDto InfoBasica { get; set; } = new();
    public InfoMedicaPacienteDto InfoMedica { get; set; } = new();
    public EstadisticasPacienteDto Estadisticas { get; set; } = new();
}
```

---

### Capa 2: Frontend (JavaScript Compartido)

#### 2.1 Servicio de Perfil de Paciente

**Archivo:** `wwwroot/js/shared/perfil-paciente-service.js`

```javascript
/**
 * Servicio compartido para gestionar el perfil del paciente.
 * Este servicio centraliza las llamadas a la API de perfil y
 * mantiene un cache en memoria para evitar llamadas redundantes.
 */
class PerfilPacienteService {
  constructor() {
    this.cache = {
      infoBasica: null,
      estadisticas: null,
      infoMedica: null,
      resumenCompleto: null,
      lastFetch: null
    };
    this.cacheDuration = 5 * 60 * 1000; // 5 minutos
    this.apiBase = '/api/perfil-paciente';
  }

  /**
   * Obtiene headers de autenticación incluyendo CSRF token.
   */
  getAuthHeaders() {
    const headers = {
      'Content-Type': 'application/json',
      'Accept': 'application/json'
    };

    // CSRF Token
    const token = this.getCsrfToken();
    if (token) headers['X-CSRF-TOKEN'] = token;

    // JWT (opcional)
    try {
      const jwt = sessionStorage.getItem('st_jwt');
      if (jwt) headers['Authorization'] = `Bearer ${jwt}`;
    } catch (e) { /* navegación privada */ }

    return headers;
  }

  getCsrfToken() {
    const match = document.cookie.match(/(^|; )XSRF-TOKEN=([^;]+)/);
    return match ? decodeURIComponent(match[2]) : null;
  }

  /**
   * Verifica si el cache es válido.
   */
  isCacheValid() {
    if (!this.cache.lastFetch) return false;
    const now = Date.now();
    return (now - this.cache.lastFetch) < this.cacheDuration;
  }

  /**
   * Invalida el cache forzando una nueva carga en el próximo fetch.
   */
  invalidateCache() {
    this.cache = {
      infoBasica: null,
      estadisticas: null,
      infoMedica: null,
      resumenCompleto: null,
      lastFetch: null
    };
  }

  /**
   * Obtiene información básica del paciente.
   * @param {boolean} forceRefresh - Forzar recarga ignorando cache
   * @returns {Promise<Object|null>}
   */
  async getInfoBasica(forceRefresh = false) {
    if (!forceRefresh && this.isCacheValid() && this.cache.infoBasica) {
      return this.cache.infoBasica;
    }

    try {
      const response = await fetch(`${this.apiBase}/info-basica`, {
        method: 'GET',
        credentials: 'include',
        headers: this.getAuthHeaders()
      });

      if (!response.ok) {
        if (response.status === 401 || response.status === 403) {
          console.warn('[PerfilPaciente] No autenticado o sin permisos');
          return null;
        }
        throw new Error(`HTTP ${response.status}`);
      }

      const data = await response.json();
      
      if (data.success && data.data) {
        this.cache.infoBasica = data.data;
        this.cache.lastFetch = Date.now();
        return data.data;
      }

      return null;
    } catch (error) {
      console.error('[PerfilPaciente] Error obteniendo info básica:', error);
      return null;
    }
  }

  /**
   * Obtiene estadísticas del paciente.
   * @param {boolean} forceRefresh - Forzar recarga ignorando cache
   * @returns {Promise<Object|null>}
   */
  async getEstadisticas(forceRefresh = false) {
    if (!forceRefresh && this.isCacheValid() && this.cache.estadisticas) {
      return this.cache.estadisticas;
    }

    try {
      const response = await fetch(`${this.apiBase}/estadisticas`, {
        method: 'GET',
        credentials: 'include',
        headers: this.getAuthHeaders()
      });

      if (!response.ok) throw new Error(`HTTP ${response.status}`);

      const data = await response.json();
      
      if (data.success && data.data) {
        this.cache.estadisticas = data.data;
        this.cache.lastFetch = Date.now();
        return data.data;
      }

      return null;
    } catch (error) {
      console.error('[PerfilPaciente] Error obteniendo estadísticas:', error);
      return null;
    }
  }

  /**
   * Obtiene información médica del paciente.
   * @param {boolean} forceRefresh - Forzar recarga ignorando cache
   * @returns {Promise<Object|null>}
   */
  async getInfoMedica(forceRefresh = false) {
    if (!forceRefresh && this.isCacheValid() && this.cache.infoMedica) {
      return this.cache.infoMedica;
    }

    try {
      const response = await fetch(`${this.apiBase}/info-medica`, {
        method: 'GET',
        credentials: 'include',
        headers: this.getAuthHeaders()
      });

      if (!response.ok) throw new Error(`HTTP ${response.status}`);

      const data = await response.json();
      
      if (data.success && data.data) {
        this.cache.infoMedica = data.data;
        this.cache.lastFetch = Date.now();
        return data.data;
      }

      return null;
    } catch (error) {
      console.error('[PerfilPaciente] Error obteniendo info médica:', error);
      return null;
    }
  }

  /**
   * Obtiene resumen completo del paciente.
   * @param {boolean} forceRefresh - Forzar recarga ignorando cache
   * @returns {Promise<Object|null>}
   */
  async getResumenCompleto(forceRefresh = false) {
    if (!forceRefresh && this.isCacheValid() && this.cache.resumenCompleto) {
      return this.cache.resumenCompleto;
    }

    try {
      const response = await fetch(`${this.apiBase}/resumen-completo`, {
        method: 'GET',
        credentials: 'include',
        headers: this.getAuthHeaders()
      });

      if (!response.ok) throw new Error(`HTTP ${response.status}`);

      const data = await response.json();
      
      if (data.success && data.data) {
        this.cache.resumenCompleto = data.data;
        this.cache.infoBasica = data.data.infoBasica;
        this.cache.estadisticas = data.data.estadisticas;
        this.cache.infoMedica = data.data.infoMedica;
        this.cache.lastFetch = Date.now();
        return data.data;
      }

      return null;
    } catch (error) {
      console.error('[PerfilPaciente] Error obteniendo resumen completo:', error);
      return null;
    }
  }

  /**
   * Formatea el nombre completo del paciente.
   */
  formatNombreCompleto(info) {
    if (!info) return 'Usuario';
    return info.nombreCompleto || `${info.nombres || ''} ${info.apellidos || ''}`.trim() || 'Usuario';
  }

  /**
   * Formatea el teléfono en formato (###) ###-####.
   */
  formatTelefono(telefono) {
    if (!telefono) return '';
    const cleaned = telefono.replace(/\D/g, '');
    if (cleaned.length === 10) {
      return `(${cleaned.slice(0, 3)}) ${cleaned.slice(3, 6)}-${cleaned.slice(6)}`;
    }
    return telefono;
  }

  /**
   * Formatea la fecha de nacimiento y calcula la edad.
   */
  formatEdad(fechaNacimiento, edad) {
    if (!fechaNacimiento) return '';
    const fecha = new Date(fechaNacimiento);
    const opciones = { year: 'numeric', month: 'long', day: 'numeric' };
    return `${fecha.toLocaleDateString('es-ES', opciones)} (${edad} años)`;
  }
}

// Instancia global del servicio
window.PerfilPacienteService = window.PerfilPacienteService || new PerfilPacienteService();
```

#### 2.2 Componente de UI: Header de Usuario

**Archivo:** `wwwroot/js/shared/perfil-paciente-ui.js`

```javascript
/**
 * Componente de UI para el header de usuario del paciente.
 * Se inyecta en la parte superior de las vistas de paciente.
 */
class PerfilPacienteUI {
  constructor() {
    this.service = window.PerfilPacienteService;
    this.container = null;
  }

  /**
   * Renderiza el header de usuario en el elemento especificado.
   * @param {string|HTMLElement} targetSelector - Selector o elemento donde renderizar
   * @param {Object} options - Opciones de visualización
   */
  async renderUserHeader(targetSelector, options = {}) {
    const target = typeof targetSelector === 'string' 
      ? document.querySelector(targetSelector)
      : targetSelector;

    if (!target) {
      console.warn('[PerfilPacienteUI] Target element not found');
      return;
    }

    const info = await this.service.getInfoBasica();
    const stats = await this.service.getEstadisticas();

    if (!info) {
      console.warn('[PerfilPacienteUI] No se pudo obtener información del paciente');
      return;
    }

    const html = this.generateUserHeaderHTML(info, stats, options);
    target.innerHTML = html;
    this.attachEventListeners(target);
  }

  generateUserHeaderHTML(info, stats, options) {
    const defaults = {
      showAvatar: true,
      showStats: true,
      showProximaCita: true,
      theme: 'light'
    };

    const opts = { ...defaults, ...options };

    const avatar = this.generateAvatar(info);
    const statsHTML = opts.showStats && stats ? this.generateStatsHTML(stats) : '';
    const proximaCitaHTML = opts.showProximaCita && stats?.proximaCita 
      ? this.generateProximaCitaHTML(stats.proximaCita) 
      : '';

    return `
      <div class="user-header" data-theme="${opts.theme}">
        <div class="user-header__main">
          ${opts.showAvatar ? `<div class="user-header__avatar">${avatar}</div>` : ''}
          <div class="user-header__info">
            <h2 class="user-header__name">${this.service.formatNombreCompleto(info)}</h2>
            <p class="user-header__meta">
              <span class="user-header__doc">${info.tipoDocumento} ${info.documentoOculto || info.documento}</span>
              <span class="user-header__separator">•</span>
              <span class="user-header__edad">${info.edad} años</span>
              ${info.ciudad ? `<span class="user-header__separator">•</span><span class="user-header__ciudad">${info.ciudad}</span>` : ''}
            </p>
            <p class="user-header__contact">
              <span class="material-symbols-outlined" aria-hidden="true">email</span>
              <a href="mailto:${info.correo}">${info.correo}</a>
              ${info.telefono ? `<span class="user-header__separator">•</span>
              <span class="material-symbols-outlined" aria-hidden="true">phone</span>
              <a href="tel:${info.telefono}">${this.service.formatTelefono(info.telefono)}</a>` : ''}
            </p>
          </div>
        </div>
        ${statsHTML}
        ${proximaCitaHTML}
      </div>
    `;
  }

  generateAvatar(info) {
    const initials = this.getInitials(info.nombreCompleto);
    const colors = ['#2563eb', '#7c3aed', '#db2777', '#ea580c', '#65a30d'];
    const colorIndex = info.idPaciente % colors.length;
    const bgColor = colors[colorIndex];

    return `
      <div class="avatar" style="background-color: ${bgColor}">
        <span class="avatar__initials">${initials}</span>
      </div>
    `;
  }

  getInitials(nombreCompleto) {
    if (!nombreCompleto) return 'U';
    const parts = nombreCompleto.trim().split(' ');
    if (parts.length === 1) return parts[0][0].toUpperCase();
    return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
  }

  generateStatsHTML(stats) {
    return `
      <div class="user-header__stats">
        <div class="stat-item">
          <span class="stat-item__value">${stats.totalCitas}</span>
          <span class="stat-item__label">Total Citas</span>
        </div>
        <div class="stat-item">
          <span class="stat-item__value">${stats.citasPendientes}</span>
          <span class="stat-item__label">Pendientes</span>
        </div>
        <div class="stat-item">
          <span class="stat-item__value">${stats.citasCompletadas}</span>
          <span class="stat-item__label">Completadas</span>
        </div>
        <div class="stat-item">
          <span class="stat-item__value">${stats.antiguedad}</span>
          <span class="stat-item__label">Años con nosotros</span>
        </div>
      </div>
    `;
  }

  generateProximaCitaHTML(proximaCita) {
    const fecha = new Date(proximaCita.fechaHora);
    const opciones = { 
      weekday: 'long', 
      year: 'numeric', 
      month: 'long', 
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    };
    const fechaFormateada = fecha.toLocaleDateString('es-ES', opciones);

    return `
      <div class="user-header__proxima-cita">
        <span class="material-symbols-outlined" aria-hidden="true">event</span>
        <div class="proxima-cita__info">
          <strong>Próxima cita:</strong> ${fechaFormateada}
          <br>
          <span>${proximaCita.servicio} con ${proximaCita.profesional}</span>
        </div>
      </div>
    `;
  }

  attachEventListeners(container) {
    // Aquí se pueden agregar event listeners si es necesario
  }
}

// Instancia global del componente UI
window.PerfilPacienteUI = window.PerfilPacienteUI || new PerfilPacienteUI();
```

---

### Capa 3: Estilos CSS

**Archivo:** `wwwroot/css/shared/perfil-paciente-ui.css`

```css
/* ===============================================
   User Header Component - Perfil de Paciente
   =============================================== */

.user-header {
  background: var(--white);
  border-radius: var(--radius);
  padding: 1.5rem;
  margin-bottom: 1.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  border: 1px solid var(--border);
}

.user-header[data-theme="dark"] {
  background: var(--dark-bg);
  color: var(--dark-text);
  border-color: var(--dark-border);
}

.user-header__main {
  display: flex;
  align-items: center;
  gap: 1.5rem;
  margin-bottom: 1rem;
}

.user-header__avatar {
  flex-shrink: 0;
}

.avatar {
  width: 80px;
  height: 80px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 2rem;
  font-weight: 700;
  color: white;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
}

.avatar__initials {
  user-select: none;
}

.user-header__info {
  flex: 1;
}

.user-header__name {
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--text);
  margin: 0 0 0.5rem 0;
}

.user-header__meta,
.user-header__contact {
  font-size: 0.875rem;
  color: var(--text-muted);
  margin: 0.25rem 0;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.user-header__contact .material-symbols-outlined {
  font-size: 1rem;
}

.user-header__contact a {
  color: var(--primary);
  text-decoration: none;
}

.user-header__contact a:hover {
  text-decoration: underline;
}

.user-header__separator {
  color: var(--border);
  user-select: none;
}

.user-header__stats {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(100px, 1fr));
  gap: 1rem;
  padding-top: 1rem;
  border-top: 1px solid var(--border);
}

.stat-item {
  text-align: center;
  padding: 0.75rem;
  background: var(--bg-light);
  border-radius: var(--radius-sm);
}

.stat-item__value {
  display: block;
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--primary);
  margin-bottom: 0.25rem;
}

.stat-item__label {
  display: block;
  font-size: 0.75rem;
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.user-header__proxima-cita {
  margin-top: 1rem;
  padding: 1rem;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border-radius: var(--radius-sm);
  display: flex;
  align-items: center;
  gap: 1rem;
}

.user-header__proxima-cita .material-symbols-outlined {
  font-size: 2rem;
  opacity: 0.9;
}

.proxima-cita__info strong {
  font-weight: 600;
}

/* Responsive */
@media (max-width: 768px) {
  .user-header__main {
    flex-direction: column;
    text-align: center;
  }

  .user-header__info {
    width: 100%;
  }

  .user-header__meta,
  .user-header__contact {
    justify-content: center;
  }

  .user-header__stats {
    grid-template-columns: repeat(2, 1fr);
  }

  .avatar {
    width: 60px;
    height: 60px;
    font-size: 1.5rem;
  }
}

@media (max-width: 480px) {
  .user-header__stats {
    grid-template-columns: 1fr;
  }
}
```

---

## 🔐 Medidas de Seguridad

### 1. Protección de Datos Sensibles

#### Backend
```csharp
// Ocultar documento parcialmente
private static string OcultarDocumento(string? documento)
{
    if (string.IsNullOrWhiteSpace(documento) || documento.Length < 4)
        return "****";
    
    return $"***{documento.Substring(documento.Length - 4)}";
}

// No exponer información médica sin autorización explícita
[Authorize(Policy = "ApiOrCookie")]
[Route("info-medica")]
public async Task<IActionResult> ObtenerInfoMedica(...)
{
    // Solo el paciente propietario puede ver su info médica
    if (!int.TryParse(User.FindFirstValue("IdPaciente"), out int idPaciente))
        return Forbid();
    
    // ...
}
```

#### Frontend
```javascript
// No almacenar datos sensibles en localStorage
// Usar solo cache en memoria que se limpia al cerrar pestaña
class PerfilPacienteService {
  constructor() {
    // Cache en memoria (NO persiste)
    this.cache = {
      infoBasica: null,
      // ...
    };
    // NO usar localStorage para datos médicos
  }
}
```

### 2. Validación de Ownership

```csharp
// Siempre validar que el usuario sea propietario de los datos
public async Task<InfoBasicaPacienteDto?> ObtenerInfoBasicaAsync(
    int idPaciente, 
    CancellationToken ct = default)
{
    // El claim IdPaciente ya está validado en el controller
    // Aquí solo consultamos los datos del paciente especificado
    
    var paciente = await _context.Pacientes
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.IdPaciente == idPaciente, ct);
    
    // ...
}
```

### 3. Protección CSRF

```javascript
getAuthHeaders() {
  const headers = {
    'Content-Type': 'application/json',
    'Accept': 'application/json'
  };

  // CSRF Token obligatorio
  const token = this.getCsrfToken();
  if (token) headers['X-CSRF-TOKEN'] = token;

  return headers;
}
```

### 4. Rate Limiting (Recomendado)

```csharp
// En Program.cs, agregar rate limiting para endpoints de perfil
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("PerfilPaciente", opt =>
    {
        opt.PermitLimit = 30;  // 30 peticiones
        opt.Window = TimeSpan.FromMinutes(1);  // por minuto
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

// En el controller
[EnableRateLimiting("PerfilPaciente")]
public async Task<IActionResult> ObtenerInfoBasica(...)
```

---

## 🧪 Protocolo de Pruebas

### Fase 1: Pruebas Unitarias

#### Backend Tests

**Archivo:** `SmileTrack_MVC.Tests/Services/PerfilPacienteServiceTests.cs`

```csharp
public class PerfilPacienteServiceTests
{
    [Fact]
    public async Task ObtenerInfoBasica_DevuelveDatos_CuandoPacienteExiste()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDB")
            .Options;

        using var context = new AppDbContext(options);
        
        context.Pacientes.Add(new Paciente
        {
            IdPaciente = 1,
            Nombres = "Juan",
            Apellidos = "Pérez",
            Documento = "123456789",
            FechaNacimiento = new DateTime(1990, 1, 1),
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var service = new PerfilPacienteService(context, null);

        // Act
        var result = await service.ObtenerInfoBasicaAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Juan Pérez", result.NombreCompleto);
        Assert.Equal(34, result.Edad); // En 2026
    }

    [Fact]
    public async Task ObtenerInfoBasica_DevuelveNull_CuandoPacienteNoExiste()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDB2")
            .Options;

        using var context = new AppDbContext(options);
        var service = new PerfilPacienteService(context, null);

        // Act
        var result = await service.ObtenerInfoBasicaAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ObtenerEstadisticas_CalculaCorrectamente()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDB3")
            .Options;

        using var context = new AppDbContext(options);
        
        context.Pacientes.Add(new Paciente
        {
            IdPaciente = 1,
            FechaRegistro = DateTime.Now.AddYears(-2)
        });

        context.Citas.AddRange(
            new Cita { IdCita = 1, IdPaciente = 1, Estado = "Agendada", FechaHora = DateTime.Now.AddDays(5) },
            new Cita { IdCita = 2, IdPaciente = 1, Estado = "Completada", FechaHora = DateTime.Now.AddDays(-10) },
            new Cita { IdCita = 3, IdPaciente = 1, Estado = "Cancelada", FechaHora = DateTime.Now.AddDays(-20) }
        );

        await context.SaveChangesAsync();

        var service = new PerfilPacienteService(context, null);

        // Act
        var result = await service.ObtenerEstadisticasAsync(1);

        // Assert
        Assert.Equal(3, result.TotalCitas);
        Assert.Equal(1, result.CitasPendientes);
        Assert.Equal(1, result.CitasCompletadas);
        Assert.Equal(1, result.CitasCanceladas);
        Assert.Equal(2, result.Antiguedad);
    }
}
```

### Fase 2: Pruebas de Integración

#### Test Manual con Herramienta Interactiva

**Archivo:** `test_perfil_paciente.html`

```html
<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Test Perfil Paciente - SmileTrack</title>
    <link rel="stylesheet" href="/css/shared/perfil-paciente-ui.css">
    <style>
        body {
            font-family: system-ui, -apple-system, sans-serif;
            max-width: 1200px;
            margin: 20px auto;
            padding: 20px;
            background: #f5f5f5;
        }
        .test-section {
            background: white;
            padding: 20px;
            margin-bottom: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }
        .btn {
            background: #2563eb;
            color: white;
            border: none;
            padding: 10px 20px;
            border-radius: 6px;
            cursor: pointer;
            margin-right: 10px;
        }
        .btn:hover { background: #1d4ed8; }
        pre {
            background: #1f2937;
            color: #d1d5db;
            padding: 15px;
            border-radius: 6px;
            overflow-x: auto;
        }
    </style>
</head>
<body>
    <h1>🧪 Test de Integración - Perfil de Paciente</h1>

    <div class="test-section">
        <h2>1. Autenticación</h2>
        <input type="email" id="email" value="pac@smiletrack.co" placeholder="Email">
        <input type="password" id="password" value="123456" placeholder="Password">
        <button class="btn" onclick="login()">Iniciar Sesión</button>
        <div id="authResult"></div>
    </div>

    <div class="test-section">
        <h2>2. Probar Endpoints</h2>
        <button class="btn" onclick="testInfoBasica()">Info Básica</button>
        <button class="btn" onclick="testEstadisticas()">Estadísticas</button>
        <button class="btn" onclick="testInfoMedica()">Info Médica</button>
        <button class="btn" onclick="testResumen()">Resumen Completo</button>
        <div id="endpointResult"></div>
    </div>

    <div class="test-section">
        <h2>3. Renderizar Componente UI</h2>
        <button class="btn" onclick="renderUserHeader()">Renderizar Header</button>
        <div id="uiTarget"></div>
    </div>

    <div class="test-section">
        <h2>4. Console Log</h2>
        <pre id="consoleLog"></pre>
    </div>

    <script src="/js/shared/perfil-paciente-service.js"></script>
    <script src="/js/shared/perfil-paciente-ui.js"></script>
    <script>
        const log = (msg) => {
            const logEl = document.getElementById('consoleLog');
            logEl.textContent += `[${new Date().toLocaleTimeString()}] ${msg}\n`;
            console.log(msg);
        };

        async function login() {
            const email = document.getElementById('email').value;
            const password = document.getElementById('password').value;
            
            try {
                const response = await fetch('/acceso-y-seguridad/login', {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'Accept': 'application/json' },
                    body: new URLSearchParams({ email, password, rol: 'Paciente' })
                });
                
                const data = await response.json();
                
                if (data.success) {
                    document.getElementById('authResult').innerHTML = '<p style="color:green;">✅ Autenticado</p>';
                    log('Login exitoso');
                } else {
                    document.getElementById('authResult').innerHTML = '<p style="color:red;">❌ Error: ' + data.message + '</p>';
                    log('Login fallido: ' + data.message);
                }
            } catch (error) {
                log('Error: ' + error.message);
            }
        }

        async function testInfoBasica() {
            log('Testing getInfoBasica()...');
            const data = await window.PerfilPacienteService.getInfoBasica(true);
            document.getElementById('endpointResult').innerHTML = '<pre>' + JSON.stringify(data, null, 2) + '</pre>';
            log('Info básica obtenida');
        }

        async function testEstadisticas() {
            log('Testing getEstadisticas()...');
            const data = await window.PerfilPacienteService.getEstadisticas(true);
            document.getElementById('endpointResult').innerHTML = '<pre>' + JSON.stringify(data, null, 2) + '</pre>';
            log('Estadísticas obtenidas');
        }

        async function testInfoMedica() {
            log('Testing getInfoMedica()...');
            const data = await window.PerfilPacienteService.getInfoMedica(true);
            document.getElementById('endpointResult').innerHTML = '<pre>' + JSON.stringify(data, null, 2) + '</pre>';
            log('Info médica obtenida');
        }

        async function testResumen() {
            log('Testing getResumenCompleto()...');
            const data = await window.PerfilPacienteService.getResumenCompleto(true);
            document.getElementById('endpointResult').innerHTML = '<pre>' + JSON.stringify(data, null, 2) + '</pre>';
            log('Resumen completo obtenido');
        }

        async function renderUserHeader() {
            log('Rendering user header...');
            await window.PerfilPacienteUI.renderUserHeader('#uiTarget', {
                showAvatar: true,
                showStats: true,
                showProximaCita: true
            });
            log('Header renderizado');
        }
    </script>
</body>
</html>
```

### Fase 3: Casos de Uso a Validar

#### CU-01: Visualización de Perfil en st-pac-01 (Mis Citas)
- ✅ Header muestra nombre completo del paciente
- ✅ Stats muestran total de citas, pendientes, completadas
- ✅ Próxima cita aparece destacada si existe
- ✅ Documento aparece parcialmente oculto

#### CU-02: Sincronización tras Actualización de Datos
- ✅ Usuario edita perfil en st-pac-04
- ✅ Cache se invalida automáticamente
- ✅ Al volver a st-pac-01, datos actualizados aparecen
- ✅ No requiere recarga manual de página

#### CU-03: Manejo de Sesión Expirada
- ✅ Si sesión expira, API devuelve 401
- ✅ JavaScript detecta 401 y muestra mensaje
- ✅ No se muestran datos obsoletos del cache
- ✅ Usuario es redirigido a login

#### CU-04: Performance con Cache
- ✅ Primera carga consulta API (tiempo: ~200ms)
- ✅ Navegación entre vistas usa cache (tiempo: <10ms)
- ✅ Cache expira después de 5 minutos
- ✅ Recarga forzada ignora cache correctamente

#### CU-05: Responsive Design
- ✅ Desktop (>1024px): Layout horizontal completo
- ✅ Tablet (768-1024px): Grid adaptativo
- ✅ Mobile (320-768px): Layout vertical apilado
- ✅ Avatar se reduce en móvil

---

## 📦 Entregables

### Archivos Backend
1. ✅ `Api/Controllers/PerfilesApiController.cs`
2. ✅ `Services/Perfiles/IPerfilPacienteService.cs`
3. ✅ `Services/Perfiles/PerfilPacienteService.cs`
4. ✅ `Models/DTOs/PerfilPacienteDtos.cs`

### Archivos Frontend
5. ✅ `wwwroot/js/shared/perfil-paciente-service.js`
6. ✅ `wwwroot/css/shared/perfil-paciente-ui.css`
7. ✅ `wwwroot/js/shared/perfil-paciente-ui.js`

### Archivos de Prueba
8. ✅ `test_perfil_paciente.html`
9. ✅ `SmileTrack_MVC.Tests/Services/PerfilPacienteServiceTests.cs`

### Documentación
10. ✅ `Documentacion/INTEGRACION_PERFIL_PACIENTE.md`
11. ✅ `Documentacion/API_PERFIL_PACIENTE.md`

---

## ✅ Criterios de Aceptación

### Funcionalidad
- [ ] API devuelve datos correctos para paciente autenticado
- [ ] Filtrado por IdPaciente se aplica automáticamente
- [ ] Cache funciona correctamente (5 min de duración)
- [ ] Invalidación de cache tras edición de perfil
- [ ] Componente UI se renderiza sin errores
- [ ] Responsive funciona en todos los breakpoints

### Seguridad
- [ ] Documento se muestra parcialmente oculto
- [ ] No se exponen datos de otros pacientes
- [ ] CSRF token se incluye en todas las peticiones
- [ ] Sesión expirada maneja correctamente
- [ ] Info médica requiere autenticación explícita

### Performance
- [ ] Primera carga < 500ms
- [ ] Navegación con cache < 50ms
- [ ] Tamaño de payload < 30KB
- [ ] No hay memory leaks en JavaScript

### Accesibilidad
- [ ] ARIA labels en todos los elementos
- [ ] Navegación con teclado funcional
- [ ] Alto contraste cumple WCAG 2.1 AA
- [ ] Screen readers pueden leer contenido

### UX
- [ ] Transiciones suaves (300ms)
- [ ] Loading states visibles
- [ ] Errores con mensajes claros
- [ ] Consistencia visual entre vistas

---

## 🚀 Plan de Implementación

### Sprint 1: Backend (3 días)
- Día 1: DTOs y interfaces
- Día 2: Servicio y lógica de negocio
- Día 3: Controller y endpoints API

### Sprint 2: Frontend Core (3 días)
- Día 1: Servicio JavaScript
- Día 2: Componente UI base
- Día 3: Estilos CSS y responsive

### Sprint 3: Integración (2 días)
- Día 1: Integrar en st-pac-01 y st-pac-03
- Día 2: Integrar en st-pac-02 y st-pac-04

### Sprint 4: Testing (2 días)
- Día 1: Pruebas unitarias backend
- Día 2: Pruebas de integración y E2E

### Sprint 5: Refinamiento (2 días)
- Día 1: Correcciones de bugs
- Día 2: Optimizaciones y documentación

**Total:** 12 días hábiles

---

## 📞 Contacto y Soporte

**Equipo Responsable:** Backend & Frontend SmileTrack  
**Prioridad:** Alta  
**Fecha Límite:** 30 de septiembre de 2026  
**Estado:** 📋 Pendiente de Implementación

---

**Última actualización:** 16 de septiembre de 2026  
**Versión del documento:** 1.0
