# Matriz de permisos y catálogo — MCA

## Resumen por rol

| Rol | Conocido | Ejemplos | Restricciones |
|---|---|---|---|
| Administrador | Usuarios, roles, reportes, facturación, profesionales, pacientes, configuración, PQR, soporte | Gestión de usuarios, odontología administrativa | Sin enlaces de información ajena |
| Recepcionista | Citas, pacientes, facturación, PQR, perfil, historial autorizado | Registrar paciente, generar factura | No recibe administración de roles |
| Profesional | Agenda, pacientes, historia clínica, odontograma, tratamientos, reportes propios | Revisar odontograma | No recibe gestión administrativa de usuarios |
| Auxiliar | Agenda de apoyo, preparación de consulta, control postoperatorio, citas finalizadas | Preparar consulta | No recibe facturación ni gestión clínica profesional completa |
| Paciente | Mis citas, historial propio, perfil, PQR, tickets propios | Cambiar cita solicitada | No recibe enlaces administrativos ni clínicos ajenos |

Entradas autorizadas por rol (conteo real del catálogo):

| Rol | Entradas |
|---|---|
| Administrador | 38 |
| Recepcionista | 29 |
| Profesional | 27 |
| Auxiliar | 22 |
| Paciente | 18 |

## Métricas del catálogo

| Métrica | Valor | Nota |
|---|---|---|
| `EntryCount` | 75 | Total de entradas |
| `ModuleCount` | 14 | Módulos distintos |
| `ViewCount` | 53 | Rutas únicas |
| `ProcedureCount` | 21 | Entradas con pasos |
| `QuestionCount` | 66 | Preguntas frecuentes catalogadas |
| `EntriesSnapshot.Count` | 75 | Coincide con `EntryCount` |

## Desglose por módulo

| Módulo | Entradas | Páginas | Procedimientos |
|---|---|---|---|
| Gestión de Citas | 19 | 15 | 4 |
| Historia Clínica | 10 | 7 | 3 |
| Centro de Ayuda | 6 | 4 | 2 |
| Gestión de Pacientes | 6 | 4 | 2 |
| Gestión de Profesionales | 6 | 4 | 2 |
| Acceso y Seguridad | 5 | 4 | 1 |
| Perfil | 5 | 4 | 1 |
| Facturación y Pagos | 4 | 2 | 2 |
| Servicios y Recursos | 4 | 3 | 1 |
| Gestión de PQR | 3 | 2 | 1 |
| Reportes | 3 | 2 | 1 |
| Inicio | 2 | 2 | 0 |
| Ayuda General | 1 | 0 | 1 |
| Reportes y Analítica | 1 | 1 | 0 |
| **Total** | **75** | **54** | **21** |

## Clasificación de entradas

- **Entradas informativas/contextuales (54 sin pasos).** Sirven como ancla de la vista
  actual y describen el propósito de la pantalla para la ayuda contextual.
- **Procedimientos detallados (21 con pasos).** Aportan instrucciones operativas
  verificables: `appointment-guide-steps`, `appointment-patient-change`,
  `appointment-reception-cancel`, `professional-registration`, `professional-absence`,
  `support-faq-attachments`, `support-ticket-status`, `patient-registration-help`,
  `patient-search-help`, `clinical-history-help`, `odontogram-help`,
  `treatment-plan-help`, `assistant-preparation-help`, `billing-admin-help`,
  `billing-reception-help`, `reports-admin-help`, `pqr-help`, `profile-help`,
  `services-help`, `users-help`, `save-error-help`.
- **Preguntas frecuentes:** 22 entradas con preguntas catalogadas (66 preguntas en total).

## Conciliación del conteo "53"

El conteo de 53 corresponde a las **rutas únicas** (`ViewCount`), no al número de
entradas de página. Hay 54 entradas sin pasos, pero dos de ellas comparten la misma
ruta (`home-professional` y `professional-dashboard` apuntan ambas a
`/gestion-de-profesionales/st-odo-01-dashboard`), por lo que 54 entradas de página
ocupan 53 rutas. No hay duplicados contabilizados como funcionalidades distintas:
las entradas que comparten ruta son guías/procedimientos específicos del mismo
módulo, no pantallas diferentes.

## Evaluación de suficiencia (Sección 7)

- No se detectaron duplicados que inflen el conteo: cada entrada tiene `Id` único y
  un propósito distinto.
- Las entradas sin pasos son anclas de navegación con una descripción breve; la
  orientación operativa se concentra en los 21 procedimientos, que cubren las
  operaciones más relevantes de cada módulo (citas, pacientes, historia clínica,
  profesionales, facturación, PQR, perfil, servicios, usuarios y errores de guardado).
- No se convirtieron las entradas de página restantes en procedimientos detallados
  porque no se dispone de evidencia en el código para instrucciones paso a paso de
  cada pantalla; convertirlas habría implicado inventar funcionalidades.
- Identificadores, rutas y roles se tomaron del propio catálogo C# y de los
  controladores existentes; no se incorporaron procedimientos no encontrados en el
  código.

Las afirmaciones proceden de las rutas y acciones realmente presentes en controladores y vistas. No se incorporaron procedimientos no encontrados en el código.
