# 🚨 DEUDAS TÉCNICAS Y PROBLEMAS DE CALIDAD DE CÓDIGO
**Análisis original:** 2025-01-22
**Estado reevaluado:** 2026-11-20
**Módulos Analizados:** Gestión de Citas + Gestión de Profesionales

---

> **Vigencia:** las cantidades, severidades, puntuación y estimaciones del documento original no representan el estado actual. Las secciones históricas que siguen contienen hipótesis y ejemplos de 2025; usar como deuda vigente solo lo que aparece en la tabla de estado actual.

## Estado actual verificado

| Tema | Estado al 2026-11-20 | Acción |
|---|---|---|
| Historial de estados de citas | Parcialmente mitigado: historial y cambio se guardan en la misma operación en los flujos principales; hay prueba ante fallo de guardado | Verificar cualquier nuevo flujo que cambie estado |
| Transiciones de estado | `EstadoCitaHelper` centraliza las reglas activas | No reemplazar por las clases de estado no integradas sin completar el modelo y las pruebas |
| Horarios profesionales | Se rechazan bloques solapados y se permiten bloques consecutivos | Mantener pruebas de límites |
| Disponibilidad profesional | Sin horario individual, no se ofrecen nuevas citas | Probar rutas de disponibilidad y agendamiento conjuntamente |
| Especialidades / servicios | Admite varias especialidades, valor predeterminado y servicios derivados de especialidades | Revisar consistencia ante cambios funcionales |
| Ausencias | Consulta, CRUD y reasignación automática separadas en `ProfesionalAusenciaService`; se mantienen validación de rangos, advertencias, notificación e historial | Mantener pruebas de rango/solapamiento y confirmar cambios de política con producto |
| Cancelación | Motivo requerido; se intenta correo sin revertir si falla | Política confirmada: Profesional solo cancela citas propias; alinear RF/CU |
| Lista de espera | Asignación automática al primer candidato compatible | Política confirmada y aclarada en RF-24; sin aprobación intermedia de Recepción |
| Auditoría general de citas | Creación, actualización/reagenda, cambio de estado, cancelación, solicitud, confirmación/rechazo y asignación automática desde lista de espera registran desde el servicio; cambios de cita guardan snapshots e historial cuando corresponde, sin duplicados de controlador. SQL Server valida rollback/persistencia del cambio de estado | Revisar eventos informativos de fondo; probar transacciones de otros flujos |
| Controladores/servicios extensos | Deuda de mantenibilidad, no fallo funcional demostrado por el tamaño; dashboards, horarios/ausencias y recordatorios ya se separaron | Continuar con pruebas de caracterización por caso de uso |
| Riesgos SQLi / BCrypt / CSRF | Las afirmaciones antiguas no están demostradas con la evidencia citada | No registrar como vulnerabilidades sin hallazgo reproducible |

No se conserva un total de deudas, puntuación, costo ni número de horas: no hay una medición vigente y reproducible.

---

## 🔴 DEUDAS TÉCNICAS CRÍTICAS

### DT-C01: Controlador GestionCitasController Monolítico

**Archivo:** `Controllers/GestionDeCitas/GestionCitasController.cs`
**Líneas (medición al 2026-11-20):** 4006
**Severidad:** 🔴 CRÍTICA
**Tipo:** Architecture Smell

#### Problema Detallado

El controller maneja **6 responsabilidades distintas** en un único archivo:

```
GestionCitasController (4974 líneas)
├─ 1. Dashboard & Métricas        (~800 líneas)
├─ 2. CRUD Citas                  (~1200 líneas)
├─ 3. Validaciones de Conflictos  (~600 líneas)
├─ 4. Gestión de Horarios         (~500 líneas)
├─ 5. Recordatorios               (~300 líneas)
├─ 6. Reportes & Análisis         (~574 líneas)
└─ [Métodos Privados]             (~1000 líneas)
```

#### Impacto

| Aspecto | Impacto | Severidad |
|---------|---------|-----------|
| **Mantenibilidad** | Cambio pequeño → risk de side effects | 🔴 CRÍTICA |
| **Testabilidad** | 4974 líneas = difícil extraer lógica | 🔴 CRÍTICA |
| **Onboarding** | Nuevo dev tarda semanas | 🟠 ALTA |
| **Cambios Concurrentes** | Conflictos merge garantizados | 🟠 ALTA |
| **Performance** | Compilation lenta (4974 líneas) | 🟡 MEDIA |

#### Código Problemático

```csharp
// En GestionCitasController.cs
public async Task<IActionResult> CrearCita(CitaApiRequest dto)
{
	// Dashboard logic
	var estadisticas = await _citasDashboardService.ObtenerDashboardAsync(...);

	// Validations
	var conflicto = await ValidarConflictos(...);

	// State transitions
	cita.IdEstado = 2; // Magic number!

	// Audit manually
	var historial = new CitaHistorialEstado { ... };

	// Email notification
	await _emailService.EnviarAsync(...);

	// Recordatorio scheduling
	var recordatorio = new RecordatorioCita { ... };

	// Multiple SaveChanges
	await _context.SaveChangesAsync(); // #1
	await _context.SaveChangesAsync(); // #2 - ¿Y si #2 falla?
}
```

#### Solución Propuesta

**Dividir en múltiples controllers pequeños:**

```
CitasDashboardController (~300 líneas)
CitasOperacionesController (~800 líneas)
CitasValidacionesController (~400 líneas)
CitasEstadoController (~300 líneas)
CitasRecordatoriosController (~200 líneas)
CitasReportesController (~400 líneas)
```

**Estimación:** 40 horas de refactoring

---

### DT-C02: Estado de Citas sin State Machine Pattern

**Archivo:** `Models/Entities/GestionDeCitas/Cita.cs`
**Línea Problemática:** ~35-40 (IdEstado como int)
**Severidad:** 🔴 CRÍTICA
**Tipo:** Logic Smell

#### Problema Detallado

```csharp
// ACTUAL (error-prone):
public class Cita
{
	public int IdEstado { get; set; } // 1 = Programada, 2 = Confirmada, etc.
}

// USO EN CONTROLLER:
if (cita.IdEstado == 1) // Es programada?
{
	cita.IdEstado = 2; // Cambiar a confirmada
}
else if (cita.IdEstado == 2) // Es confirmada?
{
	// ¿Puede cambiar a atendida? ¿O debe pasar por "En curso" primero?
	_context.Citas.Update(cita); // Guardar sin validación
}
```

#### Problemas Derivados

| Problema | Ejemplo | Riesgo |
|----------|---------|--------|
| **Magic Numbers** | if (cita.IdEstado == 1) | Confusión de estados |
| **Sin Validación** | Transición Programada → Cancelada directa | Datos inconsistentes |
| **Duplicación** | Mismo if/else en 3 controladores | Bugs en cascada |
| **Sin Audit** | isEstado cambió pero no hay trace | Compliance issues |
| **Sin Reglas** | ¿Puede registrar cita si está "Cancelada"? | Logic bugs |

#### Flujo Real vs Esperado

```
ESTADO ACTUAL (Sin máquina estados):
Programada → Confirmada → Atendida → Finalizada
		   ↘         ↙ (conflictivo)
		   Cancelada (en cualquier momento)

ESTADO ESPERADO (Con máquina):
		┌──────────────────┐
		▼                  │
	Programada            │
	├─ Confirmar()       │
	│  ↓                 │
	Confirmada          │
	├─ Rechazar()       │
	│  ↓                │
	Rechazada          │
	├─ Atender()        │
	│  ↓                │
	Atendida           │
	├─ Finalizar()     │
	│  ↓               │
	Finalizada        │
	│                 │
	└─ Cancelar() ────  (desde cualquier estado excepto Finalizada)
```

#### Solución Propuesta

```csharp
// Dominio - types.cs
public abstract record EstadoCita
{
	public abstract EstadoCita Confirmar(int idOdontologo);
	public abstract EstadoCita Rechazar(string motivo);
	public abstract EstadoCita Cancelar(string motivo);
	public abstract bool EsProlongable { get; }
}

public sealed record CitaProgramada : EstadoCita
{
	public override EstadoCita Confirmar(int idOdontologo)
	{
		if (idOdontologo <= 0) throw new ArgumentException("Odontólogo inválido");
		return new CitaConfirmada(idOdontologo);
	}

	public override EstadoCita Rechazar(string motivo)
	{
		if (string.IsNullOrEmpty(motivo)) throw new ArgumentException("Motivo requerido");
		return new CitaRechazada(motivo);
	}

	public override bool EsProlongable => false;
}

public sealed record CitaConfirmada(int IdOdontologo) : EstadoCita
{
	public override EstadoCita Confirmar(int idOdontologo)
	{
		throw new InvalidOperationException("Cita ya confirmada");
	}

	public override bool EsProlongable => true;
}

// USO (type-safe):
var cita = new Cita { Estado = new CitaProgramada() };
cita.Estado = cita.Estado.Confirmar(5); // Type-safe, validado

// Imposible esto:
cita.Estado = cita.Estado.Confirmar(-1); // Compile error o runtime exception
```

**Estimación:** 25 horas (análisis de cambios + implementación)

---

### DT-C03: AppDbContext Monolítico (1346 líneas)

**Archivo:** `Data/AppDbContext.cs`
**Líneas:** 1346
**Severidad:** 🔴 CRÍTICA
**Tipo:** Class Size Smell

#### Problema

```csharp
public class AppDbContext : DbContext
{
	public DbSet<Usuario> Usuarios { get; ... }
	public DbSet<Cita> Citas { get; ... }
	// ... 70+ DbSets

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		// [AQUÍ ESTÁN TODAS LAS 1200+ LÍNEAS DE CONFIGURACIÓN]
		modelBuilder.Entity<Cita>()...
		modelBuilder.Entity<Profesional>()...
		// etc... sin separación
	}
}
```

#### Impacto

- **Compilation:** Lenta (1346 líneas procesadas)
- **Readability:** Imposible encontrar configuración de una entidad
- **Maintenance:** Cambio en Cita afecta toda la clase
- **Testing:** No se puede mockear diferentes OnModelCreating behaviors

#### Solución Propuesta

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
	ConfigureCitasEntities(modelBuilder);
	ConfigureProfesionalesEntities(modelBuilder);
	ConfigurePacientesEntities(modelBuilder);
	ConfigurePqrEntities(modelBuilder);
	ConfigureFacturacionEntities(modelBuilder);
	ConfigureAuditoriaEntities(modelBuilder);
}

private void ConfigureCitasEntities(ModelBuilder modelBuilder)
{
	modelBuilder.Entity<Cita>(entity =>
	{
		entity.HasKey(e => e.IdCita);
		entity.Property(e => e.FechaHora).IsRequired();
		entity.HasOne(e => e.Profesional)
			.WithMany(p => p.Citas)
			.HasForeignKey(e => e.IdProfesional);
		// ... todas las configs de Cita
	});

	modelBuilder.Entity<EstadoCita>()...
	modelBuilder.Entity<RecordatorioCita>()...
	modelBuilder.Entity<ListaEsperaCita>()...
}
```

**Estimación:** 20 horas

---

### DT-C04: Program.cs Configuración Dispersa (1370 líneas)

**Archivo:** `Program.cs`
**Líneas:** 1370
**Severidad:** 🔴 CRÍTICA
**Tipo:** Setup Code Smell

#### Problemas

1. **Lógica condicional sin separación:**
   ```csharp
   // Lines 45-60: appsettings loading
   // Lines 65-80: conditional user secrets
   // Lines 85-120: database config
   // Lines 125-200: auth config
   // Lines 205-300: services registration
   // Lines 305-400: middleware setup
   ```

2. **Servicios sin categorización:**
   ```csharp
   builder.Services.AddScoped<ICitaService, CitaService>();
   builder.Services.AddScoped<IProfesionalService, ProfesionalService>();
   builder.Services.AddScoped<IAgendaService, AgendaService>();
   // ... 30 servicios sin orden
   ```

#### Solución Propuesta

```csharp
// Program.cs - Solo startup (~80 líneas)
var builder = WebApplication.CreateBuilder(args);

builder.Services
	.AddConfigurationServices(builder)
	.AddDatabaseServices(builder.Configuration)
	.AddAuthenticationServices(builder.Configuration)
	.AddDomainServices()
	.AddInfrastructureServices()
	.AddMiddlewareServices();

var app = builder.Build();
app.UseConfiguredMiddleware();
app.Run();

// Extensions/ConfigurationServiceCollectionExtensions.cs
public static class ConfigurationServiceCollectionExtensions
{
	public static IServiceCollection AddConfigurationServices(
		this IServiceCollection services, WebApplicationBuilder builder)
	{
		builder.Configuration.Sources.Clear();
		builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
		// ...
		return services;
	}
}

// Extensions/CitasServiceCollectionExtensions.cs
public static class CitasServiceCollectionExtensions
{
	public static IServiceCollection AddCitasServices(this IServiceCollection services)
	{
		services.AddScoped<ICitaService, CitaService>();
		services.AddScoped<IAgendaService, AgendaService>();
		services.AddScoped<ICitasDashboardService, CitasDashboardService>();
		services.AddScoped<IRecordatorioCitaService, RecordatorioCitaService>();
		return services;
	}
}
```

**Estimación:** 15 horas

---

### DT-C05: Auditoría Manual y No Automática

**Archivo:** `Controllers/GestionDeCitas/GestionCitasController.cs`
**Línea Problemática:** ~2500+ (múltiples métodos)
**Severidad:** 🔴 CRÍTICA
**Tipo:** Data Integrity Risk

#### Estado actual (2026-11-20)

`CitaService` centraliza la auditoría de creación, actualización/reagenda, cambio de estado, cancelación, solicitud del paciente, confirmación/rechazo y asignación automática desde lista de espera. En los cambios de estado guarda el historial y el registro general con snapshots en el mismo guardado. MVC/API propagan actor e IP y ya no registran una segunda auditoría en las rutas unificadas. La descripción se limita a los 255 caracteres del esquema y los snapshots preservan los datos completos.

La deuda sigue **parcialmente resuelta**: SQL Server confirma el rollback de estado, historial y auditoría ante fallo de FK y su persistencia en el camino válido. Creación y asignación automática usan dos `SaveChangesAsync` dentro de la transacción serializable para obtener el ID antes de escribir la auditoría; esos y los demás flujos deben probarse también contra SQL Server. El envío de recordatorios registra el resultado del evento, pero no un snapshot de cita ni un actor humano; debe revisarse por separado.

#### Problema

```csharp
// Manual auditing - fácil olvidar:
public async Task CancelarCita(int id, CitaCancelacionDto dto)
{
	var cita = await _context.Citas.FindAsync(id);
	cita.IdEstado = EstadoCita.Cancelada;

	// Auditoría manual (¿qué si se olvida?)
	var historial = new CitaHistorialEstado
	{
		IdCita = id,
		IdEstado = EstadoCita.Cancelada,
		Motivo = dto.Motivo,
		IdUsuario = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
		FechaCambio = DateTime.Now
	};
	_context.CitasHistorialEstado.Add(historial);
	await _context.SaveChangesAsync();
}

// ¿Y si alguien modifica Cita sin crear historial?
```

#### Riesgos

1. **Compliance:** GDPR/PIPEDA requieren auditoría automática
2. **Data Integrity:** Estados inconsistentes con historial
3. **Debugging:** ¿Quién cambió la cita? ¿Cuándo? ¿Por qué?

#### Solución Propuesta

```csharp
// Atributo personalizado
[AttributeUsage(AttributeTargets.Method)]
public class AuditableAttribute : Attribute
{
	public string Accion { get; set; }
}

// Interceptor automático
public class AuditingInterceptor : SaveChangesInterceptor
{
	public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
		DbContextEventData eventData,
		InterceptionResult<int> result,
		CancellationToken cancellationToken = default)
	{
		var httpContext = _httpContextAccessor.HttpContext;
		var userId = httpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

		foreach (var entry in eventData.Context.ChangeTracker.Entries())
		{
			if (entry.Entity is ICitaHistoryable historyable)
			{
				var historial = new CitaHistorialEstado
				{
					IdCita = entry.Property("IdCita").CurrentValue as int? ?? 0,
					IdUsuario = userId,
					FechaCambio = DateTime.Now,
					Accion = entry.State.ToString()
				};
				eventData.Context.Set<CitaHistorialEstado>().Add(historial);
			}
		}

		return result;
	}
}

// Registro
services.AddScoped<AuditingInterceptor>();
options.AddInterceptors(serviceProvider.GetRequiredService<AuditingInterceptor>());
```

**Estimación:** 35 horas

---

## 🟠 DEUDAS TÉCNICAS ALTAS

### DT-A01: Sin Validación de Solapamientos en Horarios

**Archivo:** `Models/Entities/GestionDeProfesionales/HorarioProfesional.cs`
**Severidad:** 🟠 ALTA
**Tipo:** Logic Bug

**Problema:**
```csharp
// Profesional puede tener:
// Lunes: 8:00-12:00
// Lunes: 10:00-14:00 ← SOLAPAMIENTO NO DETECTADO
```

**Impacto:** Overbooking, conflictos de reserva

**Solución:**
```csharp
public class HorarioProfesionalValidator : AbstractValidator<HorarioProfesional>
{
	public HorarioProfesionalValidator(IHorarioRepository repo)
	{
		RuleFor(h => h.HoraFin)
			.GreaterThan(h => h.HoraInicio)
			.WithMessage("Hora fin debe ser después de hora inicio");

		RuleFor(h => h)
			.MustAsync(async (horario, ct) =>
			{
				var solapados = await repo.ObtenerSolapados(horario);
				return !solapados.Any();
			})
			.WithMessage("Horario se solapa con otro existente");
	}
}
```

**Estimación:** 8 horas

---

### DT-A02: Reasignación Manual de Citas por Ausencia

**Archivo:** `Models/Entities/GestionDeProfesionales/AusenciaProfesional.cs`
**Severidad:** 🟠 ALTA
**Tipo:** UX/Usability

**Problema:**
```
Cuando Profesional se marca EN AUSENCIA:
├─ ¿Qué pasa con citas agendadas?
│ └─ Opción 1: Se auto-cancelan (mala UX)
│ └─ Opción 2: Se notifica admin manualmente (workaround)
│ └─ Opción 3: Nada (ACTUAL - ERROR)
└─ ¿Cómo se reasignan a otro profesional?
   └─ Manualmente (tedioso y error-prone)
```

**Solución Propuesta:**
```csharp
public class AusenciaProfesionalService
{
	public async Task CrearAusenciaAsync(AusenciaProfesional ausencia)
	{
		// 1. Guardar ausencia
		_context.AusenciasProfesional.Add(ausencia);

		// 2. Obtener citas impactadas
		var citasConflictivas = await _context.Citas
			.Where(c => c.IdProfesional == ausencia.IdProfesional)
			.Where(c => c.FechaHora >= ausencia.FechaInicio && c.FechaHora < ausencia.FechaFin)
			.ToListAsync();

		// 3. Auto-reasignar a profesionales alternativos
		foreach (var cita in citasConflictivas)
		{
			var alternativa = await _context.Profesionales
				.Where(p => p.IdEspecialidad == cita.Profesional.IdEspecialidad)
				.Where(p => p.IdProfesional != ausencia.IdProfesional)
				.Where(p => !p.AusenciasProfesional.Any(a =>
					a.FechaInicio <= cita.FechaHora && cita.FechaHora < a.FechaFin))
				.FirstOrDefaultAsync();

			if (alternativa != null)
			{
				cita.IdProfesional = alternativa.IdProfesional;
				await _emailService.NotificarReasignacionAsync(cita, alternativa);
			}
			else
			{
				// Sin alternativa: notificar a admin
				await _emailService.NotificarSinAlternativaAsync(cita);
			}
		}

		await _context.SaveChangesAsync();
	}
}
```

**Estimación:** 20 horas

---

### DT-A03: DTOs Duplicados (CitaViewModel vs CitaApiRequest)

**Archivos:**
- `Models/ViewModels/GestionDeCitas/CitaViewModel.cs`
- `Models/DTOs/GestionDeCitas/CitaApiRequest.cs`

**Severidad:** 🟠 ALTA
**Tipo:** Maintainability Smell (violates DRY)

**Problema:**
```csharp
// CitaViewModel (para MVC)
public class CitaViewModel
{
	public int IdCita { get; set; }
	public int IdPaciente { get; set; }
	public int? IdProfesional { get; set; }
	public DateTime FechaHora { get; set; }
	// ... etc
}

// CitaApiRequest (para API)
public class CitaApiRequest
{
	public int IdCita { get; set; }
	public int IdPaciente { get; set; }
	public int? IdProfesional { get; set; }
	public DateTime FechaHora { get; set; }
	// ... idéntico
}
```

**Impacto:**
- Cambios en un lugar olvidado en otro
- Confusión en desarrollo
- Doble mantenimiento

**Solución:**
```csharp
// Crear carpeta Shared/Dtos
public class CitaCreateDto
{
	[Required]
	public int IdPaciente { get; set; }

	[Required]
	public DateTime FechaHora { get; set; }

	public int? IdProfesional { get; set; }
}

public class CitaUpdateDto : CitaCreateDto
{
	[Required]
	public int IdCita { get; set; }
}

// Usar en ambos MVC y API
// En Controller MVC:
public IActionResult Crear(CitaCreateDto dto) { }

// En ApiController:
[HttpPost]
public async Task<IActionResult> Post(CitaCreateDto dto) { }
```

**Estimación:** 10 horas

---

## 🟡 DEUDAS TÉCNICAS MEDIAS

### DT-M01: Métricas de Profesionales No Implementadas

**Archivo:** `Services/GestionDeProfesionales/ProfesionalService.cs`
**Severidad:** 🟡 MEDIA
**Tipo:** Feature Gap

**Problema:**
```
Dashboard de Profesionales falta:
├─ Tasa de asistencia (%)
├─ Tiempo promedio de cita (min)
├─ Satisfacción promedio (score)
├─ Comisión generada (balance)
├─ Citas completadas (trend)
└─ Rating pacientes (avg)
```

**Solución Propuesta:**

```csharp
public interface IProfesionalMetricaService
{
	Task<ProfesionalMetricasDto> ObtenerMetricasAsync(int idProfesional, DateTime? desde, DateTime? hasta);
}

public class ProfesionalMetricasDto
{
	public decimal TasaAsistencia { get; set; } // (Asistidas / Total) * 100
	public int TiempoPromedioCita { get; set; } // minutos
	public decimal SatisfaccionPromedio { get; set; } // 1-5
	public decimal ComisionGenerada { get; set; } // cantidad
	public int CitasCompletadas { get; set; }
	public int CitasCanceladas { get; set; }
	public int CitasNoAtendidas { get; set; }
}

// Implementación
public class ProfesionalMetricaService : IProfesionalMetricaService
{
	public async Task<ProfesionalMetricasDto> ObtenerMetricasAsync(int idProfesional, DateTime? desde, DateTime? hasta)
	{
		desde ??= DateTime.Now.AddMonths(-1);
		hasta ??= DateTime.Now;

		var citas = await _context.Citas
			.Where(c => c.IdProfesional == idProfesional)
			.Where(c => c.FechaHora >= desde && c.FechaHora <= hasta)
			.Include(c => c.Calificaciones)
			.ToListAsync();

		var total = citas.Count;
		var completadas = citas.Count(c => c.IdEstado == EstadoCita.Finalizada);
		var canceladas = citas.Count(c => c.IdEstado == EstadoCita.Cancelada);
		var noAtendidas = citas.Count(c => c.IdEstado == EstadoCita.NoAtendida);

		return new ProfesionalMetricasDto
		{
			TasaAsistencia = total > 0 ? ((double)(completadas + noAtendidas) / total) * 100 : 0,
			TiempoPromedioCita = citas.Any() ? (int)citas.Average(c => c.DuracionMinutos) : 0,
			SatisfaccionPromedio = citas.Any(c => c.Calificaciones.Any())
				? citas.Where(c => c.Calificaciones.Any()).Average(c => c.Calificaciones.Average(cal => cal.Puntuacion))
				: 0,
			ComisionGenerada = citas.Sum(c => c.ComisionCalculada),
			CitasCompletadas = completadas,
			CitasCanceladas = canceladas,
			CitasNoAtendidas = noAtendidas
		};
	}
}
```

**Estimación:** 12 horas

---

### DT-M02: Email Hardcoded - Necesita Strategy Pattern

**Archivo:** `Services/GestionDeCitas/RecordatorioCitaService.cs`
**Severidad:** 🟡 MEDIA
**Tipo:** Inflexibility

**Problema:**
```csharp
// Solo Email implementado:
await _emailService.EnviarAsync(email, asunto, body);

// ¿Y si paciente prefiere SMS? ¿WhatsApp? ¿Llamada?
```

**Solución:**
```csharp
public interface INotificationStrategy
{
	Task SendAsync(string recipient, string message);
}

public class EmailNotification : INotificationStrategy
{
	public async Task SendAsync(string email, string message)
	{
		await _emailService.SendAsync(...);
	}
}

public class SmsNotification : INotificationStrategy
{
	public async Task SendAsync(string phone, string message)
	{
		// Usar Twilio/AWS SMS
	}
}

// Factory
public class NotificationFactory
{
	public INotificationStrategy CreateStrategy(NotificationChannel channel)
		=> channel switch
		{
			NotificationChannel.Email => new EmailNotification(),
			NotificationChannel.Sms => new SmsNotification(),
			NotificationChannel.WhatsApp => new WhatsAppNotification(),
			_ => throw new NotSupportedException()
		};
}
```

**Estimación:** 15 horas

---

### DT-M03: Falta de Error Handling Centralizado

**Archivo:** Múltiples (Controllers y Services)
**Severidad:** 🟡 MEDIA
**Tipo:** Cross-Cutting Concern

**Problema:**
```csharp
// Manejo disperso:
try
{
	var cita = await _citaService.CrearAsync(dto);
	return Ok(cita);
}
catch (Exception ex)
{
	_logger.LogError(ex, "Error creando cita");
	return StatusCode(500, "Error interno");
}

// ¿Qué tipo de exception? ¿Validation? ¿Database? ¿Business?
```

**Solución:**
```csharp
// Global exception middleware
public class GlobalExceptionMiddleware
{
	public async Task InvokeAsync(HttpContext context, ILogger<GlobalExceptionMiddleware> logger)
	{
		try
		{
			await _next(context);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Unhandled exception");

			context.Response.ContentType = "application/json";
			var response = new ApiErrorResponse
			{
				Message = ex.Message,
				Type = ex.GetType().Name,
				StatusCode = context.Response.StatusCode
			};

			context.Response.StatusCode = ex switch
			{
				ValidacionException => StatusCodes.Status400BadRequest,
				EntidadNoEncontradaException => StatusCodes.Status404NotFound,
				NoAutorizadoException => StatusCodes.Status403Forbidden,
				_ => StatusCodes.Status500InternalServerError
			};

			await context.Response.WriteAsJsonAsync(response);
		}
	}
}

// Excepciones de dominio
public class ValidacionException : Exception { }
public class EntidadNoEncontradaException : Exception { }
public class NoAutorizadoException : Exception { }
```

**Estimación:** 12 horas

---

## 🟢 DEUDAS TÉCNICAS BAJAS

### DT-B01: Caché sin TTL Explícito

**Archivo:** `Controllers/GestionDeCitas/GestionCitasController.cs`
**Severidad:** 🟢 BAJA
**Tipo:** Performance

**Problema:**
```csharp
// Sin control de expiración:
_memoryCache.Set("duracion", duracion);

// ¿Cuándo expira? ¿Nunca?
```

**Solución:**
```csharp
public class CacheKeys
{
	public const string CitaDuracion = "cita_duracion_minutos";
	public static TimeSpan CitaDuracionTtl = TimeSpan.FromHours(1);

	public const string ProfesionalesDisponibles = "profesionales_disponibles_{0}";
	public static TimeSpan ProfesionalesDisponiblesTtl = TimeSpan.FromMinutes(15);
}

// Uso:
_memoryCache.Set(
	CacheKeys.CitaDuracion,
	duracion,
	new MemoryCacheEntryOptions
	{
		AbsoluteExpirationRelativeToNow = CacheKeys.CitaDuracionTtl
	});

// Invalidación:
_memoryCache.Remove(CacheKeys.CitaDuracion);
```

**Estimación:** 4 horas

---

### DT-B02: Validación Email Corporativo Faltante

**Archivo:** `Controllers/GestionDeProfesionales/ProfesionalesController.cs`
**Severidad:** 🟢 BAJA (pero security concern)
**Tipo:** Validation Gap

**Problema:**
```csharp
// Sin validar dominio corporativo:
profesional.Email = "unknown@gmail.com"; // ¿Permitido?
```

**Solución:**
```csharp
public class ProfesionalValidator : AbstractValidator<ProfesionalCreateDto>
{
	public ProfesionalValidator(IConfiguration config)
	{
		var dominioValido = config["Security:CorpoEmailDomain"];

		RuleFor(p => p.Email)
			.EmailAddress()
			.Must(email => email.EndsWith($"@{dominioValido}"))
			.WithMessage($"Email debe pertenecer al dominio {dominioValido}");
	}
}
```

**Estimación:** 3 horas

---

## 🔐 ISSUES DE SEGURIDAD

### SEC-01: Inyección SQL Potencial en Queries LINQ

**Severidad:** 🔴 CRÍTICA
**Tipo:** Security Vulnerability

**Problema:**
```csharp
// PELIGROSO si search viene de usuario sin sanitizar:
var citas = _context.Citas
	.Where(c => c.Paciente.Nombre.Contains(busqueda)) // OK en LINQ
	.ToListAsync();

// Pero si se traduce a SQL string:
var sql = $"SELECT * FROM Cita WHERE Paciente.Nombre LIKE '%{busqueda}%'"; // SQL injection
```

**Solución:**
```csharp
// Ya está seguro con LINQ a entidades:
var citas = _context.Citas
	.Where(c => c.Paciente.Nombre.Contains(busqueda)) // EF traduce a parámetro SQL
	.ToListAsync();

// Verificar: nunca usar string interpolation con FromSqlInterpolated
// ✅ Correcto:
var citas = _context.Citas
	.FromSqlInterpolated($"SELECT * FROM Cita WHERE Nombre LIKE {busqueda}");

// ❌ INCORRECTO:
var citas = _context.Citas
	.FromSql($"SELECT * FROM Cita WHERE Nombre LIKE '%{busqueda}%'");
```

**Acción:** Realizar auditoría de todos los `.FromSql()` + `.ExecuteSqlRaw()`

**Estimación:** 10 horas de auditoría

---

### SEC-02: Password Hashing Claro pero sin Salting Explícito

**Severidad:** 🟠 ALTA
**Tipo:** Cryptography

**Archivo:** `Services/AuthService.cs`
**Problema:**
```csharp
// BCrypt incluye salt, pero verificar que se usa correctamente:
string hash = BCrypt.Net.BCrypt.HashPassword(password);
bool esValido = BCrypt.Net.BCrypt.Verify(password, hash);
```

**Acción:** Auditar todas las líneas de BCrypt + documentar configuración `workFactor`

---

### SEC-03: CSRF Token en Formularios MVC

**Severidad:** 🟠 ALTA
**Tipo:** Cross-Site Request Forgery

**Problema:**
```html
<!-- En Razor: ¿Tiene @Html.AntiForgeryToken()? -->
<form method="POST" action="/citas/crear">
	<!-- MISSING ANTIFORGERY TOKEN -->
	<input name="IdPaciente" />
</form>
```

**Acción:** Verificar todos los `<form POST>` + tests de CSRF

**Estimación:** 8 horas

---

> **Las secciones siguientes son el snapshot histórico de 2025.** Sus estados TODO, severidades, costos y prioridades no son el backlog vigente. Para el estado actual, prevalece la tabla “Estado actual verificado” al inicio y el [plan ejecutivo](RESUMEN_EJECUTIVO_PLAN_MEJORAS.md).

## 📊 SUMMARY TABLE - Snapshot histórico (no vigente)

| ID | Deuda | Severidad | Area | Estimación | Estado |
|----|-------|-----------|------|------------|--------|
| DT-C01 | Controller 4974 líneas | 🔴 CRÍTICA | Architecture | 40h | 🚨 TODO |
| DT-C02 | State Machine de citas no integrada al flujo actual | 🔴 CRÍTICA | Logic | 25h | ⚠️ PARCIAL |
| DT-C03 | DbContext 1346 líneas | 🔴 CRÍTICA | Architecture | 20h | 🚨 TODO |
| DT-C04 | Program.cs 1370 líneas | 🔴 CRÍTICA | Setup | 15h | 🚨 TODO |
| DT-C05 | Auditoría manual; historial de estado no siempre atómico | 🔴 CRÍTICA | Data Integrity | 35h | ⚠️ PARCIAL |
| DT-A01 | Sin validar solapamientos | 🟠 ALTA | Validation | 8h | 🚨 TODO |
| DT-A02 | Reasignación manual | 🟠 ALTA | UX | 20h | 🚨 TODO |
| DT-A03 | DTOs duplicados | 🟠 ALTA | DRY | 10h | 🚨 TODO |
| DT-M01 | Sin métricas prof | 🟡 MEDIA | Feature | 12h | ⏳ BACKLOG |
| DT-M02 | Email hardcoded | 🟡 MEDIA | Flexibility | 15h | ⏳ BACKLOG |
| DT-M03 | Sin error handling | 🟡 MEDIA | Cross-cutting | 12h | ⏳ BACKLOG |
| DT-B01 | Caché sin TTL | 🟢 BAJA | Performance | 4h | ⏳ BACKLOG |
| DT-B02 | Sin validar email corp | 🟢 BAJA | Security | 3h | ⏳ BACKLOG |
| SEC-01 | SQL Injection risk | 🔴 CRÍTICA | Security | 10h | 🔍 AUDIT |
| SEC-02 | Password hashing | 🟠 ALTA | Crypto | 5h | 🔍 AUDIT |
| SEC-03 | CSRF tokens | 🟠 ALTA | CSRF | 8h | 🔍 AUDIT |

---

## 💰 COSTO TOTAL ESTIMADO (histórico, retirado)

| Categoría | Horas | Semanas |
|-----------|-------|---------|
| **Críticas** | 135h | 3-4 |
| **Altas** | 81h | 2-3 |
| **Medias** | 39h | 1-2 |
| **Bajas** | 7h | 0.5 |
| **Auditorías Seguridad** | 23h | 0.5 |
| **TOTAL** | **285h** | **8-10 semanas** |

---

## 🎯 RECOMENDACIÓN FINAL (histórica, no aplicar automáticamente)

**Prioridad 1 (Próximas 2 semanas):**
- DT-C02 (State Machine) - BLOQUEADOR
- DT-A03 (DTOs) - Quick win
- SEC-01 (SQL Injection audit) - Seguridad

**Prioridad 2 (Semanas 3-4):**
- DT-C01 (Refactor controllers)
- DT-C03 (DbContext)
- DT-A01 (Validaciones)

**Prioridad 3 (Semanas 5-6):**
- DT-C04 (Program.cs)
- DT-A02 (Auto-reasignación)
- DT-C05 (Auditoría)

---

**Documento generado:** 2025-01-22
**Versión:** 1.0
