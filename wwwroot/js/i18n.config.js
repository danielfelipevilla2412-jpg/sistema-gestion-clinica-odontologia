(function () {
  "use strict";

  var DICCIONARIO = {
    es: {
      "sidebar_durante_atencion": "Durante la Atención",
      "nav.group_inicio": "Inicio",
      "nav.group_gestion_citas": "Gestión de Citas",
      "nav.group_historia_clinica": "Historia Clínica",
      "nav.group_gestion_pqr": "Gestión de PQR",
      "nav.group_centro_ayuda": "Centro de Ayuda",
      "nav.item_homepage": "Página Principal",
      "citas.title": "Mis Citas",
      "citas.pac_subtitle": "Gestiona y consulta tus citas odontológicas personales",
      "citas.stat_total": "Total citas",
      "citas.modal_detail_title": "Detalle de Cita",
      "citas.modal_cancel_title": "¿Cancelar esta cita?",
      "citas.btn_confirm_cancel": "Sí, cancelar",
      "citas.modal_new_title": "Solicitar nueva cita",
      "citas.form_preferred_date": "Fecha preferida",
      "citas.form_service_type": "Tipo de servicio",
      "citas.form_notes": "Notas adicionales",
      "citas.form_notes_ph": "Describe tu motivo...",
      "citas.loading_services": "Cargando servicios...",
      "citas.btn_request": "Solicitar",
      "citas.filter_all_status": "Todos los estados",
      "citas.stats_aria": "Resumen de mis citas",
      "citas.table_aria": "Tabla de mis citas odontológicas",
      "citas.checklist_new_item_ph": "Escribir nuevo ítem...",
      "citas.observations_ph": "Ej: Camilla con humedad, se procedió a secar...",
      "dashboard.subtitle_metrics": "Resumen operativo y métricas clave del centro odontológico",
      "dashboard.subtitle_occupancy": "Porcentaje estimado de citas utilizadas este mes",
      "historia.title": "Mi Historial Clínico",
      "pqr.new": "Nueva Solicitud PQR",

      "common_bc_administracion": "Administración",
      "rec01.stats_aria": "Resumen de actividad del día",
      "rec01.stat_today": "Citas hoy",
      "rec01.stat_invoices_pending": "Facturas pendientes",
      "rec01.quick_actions_aria": "Acciones rápidas",
      "rec01.btn_new_patient_aria": "Registrar nuevo paciente en el sistema",
      "rec01.btn_generate_invoice_aria": "Generar nueva factura para paciente",
      "rec01.btn_generate_invoice": "Generar factura",
      "rec01.table_aria": "Listado de citas del día",
      "adm09_bc_admin": "Administración",
      "adm09_bc_aria": "Administración > Gestión de Citas",
      "adm09_bc_active": "Gestión de Citas",
      "gestionprofesionales_breadcrumb_admin": "Administración",
      "gestionprofesionales_breadcrumb_current": "Gestión de Profesionales",
      "reportesclinicos_breadcrumb_admin": "Administración",
      "reportesclinicos_breadcrumb_current": "Reportes Clínicos",
      "gestion_pacientes_breadcrumb_administracion": "Administración",
      "gestion_pacientes_breadcrumb_gestion_pacientes": "Gestión de Pacientes",
      "gestion_pacientes_ruta_navegacion": "Administración -> Gestión de Pacientes",
      "rec01_breadcrumb": "Recepción > Panel",
      "detalle_paciente_breadcrumb_pacientes": "Pacientes",
      "detalle_paciente_ruta_navegacion": "Pacientes > Detalle",
      "layout_skip_to_content": "Saltar al contenido principal",
      "layout_hamburger_open_menu": "Abrir menú",
      "common_hamburger_aria": "Abrir menú de navegación",
      "gestion_pacientes_skip_link": "Saltar al contenido principal",
      "gestion_pacientes_abrir_menu": "Abrir menú de navegación",
      "common_role_admin": "Administrador",
      "common_nav_inicio": "Inicio",
      "common_nav_homepage": "Homepage",
      "common_nav_dashboard": "Dashboard General",
      "common_nav_reportes_clinicos": "Reportes Clínicos",
      "common_nav_acceso_seguridad": "Acceso y Seguridad",
      "common_nav_gestion_usuarios": "Gestión de Usuarios",
      "common_nav_gestion_roles": "Gestión de Roles",
      "common_nav_bitacora": "Bitácora del Sistema",
      "common_nav_gestion_pacientes": "Gestión de Pacientes",
      "common_nav_historia_clinica": "Historia Clínica",
      "common_nav_gestion_profesionales": "Gestión de Profesionales",
      "common_nav_gestion_citas": "Gestión de Citas",
      "common_nav_agenda": "Agenda General",
      "common_nav_gestion_citas_int": "Gestión Integral de Citas",
      "common_nav_facturacion_pagos": "Facturación y Pagos",
      "common_nav_gestion_facturacion": "Gestión de Facturación",
      "common_nav_servicios_recursos": "Servicios y Recursos",
      "common_nav_catalogo_servicios": "Catálogo de Servicios",
      "common_nav_configuracion": "Configuración General",
      "common_nav_cerrar_sesion": "Cerrar sesión",
      "common_nav_logout_aria": "Cerrar sesión",
      "nav.group_mi_dashboard": "Mi Dashboard",
      "nav.group_mi_agenda": "Mi Agenda",
      "nav.group_perfil": "Mi Perfil",
      "nav.group_gestion_ayuda": "Centro de Ayuda",
      "nav.item_mi_dashboard": "Mi Dashboard",
      "nav.item_mi_agenda": "Mi Agenda",
      "nav.item_historia_clinica": "Historia Clínica",
      "nav.item_odontograma": "Odontograma Digital",
      "nav.item_tratamientos": "Seguimiento de Tratamientos",
      "nav.item_mis_pacientes": "Mis Pacientes",
      "nav.item_reportes_analitica": "Reportes y Analítica",
      "nav.item_servicios": "Servicios",
      "nav.item_centro_ayuda": "Centro de Ayuda",
      "nav.item_mi_perfil": "Mi Perfil",
      "nav.item_panel_auxiliar": "Panel Operativo",
      "nav.item_historial_paciente": "Historial de Paciente",
      "nav.item_asistencia_procedimiento": "Asistencia de Procedimiento",
      "nav.item_agenda_apoyo": "Agenda de Apoyo",
      "nav.item_preparacion_consulta": "Preparación de Consulta",
      "nav.item_estado_consultorio": "Estado de Consultorio",
      "nav.item_citas_finalizadas": "Citas Finalizadas",
      "nav.item_panel_recepcion": "Panel de Recepción",
      "nav.item_registrar_paciente_cita": "Registrar Paciente / Cita",
      "nav.item_gestion_citas": "Gestión de Citas",
      "nav.item_generar_factura_pago": "Generar Factura / Pago",
      "nav.item_recordatorios": "Recordatorios",
      "nav.item_perfil_recepcionista": "Perfil Recepcionista",
      "nav.item_reportes_operativos": "Reportes Operativos",
      "adm09_title": "Gestión Integral de Citas",
      "adm09_subtitle": "Supervisión y control",
      "adm09_btn_nueva_cita": "Nueva Cita",
      "adm09_nueva_cita_aria": "Crear nueva cita",
      "adm09_modal_delete_title": "Confirmar cancelación",
      "adm09_modal_delete_msg": "¿Está seguro de cancelar esta cita? Esta acción no se puede deshacer.",
      "adm09_modal_delete_reason": "Motivo de cancelación",
      "adm09_modal_delete_reason_ph": "Indique el motivo...",
      "adm09_modal_delete_confirm": "Cancelar cita",
      "adm09_search_ph": "Buscar por paciente, profesional o servicio...",
      "adm09_search_aria": "Buscar citas",
      "adm09_filters_aria": "Filtros de búsqueda",
      "adm09_filter_status_all": "Todos los estados",
      "adm09_filter_status_aria": "Filtrar por estado",
      "adm09_filter_prof_all": "Cualquier profesional",
      "adm09_filter_prof_aria": "Filtrar por profesional",
      "adm09_filter_date_aria": "Filtrar por fecha",
      "adm09_stats_aria": "Estadísticas de citas",
      "adm09_stat_total": "Total Citas",
      "adm09_stat_total_sub": "Este mes",
      "adm09_stat_programadas": "Programadas",
      "adm09_stat_vs_week": "vs semana pasada",
      "adm09_stat_canceladas": "Canceladas",
      "adm09_stat_rate": "Tasa",
      "adm09_stat_atendidas": "Atendidas",
      "adm09_stat_attendance": "asistencia",
      "adm09_tabs_aria": "Pestañas de filtrado",
      "adm09_tab_all": "Todas las citas",
      "adm09_tab_cancelled": "Cancelaciones",
      "adm09_tab_noshow": "No asistió",
      "adm09_table_aria": "Tabla de citas",
      "adm09_table_title": "Lista de citas",
      "gestionroles_subtitle": "Configura qué puede hacer cada rol en cada módulo del sistema",
      "gestionroles_pending_changes": "cambios pendientes de guardar",
      "gestionroles_save_pending": "Guardar {count}",
      "gestionroles_saving": "Guardando...",
      "gestionroles_toast_success": "Éxito",
      "gestionroles_toast_error": "Error",
      "gestionroles_toast_warning": "Atención",
      "gestionroles_toast_info": "Información",
      "gestionroles_shortcuts_aria": "Ver atajos de teclado",
      "gestionroles_shortcuts_title": "Atajos de teclado",
      "gestionroles_export_aria": "Exportar configuración de permisos",
      "gestionroles_filters_aria": "Filtros de matriz",
      "gestionroles_search_aria": "Buscar módulo o permiso",
      "gestionroles_search_ph": "Buscar módulo o permiso...",
      "gestionroles_search_clear_aria": "Limpiar búsqueda",
      "gestionroles_filter_role_label": "Filtrar por rol",
      "gestionroles_filter_role_all": "Todos los roles",
      "gestionroles_filter_module_label": "Filtrar por módulo",
      "gestionroles_filter_module_all": "Todos los módulos",
      "gestionroles_reset_filters_aria": "Restablecer todos los filtros",
      "gestionroles_reset_filters": "Restablecer filtros",
      "gestionroles_active_filters_aria": "Filtros aplicados",
      "gestionroles_sort_modules": "Ordenar módulos alfabéticamente",
      "gestionroles_matrix_title": "Matriz de permisos por rol y módulo",
      "gestionroles_matrix_scroll": "Contenedor desplazable de la matriz",
      "gestionroles_module_permission": "Módulo / Permiso",
      "gestionroles_empty_title": "No se encontraron permisos",
      "gestionroles_empty_desc": "Intenta ajustar los filtros o limpiar la búsqueda",
      "gestionroles_loading": "Cargando permisos...",
      "gestionroles_history_title": "Cambios recientes",
      "gestionroles_history_clear_confirm": "Se eliminará el historial de cambios de permisos guardado en este navegador. Esta acción no se puede deshacer.",
      "gestionroles_history_clear_action": "Limpiar historial",
      "gestionroles_history_cleared": "Historial limpiado",
      "gestionroles_confirm_unavailable": "No se pudo abrir el diálogo de confirmación.",
      "gestionroles_critical_revocation": "Revoca un permiso crítico",
      "gestionroles_breadcrumb_aria": "Ruta de navegación",
      "gestionroles_skip_matrix": "Saltar a la matriz de permisos",
      "gestionusuarios_subtitle": "Administra los usuarios y sus permisos de acceso",
      "gestionusuarios_btn_nuevo": "Nuevo usuario",
      "gestionusuarios_btn_nuevo_aria": "Crear nuevo usuario",
      "gestionusuarios_stats_aria": "Estadísticas de usuarios",
      "gestionusuarios_stat_total": "Total de usuarios",
      "gestionusuarios_filters_aria": "Filtros de búsqueda",
      "gestionusuarios_search_ph": "Buscar por nombre o correo...",
      "gestionusuarios_search_aria": "Buscar usuario por nombre o correo",
      "gestionusuarios_filter_role_label": "Filtrar por rol",
      "gestionusuarios_filter_role_all": "Todos los roles",
      "gestionusuarios_filter_status_label": "Filtrar por estado",
      "gestionusuarios_filter_status_all": "Todos los estados",
      "gestionusuarios_btn_buscar": "Buscar",
      "gestionusuarios_btn_buscar_aria": "Aplicar los filtros de usuarios",
      "gestionusuarios_btn_limpiar": "Limpiar filtros",
      "gestionusuarios_btn_limpiar_aria": "Limpiar los filtros de usuarios",
      "gestionusuarios_table_aria": "Tabla de usuarios",
      "gestionusuarios_th_role": "ROL",
      "gestionusuarios_pagination_aria": "Paginación de usuarios",
      "gestionusuarios_pagination_prev_aria": "Página anterior",
      "gestionusuarios_pagination_next_aria": "Página siguiente",
      "gestionusuarios_pagination_page_aria": "Ir a la página {page}",
      "gestionusuarios_pagination_info": "Mostrando {start}-{end} de {total} usuarios",
      "gestionusuarios_empty": "No se encontraron usuarios.",
      "gestionusuarios_action_view": "Ver",
      "gestionusuarios_action_view_aria": "Ver detalles del usuario",
      "gestionusuarios_action_edit": "Editar",
      "gestionusuarios_action_edit_aria": "Editar usuario",
      "gestionusuarios_action_block": "Bloquear",
      "gestionusuarios_action_block_aria": "Bloquear usuario",
      "gestionusuarios_action_unblock": "Desbloquear",
      "gestionusuarios_action_unblock_aria": "Desbloquear usuario",
      "gestionusuarios_password_show": "Mostrar contraseña",
      "gestionusuarios_password_hide": "Ocultar contraseña",
      "gestionprofesionales_title": "Gestión de Profesionales",
      "gestionprofesionales_subtitle": "Administra los odontólogos y especialistas de la clínica",
      "gestionprofesionales_btn_nuevo": "Nuevo Profesional",
      "gestionprofesionales_btn_nuevo_aria": "Crear nuevo profesional",
      "gestionprofesionales_search_ph": "Buscar profesional...",
      "gestionprofesionales_search_aria": "Buscar profesional por nombre o especialidad",
      "gestionprofesionales_filters_aria": "Filtros de búsqueda",
      "gestionprofesionales_filter_specialty_label": "Filtrar por especialidad",
      "gestionprofesionales_filter_specialty_all": "Todas las especialidades",
      "gestionprofesionales_filter_specialty_aria": "Filtrar por especialidad",
      "gestionprofesionales_filter_status_label": "Filtrar por estado",
      "gestionprofesionales_filter_status_all": "Todos los estados",
      "gestionprofesionales_filter_status_aria": "Filtrar por estado",
      "gestionprofesionales_btn_buscar": "Buscar",
      "gestionprofesionales_btn_buscar_aria": "Ejecutar búsqueda",
      "gestionprofesionales_btn_limpiar": "Limpiar filtros",
      "gestionprofesionales_btn_limpiar_aria": "Limpiar filtros de búsqueda",
      "gestionprofesionales_stats_aria": "Estadísticas de profesionales",
      "gestionprofesionales_stat_total": "Total Profesionales",
      "gestionprofesionales_stat_activos": "Activos",
      "gestionprofesionales_stat_vacaciones": "En vacaciones",
      "gestionprofesionales_stat_inactivos": "Inactivos",
      "gestionprofesionales_table_aria": "Tabla de profesionales",
      "gestionprofesionales_table_title": "Lista de profesionales",
      "gestionprofesionales_th_profesional": "PROFESIONAL",
      "gestionprofesionales_th_especialidad": "ESPECIALIDAD",
      "gestionprofesionales_th_registro": "REGISTRO",
      "gestionprofesionales_th_telefono": "TELÉFONO",
      "gestionprofesionales_th_estado": "ESTADO",
      "gestionprofesionales_th_acciones": "ACCIONES",
      "gestionprofesionales_btn_ver": "Ver",
      "gestionprofesionales_btn_ver_title": "Ver detalles del profesional",
      "gestionprofesionales_btn_editar": "Editar",
      "gestionprofesionales_btn_editar_title": "Editar profesional",
      "gestionprofesionales_btn_eliminar": "Eliminar",
      "gestionprofesionales_btn_eliminar_title": "Desactivar profesional",
      "gestionprofesionales_btn_desactivar": "Desactivar",
      "gestionprofesionales_btn_desactivar_title": "Desactivar profesional",
      "gestionprofesionales_btn_reactivar": "Reactivar",
      "gestionprofesionales_btn_reactivar_title": "Reactivar profesional",
      "gestionprofesionales_pagination_aria": "Paginación de profesionales",
      "gestionprofesionales_pagination_info": "Mostrando profesionales registrados",
      "gestionprofesionales_pagination_info_prefix": "Mostrando",
      "gestionprofesionales_pagination_info_suffix": "profesionales registrados",
      "gestionprofesionales_pagination_prev_aria": "Página anterior",
      "gestionprofesionales_pagination_page_aria": "Ir a página",
      "gestionprofesionales_pagination_next_aria": "Página siguiente",
      "gestionprofesionales_modal_form_title": "Nuevo Profesional",
      "gestionprofesionales_modal_detail_title": "Ficha de Profesional",
      "gestionprofesionales_modal_delete_title": "Desactivar profesional",
      "gestionprofesionales_modal_close_aria": "Cerrar ventana",
      "gestionprofesionales_form_nombres_label": "Nombres *",
      "gestionprofesionales_form_nombres_ph": "Ej. Carlos",
      "gestionprofesionales_form_nombres_error": "Por favor ingresa los nombres.",
      "gestionprofesionales_form_apellidos_label": "Apellidos *",
      "gestionprofesionales_form_apellidos_ph": "Ej. Méndez Ruiz",
      "gestionprofesionales_form_apellidos_error": "Por favor ingresa los apellidos.",
      "gestionprofesionales_form_correo_label": "Correo de acceso",
      "gestionprofesionales_form_correo_no_account": "Este profesional no tiene una cuenta de acceso vinculada. Esta edición solo actualizará sus datos profesionales.",
      "gestionprofesionales_form_access_note": "La contraseña de acceso no se configura desde este formulario.",
      "gestionprofesionales_form_correo_ph": "Ej. profesional@smiletrack.co",
      "gestionprofesionales_form_correo_error": "Ingresa un correo válido.",
      "gestionprofesionales_form_telefono_label": "Teléfono",
      "gestionprofesionales_form_telefono_ph": "Ej. 300 123 4567",
      "gestionprofesionales_form_registro_label": "Registro Médico *",
      "gestionprofesionales_form_registro_ph": "Ej. RM-2026-001",
      "gestionprofesionales_form_registro_error": "Por favor ingresa el registro médico.",
      "gestionprofesionales_form_especialidad_label": "Especialidad",
      "gestionprofesionales_form_especialidad_primary": "Especialidad principal",
      "gestionprofesionales_form_especialidad_secondary": "Especialidades secundarias",
      "gestionprofesionales_form_especialidad_help": "Mantén Ctrl (Windows) o Cmd (Mac) para seleccionar varias especialidades.",
      "gestionprofesionales_form_especialidad_none": "Sin especialidad",
      "gestionprofesionales_form_categoria_label": "Categoría",
      "gestionprofesionales_form_categoria_ph": "Ej. Odontología General",
      "gestionprofesionales_form_estado_label": "Estado",
      "gestionprofesionales_status_activo": "Activo",
      "gestionprofesionales_status_vacaciones": "Vacaciones",
      "gestionprofesionales_status_inactivo": "Inactivo",
      "gestionprofesionales_form_password_label": "Contraseña",
      "gestionprofesionales_form_password_ph": "Mínimo 8 caracteres, mayúscula, minúscula, número y símbolo",
      "gestionprofesionales_form_password_help": "Debe tener 8+ caracteres, mayúscula, minúscula, número y símbolo.",
      "gestionprofesionales_form_password_rules_title": "La contraseña debe cumplir:",
      "gestionprofesionales_form_password_rule_length": "Mínimo 8 caracteres",
      "gestionprofesionales_form_password_rule_upper": "Una letra mayúscula",
      "gestionprofesionales_form_password_rule_lower": "Una letra minúscula",
      "gestionprofesionales_form_password_rule_number": "Un número",
      "gestionprofesionales_form_password_rule_symbol": "Un símbolo especial",
      "gestionprofesionales_form_password_toggle_aria": "Mostrar u ocultar contraseña",
      "gestionprofesionales_btn_guardar": "Guardar Profesional",
      "gestionprofesionales_detail_registro": "Registro Médico",
      "gestionprofesionales_detail_telefono": "Teléfono",
      "gestionprofesionales_detail_estado": "Estado",
      "gestionprofesionales_tab_general": "Ficha General",
      "gestionprofesionales_tab_horarios": "Horarios de Atención",
      "gestionprofesionales_tab_ausencias": "Ausencias y Permisos",
      "gestionprofesionales_schedule_title": "Horario Semanal de Atención",
      "gestionprofesionales_schedule_btn_default": "Aplicar Horario Estándar (Lun-Vie 08:00 - 17:00)",
      "gestionprofesionales_schedule_hint": "Configura los días y franjas horarias en las que este profesional atiende citas. Si no tiene días configurados, el sistema usará el horario general de la clínica por defecto.",
      "gestionprofesionales_schedule_btn_save": "Guardar Horarios",
      "gestionprofesionales_absences_title": "Registro de Ausencias",
      "gestionprofesionales_modal_delete_question": "¿Estás seguro de desactivar a este profesional?",
      "gestionprofesionales_modal_delete_warning": "El profesional pasará a estado inactivo y no podrá recibir nuevas citas. Esta operación es reversible.",
      "gestionprofesionales_modal_delete_cancel": "Cancelar",
      "gestionprofesionales_modal_delete_cancel_aria": "Cancelar - no desactivar",
      "gestionprofesionales_modal_delete_confirm": "Confirmar desactivación",
      "gestionprofesionales_modal_delete_confirm_aria": "Confirmar desactivación del profesional",
      "gestionprofesionales_footer_sub": "Proyecto Formativo ADSO — SENA",
      "gestionprofesionales_mobile_nav_aria": "Navegación móvil",
      "gestionprofesionales_mobile_dashboard": "Dashboard",
      "gestionprofesionales_mobile_profesionales": "Profesionales",
      "gestionprofesionales_mobile_agenda": "Agenda",
      "reportesclinicos_title": "Reportes Clínicos",
      "reportesclinicos_subtitle": "Análisis médico y seguimiento de pacientes",
      "reportesclinicos_search_ph": "Buscar por paciente, cédula o diagnóstico...",
      "reportesclinicos_search_aria": "Buscar reportes",
      "reportesclinicos_filters_aria": "Filtros de búsqueda",
      "reportesclinicos_filter_profesional_label": "Filtrar por profesional",
      "reportesclinicos_filter_profesional_all": "Todos los profesionales",
      "reportesclinicos_filter_profesional_aria": "Filtrar por profesional",
      "reportesclinicos_filter_mes_label": "Filtrar por mes",
      "reportesclinicos_filter_mes_all": "Todos los meses",
      "reportesclinicos_filter_mes_aria": "Filtrar por mes",
      "reportesclinicos_btn_aplicar": "Aplicar",
      "reportesclinicos_btn_limpiar": "Limpiar",
      "reportesclinicos_stats_aria": "Estadísticas de reportes",
      "reportesclinicos_stat_pacientes": "Pacientes Registrados",
      "reportesclinicos_stat_consultas": "Consultas Este Mes",
      "reportesclinicos_stat_satisfaccion": "Satisfacción Pacientes",
      "reportesclinicos_stat_asistencia": "Tasa de asistencia",
      "reportesclinicos_table_aria": "Tabla de reportes clínicos",
      "reportesclinicos_table_title": "Lista de reportes clínicos",
      "reportesclinicos_th_paciente": "PACIENTE",
      "reportesclinicos_th_ultconsulta": "ÚLTIMA CONSULTA",
      "reportesclinicos_th_diagnostico": "DIAGNÓSTICO",
      "reportesclinicos_th_procedimiento": "PROCEDIMIENTO",
      "reportesclinicos_th_profesional": "PROFESIONAL",
      "reportesclinicos_th_proxcita": "PRÓXIMA CITA",
      "reportesclinicos_th_alerta": "ALERTA",
      "reportesclinicos_empty_title": "Sin resultados",
      "reportesclinicos_badge_sin_cita": "Sin cita",
      "reportesclinicos_empty_desc": "Ningún reporte coincide con los filtros aplicados.",
      "reportesclinicos_pagination_aria": "Paginación de reportes",
      "reportesclinicos_pagination_prev_aria": "Página anterior",
      "reportesclinicos_pagination_next_aria": "Página siguiente",
      "reportesclinicos_pagination_info_prefix": "Mostrando",
      "reportesclinicos_pagination_info_suffix": "reportes clínicos",
      "detalle_paciente_title": "Detalle del Paciente",
      "detalle_paciente_volver": "Volver",
      "detalle_paciente_volver_listado": "Volver al listado",
      "detalle_paciente_datos_personales": "Datos personales",
      "detalle_paciente_documento_label": "Documento",
      "detalle_paciente_correo_label": "Correo",
      "detalle_paciente_telefono_label": "Teléfono",
      "detalle_paciente_fecha_nacimiento_label": "Fecha de nacimiento",
      "detalle_paciente_genero_label": "Género",
      "detalle_paciente_ciudad_label": "Ciudad",
      "detalle_paciente_direccion_label": "Dirección",
      "detalle_paciente_datos_medicos": "Datos médicos",
      "detalle_paciente_grupo_sanguineo_label": "Grupo sanguíneo",
      "detalle_paciente_alergias_label": "Alergias",
      "detalle_paciente_alergias_badge": "Alergias",
      "detalle_paciente_sin_alergias": "Sin alergias registradas",
      "detalle_paciente_alergias_registradas": "Alergias registradas",
      "detalle_paciente_lista_alergias": "Lista de alergias",
      "detalle_paciente_antecedentes_medicos_label": "Antecedentes médicos",
      "detalle_paciente_ultimo_diagnostico_label": "Último diagnóstico",
      "detalle_paciente_contacto_emergencia": "Contacto de emergencia",
      "detalle_paciente_nombre_emergencia_label": "Nombre",
      "detalle_paciente_telefono_emergencia_label": "Teléfono",
      "detalle_paciente_historia_clinica": "Historia Clínica",
      "detalle_paciente_abrir_historia_clinica": "Historia Clínica",
      "detalle_paciente_historial_citas": "Historial de citas",
      "detalle_paciente_proxima_cita": "Próxima cita",
      "detalle_paciente_fecha_cita_label": "Fecha",
      "detalle_paciente_citas_totales": "Citas totales",
      "detalle_paciente_citas_pendientes": "Citas pendientes",
      "detalle_paciente_ultima_visita": "Última visita",
      "detalle_paciente_resumen_actividad": "Resumen de actividad",
      "detalle_paciente_sin_citas_programadas": "Sin citas programadas",
      "detalle_paciente_sin_historial_citas": "Sin historial de citas registrado",
      "detalle_paciente_editar": "Editar",
      "detalle_paciente_editar_datos": "Editar datos",
      "detalle_paciente_eliminar": "Eliminar",
      "detalle_paciente_confirm_fusion_title": "Confirmar fusión de pacientes",
      "detalle_paciente_confirm_fusion_message": "¿Fusionar el paciente seleccionado con {name}? El registro seleccionado quedará inactivo.",
      "detalle_paciente_confirm_fusion": "Confirmar fusión",
      "detalle_paciente_confirmar_eliminar_archivo_titulo": "Eliminar archivo adjunto",
      "detalle_paciente_confirmar_eliminar_archivo_mensaje": "¿Seguro que deseas eliminar el archivo adjunto de este paciente?",
      "detalle_paciente_foto_documento_adjunto": "Foto / Documento adjunto",
      "detalle_paciente_archivo_actual": "Archivo actual:",
      "detalle_paciente_sin_archivo_adjunto": "Sin archivo adjunto",
      "detalle_paciente_archivo_adjunto_de": "Archivo adjunto de",
      "detalle_paciente_seleccionar_imagen": "Seleccionar imagen",
      "detalle_paciente_seleccionar_archivo_adjunto": "Seleccionar archivo adjunto",
      "detalle_paciente_cambiar": "Cambiar",
      "detalle_paciente_subir": "Subir",
      "detalle_paciente_abrir_menu": "Abrir menú",
      "gestion_pacientes_title": "SmileTrack — Gestión de Pacientes",
      "gestion_pacientes_page_title": "Gestión de Pacientes",
      "gestion_pacientes_page_subtitle": "Historial de pacientes — Solo lectura",
      "gestion_pacientes_meta_description": "Gestión de pacientes del centro odontológico",
      "gestion_pacientes_buscar_paciente": "Buscar paciente",
      "gestion_pacientes_buscar_placeholder": "Buscar paciente, cédula o diagnóstico...",
      "gestion_pacientes_breadcrumb_aria": "Ruta de navegación",
      "gestion_pacientes_stats_aria": "Resumen de pacientes",
      "gestion_pacientes_stat_total": "Total de pacientes",
      "gestion_pacientes_stat_total_sub": "Registrados en el sistema",
      "gestion_pacientes_stat_history": "Con historial clínico",
      "gestion_pacientes_stat_history_sub": "Tienen registros médicos",
      "gestion_pacientes_stat_next": "Próximas citas",
      "gestion_pacientes_stat_next_sub": "Citas programadas",
      "gestion_pacientes_stat_no_allergies": "Sin alergias",
      "gestion_pacientes_stat_no_allergies_sub": "Sin restricciones registradas",
      "gestion_pacientes_filters_aria": "Filtros de búsqueda",
      "gestion_pacientes_filter_status": "Estado",
      "gestion_pacientes_filter_status_aria": "Filtrar por estado del paciente",
      "gestion_pacientes_active_plural": "Activos",
      "gestion_pacientes_inactive_plural": "Inactivos",
      "gestion_pacientes_status_retired": "Retirados",
      "gestion_pacientes_all_male": "Todos",
      "gestion_pacientes_all_feminine": "Todas",
      "gestion_pacientes_filter_allergies": "Alergias",
      "gestion_pacientes_filter_allergies_aria": "Filtrar por alergias",
      "gestion_pacientes_with_allergies": "Con alergias",
      "gestion_pacientes_without_allergies": "Sin alergias",
      "gestion_pacientes_filter_appointment": "Próxima cita",
      "gestion_pacientes_filter_appointment_aria": "Filtrar por próxima cita",
      "gestion_pacientes_with_appointment": "Con cita programada",
      "gestion_pacientes_without_appointment": "Sin cita programada",
      "gestion_pacientes_filter_history": "Historial clínico",
      "gestion_pacientes_filter_history_aria": "Filtrar por historial clínico",
      "gestion_pacientes_with_history": "Con historial",
      "gestion_pacientes_without_history": "Sin historial",
      "gestion_pacientes_clear_filters_aria": "Limpiar todos los filtros",
      "gestion_pacientes_clear_filters": "Limpiar filtros",
      "gestion_pacientes_table_title": "Lista de pacientes",
      "gestion_pacientes_table_aria": "Tabla de pacientes",
      "gestion_pacientes_column_patient": "Paciente",
      "gestion_pacientes_column_last_visit": "Última consulta",
      "gestion_pacientes_column_diagnosis": "Diagnóstico",
      "gestion_pacientes_column_next_visit": "Próxima cita",
      "gestion_pacientes_column_allergies": "Alergias",
      "gestion_pacientes_empty_title": "No se encontraron pacientes",
      "gestion_pacientes_empty_description": "Ajusta los filtros o la búsqueda e inténtalo de nuevo.",
      "gestion_pacientes_pagination_aria": "Navegación de páginas",
      "gestion_pacientes_showing": "Mostrando",
      "gestion_pacientes_of": "de",
      "gestion_pacientes_patient_count": "pacientes",
      "gestion_pacientes_page_navigation_aria": "Páginas de resultados",
      "gestion_pacientes_modal_title": "Detalle del paciente",
      "gestion_pacientes_page_number_aria": "Página {page}",
      "gestion_pacientes_filter_results": "{count} resultados encontrados",
      "gestion_pacientes_error_loading": "No se pudo cargar la lista de pacientes.",
      "gestion_pacientes_unnamed": "Sin nombre",
      "gestion_pacientes_no_document": "Sin documento",
      "gestion_pacientes_remove_allergy_aria": "Quitar alergia {name}",
      "gestion_pacientes_allergy_max_length": "Cada alergia debe tener como máximo 150 caracteres.",
      "gestion_pacientes_profile": "Perfil",
      "gestion_pacientes_action_view": "Ver",
      "gestion_pacientes_view_profile": "Ver perfil completo",
      "gestion_pacientes_view_profile_aria": "Ver perfil de {name}",
      "gestion_pacientes_edit_patient": "Editar paciente",
      "gestion_pacientes_edit_patient_aria": "Editar {name}",
      "gestion_pacientes_history": "Historial",
      "gestion_pacientes_history_aria": "Historia clínica de {name}",
      "gestion_pacientes_detail_title": "Detalle de {name}",
      "gestion_pacientes_birth_date": "Fecha de nacimiento",
      "gestion_pacientes_gender": "Género",
      "gestion_pacientes_city": "Ciudad",
      "gestion_pacientes_blood_type": "Grupo sanguíneo",
      "gestion_pacientes_deactivate": "Desactivar paciente",
      "gestion_pacientes_deactivate_aria": "Desactivar a {name}",
      "gestion_pacientes_history_title": "Historial clínico de {name}",
      "gestion_pacientes_doctor_prefix": "Dr.",
      "gestion_pacientes_no_history": "Sin historial registrado",
      "gestion_pacientes_edit_title": "Editar paciente: {name}",
      "gestion_pacientes_form_personal": "Información personal",
      "gestion_pacientes_document_type": "Tipo de documento",
      "gestion_pacientes_id_cc": "Cédula de ciudadanía",
      "gestion_pacientes_id_ti": "Tarjeta de identidad",
      "gestion_pacientes_id_ce": "Cédula de extranjería",
      "gestion_pacientes_id_passport": "Pasaporte",
      "gestion_pacientes_id_nit": "NIT",
      "gestion_pacientes_unspecified": "Sin especificar",
      "gestion_pacientes_gender_male": "Masculino",
      "gestion_pacientes_gender_female": "Femenino",
      "gestion_pacientes_gender_other": "Otro",
      "gestion_pacientes_form_contact": "Información de contacto",
      "gestion_pacientes_emergency_contact": "Contacto de emergencia",
      "gestion_pacientes_emergency_name": "Nombre del contacto",
      "gestion_pacientes_full_name_placeholder": "Nombre completo",
      "gestion_pacientes_emergency_phone": "Teléfono de emergencia",
      "gestion_pacientes_phone_example": "Ej. 300 123 4567",
      "gestion_pacientes_form_health": "Información de salud",
      "gestion_pacientes_known_allergies": "Alergias conocidas",
      "gestion_pacientes_allergy_example": "Ej. Penicilina",
      "gestion_pacientes_medical_history": "Antecedentes médicos",
      "gestion_pacientes_medical_history_placeholder": "Enfermedades previas, cirugías, condiciones crónicas, tratamientos actuales...",
      "gestion_pacientes_save_changes": "Guardar cambios",
      "gestion_pacientes_csrf_missing": "No se encontró el token de seguridad.",
      "gestion_pacientes_update_success": "Paciente actualizado correctamente.",
      "gestion_pacientes_update_error": "No fue posible actualizar el paciente.",
      "gestion_pacientes_error_deactivate": "No fue posible desactivar el paciente.",
      "gestion_pacientes_confirm_deactivate_title": "¿Desactivar paciente?",
      "gestion_pacientes_confirm_deactivate_message": "¿Seguro que deseas desactivar a {name}?",
      "gestion_pacientes_deactivate_success": "Paciente desactivado correctamente.",
      "common.add": "Agregar",
      "common.edit": "Editar",
      "common.not_available": "No disponible",
      "common.saving": "Guardando...",
      "gestion_facturacion_title": "Facturación",
      "gestion_facturacion_subtitle": "Ingresos y cobros",
      "gestion_facturacion_kpi_total_facturas": "Total Facturas",
      "gestion_facturacion_kpi_por_cobrar": "Por Cobrar",
      "gestion_facturacion_kpi_pagadas_hoy": "Pagadas Hoy",
      "gestion_facturacion_kpi_anuladas": "Anuladas",
      "gestion_facturacion_kpi_anio_fiscal": "Todos los periodos",
      "gestion_facturacion_kpi_ultimas_24h": "En todos los periodos",
      "gestion_facturacion_kpi_actualizado_hace": "Según pagos registrados",
      "gestion_facturacion_kpi_vs_mes_anterior_up": "Suma de saldos pendientes",
      "billing.summary": "Resumen de facturación",
      "billing.filters": "Filtros de facturas",
      "billing.search_placeholder": "Buscar por número o paciente...",
      "billing.search_aria": "Buscar por número de factura o paciente",
      "billing.filter_status": "Filtrar por estado",
      "billing.filter_month": "Filtrar por mes",
      "billing.all_statuses": "Todos los estados",
      "billing.status_paid": "Pagada",
      "billing.status_pending": "Pendiente",
      "billing.status_partial": "Parcial",
      "billing.status_voided": "Anulada",
      "billing.all_months": "Todos los meses",
      "billing.create_invoice": "Nueva factura",
      "billing.invoice_number": "N° Factura",
      "billing.pending_amount": "Pendiente",
      "billing.pagination_showing": "Mostrando",
      "billing.pagination_of": "de",
      "billing.pagination_records": "registros",
      "billing.pagination": "Páginas de resultados",
      "billing.close_panel": "Cerrar panel",
      "billing.tax_19": "IVA (19%):",
      "billing.view_details": "Ver detalle",
      "billing.void_invoice": "Anular",
      "billing.close_details": "Cerrar detalle",
      "billing.document": "Documento",
      "billing.service": "Servicio",
      "billing.paid_amount": "Pagado",
      "billing.invoice_list": "Lista de facturas",
      "billing.invoice_details": "Detalle de factura",
      "billing.fallback_service_cleaning": "Limpieza dental ($150.000)",
      "billing.fallback_service_whitening": "Blanqueamiento ($350.000)",
      "billing.fallback_service_orthodontics": "Ortodoncia ($800.000)",
      "billing.fallback_service_root_canal": "Endodoncia ($450.000)",
      "billing.fallback_service_implants": "Implantes ($1.200.000)",
      "billing.fallback_service_pediatric": "Odontopediatra ($120.000)",
      "billing.fallback_service_orthodontic_visit": "Ortodoncia - consulta ($90.000)",
      "billing.fallback_service_dental_implant": "Implante dental ($1.500.000)",
      "billing.payment_history": "Historial de transacciones",
      "billing.close_confirmation": "Cerrar confirmación",
      "billing.payment_success_title": "Registro exitoso",
      "billing.payment_success_message": "El pago se registró correctamente.",
      "billing.close_form": "Cerrar formulario",
      "billing.invoice_required_note": "Completa los datos para generar la factura.",
      "billing.select_registered_patient": "Seleccionar paciente registrado",
      "billing.select_patient_required": "-- Seleccionar un paciente --",
      "billing.patient_selection_required": "Selecciona un paciente registrado para crear la factura.",
      "billing.patient_name_required": "Nombre del paciente *",
      "billing.patient_name_example": "Ej. Ana Gómez",
      "billing.document_required": "Documento *",
      "billing.document_example": "Ej. 1020304050",
      "billing.invoice_date_auto": "Fecha de factura (automática)",
      "billing.service_required": "Servicio o concepto *",
      "billing.select_service": "Selecciona un servicio",
      "billing.total_cop_required": "Total a cobrar (COP) *",
      "billing.amount_example": "Ej. 150000",
      "billing.generate_invoice": "Generar factura",
      "billing.empty_title": "No hay facturas",
      "billing.empty_filtered": "No se encontraron facturas con los criterios de búsqueda.",
      "billing.empty_initial": "Aún no hay facturas registradas.",
      "billing.invoice_for": "Factura {number} de {patient}",
      "billing.document_short": "Doc.",
      "billing.status_aria": "Estado: {status}",
      "billing.view_invoice": "Ver detalle de factura {number}",
      "billing.view": "Ver",
      "billing.manage_invoice": "Gestionar factura {number}",
      "billing.manage": "Gestionar",
      "billing.loading_payments": "Cargando transacciones...",
      "billing.payment_history_error": "No fue posible cargar el historial de transacciones.",
      "billing.no_payments": "Sin transacciones registradas.",
      "billing.payment_success_with_amount": "El pago de {amount} de {patient} se registró correctamente.",
      "billing.reminder_sent": "Recordatorio enviado a {patient}.",
      "billing.already_paid": "Esta factura ya está pagada.",
      "billing.payment_error": "No fue posible registrar el pago.",
      "billing.payment_connection_error": "Error de conexión al registrar el pago.",
      "billing.already_voided": "Esta factura ya está anulada.",
      "billing.confirmation_unavailable": "No se pudo abrir la confirmación. Intenta de nuevo.",
      "billing.void_confirmation_title": "Anular factura",
      "billing.void_confirmation_message": "¿Anular la factura {number} de {patient}? Esta acción no se puede deshacer.",
      "billing.void_reason": "Anulada desde el panel administrativo",
      "billing.void_error": "No fue posible anular la factura.",
      "billing.void_success": "Factura {number} anulada correctamente.",
      "billing.void_connection_error": "Error de conexión al anular la factura.",
      "billing.page_number": "Página {page}",
      "billing.previous_page": "Página anterior",
      "billing.next_page": "Página siguiente",
      "billing.toast_success": "Completado",
      "billing.toast_error": "Error",
      "billing.toast_warning": "Aviso",
      "billing.toast_info": "Información",
      "billing.patient_catalog_error": "No se pudo cargar la lista de pacientes.",
      "billing.required_fields_error": "Completa todos los campos requeridos.",
      "billing.invoice_created": "Factura {number} generada correctamente.",
      "billing.invoice_create_error": "No se pudo generar la factura. Intenta de nuevo.",
      "billing.invoice_load_error": "No fue posible cargar las facturas.",
      "layout_breadcrumb_aria": "Ruta de navegación",
      "rec01_title": "Panel de Recepción",
      "rec01_alert_banner_title": "Próximas citas — en los siguientes 30 minutos",
      "profile.edit": "Editar perfil",

      // ═══════════════════════════════════════════════════════
      // COMUNES
      // ═══════════════════════════════════════════════════════
      "common.smiletrack": "SmileTrack",
      "common.back_to_home": "Volver al inicio",
      "common.loading": "Cargando...",
      "common.close": "Cerrar",
      "common.cancel": "Cancelar",
      "common.confirm": "Confirmar",
      "common.confirmation_unavailable": "No se pudo abrir la confirmación. Inténtalo de nuevo.",
      "common.save": "Guardar",
      "common.send": "Enviar",
      "common.accept": "Aceptar",
      "common.back": "Atrás",
      "common.next": "Siguiente",
      "common.yes": "Sí",
      "common.no": "No",
      "common.email": "Correo electrónico",
      "common.phone": "Teléfono",
      "common.password": "Contraseña",
      "common.confirm_password": "Confirmar contraseña",
      "common.name": "Nombre",
      "common.lastname": "Apellido",
      "common.document": "Documento",
      "common.last_visit": "Último acceso",
      "common.address": "Dirección",
      "common.required_field": "Este campo es obligatorio",
      "common.invalid_email": "Ingresa un correo válido",
      "common.invalid_phone": "Ingresa un teléfono válido",
      "common.password_mismatch": "Las contraseñas no coinciden",
      "common.password_min": "Mínimo 8 caracteres",
      "common.password_upper": "Al menos una letra mayúscula",
      "common.password_number": "Al menos un número",
      "common.password_symbol": "Al menos un símbolo especial",
      "common.terms_accept": "Acepto los Términos de Servicio y Política de Privacidad de SmileTrack.",
      "common.terms": "Términos de Servicio",
      "common.privacy": "Política de Privacidad",
      "common.optional": "(opcional)",
      "common.select": "Seleccionar",
      "common.success": "Éxito",
      "common.error": "Error",
      "common.warning": "Advertencia",
      "common.info": "Información",
      "common.notifications": "Notificaciones",
      "common.read_only": "Solo lectura",
      "common.no_notifications": "No hay notificaciones en esta categoría.",
      "common.previous": "Anterior",
      "common.view_all": "Ver todo",
      "common_skip_link": "Saltar al contenido principal",
      "common_role_patient": "Paciente",
      "common_role_auxiliar": "Auxiliar",
      "common_role_profesional": "Odontólogo",
      "common_role_recepcionista": "Recepción",
      "common.personal_data": "Datos personales",
      "common.messages": "Mensajes",
      "common.today": "Hoy",
      "common.breadcrumb_nav": "Ruta de navegación",
      "rec05.subtitle": "Gestiona los recordatorios automáticos y manuales de citas para pacientes",
      "rec05.history_title": "Enviados recientemente",
      "rec05.view_all_aria": "Ver historial completo de recordatorios enviados",
      "rec05.history_scroller_aria": "Historial de recordatorios enviados",
      "rec05.no_history": "Aún no se ha registrado el envío de recordatorios.",
      "rec05.alerts_title": "Alertas pendientes",
      "profile.title": "Mi Perfil",
      "common.retry": "Intenta de nuevo",
      "common.connection_error": "Error de conexión con el servidor. Inténtalo nuevamente.",
      "common.field_mandatory": "*",

      "help.title": "Centro de Ayuda",
      "help.subtitle": "Busca guías, tutoriales y soporte para tu clínica",
      "help.support_channels": "Canales de soporte",
      "help.support_title": "Soporte y Tickets",
      "help.support_subtitle": "Guías de usuario y atención técnica de SmileTrack",
      "help.guides": "Guías",
      "help.guide_tutorial": "Tutorial paso a paso",
      "help.guide_updated": "Actualizado el",
      "help.recommended": "Artículos recomendados",
      "help.direct_support": "+ Soporte Directo",
      "help.search_placeholder": "¿Cómo programar una cita?...",
      "help.view_all": "Ver todo",
      "help.skip_to_content": "Saltar al contenido principal",
      "help.chat_hours": "Chat en línea: Lun–Vie 8 am–6 pm",
      "help.email": "soporte@smiletrack.local",
      "help.phone": "+57 300 000 0000",
      "help.categories_label": "Categorías del centro de ayuda",
      "role.panel_administrador": "Panel Administrador",
      "role.panel_profesional": "Panel Profesional",
      "role.panel_paciente": "Panel Paciente",
      "role.panel_recepcionista": "Panel Recepcionista",
      "role.panel_auxiliar": "Panel Auxiliar",
      "help.category_citas": "Citas",
      "help.category_pacientes": "Pacientes",
      "help.category_facturacion": "Facturación",
      "help.category_profesionales": "Profesionales",
      "help.category_soporte": "Soporte",
      "help.category_desc_citas": "Aprende paso a paso cómo agendar una nueva cita asignando paciente, profesional, consultorio y servicio en menos de 1 minuto.",
      "help.category_desc_pacientes": "Cómo registrar nuevos pacientes, actualizar datos y consultar el historial clínico.",
      "help.category_desc_facturacion": "Guía para generar facturas, registrar pagos y consultar información de facturación.",
      "help.category_desc_profesionales": "Cómo registrar profesionales, asignar especialidades y gestionar sus horarios.",
      "help.category_desc_soporte": "Canales de contacto y formulario para reportar incidencias al equipo de soporte.",
      "help.article_title_cita": "Cómo programar una cita",
      "help.article_title_pacientes": "Gestión de pacientes",
      "help.article_title_facturacion": "Facturación y pagos",
      "help.article_title_profesionales": "Gestión de profesionales",
      "help.article_title_soporte": "Soporte técnico",
      "help.article_desc_cita": "Aprende paso a paso cómo agendar una nueva cita asignando paciente, profesional, consultorio y servicio en menos de 1 minuto.",
      "help.article_desc_pacientes": "Cómo registrar nuevos pacientes, actualizar datos y consultar el historial clínico.",
      "help.article_desc_facturacion": "Guía para generar facturas, registrar pagos y consultar información de facturación.",
      "help.article_desc_profesionales": "Cómo registrar profesionales, asignar especialidades y gestionar sus horarios.",
      "help.article_desc_soporte": "Canales de contacto y formulario para reportar incidencias al equipo de soporte.",
      "nav.dashboard": "Panel",
      "nav.patients": "Pacientes",
      "nav.appointments": "Citas",
      "nav.billing": "Facturas",
      "nav.agenda": "Agenda",
      "status.attended": "Atendidas",
      "status.sent": "Enviados",
      "audit.level": "Nivel",
      "audit.not_available": "No disponible",
      "audit.live_aria": "Activar o pausar la actualización en vivo",
      "audit.live_label": "Actualización en vivo",
      "audit.export_aria": "Exportar bitácora",
      "audit.stats_aria": "Indicadores de bitácora",
      "audit.total_events": "Eventos recientes",
      "audit.latest_records": "Registros recientes",
      "audit.unique_users": "Usuarios en registros",
      "audit.critical_alerts": "Alertas críticas",
      "audit.requires_attention": "Requieren atención",
      "audit.warnings": "Advertencias",
      "audit.last_24_hours": "Últimas 24 h",
      "audit.system_active": "Sistema activo",
      "audit.uptime": "Disponibilidad",
      "audit.filters_aria": "Filtros de bitácora",
      "audit.search_ph": "Buscar en bitácora...",
      "audit.search_aria": "Buscar eventos por descripción o usuario",
      "audit.clear_search": "Limpiar búsqueda",
      "audit.level_filter": "Filtrar por nivel de evento",
      "audit.all_levels": "Todos los niveles",
      "audit.level_critical": "Crítico",
      "audit.level_error": "Error",
      "audit.level_warning": "Advertencia",
      "audit.level_info": "Información",
      "audit.level_success": "Éxito",
      "audit.level_debug": "Depuración",
      "audit.module": "Módulo",
      "audit.module_filter": "Filtrar por módulo",
      "audit.all_modules": "Todos los módulos",
      "audit.module_auth": "Autenticación",
      "audit.module_appointments": "Citas",
      "audit.module_billing": "Facturación",
      "audit.module_database": "Base de datos",
      "audit.module_services": "Servicios",
      "audit.module_system": "Sistema",
      "audit.date": "Fecha",
      "audit.date_filter": "Filtrar por fecha",
      "audit.reset_filters": "Restablecer filtros",
      "audit.bulk_actions": "Acciones masivas",
      "audit.selected_count": "{count} seleccionados",
      "audit.mark_read_aria": "Marcar seleccionados como leídos",
      "audit.mark_read": "Marcar leídos",
      "audit.archive_aria": "Archivar seleccionados",
      "audit.archive": "Archivar",
      "audit.clear_selection": "Limpiar selección",
      "audit.clear": "Limpiar",
      "audit.table_title": "Registro de eventos del sistema",
      "audit.events_registered": "eventos registrados",
      "audit.items_per_page": "Elementos por página",
      "audit.per_page_10": "10 por página",
      "audit.per_page_25": "25 por página",
      "audit.per_page_50": "50 por página",
      "audit.per_page_100": "100 por página",
      "audit.refresh": "Actualizar bitácora",
      "audit.table_aria": "Tabla de eventos de bitácora",
      "audit.select_all": "Seleccionar todos los eventos visibles",
      "audit.timestamp": "Fecha y hora",
      "audit.sort_timestamp": "Ordenar por fecha y hora",
      "audit.description": "Descripción",
      "audit.user": "Usuario",
      "audit.empty_title": "No se encontraron eventos",
      "audit.empty_description": "Intenta ajustar los filtros o cambiar la fecha.",
      "audit.loading": "Cargando eventos...",
      "audit.pagination_aria": "Paginación de eventos",
      "audit.showing": "Mostrando",
      "audit.of": "de",
      "audit.records": "eventos",
      "audit.first_page": "Primera página",
      "audit.previous_page": "Página anterior",
      "audit.next_page": "Página siguiente",
      "audit.last_page": "Última página",
      "audit.page_navigation": "Navegación de páginas",
      "audit.jump_page": "Ir a página",
      "audit.page_number": "Número de página",
      "audit.levels_title": "Niveles de evento",
      "audit.legend_critical": "Crítico - Requiere atención inmediata",
      "audit.legend_error": "Error - Fallo en operación",
      "audit.legend_warning": "Advertencia - Situación inusual",
      "audit.legend_info": "Información - Evento informativo",
      "audit.legend_success": "Éxito - Operación completada",
      "audit.detail_title": "Detalle de evento",
      "audit.event_detail_title": "Evento #{id}",
      "audit.ip_address": "Dirección IP",
      "audit.stack_trace": "Traza de ejecución",
      "audit.metadata": "Metadatos",
      "audit.copy_aria": "Copiar detalles del evento",
      "audit.copy": "Copiar",
      "audit.report_aria": "Reportar este evento",
      "audit.report": "Reportar",
      "audit.export_title": "Exportar bitácora",
      "audit.export_format": "Formato de exportación",
      "audit.export_pdf_description": "Documento para imprimir o archivar",
      "audit.export_csv_description": "Datos para hojas de cálculo o análisis",
      "audit.export_json_description": "Formato técnico para desarrolladores",
      "audit.export_range": "Rango de eventos",
      "audit.export_filtered": "Solo eventos filtrados",
      "audit.export_all": "Todos los eventos",
      "audit.export_preparing": "Preparando archivo...",
      "audit.export_generating": "Generando archivo...",
      "audit.export_filtering": "Filtrando eventos...",
      "audit.export_compressing": "Comprimiendo...",
      "audit.export_ready": "Listo",
      "audit.download": "Descargar",
      "audit.toast_success_title": "Éxito",
      "audit.toast_error_title": "Error",
      "audit.toast_warning_title": "Aviso",
      "audit.toast_info_title": "Información",
      "audit.no_results_announcement": "No se encontraron eventos con los filtros aplicados",
      "audit.events_loaded_announcement": "{count} eventos cargados, página {page} de {totalPages}",
      "audit.active_filter_count": "{count} filtros activos",
      "audit.select_event_aria": "Seleccionar evento {id}",
      "audit.level_named_aria": "Nivel: {level}",
      "audit.view_details": "Ver detalle",
      "audit.view_details_aria": "Ver detalle del evento {id}",
      "audit.go_to_page_aria": "Ir a la página {page}",
      "audit.detail_loaded_announcement": "Detalle del evento {id} cargado",
      "audit.copy_success": "Detalles copiados al portapapeles",
      "audit.copy_error": "No se pudieron copiar los detalles",
      "audit.report_sent": "Reporte enviado al equipo de soporte",
      "audit.mark_read_success": "{count} eventos marcados como leídos",
      "audit.archive_success": "{count} eventos archivados",
      "audit.filters_reset": "Filtros restablecidos",
      "audit.new_event": "Nuevo evento en bitácora",
      "audit.live_enabled": "Actualización en vivo activada",
      "audit.live_paused": "Actualización en vivo pausada",
      "audit.refreshed": "Bitácora actualizada",
      "audit.refresh_error": "No se pudo actualizar la bitácora",
      "audit.loaded": "Bitácora cargada",
      "audit.page_out_of_range": "Ingresa una página entre 1 y {totalPages}",
      "audit.export_downloaded": "Se preparó {fileName} para {range}",
      "audit.export_error": "No se pudo generar el archivo",
      "notifications.reminders": "Recordatorios",
      "notifications.mark_all": "Marcar todas como leídas",
      "citas.procedure_assistance": "Asistencia en procedimiento",
      "citas.new_aria": "Crear nueva cita",
      "citas.appointment_data": "Datos de la cita",
      "citas.office_status": "Estado del consultorio",
      "citas.checklist": "Checklist post-atención",
      "historia.healthy": "Sanos",
      "historia.treated": "Tratados",
      "historia.treatment_followup": "Seguimiento de Tratamientos",
      "historia.treatment_total": "Total de tratamientos",
      "odo07.active": "Activos",
      "odo07.add_plan": "Agregar tratamiento al plan",
      "odo07.all_patients": "Todos los pacientes",
      "odo07.all_professionals": "Todos los profesionales",
      "odo07.all_statuses": "Todos los estados",
      "odo07.all_types": "Todos los tipos",
      "odo07.annul": "Anular",
      "odo07.annul_reason": "Motivo de anulación",
      "odo07.annul_session": "Anular sesión",
      "odo07.annulled_by": "Anulada por {name}: {reason}",
      "odo07.antiforgery_missing": "No se pudo validar la solicitud. Recarga la página e inténtalo de nuevo.",
      "odo07.appointment_date": "Cita #{number} · {date}",
      "odo07.appointment_number": "Cita #{number}",
      "odo07.breadcrumb_aria": "Ruta de navegación",
      "odo07.cancel": "Cancelar",
      "odo07.clear_filters": "Limpiar filtros",
      "odo07.clinical_note_optional": "Observación clínica (opcional)",
      "odo07.completed": "Completados",
      "odo07.date_locale": "es-CO",
      "odo07.document_label": "Documento: {document}",
      "odo07.document_unknown": "No disponible",
      "odo07.estimated": "Sesiones estimadas",
      "odo07.estimated_sessions": "Sesiones estimadas",
      "odo07.fdi_optional": "Pieza dental permanente FDI (opcional)",
      "odo07.fdi_placeholder": "Ej. 16",
      "odo07.filter_patient_aria": "Filtrar por paciente",
      "odo07.filter_professional_aria": "Filtrar por profesional",
      "odo07.filter_status_aria": "Filtrar por estado",
      "odo07.filter_type_aria": "Filtrar por tipo de tratamiento",
      "odo07.filters_aria": "Filtros de tratamientos",
      "odo07.first_page": "Primera página",
      "odo07.general": "General",
      "odo07.last_page": "Última página",
      "odo07.list_aria": "Lista de tratamientos",
      "odo07.load_error": "No se pudo cargar el seguimiento de tratamientos.",
      "odo07.next_page": "Página siguiente",
      "odo07.no_appointment": "Sin cita",
      "odo07.no_note": "Sin nota",
      "odo07.no_results": "No se encontraron tratamientos",
      "odo07.no_plans": "Aún no hay tratamientos registrados",
      "odo07.no_results_help": "Ajusta los filtros o la búsqueda e inténtalo de nuevo.",
      "odo07.no_sessions": "No hay sesiones registradas.",
      "odo07.note_date": "Nota #{number} · {date}",
      "odo07.note_number": "Nota #{number}",
      "odo07.over_estimate": "Las sesiones realizadas superan la estimación. Ajusta el plan.",
      "odo07.page_number": "Página {number}",
      "odo07.page_size": "Por página",
      "odo07.page_size_aria": "Tratamientos por página",
      "odo07.pagination_aria": "Paginación de tratamientos",
      "odo07.patient": "Paciente",
      "odo07.paused": "Pausados",
      "odo07.per_page_10": "10 por página",
      "odo07.per_page_15": "15 por página",
      "odo07.per_page_20": "20 por página",
      "odo07.per_page_5": "5 por página",
      "odo07.plan_saved": "Plan de tratamiento guardado.",
      "odo07.previous_page": "Página anterior",
      "odo07.professional": "Profesional",
      "odo07.register": "Registrar sesión",
      "odo07.register_session": "Registrar sesión realizada",
      "odo07.registered_on": "Registrado",
      "odo07.registered_treatments": "Tratamientos registrados",
      "odo07.related_appointment": "Cita relacionada (opcional)",
      "odo07.related_note": "Nota clínica relacionada (opcional)",
      "odo07.save": "Guardar",
      "odo07.save_plan_error": "No se pudo guardar el plan de tratamiento.",
      "odo07.save_to_plan": "Guardar en el plan",
      "odo07.search_aria": "Buscar tratamientos",
      "odo07.search_placeholder": "Buscar por paciente, documento o tratamiento...",
      "odo07.select_patient": "Selecciona un paciente",
      "odo07.select_patient_service_error": "Selecciona el paciente y el servicio.",
      "odo07.select_service": "Selecciona un servicio",
      "odo07.service": "Servicio",
      "odo07.session_annulled": "Sesión anulada con motivo registrado.",
      "odo07.session_date": "Fecha de sesión",
      "odo07.attended_appointment": "Cita atendida",
      "odo07.select_attended_appointment": "Selecciona una cita atendida",
      "odo07.no_attended_appointments": "Aún no hay citas atendidas disponibles para este tratamiento.",
      "odo07.session_date_help": "Selecciona la fecha real de atención. Puedes elegir hoy o una fecha anterior.",
      "odo07.session_followup_aria": "Seguimiento de sesiones",
      "odo07.session_progress_aria": "Progreso de sesiones: {progress}%",
      "odo07.session_registered": "Sesión realizada registrada.",
      "odo07.session_update_error": "No se pudo actualizar la sesión.",
      "odo07.sessions_completed_count": "Sesiones realizadas: {completed} de {estimated} estimadas",
      "odo07.showing_none": "Mostrando 0 tratamientos",
      "odo07.showing_range": "Mostrando {start}-{end} de {total} tratamientos",
      "odo07.stats_aria": "Estadísticas de tratamientos",
      "odo07.status": "Estado",
      "odo07.status_approved": "Aprobado",
      "odo07.status_cancelled": "Cancelado",
      "odo07.status_completed": "Completado",
      "odo07.status_in_progress": "En curso",
      "odo07.status_named": "Estado: {status}",
      "odo07.status_paused": "Pausado",
      "odo07.status_proposed": "Propuesto",
      "odo07.submit_button_missing": "No se encontró el botón para enviar el formulario.",
      "odo07.subtitle": "Consulta y actualiza los planes de tratamiento de tus pacientes.",
      "odo07.tooth_fdi": "Pieza FDI",
      "odo07.treatment_type": "Tipo de tratamiento",
      "odo07.unassigned": "Sin asignar",
      "odo07.update_status": "Actualizar estado",
      "odo07.view_sessions": "Ver registro de sesiones ({count})",
      "pqr.applicant_data": "Datos del solicitante",
      "audit.subtitle": "Auditoría y registro de operaciones",
      "styleguide.development": "Desarrollo",
      "styleguide.title": "Guía de estilos",
      "styleguide.design_system": "Sistema de diseño",
      "pqr.petition_description": "Solicita información, documentos o servicios relacionados con tu atención.",
      "pqr.complaint_description": "Manifiesta inconformidad con la atención o servicio recibido.",
      "pqr.claim_description": "Exige la corrección de irregularidades o incumplimientos del servicio.",
      "notifications.title": "Recordatorios y Notificaciones",
      "notifications.page_title": "Notificaciones",
      "notifications.subtitle": "Recordatorios, confirmaciones y mensajes de la clínica",
      "notifications.mark_all_aria": "Marcar todas las notificaciones como leídas",
      "notifications.search_ph": "Buscar notificaciones…",
      "notifications.search_aria": "Buscar notificaciones",
      "notifications.filter_aria": "Filtrar notificaciones por tipo",
      "notifications.filter_all": "Todas",
      "notifications.filter_reminders": "Recordatorios",
      "notifications.filter_confirmed": "Confirmaciones",
      "notifications.filter_cancelled": "Cancelaciones",
      "notifications.filter_messages": "Mensajes",
      "notifications.filter_waitlist": "Lista de espera",
      "notifications.filter_invoices": "Facturación",
      "notifications.list_aria": "Lista de notificaciones",
      "notifications.tab_notices": "Avisos",
      "servicios.total": "Total Servicios",
      "pqr.total": "Total PQR",
      "pqr.management": "Gestión de PQR",
      "patients.active": "Pacientes activos",
      "professionals.active": "Profesionales activos",

      // Selector de idioma
      "lang.es": "ES",
      "lang.en": "EN",
      "lang.switch_es": "Español",
      "lang.switch_en": "English",

      // ═══════════════════════════════════════════════════════
      // LOGIN
      // ═══════════════════════════════════════════════════════
      "login.page_title": "SmileTrack — Iniciar Sesión",
      "login.meta_description": "Inicia sesión en SmileTrack para acceder a tu cuenta y gestionar tus citas, historial y más.",
      "login.subtitle": "Sistema de Gestión Odontológica",
      "access_denied.title": "Acceso denegado",
      "access_denied.subtitle": "Tu cuenta no tiene permiso para ver esta página.",
      "access_denied.message": "Iniciaste sesión correctamente, pero tu rol no tiene acceso a este recurso.",
      "access_denied.home": "Volver al inicio",
      "login.select_role": "Selecciona tu rol",
      "login.role_label": "ROL DE ACCESO",
      "login.role_admin": "Administrador",
      "login.role_profesional": "Profesional",
      "login.role_paciente": "Paciente",
      "login.role_recepcionista": "Recepcionista",
      "login.role_auxiliar": "Auxiliar",
      "login.role_aria_admin": "Ingresar como Administrador",
      "login.role_aria_profesional": "Ingresar como Profesional",
      "login.role_aria_paciente": "Ingresar como Paciente",
      "login.role_aria_recepcionista": "Ingresar como Recepcionista",
      "login.role_aria_auxiliar": "Ingresar como Auxiliar",
      "login.email_label": "CORREO ELECTRÓNICO",
      "login.email_placeholder": "dr.smith@smiletrack.com",
      "login.password_label": "CONTRASEÑA",
      "login.password_placeholder": "••••••••",
      "login.show_password": "Mostrar contraseña",
      "login.hide_password": "Ocultar contraseña",
      "login.remember_me": "Recordarme",
      "login.forgot_password": "¿Olvidaste tu contraseña?",
      "login.button_submit": "Iniciar Sesión",
      "login.button_loading": "Iniciando sesión...",
      "login.no_account": "¿No tienes cuenta?",
      "login.register_here": "Regístrate aquí",
      "login.footer_badge": "Portal Clínico Seguro · v2.4.0",
      "login.error_required_email": "Completa tu correo electrónico",
      "login.error_required_password": "Completa tu contraseña",
      "login.error_incomplete_data": "Completa tu correo y contraseña para ingresar.",
      "login.error_invalid_format_title": "Formato inválido",
      "login.error_invalid_format_msg": "Por favor ingresa un correo electrónico válido.",
      "login.error_verify_email": "Verifica tu correo",
      "login.error_verify_password": "Verifica tu contraseña",
      "login.toast_welcome_title": "¡Bienvenido!",
      "login.toast_welcome_msg": "Iniciando sesión de forma segura...",
      "login.toast_denied_title": "Acceso denegado",
      "login.toast_connection_title": "Error de conexión",
      "login.toast_connection_msg": "No pudimos conectar con el servidor. Intenta nuevamente.",
      "login.error_incomplete_title": "Datos incompletos",

      // Modal auth error
      "login.auth_error_title": "Error de autenticación",
      "login.auth_error_close_aria": "Cerrar ventana",
      "login.auth_error_message": "correo o contraseña incorrectos",
      "login.auth_error_button": "Cerrar",

      // ═══════════════════════════════════════════════════════
      // REGISTER
      // ═══════════════════════════════════════════════════════
      "register.page_title": "Crear cuenta — SmileTrack",
      "register.meta_description": "Crear cuenta en SmileTrack - Sistema de Gestión Clínica Odontológica",
      "register.tagline": "Únete a la red dental profesional",
      "register.button_submit": "Crear cuenta",
      "register.button_loading": "Creando cuenta...",
      "register.have_account": "¿Ya tienes cuenta?",
      "register.login_here": "Inicia sesión aquí",
      "register.footer_badge": "Portal Clínico Seguro • v2.4.0",

      // Fieldsets
      "register.fieldset_personal": "Datos Personales",
      "register.fieldset_docs": "Documentación",
      "register.fieldset_contact": "Contacto",
      "register.fieldset_security": "Seguridad",

      // Campos
      "register.firstname_label": "Nombres",
      "register.firstname_placeholder": "Ej: Juan Carlos",
      "register.lastname_label": "Apellidos",
      "register.lastname_placeholder": "Ej: Pérez García",
      "register.doctype_label": "Tipo Documento",
      "register.doctype_select": "Seleccionar tipo...",
      "register.doctype_cc": "Cédula de ciudadanía",
      "register.doctype_ti": "Tarjeta de identidad",
      "register.doctype_ce": "Cédula de extranjería",
      "register.doctype_pas": "Pasaporte",
      "register.docnum_label": "Número Documento",
      "register.docnum_placeholder": "Ej: 12345678",
      "register.email_label": "Correo Electrónico",
      "register.email_placeholder": "odontologo@clinicadental.com",
      "register.phone_label": "Teléfono",
      "register.phone_placeholder": "+57 300 123 456",
      "register.password_label": "Contraseña",
      "register.password_placeholder": "Mínimo 8 caracteres",
      "register.confirm_password_label": "Confirmar Contraseña",
      "register.confirm_password_placeholder": "Repite tu contraseña",

      // Password criteria
      "register.pwd_criterion_length": "• Mínimo 8 caracteres",
      "register.pwd_criterion_upper": "• Al menos una letra mayúscula",
      "register.pwd_criterion_number": "• Al menos un número",
      "register.pwd_criterion_symbol": "• Al menos un símbolo especial",

      // Noscript
      "register.noscript_warning": "JavaScript está deshabilitado en tu navegador. El formulario puede enviarse, pero algunas validaciones y la experiencia mejorada no estarán disponibles.",

      // Validaciones / mensajes
      "register.error_all_fields": "Completa todos los campos requeridos antes de continuar",
      "register.error_password_mismatch": "Las contraseñas no coinciden. Revisa el campo de confirmación.",
      "register.error_password_weak": "La contraseña debe tener mínimo 8 caracteres, una mayúscula, un número y un símbolo.",
      "register.error_select_role": "Selecciona un rol válido antes de continuar.",
      "register.success_created": "¡Cuenta creada exitosamente!",
      "register.error_create": "Error al crear cuenta. Revisa tus datos.",
      "register.show_password": "Mostrar contraseña",
      "register.hide_password": "Ocultar contraseña",

      // ═══════════════════════════════════════════════════════
      // RECOVER CONTRASEÑA
      // ═══════════════════════════════════════════════════════
      "recover.page_title": "Recuperar Contraseña - Smile Track",
      "recover.meta_description": "Recuperar contraseña de SmileTrack - Sistema de Gestión Clínica Odontológica",
      "recover.header_title": "Recuperar Contraseña",
      "recover.footer_badge": "Portal Clínico Seguro • v2.4.0",

      // Descripciones de pasos
      "recover.step1_description": "Ingresa tu correo electrónico y te enviaremos un código de verificación.",
      "recover.step2_description": "Revisa tu correo e ingresa el código de 6 dígitos que recibiste.",
      "recover.step3_description": "Crea una nueva contraseña segura para tu cuenta.",

      // Paso 1 - Email
      "recover.step1_email_label": "Correo Electrónico",
      "recover.step1_email_placeholder": "odontologo@clinicadental.com",
      "recover.step1_button": "Enviar Código",
      "recover.step1_error_invalid_email": "Ingresa un correo electrónico válido",
      "recover.step1_error_send": "Error al enviar el código. Intenta de nuevo.",
      "recover.step1_error_send_later": "No se pudo enviar el código. Intenta más tarde.",
      "recover.step1_success": "Código enviado a tu correo",

      // Paso 2 - Código OTP
      "recover.step2_code_sent_to": "Hemos enviado un código de 6 dígitos a:",
      "recover.step2_resend": "¿No recibiste el código? Reenviar",
      "recover.step2_resend_sending": "Enviando...",
      "recover.step2_resend_wait": "Reenviar en {n}s",
      "recover.step2_otp_label": "Código de Verificación",
      "recover.step2_otp_digit": "Dígito {n} de 6",
      "recover.step2_error_incomplete": "Ingresa los 6 dígitos del código",
      "recover.step2_error_invalid": "Código incorrecto o expirado.",
      "recover.step2_error_verify": "Error de verificación. Intenta de nuevo.",
      "recover.step2_success": "Código verificado",
      "recover.step2_cooldown_wait": "Espera {n} segundos antes de reenviar.",
      "recover.step2_cooldown_info": "Nuevo código enviado",
      "recover.step2_cooldown_error": "No se pudo reenviar el código.",
      "recover.step2_back_button": "Atrás",
      "recover.step2_verify_button": "Verificar Código",

      // Paso 3 - Nueva contraseña
      "recover.step3_new_password_label": "Nueva Contraseña",
      "recover.step3_new_password_placeholder": "Mínimo 8 caracteres",
      "recover.step3_confirm_password_label": "Confirmar Contraseña",
      "recover.step3_confirm_password_placeholder": "Repite tu contraseña",
      "recover.step3_criteria_length": "• Mínimo 8 caracteres",
      "recover.step3_criteria_upper": "• Al menos una mayúscula",
      "recover.step3_criteria_number": "• Al menos un número",
      "recover.step3_criteria_symbol": "• Al menos un símbolo (ej. !#$%)",
      "recover.step3_match_nomatch": "Las contraseñas no coinciden",
      "recover.step3_match_ok": "✓ Las contraseñas coinciden",
      "recover.step3_back_button": "Atrás",
      "recover.step3_reset_button": "Restablecer Contraseña",
      "recover.step3_error_requirements": "La contraseña no cumple los requisitos",
      "recover.step3_error_mismatch": "Las contraseñas no coinciden",
      "recover.step3_error_unknown": "Ocurrió un error. Regresa al paso anterior.",
      "recover.step3_error_invalid_code": "Ingresa un código válido de 6 dígitos",
      "recover.step3_error_reset": "Error al restablecer. Intenta de nuevo.",
      "recover.step3_success_reset": "¡Contraseña restablecida!",

      // Paso éxito
      "recover.success_title": "¡Contraseña restablecida!",
      "recover.success_message": "Tu contraseña ha sido actualizada exitosamente. Ahora puedes iniciar sesión con tu nueva contraseña.",
      "recover.success_goto_login": "Iniciar Sesión",
      "recover.success_updated_desc": "Tu contraseña ha sido actualizada exitosamente.",

      // Link footer
      "recover.back_to_login": "Volver al inicio de sesión",

      // Toggle password
      "recover.toggle_show": "Mostrar contraseña",
      "recover.toggle_hide": "Ocultar contraseña",

      // ═══════════════════════════════════════════════════════
      // HOMEPAGE - GENERAL
      // ═══════════════════════════════════════════════════════
      "home.meta_description": "SmileTrack es la plataforma de gestión odontológica para agendar citas, consultar tratamientos y contactar especialistas con confianza.",
      "home.meta_keywords": "odontología, citas dentales, clínica dental, historial clínico, reserva cita",
      "home.og_title": "SmileTrack | Agenda tu cita dental en línea",
      "home.og_description": "Reserva tu cita, consulta especialistas y gestiona tu salud bucal con seguridad y soporte profesional.",
      "home.page_title": "SmileTrack | Agenda cita dental, gestiona tu salud bucal",
      "home.logo_alt": "Smile Track Logo",
      "home.logo_text": "Smile Track",
      "home.jsonld_description": "Clínica dental en Bogotá que ofrece citas en línea, atención especializada y gestión de historias clínicas.",
      "home.jsonld_name": "SmileTrack",

      // Nav
      "home.nav_services": "Servicios",
      "home.nav_benefits": "La plataforma",
      "home.nav_professionals": "Profesionales",
      "home.nav_testimonials": "Testimonios",
      "home.nav_about": "Nosotros",
      "home.nav_contact": "Contacto",
      "home.nav_help": "Centro de ayuda",

      // CTA buttons header
      "home.cta_login": "Acceder",
      "home.cta_book": "Agendar cita",

      // Floating buttons
      "home.float_chat_title": "Chat",
      "home.float_phone_title": "Llamar",

      // Hero badge
      "home.hero_badge": "Plataforma odontológica SmileTrack",
      "home.hero_title_html": "Tu atención odontológica, <span>más fácil de gestionar</span>",
      "home.hero_title": "Tu atención odontológica, más fácil de gestionar",
      "home.hero_highlight": "más fácil de gestionar",
      "home.hero_subtitle": "Consulta los servicios activos y gestiona tus citas desde un solo lugar.",

      // Hero CTAs
      "home.hero_login": "Acceder a mi cuenta",
      "home.hero_register": "Crear cuenta",

      // Hero tags
      "home.tag_easy_booking": "Servicios del catálogo",
      "home.tag_reliable": "Acceso a tu cuenta",
      "home.tag_followup": "Gestión de citas",

      // Hero 3 cards
      "home.card_patient_title": "Soy paciente",
      "home.card_patient_desc": "Crea tu cuenta y agenda tu primera cita.",
      "home.card_pro_title": "Soy profesional",
      "home.card_pro_desc": "Accede a tu agenda y controla tus citas.",
      "home.card_contact_title": "Contacto rápido",
      "home.card_contact_desc": "Habla con nuestro equipo para resolver tus dudas.",

      // Glass card servicios destacados
      "home.glass_services_title": "Servicios destacados",
      "home.glass_see_specialists": "Consultar con especialistas →",
      "home.service_cleaning_name": "Limpieza dental",
      "home.service_cleaning_desc": "Prevención · 45 min",
      "home.service_cleaning_price": "Desde $80k",
      "home.service_whitening_name": "Blanqueamiento",
      "home.service_whitening_desc": "Estética · 90 min",
      "home.service_whitening_price": "Desde $350k",
      "home.service_ortho_name": "Ortodoncia",
      "home.service_ortho_desc": "Corrección · Plan mes",
      "home.service_ortho_price": "Desde $200k/mes",

      // Trust strip
      "home.trust_patients_count": "+2.400",
      "home.trust_patients_label": "Pacientes atendidos",
      "home.trust_rating_count": "4.9/5.0",
      "home.trust_rating_label": "Calificación Google",
      "home.trust_experience_count": "15 años",
      "home.trust_experience_label": "De experiencia clínica",
      "home.trust_invima_count": "INVIMA",
      "home.trust_invima_label": "Habilitados y certificados",

      // Services section
      "home.services_eyebrow": "Lo que hacemos",
      "home.services_title": "Servicios odontológicos",
      "home.services_subtitle": "Información obtenida del catálogo activo de SmileTrack.",
      "home.active_services_label": "servicios activos en el catálogo",
      "home.services_empty_title": "Aún no hay servicios publicados",
      "home.services_empty": "Los servicios aparecerán aquí cuando se registren y activen en el catálogo.",
      "home.view_services": "Ver catálogo",
      "home.live_catalog": "Catálogo actualizado",
      "home.feature_appointments": "Gestión de citas",
      "home.feature_appointments_desc": "Consulta y administra tus solicitudes desde tu cuenta.",
      "home.feature_account": "Portal personal",
      "home.feature_account_desc": "Accede con tu cuenta SmileTrack.",
      "home.journey_eyebrow": "Empieza en pocos pasos",
      "home.journey_title": "Tu atención, más fácil de organizar",
      "home.journey_desc": "Encuentra un servicio y usa tu cuenta para dar seguimiento a tus citas.",
      "home.journey_services_title": "Explora los servicios",
      "home.journey_services_desc": "Consulta el catálogo activo, sus descripciones, duración y precios publicados.",
      "home.journey_account_title": "Crea tu cuenta",
      "home.journey_account_desc": "Regístrate para acceder a las funciones disponibles para tu perfil.",
      "home.journey_appointments_title": "Gestiona tus citas",
      "home.journey_appointments_desc": "Accede a tu cuenta para consultar y gestionar tus solicitudes de cita.",
      "home.get_started_eyebrow": "Empieza aquí",
      "home.get_started_title": "Gestiona tu atención desde SmileTrack",
      "home.get_started_desc": "Crea una cuenta o inicia sesión para acceder a las funciones disponibles para tu perfil.",

      "home.svc_cleaning_title": "Limpieza dental",
      "home.svc_cleaning_price": "$80.000",
      "home.svc_cleaning_desc": "Elimina el sarro y placa bacteriana que el cepillo no alcanza.",
      "home.svc_cleaning_tag": "Para toda la familia",

      "home.svc_whitening_title": "Blanqueamiento",
      "home.svc_whitening_price": "$350.000",
      "home.svc_whitening_desc": "Recupera el brillo natural de tus dientes de forma segura y efectiva.",
      "home.svc_whitening_tag": "Estética avanzada",

      "home.svc_ortho_title": "Ortodoncia",
      "home.svc_ortho_price": "$200k/mes",
      "home.svc_ortho_desc": "Corrección de la posición dental con técnicas fijas e invisibles.",
      "home.svc_ortho_tag": "Niños y adultos",

      "home.svc_endo_title": "Endodoncia",
      "home.svc_endo_price": "$450.000",
      "home.svc_endo_desc": "Tratamiento de conductos para salvar piezas dentales comprometidas.",
      "home.svc_endo_tag": "Alta precisión",

      "home.svc_implants_title": "Implantes",
      "home.svc_implants_price": "$2.800.000",
      "home.svc_implants_desc": "Reemplaza piezas perdidas con implantes de titanio de larga duración.",
      "home.svc_implants_tag": "Solución permanente",

      "home.svc_pediatrics_title": "Odontopediatría",
      "home.svc_pediatrics_price": "Desde $60k",
      "home.svc_pediatrics_desc": "Cuidado dental especializado para los más pequeños del hogar.",
      "home.svc_pediatrics_tag": "Niños 0–12 años",

      // Profesionales section
      "home.pro_eyebrow": "Nuestro equipo",
      "home.pro_title": "Especialistas que inspiran confianza",
      "home.pro_1_name": "Dr. Carlos Méndez",
      "home.pro_1_specialty": "Odontología General / Endodoncia",
      "home.pro_1_bio": "Experto con más de 12 años de experiencia en tratamientos radiculares complejos.",
      "home.pro_1_edu_1": "U. Nacional",
      "home.pro_1_edu_2": "Esp. Endodoncia",
      "home.pro_1_alt": "Dr. Carlos Méndez",
      "home.pro_2_name": "Dr. Andrés Torres",
      "home.pro_2_specialty": "Cirugía Oral / Implantes",
      "home.pro_2_bio": "Especialista en restauraciones complejas y cirugía de terceros molares.",
      "home.pro_2_edu_1": "U. Rosario",
      "home.pro_2_edu_2": "Esp. Cirugía",
      "home.pro_2_alt": "Dr. Andrés Torres",
      "home.pro_3_name": "Dra. Laura Gómez",
      "home.pro_3_specialty": "Ortodoncia / Odontopediatría",
      "home.pro_3_bio": "Dedicada a crear sonrisas perfectas en niños y adultos con las últimas técnicas.",
      "home.pro_3_edu_1": "U. Javeriana",
      "home.pro_3_edu_2": "Esp. Ortodoncia",
      "home.pro_3_alt": "Dra. Laura Gómez",

      // Testimonios
      "home.test_eyebrow": "Lo que dicen",
      "home.test_title": "Pacientes que ya sonríen con confianza",
      "home.test_1_since": "Paciente desde 2021",
      "home.test_1_quote": "El Dr. Méndez fue increíblemente profesional. Mi endodoncia fue indolora y el seguimiento fue excelente. Totalmente recomendados.",
      "home.test_1_name": "María P.",
      "home.test_2_since": "Paciente desde 2020",
      "home.test_2_quote": "Llevé a mis hijos y la Dra. Gómez los atendió con una paciencia y calidez únicas. La clínica es moderna y muy limpia.",
      "home.test_2_name": "Carlos R.",
      "home.test_3_since": "Paciente desde 2023",
      "home.test_3_quote": "El blanqueamiento superó mis expectativas. En una sola sesión quedé encantada con los resultados. Volveré pronto.",
      "home.test_3_name": "Luisa F.",

      // About us
      "home.about_eyebrow": "Nuestra historia",
      "home.about_left_title": "Una clínica con historia y resultados",
      "home.about_left_year_label": "Año de fundación, estableciendo un estándar de calidad superior en Bogotá.",
      "home.about_left_patients_label": "Pacientes registrados con un 98% de satisfacción garantizada.",
      "home.about_left_digital_label": "Digital: historia clínica electrónica y diagnóstico con IA.",
      "home.about_right_title": "Tecnología y calidez humana en cada consulta",
      "home.about_right_para1": "Somos una clínica familiar fundada en 2009 con la misión de democratizar el acceso a la odontología de alta calidad en Bogotá. Nuestro equipo multidisciplinario trabaja con los más altos estándares clínicos.",
      "home.about_right_para2": "Integramos tecnología digital — radiografías 3D, diagnóstico asistido por IA y gestión de historias clínicas en la nube — para ofrecer tratamientos más precisos y seguros.",
      "home.about_badge_invima": "INVIMA habilitados",
      "home.about_badge_rips": "RIPS activo",
      "home.about_badge_registry": "Registro Distrital",

      // FAQ
      "home.faq_eyebrow": "Dudas Comunes",
      "home.faq_title": "Preguntas Frecuentes",
      "home.faq_1_q": "¿Aceptan seguros o medicina prepagada?",
      "home.faq_1_a": "Sí, tenemos convenios con las principales entidades de medicina prepagada (Colsanitas, Sura, MedPlus). Por favor contáctanos para verificar la cobertura exacta de tu plan antes de agendar.",
      "home.faq_2_q": "¿Cuáles son los medios de pago disponibles?",
      "home.faq_2_a": "Aceptamos tarjetas de crédito/débito, transferencias bancarias, Nequi, DaviPlata y efectivo. Además, contamos con opciones de financiamiento para tratamientos complejos como ortodoncia o implantes.",
      "home.faq_3_q": "¿Cómo es el proceso para agendar una primera cita?",
      "home.faq_3_a": "Puedes usar el formulario de contacto en esta página o registrarte en nuestra plataforma (\"Crear cuenta\"). Una vez registrado, podrás agendar, reagendar y cancelar tus citas de manera 100% digital, así como revisar tu historia clínica.",

      // Final CTA
      "home.final_cta_title": "¿Listo para transformar tu sonrisa?",
      "home.final_cta_subtitle": "Únete a nuestra plataforma y gestiona tu salud bucal de forma moderna y sin complicaciones.",
      "home.final_cta_begin": "Comenzar ahora",
      "home.final_cta_contact": "Contáctanos",

      // Contacto
      "home.contact_eyebrow": "Encuéntranos",
      "home.contact_title": "Estamos aquí para atenderte",
      "home.contact_address_label": "Dirección",
      "home.contact_address_value_html": "Cra. 15 #93-47, Oficina 302<br>Bogotá D.C., Colombia",
      "home.contact_address_value": "Cra. 15 #93-47, Oficina 302\nBogotá D.C., Colombia",
      "home.contact_schedule_label": "Horarios",
      "home.contact_schedule_value_html": "Lunes a Viernes: 8am – 6pm<br>Sábados: 9am – 2pm · Domingos: Cerrado",
      "home.contact_schedule_value": "Lunes a Viernes: 8am – 6pm\nSábados: 9am – 2pm · Domingos: Cerrado",
      "home.contact_phone_label": "Teléfono / WhatsApp",
      "home.contact_phone_value": "+57 300 123 4567",
      "home.contact_map_alt": "Mapa clínica Bogotá",

      // Contacto formulario
      "home.form_title": "Agenda tu cita aquí",
      "home.form_name_placeholder": "Nombre completo",
      "home.form_phone_placeholder": "Teléfono / WhatsApp",
      "home.form_service_select": "Servicio de interés",
      "home.form_option_cleaning": "Limpieza dental",
      "home.form_option_whitening": "Blanqueamiento",
      "home.form_option_ortho": "Ortodoncia",
      "home.form_option_endo": "Endodoncia",
      "home.form_option_implants": "Implantes",
      "home.form_option_general": "Consulta general",
      "home.form_submit": "Confirmar por WhatsApp",
      "home.form_consent": "Autorizo abrir WhatsApp con estos datos y acepto la",

      // Bottom final CTA
      "home.bottom_cta_title": "Tu sonrisa no debería esperar más",
      "home.bottom_cta_subtitle": "Primera valoración completa incluida. Sin compromiso.",
      "home.bottom_cta_book": "Agendar cita ahora",
      "home.bottom_cta_whatsapp": "Escribir por WhatsApp",

      // Footer
      "home.footer_logo_alt": "Smile Track",
      "home.footer_copyright_html": "© 2025 Smile Track. Todos los derechos reservados. Gestión Dental de Alta Precisión.<br>Habilitado INVIMA · RIPS activo · Registro Distrital vigente.",
      "home.footer_copyright": "© 2025 Smile Track. Todos los derechos reservados. Gestión Dental de Alta Precisión.\nHabilitado INVIMA · RIPS activo · Registro Distrital vigente.",
      "home.footer_social_fb": "Facebook",
      "home.footer_social_ig": "Instagram",
      "home.footer_social_email": "Email",
      "home.footer_legal_title": "Legal",
      "home.footer_privacy": "Privacidad",
      "home.footer_terms": "Términos",
      "home.footer_notice": "Aviso Legal",
      "home.footer_company_title": "Compañía",
      "home.footer_support": "Soporte",
      "home.footer_blog": "Blog",
      "home.footer_faq": "Preguntas frecuentes",

      // WhatsApp flotante
      "home.whatsapp_aria": "WhatsApp",
      "home.whatsapp_tooltip": "¡Hola! ¿En qué podemos ayudarte?",

      // Modales
      // Chatbot
      "home.chat_title": "Asistente SmileTrack",
      "home.chat_online": "En línea",
      "home.chat_close_aria": "Cerrar",
      "home.chat_placeholder": "Escribe tu pregunta...",
      "home.chat_send_aria": "Enviar",
      "home.chat_welcome": "¡Hola! 👋 Soy tu asistente de SmileTrack. ¿En qué puedo ayudarte?",
      "home.chat_qr_appointments": "📅 Agendar",
      "home.chat_qr_payments": "💳 Pagos",
      "home.chat_qr_services": "🦷 Servicios",
      "home.chat_qr_location": "📍 Ubicación",
      "home.chat_response_appointments": "📅 Para agendar: 1) Haz clic en \"Agendar cita\", 2) Completa el formulario, 3) Te confirmamos por WhatsApp.",
      "home.chat_response_payments": "💳 Aceptamos: Efectivo, tarjetas, Nequi, Daviplata. Consulta tus pagos en tu cuenta.",
      "home.chat_response_services": "🦷 Ofrecemos: Limpieza ($80k), Blanqueamiento ($350k), Ortodoncia y más. ¿Te interesa agendar?",
      "home.chat_response_schedule": "🕒 Lunes-Viernes: 8AM-6PM | Sábados: 9AM-2PM | Urgencias: 300 123 4567",
      "home.chat_response_emergency": "🚨 Para urgencias: Llama YA al 300 123 4567 o acude a nuestra sede.",
      "home.chat_response_location": "📍 Estamos en: Cra. 15 #93-47, Oficina 302, Bogotá. También ofrecemos consultas virtuales.",
      "home.chat_response_fallback": "😅 No entendí. Pregúntame sobre: citas, pagos, servicios, horarios, urgencias o ubicación.",
      "home.chat_emergency_toast": "🚨 Para emergencias, llama al 300 123 4567",

      // PQRS
      "home.pqrs_title": "PQRS - SmileTrack",
      "home.pqrs_status": "Respuesta en ≤ 48h hábiles",
      "home.pqrs_type_label": "Tipo de solicitud *",
      "home.pqrs_type_p": "🔍 Petición",
      "home.pqrs_type_q": "❓ Queja",
      "home.pqrs_type_r": "🙏 Reclamo",
      "home.pqrs_type_s": "💡 Sugerencia",
      "home.pqrs_name_placeholder": "Nombre completo *",
      "home.pqrs_email_placeholder": "Correo *",
      "home.pqrs_phone_placeholder": "Teléfono",
      "home.pqrs_doc_placeholder": "Documento (opcional)",
      "home.pqrs_subject_placeholder": "Asunto *",
      "home.pqrs_message_placeholder": "Descripción detallada *",
      "home.pqrs_consent_html": "Acepto la <a href=\"#\" class=\"st-link\">política de datos</a>",
      "home.pqrs_consent": "Acepto la política de datos",
      "home.pqrs_btn_cancel": "Cancelar",
      "home.pqrs_btn_send": "Enviar PQRS",
      "home.pqrs_success_title": "¡Solicitud recibida!",
      "home.pqrs_success_body1": "Tu PQRS #",
      "home.pqrs_success_body2": " ha sido registrada.",
      "home.pqrs_success_body3": "Te responderemos a ",
      "home.pqrs_success_body4": " en ≤ 48h hábiles.",
      "home.pqrs_success_new": "Nueva solicitud",
      "home.pqrs_cancel_confirm": "¿Cancelar esta solicitud?",
      "home.pqrs_unavailable_warning": "La PQRS no está disponible desde esta página. Inicia sesión para registrarla.",
      "home.pqrs_send_error": "❌ Error al enviar. Intenta de nuevo.",

      // Home: WhatApp default message
      "home.whatsapp_message": "¡Hola! Quiero agendar una cita dental en Smile Track.",

      // Toast
      "home.toast_success": "✅",
      "home.toast_error": "❌",
      "home.toast_warning": "⚠️",

      // Dashboard del profesional
      "odo01.breadcrumb_aria": "Ruta de navegación",
      "odo01.breadcrumb_professional": "Profesional",
      "odo01.breadcrumb_dashboard": "Dashboard",
      "odo01.subtitle": "Resumen de tu actividad profesional",
      "odo01.view_reports": "Ver reportes completos",
      "odo01.next_urgent": "¡Cita urgente!",
      "odo01.next_label": "Próxima cita",
      "odo01.consultation": "Consulta",
      "odo01.office_unassigned": "Consultorio sin asignar",
      "odo01.last_visit": "Última atención",
      "odo01.view_in_agenda": "Ver en agenda",
      "odo01.no_upcoming": "No tienes citas próximas. ¡Buen trabajo!",
      "odo01.daily_progress_aria": "Progreso del día",
      "odo01.daily_progress_label": "{attended} de {total} citas completadas hoy ({percent}%)",
      "odo01.daily_progress_bar_aria": "Progreso del día: {percent}%",
      "odo01.quick_actions": "Accesos rápidos",
      "odo01.my_agenda": "Mi agenda",
      "odo01.my_agenda_desc": "Ver y gestionar citas",
      "odo01.my_patients": "Mis pacientes",
      "odo01.my_patients_desc": "Historial clínico",
      "odo01.reports": "Reportes",
      "odo01.reports_desc": "Estadísticas detalladas",
      "odo01.my_profile": "Mi perfil",
      "odo01.my_profile_desc": "Datos personales",
      "odo01.stats_aria": "Métricas principales",
      "odo01.stat_patients": "Pacientes con cita este mes",
      "odo01.stat_today": "Citas hoy",
      "odo01.stat_attended": "Citas atendidas hoy",
      "odo01.stat_revenue": "Valor estimado de servicios atendidos",
      "odo01.revenue_note": "Estimado con precios de catálogo; no representa facturación ni recaudo.",
      "odo01.tooltip_patients": "Pacientes únicos atendidos este mes",
      "odo01.tooltip_today": "Total de citas programadas para hoy",
      "odo01.tooltip_attended": "Citas completadas hoy",
      "odo01.tooltip_revenue": "Valor estimado según precios de catálogo para citas atendidas",
      "odo01.revenue_title": "Valor estimado de servicios atendidos en los últimos 3 meses",
      "odo01.revenue_aria": "Valor estimado de servicios atendidos en los últimos tres meses",
      "odo01.revenue_bar_aria": "{month}: {amount}",
      "odo01.revenue_summary": "Estimación con precios de catálogo de las citas del profesional autenticado; no equivale a facturación ni recaudo.",
      "odo01.revenue_empty": "No hay servicios atendidos en el período.",
      "odo01.recent_patients": "Últimos pacientes atendidos",
      "odo01.patients_table_aria": "Tabla de últimos pacientes atendidos",
      "odo01.patient": "Paciente",
      "odo01.service": "Servicio",
      "odo01.date": "Fecha",
      "odo01.no_recent_patients": "No hay pacientes atendidos este mes.",
      "odo01.performance": "Rendimiento del día",
      "odo01.performance_excellent": "¡Excelente día! Estás cumpliendo tus objetivos.",
      "odo01.performance_good": "Buen ritmo, continúa así.",
      "odo01.performance_low": "Varias citas pendientes, organiza tu tiempo.",
      "odo01.performance_attended": "Atendidas",
      "odo01.notifications": "Notificaciones",
      "odo01.dismiss_notification": "Descartar notificación",
      "odo01.notification_unconfirmed": "{count} cita(s) sin confirmar",
      "odo01.review_agenda": "Revisa tu agenda para confirmar",
      "odo01.notification_waiting": "{count} paciente(s) en espera",
      "odo01.waiting_desc": "Esperando en sala de atención",
      "odo01.notification_records": "{count} historia(s) por completar",
      "odo01.complete_records": "Completa las historias clínicas pendientes",
      "odo01.notifications_empty": "Todo al día",
      "odo01.monthly_appointments": "Tus citas del mes",
      "odo01.status_chart_aria": "Estado de tus citas del mes",
      "odo01.status_attended": "Atendidas",
      "odo01.status_scheduled": "Programadas",
      "odo01.status_cancelled": "Canceladas",
      "odo01.status_no_show": "No asistió",
      "odo01.status_attended_aria": "Atendidas: {count} citas, {percent}%",
      "odo01.status_scheduled_aria": "Programadas: {count} citas, {percent}%",
      "odo01.status_cancelled_aria": "Canceladas: {count} citas, {percent}%",
      "odo01.status_no_show_aria": "No asistió: {count} citas, {percent}%",
      "odo01.upcoming_today": "Próximas citas de hoy",
      "odo01.patient_label": "Paciente:",
      "odo01.not_available": "No disponible",
      "odo01.open_odontogram": "Abrir odontograma",
      "odo01.no_more_appointments_today": "No tienes más citas programadas para hoy.",
      "odo01.countdown_now": "Es ahora",
      "odo01.countdown_minutes_one": "En {count} minuto",
      "odo01.countdown_minutes_other": "En {count} minutos",
      "odo01.date_locale": "es-CO",

      // ═══════════════════════════════════════════════════════
      // SIDEBAR — Navegación interna (compartida por todos los roles)
      // ═══════════════════════════════════════════════════════

      // Grupos de navegación
      "nav.group_inicio":               "Inicio",
      "nav.group_reportes":             "Reportes y Analítica",
      "nav.group_acceso":               "Acceso y Seguridad",
      "nav.group_pacientes":            "Gestión de Pacientes",
      "nav.group_historia":             "Historia Clínica",
      "nav.group_profesionales":        "Gestión de Profesionales",
      "nav.group_citas":                "Gestión de Citas",
      "nav.group_facturacion":          "Facturación y Pagos",
      "nav.group_servicios":            "Servicios y Recursos",
      "nav.group_pqr":                  "Gestión de PQR",
      "nav.group_ayuda":                "Centro de Ayuda",

      // Ítems de navegación
      "nav.homepage":                   "Homepage",
      "nav.panel_analitica":            "Panel de Analítica",
      "nav.gestion_usuarios":           "Gestión de Usuarios",
      "nav.gestion_roles":              "Gestión de Roles",
      "nav.bitacora":                   "Bitácora del Sistema",
      "nav.cambiar_contrasena":         "Cambiar Contraseña",
      "nav.gestion_pacientes":          "Gestión de Pacientes",
      "nav.historial_clinico":          "Historial Clínico",
      "nav.gestion_profesionales":      "Gestión de Profesionales",
      "nav.reportes_clinicos":          "Reportes Clínicos",
      "nav.dashboard_general":          "Dashboard General",
      "nav.agenda_general":             "Agenda General",
      "nav.gestion_citas":              "Gestión Integral de Citas",
      "nav.gestion_facturacion":        "Gestión de Facturación",
      "nav.catalogo_servicios":         "Catálogo de Servicios",
      "nav.configuracion_general":      "Configuración General",
      "nav.pqr_lista":                  "Peticiones, Quejas y Reclamos",
      "nav.pqr_nueva":                  "Nueva Solicitud",
      "nav.ayuda_programar_cita":       "Cómo programar una cita",
      "nav.ayuda_guias":                "Guías y Tutoriales",
      "nav.ayuda_soporte":              "Soporte y Tickets",
      "nav.cerrar_sesion":              "Cerrar sesión",

      // Sidebar accesibilidad / aria
      "nav.skip_link":                  "Saltar al contenido principal",
      "nav.sidebar_label":              "Navegación principal",
      "nav.hamburger_open":             "Abrir menú",
      "nav.hamburger_close":            "Cerrar menú",

      // Selector de idioma (sidebar)
      "nav.lang_switcher_label":        "Cambiar idioma",

      // ═══════════════════════════════════════════════════════
      // FOOTER INTERNO (dentro del layout autenticado)
      // ═══════════════════════════════════════════════════════
      "footer.copyright":               "© 2025 SmileTrack. Todos los derechos reservados.",
      "footer.privacy":                 "Privacidad",
      "footer.terms":                   "Términos",

      // ═══════════════════════════════════════════════════════
      // TOASTS / NOTIFICACIONES COMUNES
      // ═══════════════════════════════════════════════════════
      "toast.success_title":            "Operación exitosa",
      "toast.error_title":              "Error",
      "toast.warning_title":            "Advertencia",
      "toast.info_title":               "Información",
      "toast.saving":                   "Guardando...",
      "toast.saved":                    "Guardado correctamente",
      "toast.deleting":                 "Eliminando...",
      "toast.deleted":                  "Eliminado correctamente",
      "toast.loading":                  "Cargando...",
      "toast.confirm_delete":           "¿Confirmar eliminación?",
      "toast.confirm_delete_msg":       "Esta acción no se puede deshacer.",
      "toast.session_expired":          "Tu sesión ha expirado. Por favor inicia sesión de nuevo.",
      "toast.no_permission":            "No tienes permisos para realizar esta acción.",
      "toast.network_error":            "Error de red. Verifica tu conexión e intenta de nuevo.",

      // ═══════════════════════════════════════════════════════
      // TABLAS / LISTAS COMUNES
      // ═══════════════════════════════════════════════════════
      "table.no_data":                  "No hay datos disponibles",
      "table.loading":                  "Cargando datos...",
      "table.actions":                  "Acciones",
      "table.search":                   "Buscar...",
      "table.filter_all_states":        "Todos los estados",
      "table.filter":                   "Filtrar",
      "table.export":                   "Exportar",
      "table.page_of":                  "Página {current} de {total}",
      "table.rows_per_page":            "Filas por página",
      "table.prev":                     "Anterior",
      "table.next":                     "Siguiente",
      "table.first":                    "Primera",
      "table.last":                     "Última",
      "table.total_records":            "{n} registro(s) en total",
      "table.selected":                 "{n} seleccionado(s)",
      "table.select_all":               "Seleccionar todo",

      // ═══════════════════════════════════════════════════════
      // FORMULARIOS COMUNES
      // ═══════════════════════════════════════════════════════
      "form.save":                      "Guardar",
      "form.save_changes":              "Guardar cambios",
      "form.cancel":                    "Cancelar",
      "form.edit":                      "Editar",
      "form.delete":                    "Eliminar",
      "form.view":                      "Ver",
      "form.create":                    "Crear",
      "form.add":                       "Agregar",
      "form.search":                    "Buscar",
      "form.filter":                    "Filtrar",
      "form.clear":                     "Limpiar",
      "form.reset":                     "Restablecer",
      "form.close":                     "Cerrar",
      "form.confirm":                   "Confirmar",
      "form.upload":                    "Subir archivo",
      "form.download":                  "Descargar",
      "form.print":                     "Imprimir",
      "form.required_fields":           "Los campos marcados con * son obligatorios.",
      "form.unsaved_changes":           "Tienes cambios sin guardar. ¿Deseas salir?",

      // ═══════════════════════════════════════════════════════
      // ESTADOS / BADGES COMUNES
      // ═══════════════════════════════════════════════════════
      "status.active":                  "Activo",
      "status.inactive":                "Inactivo",
      "status.pending":                 "Pendiente",
      "status.confirmed":               "Confirmada",
      "status.cancelled":               "Cancelada",
      "status.completed":               "Completada",
      "status.in_progress":             "En progreso",
      "status.scheduled":               "Programada",
      "status.no_show":                 "No asistió",
      "status.rescheduled":             "Reprogramada",
      "status.enabled":                 "Habilitado",
      "status.disabled":                "Deshabilitado",
      "status.blocked":                 "Bloqueado",
      "status.paid":                    "Pagado",
      "status.unpaid":                  "Pendiente de pago",
      "status.partial":                 "Pago parcial",
      "status.open":                    "Abierto",
      "status.closed":                  "Cerrado",
      "status.resolved":                "Resuelto",

      // ═══════════════════════════════════════════════════════
      // MÓDULOS — CITAS
      // ═══════════════════════════════════════════════════════
      "citas.title":                    "Gestión de Citas",
      "citas.new":                      "Nueva cita",
      "citas.edit":                     "Editar cita",
      "citas.cancel":                   "Cancelar cita",
      "citas.reschedule":               "Reprogramar cita",
      "citas.confirm":                  "Confirmar cita",
      "citas.date":                     "Fecha",
      "citas.time":                     "Hora",
      "citas.patient":                  "Paciente",
      "citas.professional":             "Profesional",
      "citas.service":                  "Servicio",
      "citas.status":                   "Estado",
      "citas.notes":                    "Notas",
      "citas.duration":                 "Duración",
      "citas.office":                   "Consultorio",
      "citas.no_appointments":          "No hay citas para este período.",
      "citas.filter_date":              "Filtrar por fecha",
      "citas.filter_status":            "Filtrar por estado",
      "citas.filter_professional":      "Filtrar por profesional",
      "citas.dashboard_title":          "Dashboard de Citas",
      "citas.total_today":              "Citas hoy",
      "citas.total_week":               "Esta semana",
      "citas.total_month":              "Este mes",
      "citas.attendance_rate":          "Tasa de asistencia",

      // ═══════════════════════════════════════════════════════
      // MÓDULOS — PACIENTES
      // ═══════════════════════════════════════════════════════
      "pacientes.title":                "Gestión de Pacientes",
      "pacientes.new":                  "Nuevo paciente",
      "pacientes.edit":                 "Editar paciente",
      "pacientes.view":                 "Ver paciente",
      "pacientes.name":                 "Nombre",
      "pacientes.document":             "Documento",
      "pacientes.birth_date":           "Fecha de nacimiento",
      "pacientes.age":                  "Edad",
      "pacientes.gender":               "Género",
      "pacientes.phone":                "Teléfono",
      "pacientes.email":                "Correo",
      "pacientes.address":              "Dirección",
      "pacientes.blood_type":           "Tipo de sangre",
      "pacientes.allergies":            "Alergias",
      "pacientes.medical_history":      "Historial médico",
      "pacientes.last_visit":           "Última visita",
      "pacientes.no_patients":          "No hay pacientes registrados.",
      "pacientes.search_placeholder":   "Buscar por nombre o documento...",

      // ═══════════════════════════════════════════════════════
      // MÓDULOS — HISTORIA CLÍNICA
      // ═══════════════════════════════════════════════════════
      "odo06.breadcrumb_aria": "Ruta de navegación",
      "odo06.breadcrumb_professional": "Profesional",
      "odo06.title": "Mis pacientes",
      "odo06.subtitle": "Consulta la información de los pacientes atendidos por ti.",
      "odo06.stats_aria": "Resumen de pacientes",
      "odo06.stat_total": "Total de pacientes",
      "odo06.stat_active": "Activos",
      "odo06.stat_followup": "En seguimiento",
      "odo06.stat_discharged": "Altas médicas",
      "odo06.filters_aria": "Filtros de pacientes",
      "odo06.search_placeholder": "Buscar por nombre, documento o servicio...",
      "odo06.search_aria": "Buscar por nombre, documento o servicio",
      "odo06.filter_status": "Estado",
      "odo06.filter_status_aria": "Filtrar por estado",
      "odo06.all_statuses": "Todos los estados",
      "odo06.filter_allergies": "Alergias",
      "odo06.filter_allergies_aria": "Filtrar por alergias",
      "odo06.all": "Todas",
      "odo06.with_allergies": "Con alergias",
      "odo06.without_allergies": "Sin alergias",
      "odo06.filter_appointment": "Próxima cita",
      "odo06.filter_appointment_aria": "Filtrar por próxima cita",
      "odo06.with_appointment": "Con cita",
      "odo06.without_appointment": "Sin cita",
      "odo06.filter_service": "Servicio",
      "odo06.filter_service_aria": "Filtrar por servicio",
      "odo06.all_services": "Todos los servicios",
      "odo06.clear_filters": "Limpiar todos los filtros",
      "odo06.clear": "Limpiar",
      "odo06.page_size_aria": "Pacientes por página",
      "odo06.per_page_5": "5 por página",
      "odo06.per_page_10": "10 por página",
      "odo06.per_page_15": "15 por página",
      "odo06.per_page_100": "100 por página",
      "odo06.patient_list": "Lista de pacientes",
      "odo06.patient": "Paciente",
      "odo06.last_visit": "Última consulta",
      "odo06.service": "Servicio",
      "odo06.next_appointment": "Próxima cita",
      "odo06.status": "Estado",
      "odo06.actions": "Acciones",
      "odo06.table_aria": "Tabla de pacientes atendidos",
      "odo06.pagination_aria": "Navegación de páginas",
      "odo06.pagination_showing": "Mostrando",
      "odo06.pagination_of": "de",
      "odo06.patient_count": "pacientes",
      "odo06.pagination_pages_aria": "Páginas de resultados",
      "odo06.previous_page": "Página anterior",
      "odo06.next_page": "Página siguiente",
      "odo06.page_number": "Página {number}",
      "odo06.result_count": "{count} resultado(s) encontrado(s)",
      "odo06.patient_row_aria": "Ver perfil de {name}",
      "odo06.action_profile": "Perfil",
      "odo06.action_history": "Historial",
      "odo06.action_profile_aria": "Ver perfil de {name}",
      "odo06.action_history_aria": "Ver historial clínico de {name}",
      "odo06.no_results_title": "No se encontraron pacientes",
      "odo06.no_results_text": "Ajusta los filtros o la búsqueda e inténtalo de nuevo.",
      "odo06.load_error_title": "No fue posible cargar los pacientes",
      "odo06.load_error": "Ocurrió un error al consultar la lista. Inténtalo de nuevo.",
      "odo06.service_unavailable": "Sin servicio registrado",
      "odo06.no_appointment": "Sin cita",
      "odo06.date_locale": "es-CO",

      // ═══════════════════════════════════════════════════════
      // MÓDULOS — SERVICIOS PROFESIONAL (ODO-05)
      // ═══════════════════════════════════════════════════════
      "odo05.breadcrumb_aria": "Ruta de navegación",
      "odo05.section_title": "Servicios y Recursos",
      "odo05.title": "Mis servicios",
      "odo05.subtitle": "Consulta los procedimientos asignados a tu perfil profesional.",
      "odo05.stats_aria": "Resumen de servicios",
      "odo05.assigned_services": "Servicios asignados",
      "odo05.available": "Disponibles",
      "odo05.unavailable": "No disponibles",
      "odo05.description_not_registered": "Sin descripción registrada para este procedimiento.",
      "odo05.panel_aria": "Catálogo de servicios",
      "odo05.catalog_title": "Catálogo profesional",
      "odo05.catalog_desc": "Explora los detalles de los servicios que tienes asignados.",
      "odo05.search_placeholder": "Buscar servicio o descripción...",
      "odo05.search_aria": "Buscar servicios",
      "odo05.specialty_label": "Especialidad",
      "odo05.specialty_aria": "Filtrar por especialidad",
      "odo05.all_specialties": "Todas",
      "odo05.status_label": "Estado",
      "odo05.status_aria": "Filtrar por estado",
      "odo05.all_statuses": "Todos",
      "odo05.status_available": "Disponible",
      "odo05.status_unavailable": "No disponible",
      "odo05.service_price": "Precio del servicio",
      "odo05.view_details": "Ver detalles",
      "odo05.no_results": "No hay servicios que coincidan con los filtros.",
      "odo05.no_results_help": "Ajusta los filtros de búsqueda e inténtalo de nuevo.",
      "odo05.no_services": "No tienes servicios asignados. Contacta al administrador para gestionar tus asignaciones.",
      "odo05.no_services_title": "Aún no tienes servicios asignados.",
      "odo05.no_specialty": "Sin especialidad",
      "odo05.not_specified": "No especificado",
      "odo05.minutes": "minutos",
      "odo05.professional_profile": "Perfil profesional",
      "odo05.modal_title": "Información del procedimiento",
      "odo05.close_details": "Cerrar detalles",
      "odo05.desc_heading": "Descripción",
      "odo05.specs_heading": "Características",
      "odo05.category_label": "Categoría",
      "odo05.duration_label": "Duración estimada",
      "odo05.base_price_label": "Precio base",
      "odo05.applicable_price_label": "Precio aplicable",
      "odo05.custom_price_label": "Precio personalizado",
      "odo05.modal_note": "Los precios y asignaciones se administran desde el módulo correspondiente. Esta vista es solo de consulta.",

      "historia.title":                 "Historia Clínica",
      "historia.new_record":            "Nueva entrada",
      "historia.diagnosis":             "Diagnóstico",
      "historia.treatment":             "Tratamiento",
      "historia.observations":          "Observaciones",
      "historia.date":                  "Fecha de consulta",
      "historia.professional":          "Profesional tratante",
      "historia.attachments":           "Archivos adjuntos",
      "historia.no_records":            "No hay registros clínicos disponibles.",
      "historia.add_attachment":        "Agregar archivo",
      "historia.teeth_chart":           "Odontograma",
      "historia.evolution":             "Evolución",
      "historia_admin.title": "Historial Clínico Global",
      "historia_admin.subtitle": "Acceso de solo lectura a los historiales del sistema.",
      "historia_admin.read_only": "Solo lectura",
      "historia_admin.read_only_admin": "Vista de solo lectura — Administrador",
      "historia_admin.stats": "Estadísticas de historiales",
      "historia_admin.total": "Historiales totales",
      "historia_admin.updated_today": "Actualizados hoy",
      "historia_admin.alerts": "Con alerta médica",
      "historia_admin.active_dentists": "Odontólogos activos",
      "historia_admin.filters": "Filtros de búsqueda",
      "historia_admin.search_placeholder": "Buscar por nombre, cédula o correo...",
      "historia_admin.search": "Buscar pacientes",
      "historia_admin.filter_dentist": "Filtrar por odontólogo",
      "historia_admin.all_dentists": "Todos los odontólogos",
      "historia_admin.filter_alerts": "Filtrar por alerta médica",
      "historia_admin.all_alerts": "Todas las alertas",
      "historia_admin.with_alert": "Con alerta médica",
      "historia_admin.without_alert": "Sin alerta",
      "historia_admin.filter_status": "Filtrar por estado",
      "historia_admin.all_statuses": "Todos los estados",
      "historia_admin.page_size": "Pacientes por página",
      "historia_admin.page_size_5": "5 por página",
      "historia_admin.page_size_10": "10 por página",
      "historia_admin.page_size_20": "20 por página",
      "historia_admin.page_size_100": "100 por página",
      "historia_admin.list": "Lista de historiales clínicos",
      "historia_admin.registered_patients": "Pacientes registrados",
      "historia_admin.table": "Historiales clínicos",
      "historia_admin.patient": "Paciente",
      "historia_admin.patient_singular": "paciente",
      "historia_admin.patients": "pacientes",
      "historia_admin.document": "Cédula",
      "historia_admin.dentist": "Odontólogo tratante",
      "historia_admin.last_visit": "Última consulta",
      "historia_admin.alert": "Alerta",
      "historia_admin.empty_title": "No se encontraron pacientes",
      "historia_admin.empty_description": "Ajusta los filtros o la búsqueda e inténtalo de nuevo.",
      "historia_admin.showing": "Mostrando",
      "historia_admin.of": "de",
      "historia_admin.page": "Página",
      "historia_admin.page_navigation": "Páginas de resultados",
      "historia_admin.previous": "Página anterior",
      "historia_admin.next": "Página siguiente",
      "historia_admin.history_sections": "Secciones del historial",
      "historia_admin.consultations": "Consultas",
      "historia_admin.treatments": "Tratamientos",
      "historia_admin.documents": "Documentos",
      "historia_admin.medical_alert": "Alerta médica",
      "historia_admin.view": "Ver historial",
      "historia_admin.view_history": "Ver historial clínico de {name}",
      "historia_admin.birth_date": "Fecha de nacimiento",
      "historia_admin.phone": "Teléfono",
      "historia_admin.yes": "Sí",
      "historia_admin.no": "No",
      "historia_admin.sessions": "sesiones",
      "historia_admin.no_consultation_notes": "Sin notas clínicas.",
      "historia_admin.no_consultations": "No hay consultas registradas.",
      "historia_admin.no_treatments": "No hay tratamientos registrados.",
      "historia_admin.no_documents": "No hay documentos registrados.",
      "historia_admin.results_count": "Mostrando {shown} de {total} {patients}",
      "historia_admin.error_title": "Error",
      "historia_admin.load_error": "No se pudo cargar el historial de pacientes.",

      // ═══════════════════════════════════════════════════════
      // MÓDULOS — PROFESIONALES
      // ═══════════════════════════════════════════════════════
      "profesionales.title":            "Gestión de Profesionales",
      "profesionales.new":              "Nuevo profesional",
      "profesionales.edit":             "Editar profesional",
      "profesionales.specialty":        "Especialidad",
      "profesionales.license":          "Registro profesional",
      "profesionales.schedule":         "Horario",
      "profesionales.availability":     "Disponibilidad",
      "profesionales.status":           "Estado",
      "profesionales.no_professionals": "No hay profesionales registrados.",
      "asignados.title": "Servicios asignados a profesionales",
      "asignados.subtitle": "Asigna servicios según las especialidades registradas y configura precios en COP.",
      "asignados.back": "Volver a profesionales",
      "asignados.select_professional": "Seleccionar profesional",
      "asignados.professional": "Profesional",
      "asignados.choose_professional": "Selecciona un profesional",
      "asignados.consult": "Consultar",
      "asignados.assign_title": "Asignar o actualizar un servicio",
      "asignados.compatible_services_hint": "Solo se muestran servicios activos de sus especialidades.",
      "asignados.registered_specialties": "Especialidades registradas:",
      "asignados.none": "Ninguna",
      "asignados.professional_inactive": "Este profesional no está activo. No se permiten nuevas asignaciones.",
      "asignados.no_compatible_services": "No hay servicios activos compatibles. Verifica las especialidades del profesional.",
      "asignados.service": "Servicio",
      "asignados.choose_service": "Selecciona un servicio para asignar o editar",
      "asignados.select_service_hint": "Selecciona un servicio para consultar su estado.",
      "asignados.custom_price": "Precio personalizado (COP, opcional)",
      "asignados.base_price_placeholder": "Vacío = precio base",
      "asignados.price_help": "Deja el campo vacío para utilizar el precio base. Si el servicio ya está asignado, se cargará su precio actual.",
      "asignados.choose_service_button": "Selecciona un servicio",
      "asignados.assigned_services": "Servicios del profesional",
      "asignados.no_assignments": "No hay servicios asignados",
      "asignados.no_assignments_hint": "Selecciona un servicio compatible para crear una asignación.",
      "asignados.table_aria": "Servicios asignados al profesional",
      "asignados.base_price": "Precio base",
      "asignados.custom_price_short": "Precio personalizado",
      "asignados.applied_price": "Precio aplicado",
      "asignados.active": "Activa",
      "asignados.inactive": "Inactiva",
      "asignados.deactivate": "Desactivar",
      "asignados.reactivate": "Reactivar",
      "asignados.new_assignment": "Nueva asignación: este servicio todavía no está vinculado al profesional.",
      "asignados.active_assignment": "Asignación existente y activa: guardar actualizará su precio.",
      "asignados.inactive_assignment": "Asignación inactiva: guardar actualizará su precio y la reactivará.",
      "asignados.assign_service": "Asignar servicio",
      "asignados.update_assignment": "Actualizar asignación",
      "asignados.update_reactivate": "Actualizar y reactivar",
      "asignados.unassigned": "Sin asignar",
      "asignados.already_active": "Ya asignado (activo)",
      "asignados.already_inactive": "Ya asignado (inactivo)",
      "asignados.error_invalid_pair": "El profesional y el servicio deben existir y estar activos.",
      "asignados.error_specialty": "El profesional no tiene registrada la especialidad de este servicio.",
      "asignados.error_invalid_price": "El precio personalizado debe ser mayor que cero, con máximo dos decimales; o déjalo vacío para usar el precio base.",
      "asignados.error_save": "No se pudo guardar la asignación. Revisa las restricciones de la base de datos.",
      "asignados.error_assignment_missing": "La asignación no existe.",
      "asignados.error_reactivate": "No se puede reactivar: profesional, servicio y especialidad deben ser compatibles y estar activos.",
      "asignados.error_update_status": "No se pudo actualizar el estado.",
      "asignados.success_saved": "Asignación guardada correctamente.",
      "asignados.success_reactivated": "Asignación reactivada.",
      "asignados.success_deactivated": "Asignación desactivada; se conserva su registro.",

      // ═══════════════════════════════════════════════════════
      // MÓDULOS — FACTURACIÓN
      // ═══════════════════════════════════════════════════════
      "facturacion.title":              "Facturación y Pagos",
      "facturacion.invoice":            "Factura",
      "facturacion.new_invoice":        "Nueva factura",
      "facturacion.payment":            "Pago",
      "facturacion.register_payment":   "Registrar pago",
      "facturacion.amount":             "Monto",
      "facturacion.date":               "Fecha",
      "facturacion.method":             "Método de pago",
      "facturacion.status":             "Estado",
      "facturacion.total":              "Total",
      "facturacion.subtotal":           "Subtotal",
      "facturacion.discount":           "Descuento",
      "facturacion.tax":                "Impuesto",
      "facturacion.no_invoices":        "No hay facturas disponibles.",
      "facturacion.method_cash":        "Efectivo",
      "facturacion.method_card":        "Tarjeta",
      "facturacion.method_transfer":    "Transferencia",
      "facturacion.method_nequi":       "Nequi",
      "facturacion.method_daviplata":   "DaviPlata",

      // ═══════════════════════════════════════════════════════
      // MÓDULOS — PQR
      // ═══════════════════════════════════════════════════════
      "pqr.title":                      "Gestión de PQR",
      "pqr.admin_subtitle":             "Administra, consulta y responde las solicitudes de los pacientes.",
      "pqr.notifications_aria":         "Nuevas solicitudes PQR",
      "pqr.no_notifications":           "No hay nuevas solicitudes PQR",
      "pqr.ticket":                     "Radicado",
      "pqr.priority":                   "Prioridad",
      "pqr.all_types":                  "Todos los tipos",
      "pqr.all_statuses":               "Todos los estados",
      "pqr.status_received":            "Recibido",
      "pqr.status_in_progress":         "En proceso",
      "pqr.status_pending_info":         "Pendiente de información",
      "pqr.status_resolved":             "Resuelto",
      "pqr.status_closed":               "Cerrado",
      "pqr.table_aria":                 "Solicitudes PQR",
      "pqr.search_aria":                "Buscar solicitudes PQR",
      "pqr.detail_aria":                "Detalle de solicitud PQR",
      "pqr.view_details":               "Ver detalle",
      "pqr.view_details_aria":          "Ver detalle de la solicitud {id}",
      "pqr.close_detail":               "Cerrar detalle",
      "pqr.select_case":                "Selecciona un caso",
      "pqr.select_case_help":            "Selecciona una solicitud para ver el detalle y gestionar la respuesta.",
      "pqr.unanswered":                 "Sin responder",
      "pqr.in_management":              "En gestión",
      "pqr.overdue":                    "Vencidas",
      "pqr.general_information":        "Información general",
      "pqr.open_time":                  "Tiempo abierto:",
      "pqr.patient":                    "Paciente",
      "pqr.attached_evidence":           "Evidencia adjunta",
      "pqr.view_attachment":             "Ver archivo adjunto",
      "pqr.response_thread":             "Hilo de respuestas",
      "pqr.no_response":                "Aún no se ha respondido esta solicitud.",
      "pqr.reply":                      "Responder",
      "pqr.reply_placeholder":          "Escribe tu respuesta al paciente...",
      "pqr.send_response":              "Enviar respuesta",
      "pqr.change_status":              "Cambiar estado",
      "pqr.received":                   "Recibida",
      "pqr.resolved":                   "Resuelta",
      "pqr.rejected":                   "Rechazada",
      "pqr.new":                        "Nueva solicitud",
      "pqr.type":                       "Tipo",
      "pqr.type_petition":              "Petición",
      "pqr.type_complaint":             "Queja",
      "pqr.type_claim":                 "Reclamo",
      "pqr.type_suggestion":            "Sugerencia",
      "pqr.subject":                    "Asunto",
      "pqr.description":                "Descripción",
      "pqr.status":                     "Estado",
      "pqr.date":                       "Fecha",
      "pqr.response":                   "Respuesta",
      "pqr.no_requests":                "No hay solicitudes registradas.",
      "pqr.response_time":              "Tiempo de respuesta: ≤ 48h hábiles",

      // ═══════════════════════════════════════════════════════
      // MÓDULOS — SERVICIOS Y RECURSOS
      // ═══════════════════════════════════════════════════════
      "servicios.title":                "Servicios y Recursos",
      "servicios.new":                  "Nuevo servicio",
      "servicios.edit":                 "Editar servicio",
      "servicios.name":                 "Nombre",
      "servicios.description":          "Descripción",
      "servicios.price":                "Precio",
      "servicios.duration":             "Duración",
      "servicios.category":             "Categoría",
      "servicios.active":               "Activo",
      "servicios.no_services":          "No hay servicios registrados.",
      "servicios.catalog_title":        "Catálogo de servicios",
      "servicios.catalog_subtitle":     "Administra precios, categorías y disponibilidad.",
      "servicios.summary":              "Resumen de servicios",
      "servicios.total_sub":            "Servicios registrados",
      "servicios.active_plural":        "Servicios activos",
      "servicios.active_sub":           "Disponibles para asignar",
      "servicios.inactive_plural":      "Servicios inactivos",
      "servicios.inactive_sub":         "No disponibles para asignar",
      "servicios.filters":              "Filtros de servicios",
      "servicios.search_placeholder":   "Buscar por nombre o descripción",
      "servicios.search_aria":          "Buscar servicios",
      "servicios.filter_category":      "Filtrar por categoría",
      "servicios.all_categories":       "Todas las categorías",
      "servicios.category_prevencion":  "Prevención",
      "servicios.category_estetica":    "Estética",
      "servicios.category_cirugia":     "Cirugía",
      "servicios.category_ortodoncia":  "Ortodoncia",
      "servicios.category_endodoncia":  "Endodoncia",
      "servicios.category_periodoncia": "Periodoncia",
      "servicios.category_general":     "General",
      "servicios.select_category":      "Selecciona una categoría",
      "servicios.category_help":        "Si el servicio tiene una categoría antigua, selecciona una categoría válida para guardar.",
      "servicios.filter_status":        "Filtrar por estado",
      "servicios.all_statuses":         "Todos los estados",
      "servicios.table_title":          "Lista de servicios",
      "servicios.column_name":          "Servicio",
      "servicios.pagination":           "Páginas de resultados",
      "servicios.pagination_showing":   "Mostrando",
      "servicios.pagination_of":        "de",
      "servicios.pagination_records":   "servicios",
      "servicios.close_drawer":         "Cerrar panel",
      "servicios.specialty":            "Especialidad clínica",
      "servicios.select_specialty":     "Selecciona una especialidad",
      "servicios.specialty_required_hint": "La especialidad es obligatoria para guardar el servicio.",
      "servicios.no_specialties":       "No hay especialidades configuradas; no se pueden guardar servicios.",
      "servicios.price_cop":            "Precio base (COP)",
      "servicios.duration_minutes":     "Duración (minutos)",
      "servicios.current_status":       "Estado actual",
      "servicios.status_change_hint":   "Cambia la disponibilidad desde las acciones de la tabla.",
      "servicios.save_changes":         "Guardar cambios",
      "servicios.create":               "Crear servicio",
      "servicios.empty_title":          "No se encontraron servicios",
      "servicios.empty_filtered":       "Ajusta los filtros o la búsqueda e inténtalo de nuevo.",
      "servicios.specialty_not_configured": "Sin especialidad configurada",
      "servicios.minutes_short":        "min",
      "servicios.status_aria":          "Estado: {status}",
      "servicios.service_aria":         "Servicio {name}",
      "servicios.edit_action":          "Editar servicio {name}",
      "servicios.activate_action":      "Activar servicio {name}",
      "servicios.deactivate_action":    "Desactivar servicio {name}",
      "servicios.activate":             "Activar",
      "servicios.deactivate":           "Desactivar",
      "servicios.page_number":          "Página {page}",
      "servicios.previous_page":        "Página anterior",
      "servicios.next_page":            "Página siguiente",
      "servicios.required_fields_error": "Completa el nombre, la categoría y la especialidad.",
      "servicios.price_error":          "El precio debe ser mayor que cero.",
      "servicios.duration_error":       "La duración debe estar entre 5 y 480 minutos.",
      "servicios.create_error":         "No fue posible crear el servicio.",
      "servicios.update_error":         "No fue posible actualizar el servicio.",
      "servicios.status_error":         "No fue posible cambiar el estado del servicio.",
      "servicios.created":              "Servicio «{name}» creado correctamente.",
      "servicios.updated":              "Servicio «{name}» actualizado correctamente.",
      "servicios.activated":            "Servicio «{name}» activado.",
      "servicios.deactivated":          "Servicio «{name}» desactivado.",
      "servicios.toast_success":        "Éxito",
      "servicios.toast_error":          "Error",
      "servicios.toast_warning":        "Validación",
      "servicios.toast_info":           "Información",

      // ═══════════════════════════════════════════════════════
      // MÓDULOS — CONFIGURACIÓN
      // ═══════════════════════════════════════════════════════
      "config.title":                   "Configuración General",
      "config.clinic_name":             "Nombre de la clínica",
      "config.address":                 "Dirección",
      "config.phone":                   "Teléfono",
      "config.email":                   "Correo",
      "config.schedule":                "Horario de atención",
      "config.save":                    "Guardar configuración",
      "config.saved":                   "Configuración guardada correctamente.",
      "config.subtitle": "Administra los datos del centro, las citas y las notificaciones.",
      "config.appointments": "Configuración de citas",
      "config.duration": "Duración predeterminada (minutos)",
      "config.opening_time": "Horario de apertura",
      "config.closing_time": "Horario de cierre",
      "config.center_data": "Datos del centro",
      "config.center_hint": "Información que aparece en documentos y comunicaciones.",
      "config.nit": "Número de NIT / Identificación",
      "config.nit_hint": "Formato: 000.000.000-0",
      "config.email_reminders": "Recordatorios por correo",
      "config.email_reminders_hint": "Enviar avisos por correo antes de la cita.",
      "config.sms_reminders": "Recordatorios por SMS",
      "config.sms_reminders_hint": "Enviar avisos de texto al teléfono registrado.",
      "config.system_notifications": "Notificaciones del sistema",
      "config.system_notifications_hint": "Mostrar alertas internas de actividad.",
      "config.save_title": "Guardar configuración",
      "config.save_hint": "Los cambios se aplicarán al sistema cuando guardes el formulario.",
      "config.hours_error": "La hora de apertura debe ser anterior a la hora de cierre.",
      "config.save_error": "No fue posible guardar la configuración.",
      "config.toast_success": "Configuración",
      "config.toast_error": "Error",
      "config.toast_warning": "Validación",

      "nav.item_homepage": "Inicio",
      "nav.item_panel_analitica": "Panel de Analítica",
      "nav.item_gestion_usuarios": "Gestión de Usuarios",
      "nav.item_gestion_roles": "Gestión de Roles",
      "nav.item_bitacora_sistema": "Bitácora del Sistema",
      "nav.item_cambiar_contrasena": "Cambiar Contraseña",
      "nav.item_gestion_pacientes": "Gestión de Pacientes",
      "nav.item_historial_clinico": "Historial Clínico",
      "nav.item_gestion_profesionales": "Gestión de Profesionales",
      "nav.item_reportes_clinicos": "Reportes Clínicos",
      "nav.item_dashboard_general": "Dashboard General",
      "nav.item_agenda_general": "Agenda General",
      "nav.item_gestion_integral_citas": "Gestión Integral de Citas",
      "nav.item_gestion_facturacion": "Gestión de Facturación",
      "nav.item_catalogo_servicios": "Catálogo de Servicios",
      "nav.item_configuracion_general": "Configuración General",
      "nav.item_peticiones_quejas_reclamos": "Gestión de PQR",
      "nav.item_nueva_solicitud": "Nueva Solicitud",
      "nav.item_como_programar_cita": "Cómo programar una cita",
      "nav.item_guias_tutoriales": "Guías y Tutoriales",
      "nav.item_soporte_tickets": "Soporte y Tickets",
      "nav.group_inicio": "Inicio",
      "nav.group_reportes_analitica": "Reportes y Analítica",
      "nav.group_acceso_seguridad": "Acceso y Seguridad",
      "nav.group_gestion_pacientes": "Gestión de Pacientes",
      "nav.group_historia_clinica": "Historia Clínica",
      "nav.group_gestion_profesionales": "Gestión de Profesionales",
      "nav.group_gestion_citas": "Gestión de Citas",
      "nav.group_facturacion_pagos": "Facturación y Pagos",
      "nav.group_servicios_recursos": "Servicios y Recursos",
      "nav.group_gestion_pqr": "Gestión de PQR",
      "nav.group_centro_ayuda": "Centro de Ayuda",
      "sidebar_logout_text": "Cerrar sesión",
      "sidebar_logout_aria": "Cerrar sesión y volver al inicio de sesión",
      "sidebar_main_nav": "Navegación principal",
      "sidebar_change_language": "Cambiar idioma",
      "sidebar_lang_es": "Español",
      "sidebar_lang_en": "English",

      "adm01_bc_admin": "Administración",
      "adm01_bc_aria": "Ruta de navegación",
      "adm01_bc_active": "Dashboard de citas",
      "adm01_title": "Dashboard de citas",
      "adm01_subtitle": "Resumen operativo del centro odontológico",
      "adm01_export_pdf_aria": "Exportar dashboard en PDF",
      "adm01_export_pdf_btn": "Exportar PDF",
      "adm01_stat_pacientes": "Pacientes activos",
      "adm01_stat_pacientes_sub": "Total registrados",
      "adm01_stat_citas_hoy": "Citas hoy",
      "adm01_stat_citas_hoy_sub": "No canceladas",
      "adm01_stat_profesionales": "Profesionales activos",
      "adm01_stat_profesionales_sub": "En servicio",
      "adm01_stat_valor_atendido": "Valor atendido",
      "adm01_stat_valor_atendido_sub": "Estimado según servicios",
      "adm01_occupancy_aria": "Ocupación de agenda mensual",
      "adm01_occupancy_title": "Ocupación de agenda",
      "adm01_occupancy_sub": "Reservas frente a la capacidad mensual",
      "adm01_distribution_title": "Distribución por estado — mes actual",
      "adm01_distribution_aria": "Estados de las citas del mes",
      "adm01_dist_attended": "Atendidas",
      "adm01_dist_attended_sub": "vs mismos días de la semana anterior",
      "adm01_dist_confirmed": "Confirmadas",
      "adm01_dist_scheduled": "Programadas",
      "adm01_dist_cancelled": "Canceladas",
      "adm01_dist_other": "Otros estados",
      "adm01_dist_of_total": "del total",
      "adm01_top_prof_title": "Top 3 profesionales del mes",
      "adm01_top_prof_caption": "Los tres profesionales con más citas atendidas este mes",
      "adm01_top_prof_table_aria": "Ranking de profesionales por citas",
      "adm01_th_professional": "Profesional",
      "adm01_th_specialty": "Especialidad",
      "adm01_th_total_appointments": "Total citas",
      "adm01_top_prof_empty": "No hay datos suficientes para generar el ranking.",
      "adm01_quick_aria": "Acciones rápidas",
      "adm01_quick_title": "Acciones rápidas",
      "adm01_quick_open_agenda": "Abrir agenda",
      "adm01_quick_open_agenda_aria": "Ir a la agenda general",
      "adm01_quick_manage": "Gestión de citas",
      "adm01_quick_manage_aria": "Ir a gestión de citas",
      "adm01_notes_title": "Notas del sistema",
      "adm01_notes_text": "Consulta la agenda general o la gestión de citas para modificar una cita o verificar disponibilidad.",
      "adm01_mobile_nav_aria": "Navegación móvil",
      "adm01_mobile_dashboard": "Dashboard",
      "adm01_mobile_agenda": "Agenda",
      "adm01_mobile_citas": "Citas",
      "adm08_bc_admin": "Administración",
      "adm08_bc_active": "Agenda General",
      "adm08_bc_aria": "Ruta de navegación",
      "adm08_title": "Agenda General",
      "adm08_subtitle": "Gestión de citas y horarios",
      "adm08_controls_aria": "Controles de navegación y filtros",
      "adm08_weeknav_aria": "Navegación semanal",
      "adm08_prev_week_aria": "Semana anterior",
      "adm08_next_week_aria": "Próxima semana",
      "adm08_today_aria": "Ir a la semana actual",
      "adm08_today": "Hoy",
      "adm08_filter_prof_label": "Profesional",
      "adm08_filter_prof_aria": "Filtrar por profesional",
      "adm08_filter_prof_all": "Todos los profesionales",
      "adm08_filter_office_label": "Consultorio",
      "adm08_filter_office_aria": "Filtrar por consultorio",
      "adm08_filter_office_all": "Todos los consultorios",
      "adm08_hours_aria": "Horario de atención de la clínica",
      "adm08_hours_title": "Horario de la clínica",
      "adm08_hours_min": "min por cita",
      "adm08_print_aria": "Imprimir agenda semanal",
      "adm08_print_btn": "Imprimir Agenda",
      "adm08_tabla_integral_aria": "Abrir tabla integral de citas",
      "adm08_tabla_integral_btn": "Tabla integral",
      "adm08_calendar_title": "Calendario semanal de citas",
      "adm08_day_closed": "CERRADO",
      "adm08_no_appointments": "Sin citas",
      "adm08_legend_title": "Estados de cita",
      "adm08_legend_scheduled": "Programada",
      "adm08_legend_confirmed": "Confirmada",
      "adm08_legend_attended": "Asistida",
      "adm08_legend_cancelled": "Cancelada",
      "adm08_legend_available": "Disponible",
      "adm08_modal_close_aria": "Cerrar ventana",
      "adm08_modal_appt_title": "Detalle de Cita",
      "adm08_modal_newappt_title": "Agendar Nueva Cita",
      "adm08_form_hora_inicio": "Hora Inicio",
      "adm08_form_hora_fin": "Hora Fin",
      "adm08_form_duracion_hint": "Calculada según la duración configurada.",
      "adm08_form_select_paciente": "Seleccione paciente...",
      "adm08_form_select_profesional": "Seleccione profesional",
      "adm08_form_select_consultorio": "Seleccione consultorio",
      "adm08_form_servicio_label": "Servicio (Motivo de Consulta)",
      "adm08_form_select_servicio": "Seleccione servicio",
      "adm08_form_observaciones": "Observaciones",
      "adm08_form_notas_ph": "Detalles adicionales...",
      "adm08_form_guardar_cita": "Guardar Cita",
      "adm08_mobile_nav_aria": "Navegación móvil",
      "adm08_mobile_dashboard": "Dashboard",
      "adm08_mobile_agenda": "Agenda",
      "adm08_mobile_citas": "Citas"
      ,"footer_copyright": "Todos los derechos reservados."
      ,"footer_project": "Proyecto Formativo ADSO — SENA"
      ,"reportesadmin_subtitle": "Indicadores administrativos y actividad de la clínica"
      ,"reportesadmin_updated": "Actualizado hace"
      ,"reportesadmin_minutes": "min"
      ,"reportesadmin_btn_pdf": "Descargar PDF"
      ,"reportesadmin_btn_csv": "Exportar CSV"
      ,"reportesadmin_stat_income": "Ingresos Totales"
      ,"reportesadmin_stat_patients": "Nuevos Pacientes"
      ,"reportesadmin_stat_completed": "Citas Completadas"
      ,"reportesadmin_stat_retention": "Tasa de Retención"
      ,"reportesadmin_stat_no_show": "Tasa de Inasistencia"
      ,"reportesadmin_stat_period": "Citas del periodo"
      ,"reportesadmin_trend": "vs mes anterior"
      ,"reportesadmin_no_comparison": "Sin base de comparación"
      ,"reportesadmin_chart_growth": "Crecimiento de la Clínica"
      ,"reportesadmin_this_year": "Este año"
      ,"reportesadmin_last_year": "Año pasado"
      ,"reportesadmin_distribution": "Distribución de Servicios"
      ,"reportesadmin_in_period": "citas en el periodo"
      ,"reportesadmin_total": "Totales"
      ,"reportesadmin_highest": "Mayor"
      ,"reportesadmin_professional_income": "Ingresos por Profesional"
      ,"reportesadmin_recent": "Reportes Recientes"
      ,"reportesadmin_new": "+ Nuevo Reporte"
      ,"reportesadmin_view_all": "Ver todo →"
      ,"reportesadmin_filters_aria": "Filtrar reportes por categoría"
      ,"reportesadmin_cat_all": "Todos"
      ,"reportesadmin_cat_financial": "Financiero"
      ,"reportesadmin_cat_patients": "Pacientes"
      ,"reportesadmin_cat_operational": "Operativo"
      ,"reportesadmin_cat_logistics": "Logística"
      ,"reportesadmin_th_name": "Nombre del Reporte"
      ,"reportesadmin_th_date": "Fecha Generado"
      ,"reportesadmin_th_category": "Categoría"
      ,"reportesadmin_th_status": "Estado"
      ,"reportesadmin_th_action": "Acción"
      ,"reportesadmin_download_csv": "CSV"
      ,"reportesadmin_download_pdf": "PDF"
      ,"reportesadmin_download_csv_title": "Descargar CSV"
      ,"reportesadmin_download_pdf_title": "Descargar PDF"
      ,"reportesadmin_empty_title": "No se encontraron reportes"
      ,"reportesadmin_empty_desc": "Prueba con otra categoría o genera un nuevo reporte."
      ,"reportesadmin_empty_action": "+ Generar Reporte"
      ,"reportesadmin_modal_new_title": "Nuevo Reporte"
      ,"reportesadmin_modal_new_desc": "Configura los detalles del reporte para guardar una copia agregada en el historial."
      ,"reportesadmin_name": "Nombre del reporte"
      ,"reportesadmin_name_ph": "Informe de ingresos mensuales"
      ,"reportesadmin_category": "Categoría"
      ,"reportesadmin_period": "Periodo"
      ,"reportesadmin_notes": "Notas u objetivos"
      ,"reportesadmin_notes_ph": "Ej. Comparar con el trimestre anterior"
      ,"reportesadmin_cancel": "Cancelar"
      ,"reportesadmin_create": "Crear Reporte"
      ,"reportesadmin_modal_all_title": "Todos los reportes"
      ,"reportesadmin_modal_all_desc": "Accede a los reportes generados y descarga el formato que necesites."
      ,"reportesadmin_close": "Cerrar"
      ,"reportesadmin_pdf_title": "Descargar PDF"
      ,"reportesadmin_pdf_desc": "Selecciona el contenido que deseas incluir en el PDF del reporte actual."
      ,"reportesadmin_pdf_metrics": "Incluir indicadores y tabla de métricas"
      ,"reportesadmin_pdf_summary": "Incluir resumen ejecutivo"
      ,"reportesadmin_pdf_download": "Descargar PDF"
      ,"reportesadmin_csv_title": "Exportar CSV"
      ,"reportesadmin_csv_desc": "Elige los datos a exportar y descarga los registros agregados en formato CSV."
      ,"reportesadmin_csv_headers": "Incluir encabezados de columna"
      ,"reportesadmin_csv_all": "Exportar todos los reportes"
      ,"reportesadmin_csv_export": "Exportar CSV"
      ,"reportesadmin_ready": "Listo"
      ,"reportesadmin_toast_success": "El reporte quedó guardado en el historial."
      ,"reportesadmin_toast_error": "No se pudo generar el reporte."
      ,"reportesadmin_toast_success_title": "Reporte generado"
      ,"reportesadmin_toast_error_title": "Error al generar el reporte"
      ,"reportesadmin_breadcrumb_admin": "Administración"
      ,"reportesadmin_breadcrumb_current": "Reportes y Estadísticas"
      ,"reportesadmin_stats_aria": "Indicadores clave"
      ,"reportesadmin_growth_aria": "Evolución mensual del crecimiento de la clínica"
      ,"reportesadmin_distribution_aria": "Distribución de servicios"
      ,"reportesadmin_status_title": "Estado de Citas"
      ,"reportesadmin_appointments": "citas"
      ,"reportesadmin_procedures_title": "Procedimientos Más Solicitados"
      ,"reportesadmin_view_all_procedures": "Ver todo →"
      ,"reportesadmin_th_procedure": "Procedimiento"
      ,"reportesadmin_th_quantity": "Cantidad"
      ,"reportesadmin_th_income": "Ingresos"
      ,"reportesadmin_th_average_price": "Precio Prom."
      ,"reportesadmin_th_trend": "Tendencia"
      ,"reportesadmin_all_procedures_title": "Todos los Procedimientos"
      ,"reportesadmin_all_procedures_desc": "Visualiza la lista completa de procedimientos más solicitados de la clínica."
      ,"reportesadmin_state_completed": "Completadas"
      ,"reportesadmin_state_scheduled": "Programadas"
      ,"reportesadmin_state_in_progress": "En proceso"
      ,"reportesadmin_state_cancelled": "Canceladas"
      ,"reportesadmin_state_no_show": "Inasistencias"
      ,"reportesadmin_state_other": "Otros"
      ,"reportesadmin_month_jan": "Ene"
      ,"reportesadmin_month_feb": "Feb"
      ,"reportesadmin_month_mar": "Mar"
      ,"reportesadmin_month_apr": "Abr"
      ,"reportesadmin_month_may": "May"
      ,"reportesadmin_month_jun": "Jun"
      ,"reportesadmin_month_jul": "Jul"
      ,"reportesadmin_month_aug": "Ago"
      ,"reportesadmin_month_sep": "Sep"
      ,"reportesadmin_month_oct": "Oct"
      ,"reportesadmin_month_nov": "Nov"
      ,"reportesadmin_month_dec": "Dec"
      ,"reportesprof.breadcrumb_professional": "Profesional"
      ,"reportesprof.breadcrumb_current": "Reportes y estadísticas"
      ,"reportesprof.title": "Reportes y estadísticas"
      ,"reportesprof.subtitle": "Actividad clínica del mes actual y citas recientes."
      ,"reportesprof.open_schedule": "Ir a mi agenda"
      ,"reportesprof.download_pdf": "Imprimir / guardar PDF"
      ,"reportesprof.stats_aria": "Indicadores clave"
      ,"reportesprof.appointments_month": "Citas del mes"
      ,"reportesprof.vs_previous_month": "vs. mes anterior"
      ,"reportesprof.no_previous_period": "Sin periodo anterior comparable"
      ,"reportesprof.paid_income_month": "Ingresos pagados del mes"
      ,"reportesprof.patients_month": "Pacientes del mes"
      ,"reportesprof.unique_patients_month": "Pacientes únicos con cita"
      ,"reportesprof.cancellations_month": "Cancelaciones del mes"
      ,"reportesprof.cancellation_rate": "de las citas del mes"
      ,"reportesprof.treatments_title": "Tratamientos atendidos este mes"
      ,"reportesprof.no_treatments_title": "Sin tratamientos registrados"
      ,"reportesprof.no_treatments_desc": "No hay citas atendidas este mes."
      ,"reportesprof.frequent_patients_title": "Pacientes con más citas (6 meses)"
      ,"reportesprof.no_patients_title": "Sin actividad reciente"
      ,"reportesprof.no_patients_desc": "No hay citas de pacientes en los últimos seis meses."
      ,"reportesprof.appointments": "citas"
      ,"reportesprof.upcoming_title": "Próximos días"
      ,"reportesprof.upcoming_subtitle": "Citas de los próximos tres días"
      ,"reportesprof.day_today": "Hoy"
      ,"reportesprof.day_tomorrow": "Mañana"
      ,"reportesprof.day_next": "Siguiente"
      ,"reportesprof.status_completed": "Completada"
      ,"reportesprof.status_scheduled": "Programada"
      ,"reportesprof.status_in_progress": "En proceso"
      ,"reportesprof.status_cancelled": "Cancelada"
      ,"reportesprof.status_no_show": "Inasistencia"
      ,"reportesprof.status_other": "Otro"
      ,"reportesprof.recent_title": "Citas recientes"
      ,"reportesprof.history_link": "Ir al historial clínico"
      ,"reportesprof.patient": "Paciente"
      ,"reportesprof.service": "Servicio"
      ,"reportesprof.time": "Hora"
      ,"reportesprof.status": "Estado"
      ,"reportesprof.action": "Acción"
      ,"reportesprof.no_recent_title": "Sin citas recientes"
      ,"reportesprof.no_recent_desc": "Cuando tengas citas registradas, aparecerán aquí."
      ,"reportesprof.view_appointment": "Ver detalle de la cita"
      ,"reportesprof.appointment_detail": "Detalle de la cita"
      ,"reportesprof.treatment_detail": "Detalle del tratamiento"
      ,"reportesprof.share_of_treatments": "% de tratamientos atendidos"
      ,"status.new": "Nueva"
      ,"status.requested": "Solicitada"
      ,"status.rejected": "Rechazada"
      ,"citas.stat_rejected": "Solicitudes rechazadas"
      ,"citas.tab_all": "Todas las citas"
      ,"citas.tab_pending": "Pendientes"
      ,"citas.tab_completed": "Completadas"
      ,"citas.tab_cancelled": "Canceladas"
      ,"citas.tab_rejected": "Rechazadas"
      ,"citas.loading_progress": "Cargando..."
      ,"citas.form_date": "Fecha"
      ,"citas.form_service": "Servicio"
      ,"citas.form_professional": "Profesional"
      ,"citas.form_schedule": "Horario disponible recomendado"
      ,"citas.form_notes": "Notas adicionales"
      ,"citas.select_date_service_first": "Selecciona fecha y servicio primero"
      ,"citas.select_professional_first": "Selecciona un profesional primero"
      ,"citas.schedule_help": "Elige una fecha, servicio y profesional para consultar horarios disponibles."
      ,"citas.btn_schedule": "Agendar cita"
      ,"citas.cancel_reason_label": "Motivo de cancelación *"
      ,"citas.cancel_reason_help": "Describe brevemente el motivo (mínimo 5 caracteres)."
      ,"notifications.only_unread": "Solo no leídas"
      ,"notifications.only_unread_aria": "Mostrar solo notificaciones no leídas"
      ,"notifications.show_all": "Ver todas"
      ,"notifications.filter_rejected": "Solicitudes rechazadas"
      ,"notifications.modal_close_aria": "Cerrar detalles"
      ,"notifications.modal_detail_title": "Detalle de Notificación"
      ,"notifications.meta_date": "Fecha / Hora:"
      ,"notifications.meta_channel": "Canal:"
      ,"notifications.channel_inapp": "Interno (In-App)"
      ,"notifications.go_to_section": "Ir a la sección"
      ,"pac_complete.breadcrumb_aria": "Ruta de navegación"
      ,"pac_complete.breadcrumb_paciente": "Paciente"
      ,"pac_complete.breadcrumb_current": "Completar perfil"
      ,"pac_complete.title": "Completa tus datos personales"
      ,"pac_complete.subtitle": "Necesitamos tu documento y fecha de nacimiento reales para completar tu perfil."
      ,"pac_complete.label_nombre": "Nombre"
      ,"pac_complete.label_correo": "Correo"
      ,"pac_complete.label_tipo_documento": "Tipo de documento"
      ,"pac_complete.option_select": "Selecciona un tipo"
      ,"pac_complete.option_cc": "Cédula de ciudadanía"
      ,"pac_complete.option_ti": "Tarjeta de identidad"
      ,"pac_complete.option_ce": "Cédula de extranjería"
      ,"pac_complete.option_pas": "Pasaporte"
      ,"pac_complete.label_documento": "Número de documento"
      ,"pac_complete.label_fecha_nacimiento": "Fecha de nacimiento"
      ,"pac_complete.btn_save": "Guardar perfil"
      ,"pac_pqr.page_title": "Nueva PQR"
      ,"pac_pqr.page_subtitle": "Módulo PQR - SmileTrack"
      ,"pac_pqr.label_email": "Correo electrónico"
      ,"pac_pqr.ph_email": "correo@ejemplo.com"
      ,"pac_pqr.label_phone": "Teléfono de contacto"
      ,"pac_pqr.ph_phone": "+57 300 000 0000"
      ,"pac_pqr.label_service_date": "Fecha del servicio"
      ,"pac_pqr.label_dentist": "Odontólogo tratante"
      ,"pac_pqr.ph_dentist": "Nombre del profesional"
      ,"pac_pqr.label_subject": "Asunto *"
      ,"pac_pqr.ph_subject": "Ej: Demora en la atención de la cita"
      ,"pac_pqr.label_description": "Descripción detallada *"
      ,"pac_pqr.ph_description": "Describe con detalle tu solicitud, queja o reclamo. Incluye fechas, nombres y circunstancias relevantes."
      ,"pac_pqr.label_attachment": "Adjuntar soporte (opcional)"
      ,"pac_pqr.upload_hint": "Haz clic para adjuntar un archivo (PDF, JPG, PNG — máx. 5 MB)"
      ,"pac_pqr.submit": "Radicar solicitud"
      ,"pac_pqr.terms": "Al enviar, aceptas nuestra política de tratamiento de datos personales."
      ,"pac_pqr.response_times": "Tiempos de respuesta"
      ,"pac_pqr.time_petition": "15 días hábiles"
      ,"pac_pqr.time_complaint": "10 días hábiles"
      ,"pac_pqr.time_claim": "5 días hábiles"
      ,"pac_pqr.contact_channels": "Canales de atención"
      ,"pac_pqr.channel_phone_label": "Línea PQR"
      ,"pac_pqr.channel_phone_hours": "Lun - Vie: 7 am - 6 pm"
      ,"pac_pqr.channel_email_label": "Correo oficial"
      ,"pac_pqr.channel_email_hours": "Respuesta en 24 h hábiles"
      ,"pac_pqr.how_it_works": "¿Cómo funciona?"
      ,"pac_pqr.step1": "Diligencia el formulario con tu información y descripción del caso."
      ,"pac_pqr.step2": "Recibes un número de radicado en tu correo electrónico."
      ,"pac_pqr.step3": "Nuestro equipo analiza y gestiona tu solicitud."
      ,"pac_pqr.step4": "Te notificamos la respuesta según los tiempos legales."
      ,"pac_pqr.recent_title": "Radicados recientes"
      ,"pac_pqr.view_all": "Ver todos"
      ,"pac_pqr.detail_title": "Detalle de la PQR"
      ,"pac_pqr.detail_subject": "Asunto"
      ,"pac_pqr.detail_description": "Descripción"
      ,"pac_pqr.detail_response": "Respuesta de SmileTrack"
      ,"pac_pqr.empty_recent": "Aún no tienes solicitudes radicadas."
      ,"pac_pqr.st_received": "Recibido"
      ,"pac_pqr.st_in_progress": "En proceso"
      ,"pac_pqr.st_resolved": "Resuelto"
      ,"pac_pqr.st_closed": "Cerrado"
      ,"pac_pqr.st_rejected": "Rechazado"
      ,"pac_pqr.no_status": "Sin estado"
      ,"pac_pqr.has_response": "Tiene respuesta"
      ,"pac_pqr.pending_response": "Pendiente de respuesta"
      ,"pac_pqr.filed_on": "Radicada"
      ,"pac_pqr.no_subject": "Sin asunto"
      ,"pac_pqr.no_description": "Sin descripción."
      ,"pac_pqr.answered_on": "Respondida el"
      ,"pac_pqr.response_registered": "Respuesta registrada"
      ,"pac_pqr.no_response_yet": "Esta solicitud aún no tiene una respuesta registrada."
      ,"pac_pqr.processing": "Procesando..."
      ,"pac_pqr.success_filed": "Solicitud radicada exitosamente. Radicado: "
      ,"pac_pqr.error_filed": "No se pudo radicar la solicitud"
      ,"pac_pqr.error_connection_filed": "Error de conexión al radicar la solicitud"
      ,"pac_pqr.file_too_large": "El archivo excede el tamaño máximo permitido (5 MB)"
      ,"pac_pqr.remove_file": "Eliminar archivo"
      ,"pac_miscitas.scheduled_title": "¡Cita agendada!"
      ,"pac_miscitas.progress_of": "de"
      ,"pac_miscitas.progress_completed": "citas completadas"
      ,"pac_profile.hide_password": "Ocultar contraseña"
      ,"pac_miscitas.error_close_aria": "Cerrar mensaje de error"
      ,"pac_miscitas.day_sun": "Dom"
      ,"pac_miscitas.day_mon": "Lun"
      ,"pac_miscitas.day_tue": "Mar"
      ,"pac_miscitas.day_wed": "Mié"
      ,"pac_miscitas.day_thu": "Jue"
      ,"pac_miscitas.day_fri": "Vie"
      ,"pac_miscitas.day_sat": "Sáb"
      ,"pac_miscitas.month_jan": "Ene"
      ,"pac_miscitas.month_feb": "Feb"
      ,"pac_miscitas.month_mar": "Mar"
      ,"pac_miscitas.month_apr": "Abr"
      ,"pac_miscitas.month_may": "May"
      ,"pac_miscitas.month_jun": "Jun"
      ,"pac_miscitas.month_jul": "Jul"
      ,"pac_miscitas.month_aug": "Ago"
      ,"pac_miscitas.month_sep": "Sep"
      ,"pac_miscitas.month_oct": "Oct"
      ,"pac_miscitas.month_nov": "Nov"
      ,"pac_miscitas.month_dec": "Dic"
      ,"pac_miscitas.error_load_appointments": "No fue posible consultar tus citas. La lista está vacía hasta recuperar la conexión con la API."
      ,"pac_miscitas.select_service": "Selecciona un servicio"
      ,"pac_miscitas.no_services": "No hay servicios disponibles. Intenta recargar."
      ,"pac_miscitas.error_load_services": "No se pudo cargar el catálogo de servicios. Recarga la página antes de solicitar una cita."
      ,"pac_miscitas.loading_professionals": "Cargando profesionales..."
      ,"pac_miscitas.error_load_professionals": "No se pudieron cargar los profesionales."
      ,"pac_miscitas.select_professional": "Selecciona un profesional"
      ,"pac_miscitas.no_professionals_assigned": "No hay profesionales asignados a este servicio"
      ,"pac_miscitas.no_schedules": "No hay horarios disponibles"
      ,"pac_miscitas.service_no_professionals_help": "El servicio no tiene profesionales disponibles. Prueba otro servicio."
      ,"pac_miscitas.select_professional_help": "Selecciona un profesional para ver sus horarios disponibles."
      ,"pac_miscitas.error_load_professionals_short": "No se pudieron cargar profesionales"
      ,"pac_miscitas.error_availability": "No se pudo consultar la disponibilidad."
      ,"pac_miscitas.checking_schedules": "Consultando horarios..."
      ,"pac_miscitas.select_date_service_professional_first": "Selecciona fecha, servicio y profesional primero"
      ,"pac_miscitas.error_load_schedules": "No se pudieron consultar los horarios."
      ,"pac_miscitas.select_schedule": "Selecciona un horario disponible"
      ,"pac_miscitas.schedules_available_help": "Estos horarios están disponibles ahora. La disponibilidad se verificará de nuevo al agendar."
      ,"pac_miscitas.no_schedules_help": "No hay horarios disponibles para esta fecha y profesional. Prueba otra fecha o profesional."
      ,"pac_miscitas.error_load_schedules_short": "No se pudieron cargar horarios"
      ,"pac_miscitas.cancel_reason_min": "El motivo de cancelación debe tener al menos 5 caracteres."
      ,"pac_miscitas.cancelling": "Cancelando…"
      ,"pac_miscitas.cancel_not_confirmed": "El servidor no confirmó la cancelación."
      ,"pac_miscitas.error_cancel_connection": "No fue posible conectar con la API para cancelar la cita."
      ,"pac_miscitas.cancel_success": "Cita cancelada correctamente"
      ,"pac_miscitas.error_cancel": "No fue posible cancelar la cita. Intente más tarde o contacte recepción."
      ,"pac_miscitas.data_incomplete_title": "Datos incompletos"
      ,"pac_miscitas.data_incomplete_msg": "Selecciona fecha, servicio, profesional y horario disponible."
      ,"pac_miscitas.no_service_day_title": "Día sin atención"
      ,"pac_miscitas.no_service_day_msg": "La clínica no atiende ese día. Selecciona un día hábil disponible."
      ,"pac_miscitas.scheduling": "Agendando…"
      ,"pac_miscitas.scheduled_msg_prefix": "Tu cita quedó programada para el"
      ,"pac_miscitas.at_time": "a las"
      ,"pac_miscitas.server_not_confirmed": "El servidor no confirmó el agendamiento de la cita."
      ,"pac_miscitas.error_schedule": "No fue posible agendar la cita. Inténtalo de nuevo."
      ,"pac_miscitas.schedule_taken_alt": "El horario elegido se ocupó; selecciona una alternativa"
      ,"pac_miscitas.schedule_taken_help": "El horario se ocupó mientras confirmabas. Elige una de estas alternativas."
      ,"pac_miscitas.schedule_taken_no_more": "Ese horario se ocupó y no hay más horarios disponibles para esta fecha. Prueba otra fecha."
      ,"pac_miscitas.error_schedule_title": "No se pudo agendar"
      ,"pac_miscitas.no_connection_title": "Sin conexión"
      ,"pac_miscitas.no_connection_msg": "No fue posible conectar con el servidor. Verifica tu conexión y vuelve a intentarlo."
      ,"pac_miscitas.doctor_unassigned": "Profesional sin asignar"
      ,"pac_miscitas.no_service": "Sin servicio"
      ,"pac_miscitas.count_result": "resultado"
      ,"pac_miscitas.count_results": "resultados"
      ,"pac_miscitas.empty_filtered": "No hay citas que coincidan con los filtros."
      ,"pac_miscitas.cancel_notice": "Para cancelaciones con menos de 2 horas de anticipación, por favor comuníquese directamente con recepción."
      ,"pac_miscitas.view_detail_aria": "Ver detalle de cita"
      ,"pac_miscitas.btn_view": "Ver"
      ,"pac_miscitas.appointment_in": "Esta cita es en"
      ,"pac_miscitas.hours": "horas"
      ,"pac_miscitas.cannot_cancel_status": "No se puede cancelar: la cita está en estado"
      ,"pac_miscitas.cancel_less_than_2h": "Cita en menos de 2 horas. Comuníquese con la clínica para cancelar."
      ,"pac_miscitas.appointment_of": "Cita del"
      ,"pac_miscitas.with_professional": "con"
      ,"pac_miscitas.invalid_date_title": "Fecha inválida"
      ,"pac_miscitas.invalid_date_msg": "No puedes agendar una cita en una fecha pasada."
      ,"pac_miscitas.sunday_closed_msg": "La clínica no atiende los domingos. Selecciona un día de lunes a sábado."
      ,"pac_miscitas.schedule_selected_help": "Horario seleccionado. Se verificará nuevamente al agendar."
      ,"pac_miscitas.error_init": "Error cargando módulo \"Mis Citas\". Intente recargar."
      ,"pac_notif.badge_read": "Leída"
      ,"pac_notif.error_mark_read": "No fue posible guardar el estado de lectura."
      ,"pac_notif.error_mark_all_read": "No fue posible marcar las notificaciones como leídas."
      ,"pac_notif.error_delete_informative": "Esta notificación informativa no se puede eliminar."
      ,"pac_notif.error_delete": "No fue posible eliminar la notificación."
      ,"pac_notif.unread_aria_prefix": "Notificación sin leer:"
      ,"pac_notif.status_aria_prefix": "Estado:"
      ,"pac_notif.view_detail_aria": "Ver detalle de la notificación"
      ,"pac_notif.mark_read": "Marcar como leída"
      ,"pac_notif.delete": "Eliminar"
      ,"pac_notif.delete_aria": "Eliminar notificación"
      ,"pac_notif.unread_dot_aria": "No leída"
      ,"pac_notif.count_result": "resultado"
      ,"pac_notif.count_results": "resultados"
      ,"pac_notif.toast_mark_read_error": "No se pudo marcar la notificación como leída"
      ,"pac_notif.toast_mark_read_success": "Notificación marcada como leída"
      ,"pac_notif.toast_deleted": "Notificación eliminada"
      ,"pac_notif.toast_no_unread": "No hay notificaciones sin leer"
      ,"pac_notif.toast_all_read": "Todas las notificaciones marcadas como leídas"
      ,"pac_notif.toast_some_failed": "Algunas notificaciones no se pudieron marcar como leídas"
      ,"pac_profile.toast_edit_mode": "Modo edición activado"
      ,"pac_profile.toast_changes_discarded": "Cambios descartados"
      ,"pac_profile.toast_need_current_password": "Ingresa tu contraseña actual"
      ,"pac_profile.toast_password_min": "La nueva contraseña debe tener al menos 8 caracteres"
      ,"pac_profile.toast_password_mismatch": "Las contraseñas no coinciden"
      ,"pac_profile.toast_invalid_email": "Ingresa un correo electrónico válido"
      ,"pac_profile.toast_profile_updated": "Perfil actualizado correctamente"
      ,"pac_profile.toast_image_too_large": "La imagen debe ser menor a 2MB"
      ,"pac_profile.toast_invalid_image_type": "Solo se permiten imágenes (JPG, PNG)"
      ,"pac_profile.toast_photo_updated": "Foto actualizada"
      ,"pac_profile.photo_alt": "Foto de perfil"
      ,"pac_profile.status_active": "Paciente activo"
      ,"pac_profile.status_inactive": "Paciente inactivo"
      ,"pac_profile.odontologist_label": "Odontólogo:"
      ,"pac_profile.confirm_data": "Confirmar datos personales"
      ,"pac_profile.confirm_identity_title": "Confirma tus datos de identidad"
      ,"pac_profile.confirm_identity_desc": "Necesitamos validar documento y fecha de nacimiento para habilitar las solicitudes de citas y la consulta de tu historia clínica."
      ,"pac_profile.document_type": "Tipo de documento"
      ,"pac_profile.select_type": "Selecciona un tipo"
      ,"pac_profile.doc_cc": "Cédula de ciudadanía"
      ,"pac_profile.doc_ti": "Tarjeta de identidad"
      ,"pac_profile.doc_ce": "Cédula de extranjería"
      ,"pac_profile.doc_pas": "Pasaporte"
      ,"pac_profile.document_number": "Número de documento"
      ,"pac_profile.birth_date": "Fecha de nacimiento"
      ,"pac_profile.save_confirm": "Guardar y confirmar"
      ,"pac_profile.change_photo": "Cambiar foto"
      ,"pac_profile.upload_photo_aria": "Subir foto de perfil"
      ,"pac_profile.upload_hint": "JPG, PNG. Máx 2MB"
      ,"pac_profile.full_name": "Nombre completo"
      ,"pac_profile.id_number": "Número de cédula"
      ,"pac_profile.not_editable": "No editable"
      ,"pac_profile.gender": "Género"
      ,"pac_profile.gender_m": "Masculino"
      ,"pac_profile.gender_f": "Femenino"
      ,"pac_profile.gender_o": "Otro"
      ,"pac_profile.blood_type": "Tipo de sangre"
      ,"pac_profile.marital_status": "Estado civil"
      ,"pac_profile.marital_single": "Soltero/a"
      ,"pac_profile.marital_married": "Casado/a"
      ,"pac_profile.marital_free_union": "Unión libre"
      ,"pac_profile.marital_divorced": "Divorciado/a"
      ,"pac_profile.marital_widowed": "Viudo/a"
      ,"pac_profile.contact_info": "Información de contacto"
      ,"pac_profile.city": "Ciudad"
      ,"pac_profile.department": "Departamento"
      ,"pac_profile.emergency_contact": "Contacto de emergencia"
      ,"pac_profile.relationship": "Parentesco"
      ,"pac_profile.medical_info": "Información médica"
      ,"pac_profile.medical_readonly_note": "Esta información solo puede ser modificada por tu odontólogo"
      ,"pac_profile.allergies": "Alergias"
      ,"pac_profile.no_allergies": "Sin alergias registradas"
      ,"pac_profile.conditions": "Enfermedades o condiciones"
      ,"pac_profile.none_registered_feminine": "Ninguna registrada"
      ,"pac_profile.medications": "Medicamentos actuales"
      ,"pac_profile.none_registered": "Ninguno registrado"
      ,"pac_profile.family_history": "Antecedentes familiares"
      ,"pac_profile.insurance": "Seguro / EPS"
      ,"pac_profile.eps": "EPS / Aseguradora"
      ,"pac_profile.policy_number": "Número de póliza"
      ,"pac_profile.affiliation_type": "Tipo de afiliación"
      ,"pac_profile.affiliation_contributory": "Régimen contributivo"
      ,"pac_profile.affiliation_subsidized": "Régimen subsidiado"
      ,"pac_profile.affiliation_private": "Particular"
      ,"pac_profile.security": "Seguridad"
      ,"pac_profile.current_password": "Contraseña actual"
      ,"pac_profile.show_password": "Mostrar contraseña"
      ,"pac_profile.new_password": "Nueva contraseña"
      ,"pac_profile.new_password_ph": "Mínimo 8 caracteres"
      ,"pac_profile.confirm_password_ph": "Repite la contraseña"
      ,"pac_hist.breadcrumb_aria": "Ruta de navegación"
      ,"pac_hist.breadcrumb_current": "Mi historial"
      ,"pac_hist.hero_kicker": "Historia clínica"
      ,"pac_hist.page_title": "Mi Historial Clínico"
      ,"pac_hist.page_subtitle": "Consulta la información registrada por tus profesionales de salud"
      ,"pac_hist.btn_download_aria": "Descargar informe clínico completo"
      ,"pac_hist.btn_download": "Descargar informe"
      ,"pac_hist.agenda_aria": "Contenido de historial"
      ,"pac_hist.sections_aria": "Secciones de la historia clínica"
      ,"pac_hist.tabs_label": "Secciones:"
      ,"pac_hist.tab_anamnesis": "1. Anamnesis"
      ,"pac_hist.tab_antecedentes": "2. Antecedentes"
      ,"pac_hist.tab_examen": "3. Examen clínico"
      ,"pac_hist.tab_diagnostico": "4. Diagnósticos"
      ,"pac_hist.tab_adjuntos": "5. Documentos"
      ,"pac_hist.tab_evolucion": "Evolución clínica"
      ,"pac_hist.form_aria": "Formulario clínico en solo lectura"
      ,"pac_hist.card_anamnesis_title": "Anamnesis y motivo de consulta"
      ,"pac_hist.card_anamnesis_sub": "Información registrada por el profesional"
      ,"pac_hist.field_reason": "Motivo de la consulta"
      ,"pac_hist.field_current_illness": "Enfermedad o padecimiento actual"
      ,"pac_hist.card_history_title": "Antecedentes sistémicos"
      ,"pac_hist.card_history_sub": "Antecedentes médicos y alertas registrados"
      ,"pac_hist.field_medical_history": "Antecedentes médicos"
      ,"pac_hist.field_habits": "Hábitos y estilo de vida"
      ,"pac_hist.field_allergies": "Alergias / medicamentos"
      ,"pac_hist.card_exam_title": "Examen clínico y hallazgos odontológicos"
      ,"pac_hist.card_exam_sub": "Hallazgos registrados durante la atención"
      ,"pac_hist.field_findings": "Hallazgos de examen"
      ,"pac_hist.field_observations": "Observaciones clínicas"
      ,"pac_hist.field_complementary": "Exámenes complementarios"
      ,"pac_hist.card_diagnosis_title": "Diagnósticos y evolución clínica"
      ,"pac_hist.card_diagnosis_sub": "Notas clínicas y procedimientos registrados"
      ,"pac_hist.field_main_diagnosis": "Diagnóstico principal"
      ,"pac_hist.field_secondary_diagnosis": "Diagnósticos secundarios"
      ,"pac_hist.field_procedure": "Procedimiento realizado"
      ,"pac_hist.field_evolution_note": "Nota de evolución clínica"
      ,"pac_hist.field_prescription": "Prescripción e instrucciones"
      ,"pac_hist.card_documents_title": "Documentación diagnóstica y consentimiento"
      ,"pac_hist.card_documents_sub": "Documentos disponibles en la historia clínica"
      ,"pac_hist.no_documents": "No hay documentos registrados."
      ,"pac_hist.workspace_title": "Evolución clínica"
      ,"pac_hist.workspace_sub": "Registros de atención, notas y documentos en orden cronológico."
      ,"pac_hist.records_aria": "Evolución por cita atendida"
      ,"pac_hist.professional_label": "Profesional:"
      ,"pac_hist.dt_reason": "Motivo"
      ,"pac_hist.dt_illness": "Enfermedad actual"
      ,"pac_hist.dt_history": "Antecedentes"
      ,"pac_hist.dt_habits": "Hábitos"
      ,"pac_hist.dt_allergies": "Alergias"
      ,"pac_hist.dt_symptoms": "Síntomas"
      ,"pac_hist.dt_diagnosis": "Diagnóstico"
      ,"pac_hist.dt_findings": "Hallazgos"
      ,"pac_hist.dt_odontogram_obs": "Observaciones odontológicas"
      ,"pac_hist.dt_tests": "Exámenes"
      ,"pac_hist.dt_evolution": "Evolución"
      ,"pac_hist.dt_procedures": "Procedimientos"
      ,"pac_hist.dt_prescription": "Prescripción"
      ,"pac_hist.empty_history": "Aún no hay citas atendidas con evolución clínica registrada."
      ,"pac_hist.other_records_aria": "Otros registros clínicos"
      ,"pac_hist.other_records_title": "Otros registros clínicos"
      ,"pac_hist.view_document": "Ver documento clínico"
      ,"pac_hist.modal_close_aria": "Cerrar modal"
      ,"pac_hist.modal_detail_title": "Detalle del Registro"
      ,"pac_hist.modal_download_title": "¿Descargar historial?"
      ,"pac_hist.modal_download_desc": "Se generará un PDF con todos tus registros clínicos y documentos disponibles."
      ,"pac_hist.btn_back": "Volver"
      ,"pac_hist.btn_confirm_download": "Sí, descargar"
    },
    en: {
      "sidebar_durante_atencion": "During Consultation",
      "nav.group_inicio": "Home",
      "nav.group_gestion_citas": "Appointments",
      "nav.group_historia_clinica": "Clinical Record",
      "nav.group_gestion_pqr": "PQR Management",
      "nav.group_centro_ayuda": "Help Center",
      "nav.item_homepage": "Main Page",
      "citas.title": "My Appointments",
      "citas.pac_subtitle": "Manage and view your personal dental appointments",
      "citas.stat_total": "Total appointments",
      "citas.modal_detail_title": "Appointment Details",
      "citas.modal_cancel_title": "Cancel this appointment?",
      "citas.btn_confirm_cancel": "Yes, cancel",
      "citas.modal_new_title": "Request new appointment",
      "citas.form_preferred_date": "Preferred date",
      "citas.form_service_type": "Service type",
      "citas.form_notes": "Additional notes",
      "citas.form_notes_ph": "Describe your reason...",
      "citas.loading_services": "Loading services...",
      "citas.btn_request": "Request",
      "citas.filter_all_status": "All statuses",
      "citas.stats_aria": "Summary of my appointments",
      "citas.table_aria": "My dental appointments table",
      "citas.checklist_new_item_ph": "Type new item...",
      "citas.observations_ph": "E.g. Chair dampness, wiped clean...",
      "dashboard.subtitle_metrics": "Monthly operational metrics and key center statistics",
      "dashboard.subtitle_occupancy": "Estimated percentage of appointments booked this month",
      "historia.title": "My Medical Record",
      "pqr.new": "New PQR Request",

      "common_bc_administracion": "Administration",
      "rec01.stats_aria": "Summary of daily activity",
      "rec01.stat_today": "Appointments today",
      "rec01.stat_invoices_pending": "Pending invoices",
      "rec01.quick_actions_aria": "Quick actions",
      "rec01.btn_new_patient_aria": "Register new patient in the system",
      "rec01.btn_generate_invoice_aria": "Generate new invoice for patient",
      "rec01.btn_generate_invoice": "Generate invoice",
      "rec01.table_aria": "List of appointments for the day",
      "adm09_bc_admin": "Administration",
      "adm09_bc_aria": "Administration > Appointment Management",
      "adm09_bc_active": "Appointment Management",
      "gestionprofesionales_breadcrumb_admin": "Administration",
      "gestionprofesionales_breadcrumb_current": "Professional Management",
      "reportesclinicos_breadcrumb_admin": "Administration",
      "reportesclinicos_breadcrumb_current": "Clinical Reports",
      "gestion_pacientes_breadcrumb_administracion": "Administration",
      "gestion_pacientes_breadcrumb_gestion_pacientes": "Patient Management",
      "gestion_pacientes_ruta_navegacion": "Administration -> Patient Management",
      "rec01_breadcrumb": "Reception > Dashboard",
      "detalle_paciente_breadcrumb_pacientes": "Patients",
      "detalle_paciente_ruta_navegacion": "Patients > Detail",
      "layout_skip_to_content": "Skip to main content",
      "layout_hamburger_open_menu": "Open menu",
      "common_hamburger_aria": "Open navigation menu",
      "gestion_pacientes_skip_link": "Skip to main content",
      "gestion_pacientes_abrir_menu": "Open navigation menu",
      "common_role_admin": "Administrator",
      "common_nav_inicio": "Home",
      "common_nav_homepage": "Home Page",
      "common_nav_dashboard": "General Dashboard",
      "common_nav_reportes_clinicos": "Clinical Reports",
      "common_nav_acceso_seguridad": "Access & Security",
      "common_nav_gestion_usuarios": "User Management",
      "common_nav_gestion_roles": "Role Management",
      "common_nav_bitacora": "System Audit Log",
      "common_nav_gestion_pacientes": "Patient Management",
      "common_nav_historia_clinica": "Clinical History",
      "common_nav_gestion_profesionales": "Professional Management",
      "common_nav_gestion_citas": "Appointment Management",
      "common_nav_agenda": "General Schedule",
      "common_nav_gestion_citas_int": "Integral Appointment Management",
      "common_nav_facturacion_pagos": "Billing & Payments",
      "common_nav_gestion_facturacion": "Billing Management",
      "common_nav_servicios_recursos": "Services & Resources",
      "common_nav_catalogo_servicios": "Service Catalog",
      "common_nav_configuracion": "General Settings",
      "common_nav_cerrar_sesion": "Sign out",
      "common_nav_logout_aria": "Sign out",
      "nav.group_mi_dashboard": "My Dashboard",
      "nav.group_mi_agenda": "My Schedule",
      "nav.group_perfil": "My Profile",
      "nav.group_gestion_ayuda": "Help Center",
      "nav.item_mi_dashboard": "My Dashboard",
      "nav.item_mi_agenda": "My Schedule",
      "nav.item_historia_clinica": "Clinical History",
      "nav.item_odontograma": "Digital Odontogram",
      "nav.item_tratamientos": "Treatment Follow-up",
      "nav.item_mis_pacientes": "My Patients",
      "nav.item_reportes_analitica": "Reports & Analytics",
      "nav.item_servicios": "Services",
      "nav.item_centro_ayuda": "Help Center",
      "nav.item_mi_perfil": "My Profile",
      "nav.item_panel_auxiliar": "Operative Panel",
      "nav.item_historial_paciente": "Patient History",
      "nav.item_asistencia_procedimiento": "Procedure Assistance",
      "nav.item_agenda_apoyo": "Support Schedule",
      "nav.item_preparacion_consulta": "Consultation Preparation",
      "nav.item_estado_consultorio": "Office Status",
      "nav.item_citas_finalizadas": "Finished Appointments",
      "nav.item_panel_recepcion": "Reception Panel",
      "nav.item_registrar_paciente_cita": "Register Patient / Appointment",
      "nav.item_gestion_citas": "Appointment Management",
      "nav.item_generar_factura_pago": "Generate Invoice / Payment",
      "nav.item_recordatorios": "Reminders",
      "nav.item_perfil_recepcionista": "Receptionist Profile",
      "nav.item_reportes_operativos": "Operative Reports",
      "adm09_title": "Integral Appointment Management",
      "adm09_subtitle": "Supervision and control",
      "adm09_btn_nueva_cita": "New Appointment",
      "adm09_nueva_cita_aria": "Create new appointment",
      "adm09_modal_delete_title": "Confirm Cancellation",
      "adm09_modal_delete_msg": "Are you sure you want to cancel this appointment? This action cannot be undone.",
      "adm09_modal_delete_reason": "Cancellation reason",
      "adm09_modal_delete_reason_ph": "State the reason...",
      "adm09_modal_delete_confirm": "Cancel Appointment",
      "adm09_search_ph": "Search by patient, professional or service...",
      "adm09_search_aria": "Search appointments",
      "adm09_filters_aria": "Search filters",
      "adm09_filter_status_all": "All statuses",
      "adm09_filter_status_aria": "Filter by status",
      "adm09_filter_prof_all": "Any professional",
      "adm09_filter_prof_aria": "Filter by professional",
      "adm09_filter_date_aria": "Filter by date",
      "adm09_stats_aria": "Appointment statistics",
      "adm09_stat_total": "Total Appointments",
      "adm09_stat_total_sub": "This month",
      "adm09_stat_programadas": "Scheduled",
      "adm09_stat_vs_week": "vs last week",
      "adm09_stat_canceladas": "Cancelled",
      "adm09_stat_rate": "Rate",
      "adm09_stat_atendidas": "Attended",
      "adm09_stat_attendance": "attendance",
      "adm09_tabs_aria": "Filter tabs",
      "adm09_tab_all": "All appointments",
      "adm09_tab_cancelled": "Cancellations",
      "adm09_tab_noshow": "No shows",
      "adm09_table_aria": "Appointment table",
      "adm09_table_title": "Appointment list",
      "gestionroles_subtitle": "Configure what each role can do in every system module",
      "gestionroles_pending_changes": "unsaved changes",
      "gestionroles_save_pending": "Save ({count})",
      "gestionroles_saving": "Saving...",
      "gestionroles_toast_success": "Success",
      "gestionroles_toast_error": "Error",
      "gestionroles_toast_warning": "Attention",
      "gestionroles_toast_info": "Information",
      "gestionroles_shortcuts_aria": "View keyboard shortcuts",
      "gestionroles_shortcuts_title": "Keyboard shortcuts",
      "gestionroles_export_aria": "Export permission settings",
      "gestionroles_filters_aria": "Permission matrix filters",
      "gestionroles_search_aria": "Search modules or permissions",
      "gestionroles_search_ph": "Search modules or permissions...",
      "gestionroles_search_clear_aria": "Clear search",
      "gestionroles_filter_role_label": "Filter by role",
      "gestionroles_filter_role_all": "All roles",
      "gestionroles_filter_module_label": "Filter by module",
      "gestionroles_filter_module_all": "All modules",
      "gestionroles_reset_filters_aria": "Reset all filters",
      "gestionroles_reset_filters": "Reset filters",
      "gestionroles_active_filters_aria": "Applied filters",
      "gestionroles_sort_modules": "Sort modules alphabetically",
      "gestionroles_matrix_title": "Permission matrix by role and module",
      "gestionroles_matrix_scroll": "Scrollable permission matrix",
      "gestionroles_module_permission": "Module / Permission",
      "gestionroles_empty_title": "No permissions found",
      "gestionroles_empty_desc": "Try adjusting the filters or clearing the search",
      "gestionroles_loading": "Loading permissions...",
      "gestionroles_history_title": "Recent changes",
      "gestionroles_history_clear_confirm": "The permission change history saved in this browser will be deleted. This action cannot be undone.",
      "gestionroles_history_clear_action": "Clear history",
      "gestionroles_history_cleared": "History cleared",
      "gestionroles_confirm_unavailable": "The confirmation dialog could not be opened.",
      "gestionroles_critical_revocation": "Revokes a critical permission",
      "gestionroles_breadcrumb_aria": "Breadcrumb",
      "gestionroles_skip_matrix": "Skip to the permission matrix",
      "gestionusuarios_subtitle": "Manage users and their access permissions",
      "gestionusuarios_btn_nuevo": "New user",
      "gestionusuarios_btn_nuevo_aria": "Create new user",
      "gestionusuarios_stats_aria": "User statistics",
      "gestionusuarios_stat_total": "Total users",
      "gestionusuarios_filters_aria": "Search filters",
      "gestionusuarios_search_ph": "Search by name or email...",
      "gestionusuarios_search_aria": "Search users by name or email",
      "gestionusuarios_filter_role_label": "Filter by role",
      "gestionusuarios_filter_role_all": "All roles",
      "gestionusuarios_filter_status_label": "Filter by status",
      "gestionusuarios_filter_status_all": "All statuses",
      "gestionusuarios_btn_buscar": "Search",
      "gestionusuarios_btn_buscar_aria": "Apply user filters",
      "gestionusuarios_btn_limpiar": "Clear filters",
      "gestionusuarios_btn_limpiar_aria": "Clear user filters",
      "gestionusuarios_table_aria": "Users table",
      "gestionusuarios_th_role": "ROLE",
      "gestionusuarios_pagination_aria": "User pagination",
      "gestionusuarios_pagination_prev_aria": "Previous page",
      "gestionusuarios_pagination_next_aria": "Next page",
      "gestionusuarios_pagination_page_aria": "Go to page {page}",
      "gestionusuarios_pagination_info": "Showing {start}-{end} of {total} users",
      "gestionusuarios_empty": "No users found.",
      "gestionusuarios_action_view": "View",
      "gestionusuarios_action_view_aria": "View user details",
      "gestionusuarios_action_edit": "Edit",
      "gestionusuarios_action_edit_aria": "Edit user",
      "gestionusuarios_action_block": "Block",
      "gestionusuarios_action_block_aria": "Block user",
      "gestionusuarios_action_unblock": "Unblock",
      "gestionusuarios_action_unblock_aria": "Unblock user",
      "gestionusuarios_password_show": "Show password",
      "gestionusuarios_password_hide": "Hide password",
      "gestionprofesionales_title": "Professional Management",
      "gestionprofesionales_subtitle": "Manage clinic dentists and specialists",
      "gestionprofesionales_btn_nuevo": "New Professional",
      "gestionprofesionales_btn_nuevo_aria": "Create new professional",
      "gestionprofesionales_search_ph": "Search professional...",
      "gestionprofesionales_search_aria": "Search professional by name or specialty",
      "gestionprofesionales_filters_aria": "Search filters",
      "gestionprofesionales_filter_specialty_label": "Filter by specialty",
      "gestionprofesionales_filter_specialty_all": "All specialties",
      "gestionprofesionales_filter_specialty_aria": "Filter by specialty",
      "gestionprofesionales_filter_status_label": "Filter by status",
      "gestionprofesionales_filter_status_all": "All statuses",
      "gestionprofesionales_filter_status_aria": "Filter by status",
      "gestionprofesionales_btn_buscar": "Search",
      "gestionprofesionales_btn_buscar_aria": "Execute search",
      "gestionprofesionales_btn_limpiar": "Clear filters",
      "gestionprofesionales_btn_limpiar_aria": "Clear search filters",
      "gestionprofesionales_stats_aria": "Professional statistics",
      "gestionprofesionales_stat_total": "Total Professionals",
      "gestionprofesionales_stat_activos": "Active",
      "gestionprofesionales_stat_vacaciones": "On vacation",
      "gestionprofesionales_stat_inactivos": "Inactive",
      "gestionprofesionales_table_aria": "Professional table",
      "gestionprofesionales_table_title": "Professional list",
      "gestionprofesionales_th_profesional": "PROFESSIONAL",
      "gestionprofesionales_th_especialidad": "SPECIALTY",
      "gestionprofesionales_th_registro": "LICENSE",
      "gestionprofesionales_th_telefono": "PHONE",
      "gestionprofesionales_th_estado": "STATUS",
      "gestionprofesionales_th_acciones": "ACTIONS",
      "gestionprofesionales_btn_ver": "View",
      "gestionprofesionales_btn_ver_title": "View professional details",
      "gestionprofesionales_btn_editar": "Edit",
      "gestionprofesionales_btn_editar_title": "Edit professional",
      "gestionprofesionales_btn_eliminar": "Delete",
      "gestionprofesionales_btn_eliminar_title": "Deactivate professional",
      "gestionprofesionales_btn_desactivar": "Deactivate",
      "gestionprofesionales_btn_desactivar_title": "Deactivate professional",
      "gestionprofesionales_btn_reactivar": "Reactivate",
      "gestionprofesionales_btn_reactivar_title": "Reactivate professional",
      "gestionprofesionales_pagination_aria": "Professional pagination",
      "gestionprofesionales_pagination_info": "Showing registered professionals",
      "gestionprofesionales_pagination_info_prefix": "Showing",
      "gestionprofesionales_pagination_info_suffix": "professionals registered",
      "gestionprofesionales_pagination_prev_aria": "Previous page",
      "gestionprofesionales_pagination_page_aria": "Go to page",
      "gestionprofesionales_pagination_next_aria": "Next page",
      "gestionprofesionales_modal_form_title": "New Professional",
      "gestionprofesionales_modal_detail_title": "Professional File",
      "gestionprofesionales_modal_delete_title": "Deactivate Professional",
      "gestionprofesionales_modal_close_aria": "Close window",
      "gestionprofesionales_form_nombres_label": "First Names *",
      "gestionprofesionales_form_nombres_ph": "Ex. Carlos",
      "gestionprofesionales_form_nombres_error": "Please enter first names.",
      "gestionprofesionales_form_apellidos_label": "Last Names *",
      "gestionprofesionales_form_apellidos_ph": "Ex. Méndez Ruiz",
      "gestionprofesionales_form_apellidos_error": "Please enter last names.",
      "gestionprofesionales_form_correo_label": "Login Email",
      "gestionprofesionales_form_correo_no_account": "This professional has no linked login account. This form will update profile details only.",
      "gestionprofesionales_form_access_note": "Login passwords cannot be set from this form.",
      "gestionprofesionales_form_correo_ph": "Ex. professional@smiletrack.co",
      "gestionprofesionales_form_correo_error": "Enter a valid email.",
      "gestionprofesionales_form_telefono_label": "Phone",
      "gestionprofesionales_form_telefono_ph": "Ex. 300 123 4567",
      "gestionprofesionales_form_registro_label": "Medical License *",
      "gestionprofesionales_form_registro_ph": "Ex. ML-2026-001",
      "gestionprofesionales_form_registro_error": "Please enter medical license.",
      "gestionprofesionales_form_especialidad_label": "Specialty",
      "gestionprofesionales_form_especialidad_primary": "Primary specialty",
      "gestionprofesionales_form_especialidad_secondary": "Secondary specialties",
      "gestionprofesionales_form_especialidad_help": "Hold Ctrl (Windows) or Cmd (Mac) to select multiple specialties.",
      "gestionprofesionales_form_especialidad_none": "No specialty",
      "gestionprofesionales_form_categoria_label": "Category",
      "gestionprofesionales_form_categoria_ph": "Ex. General Dentistry",
      "gestionprofesionales_form_estado_label": "Status",
      "gestionprofesionales_status_activo": "Active",
      "gestionprofesionales_status_vacaciones": "Vacation",
      "gestionprofesionales_status_inactivo": "Inactive",
      "gestionprofesionales_form_password_label": "Password",
      "gestionprofesionales_form_password_ph": "Minimum 8 characters, uppercase, lowercase, number and symbol",
      "gestionprofesionales_form_password_help": "Must have 8+ characters, uppercase, lowercase, number and symbol.",
      "gestionprofesionales_form_password_rules_title": "Password must satisfy:",
      "gestionprofesionales_form_password_rule_length": "Minimum 8 characters",
      "gestionprofesionales_form_password_rule_upper": "One uppercase letter",
      "gestionprofesionales_form_password_rule_lower": "One lowercase letter",
      "gestionprofesionales_form_password_rule_number": "One number",
      "gestionprofesionales_form_password_rule_symbol": "One special symbol",
      "gestionprofesionales_form_password_toggle_aria": "Show or hide password",
      "gestionprofesionales_btn_guardar": "Save Professional",
      "gestionprofesionales_detail_registro": "Medical License",
      "gestionprofesionales_detail_telefono": "Phone",
      "gestionprofesionales_detail_estado": "Status",
      "gestionprofesionales_tab_general": "General Profile",
      "gestionprofesionales_tab_horarios": "Working Hours",
      "gestionprofesionales_tab_ausencias": "Leaves and Absences",
      "gestionprofesionales_schedule_title": "Weekly Working Hours",
      "gestionprofesionales_schedule_btn_default": "Apply Standard Schedule (Mon-Fri 08:00 - 17:00)",
      "gestionprofesionales_schedule_hint": "Set up days and time slots when this professional attends appointments. If unconfigured, default clinic hours apply.",
      "gestionprofesionales_schedule_btn_save": "Save Hours",
      "gestionprofesionales_absences_title": "Absences Record",
      "gestionprofesionales_modal_delete_question": "Are you sure you want to deactivate this professional?",
      "gestionprofesionales_modal_delete_warning": "The professional will be set to inactive and will not receive new appointments. This operation is reversible.",
      "gestionprofesionales_modal_delete_cancel": "Cancel",
      "gestionprofesionales_modal_delete_cancel_aria": "Cancel - do not deactivate",
      "gestionprofesionales_modal_delete_confirm": "Confirm deactivation",
      "gestionprofesionales_modal_delete_confirm_aria": "Confirm professional deactivation",
      "gestionprofesionales_footer_sub": "ADSO Training Project — SENA",
      "gestionprofesionales_mobile_nav_aria": "Mobile navigation",
      "gestionprofesionales_mobile_dashboard": "Dashboard",
      "gestionprofesionales_mobile_profesionales": "Professionals",
      "gestionprofesionales_mobile_agenda": "Schedule",
      "reportesclinicos_title": "Clinical Reports",
      "reportesclinicos_subtitle": "Medical analysis and patient follow-up",
      "reportesclinicos_search_ph": "Search by patient, ID or diagnosis...",
      "reportesclinicos_search_aria": "Search reports",
      "reportesclinicos_filters_aria": "Search filters",
      "reportesclinicos_filter_profesional_label": "Filter by professional",
      "reportesclinicos_filter_profesional_all": "All professionals",
      "reportesclinicos_filter_profesional_aria": "Filter by professional",
      "reportesclinicos_filter_mes_label": "Filter by month",
      "reportesclinicos_filter_mes_all": "All months",
      "reportesclinicos_filter_mes_aria": "Filter by month",
      "reportesclinicos_btn_aplicar": "Apply",
      "reportesclinicos_btn_limpiar": "Clear",
      "reportesclinicos_stats_aria": "Report statistics",
      "reportesclinicos_stat_pacientes": "Registered Patients",
      "reportesclinicos_stat_consultas": "Consultations This Month",
      "reportesclinicos_stat_satisfaccion": "Patient Satisfaction",
      "reportesclinicos_stat_asistencia": "Attendance rate",
      "reportesclinicos_table_aria": "Clinical reports table",
      "reportesclinicos_table_title": "Clinical reports list",
      "reportesclinicos_th_paciente": "PATIENT",
      "reportesclinicos_th_ultconsulta": "LAST CONSULTATION",
      "reportesclinicos_th_diagnostico": "DIAGNOSIS",
      "reportesclinicos_th_procedimiento": "PROCEDURE",
      "reportesclinicos_th_profesional": "PROFESSIONAL",
      "reportesclinicos_th_proxcita": "NEXT APPOINTMENT",
      "reportesclinicos_th_alerta": "ALERT",
      "reportesclinicos_empty_title": "No results",
      "reportesclinicos_badge_sin_cita": "No appointment",
      "reportesclinicos_empty_desc": "No reports match the applied filters.",
      "reportesclinicos_pagination_aria": "Reports pagination",
      "reportesclinicos_pagination_prev_aria": "Previous page",
      "reportesclinicos_pagination_next_aria": "Next page",
      "reportesclinicos_pagination_info_prefix": "Showing",
      "reportesclinicos_pagination_info_suffix": "clinical reports",
      "detalle_paciente_title": "Patient Detail",
      "detalle_paciente_volver": "Back",
      "detalle_paciente_volver_listado": "Back to list",
      "detalle_paciente_datos_personales": "Personal data",
      "detalle_paciente_documento_label": "Document ID",
      "detalle_paciente_correo_label": "Email",
      "detalle_paciente_telefono_label": "Phone",
      "detalle_paciente_fecha_nacimiento_label": "Date of birth",
      "detalle_paciente_genero_label": "Gender",
      "detalle_paciente_ciudad_label": "City",
      "detalle_paciente_direccion_label": "Address",
      "detalle_paciente_datos_medicos": "Medical data",
      "detalle_paciente_grupo_sanguineo_label": "Blood type",
      "detalle_paciente_alergias_label": "Allergies",
      "detalle_paciente_alergias_badge": "Allergies",
      "detalle_paciente_sin_alergias": "No registered allergies",
      "detalle_paciente_alergias_registradas": "Registered allergies",
      "detalle_paciente_lista_alergias": "Allergies list",
      "detalle_paciente_antecedentes_medicos_label": "Medical history",
      "detalle_paciente_ultimo_diagnostico_label": "Last diagnosis",
      "detalle_paciente_contacto_emergencia": "Emergency contact",
      "detalle_paciente_nombre_emergencia_label": "Name",
      "detalle_paciente_telefono_emergencia_label": "Phone",
      "detalle_paciente_historia_clinica": "Clinical History",
      "detalle_paciente_abrir_historia_clinica": "Clinical History",
      "detalle_paciente_historial_citas": "Appointment history",
      "detalle_paciente_proxima_cita": "Next appointment",
      "detalle_paciente_fecha_cita_label": "Date",
      "detalle_paciente_citas_totales": "Total appointments",
      "detalle_paciente_citas_pendientes": "Pending appointments",
      "detalle_paciente_ultima_visita": "Last visit",
      "detalle_paciente_resumen_actividad": "Activity summary",
      "detalle_paciente_sin_citas_programadas": "No scheduled appointments",
      "detalle_paciente_sin_historial_citas": "No appointment history recorded",
      "detalle_paciente_editar": "Edit",
      "detalle_paciente_editar_datos": "Edit data",
      "detalle_paciente_eliminar": "Delete",
      "detalle_paciente_confirm_fusion_title": "Confirm patient merge",
      "detalle_paciente_confirm_fusion_message": "Merge the selected patient with {name}? The selected record will be deactivated.",
      "detalle_paciente_confirm_fusion": "Confirm merge",
      "detalle_paciente_confirmar_eliminar_archivo_titulo": "Delete attached file",
      "detalle_paciente_confirmar_eliminar_archivo_mensaje": "Are you sure you want to delete this patient's attached file?",
      "detalle_paciente_foto_documento_adjunto": "Photo / Attached document",
      "detalle_paciente_archivo_actual": "Current file:",
      "detalle_paciente_sin_archivo_adjunto": "No attached file",
      "detalle_paciente_archivo_adjunto_de": "Attached file of",
      "detalle_paciente_seleccionar_imagen": "Select image",
      "detalle_paciente_seleccionar_archivo_adjunto": "Select attached file",
      "detalle_paciente_cambiar": "Change",
      "detalle_paciente_subir": "Upload",
      "detalle_paciente_abrir_menu": "Open menu",
      "gestion_pacientes_title": "SmileTrack — Patient Management",
      "gestion_pacientes_page_title": "Patient Management",
      "gestion_pacientes_page_subtitle": "Patient history — Read-only",
      "gestion_pacientes_meta_description": "Dental center patient management",
      "gestion_pacientes_buscar_paciente": "Search patient",
      "gestion_pacientes_buscar_placeholder": "Search patient, ID or diagnosis...",
      "gestion_pacientes_breadcrumb_aria": "Breadcrumb",
      "gestion_pacientes_stats_aria": "Patient summary",
      "gestion_pacientes_stat_total": "Total patients",
      "gestion_pacientes_stat_total_sub": "Registered in the system",
      "gestion_pacientes_stat_history": "With clinical history",
      "gestion_pacientes_stat_history_sub": "Have medical records",
      "gestion_pacientes_stat_next": "Upcoming appointments",
      "gestion_pacientes_stat_next_sub": "Scheduled appointments",
      "gestion_pacientes_stat_no_allergies": "No allergies",
      "gestion_pacientes_stat_no_allergies_sub": "No restrictions recorded",
      "gestion_pacientes_filters_aria": "Search filters",
      "gestion_pacientes_filter_status": "Status",
      "gestion_pacientes_filter_status_aria": "Filter by patient status",
      "gestion_pacientes_active_plural": "Active",
      "gestion_pacientes_inactive_plural": "Inactive",
      "gestion_pacientes_status_retired": "Retired",
      "gestion_pacientes_all_male": "All",
      "gestion_pacientes_all_feminine": "All",
      "gestion_pacientes_filter_allergies": "Allergies",
      "gestion_pacientes_filter_allergies_aria": "Filter by allergies",
      "gestion_pacientes_with_allergies": "With allergies",
      "gestion_pacientes_without_allergies": "No allergies",
      "gestion_pacientes_filter_appointment": "Next appointment",
      "gestion_pacientes_filter_appointment_aria": "Filter by next appointment",
      "gestion_pacientes_with_appointment": "With scheduled appointment",
      "gestion_pacientes_without_appointment": "Without scheduled appointment",
      "gestion_pacientes_filter_history": "Clinical history",
      "gestion_pacientes_filter_history_aria": "Filter by clinical history",
      "gestion_pacientes_with_history": "With history",
      "gestion_pacientes_without_history": "Without history",
      "gestion_pacientes_clear_filters_aria": "Clear all filters",
      "gestion_pacientes_clear_filters": "Clear filters",
      "gestion_pacientes_table_title": "Patient list",
      "gestion_pacientes_table_aria": "Patient table",
      "gestion_pacientes_column_patient": "Patient",
      "gestion_pacientes_column_last_visit": "Last visit",
      "gestion_pacientes_column_diagnosis": "Diagnosis",
      "gestion_pacientes_column_next_visit": "Next appointment",
      "gestion_pacientes_column_allergies": "Allergies",
      "gestion_pacientes_empty_title": "No patients found",
      "gestion_pacientes_empty_description": "Adjust the filters or search and try again.",
      "gestion_pacientes_pagination_aria": "Page navigation",
      "gestion_pacientes_showing": "Showing",
      "gestion_pacientes_of": "of",
      "gestion_pacientes_patient_count": "patients",
      "gestion_pacientes_page_navigation_aria": "Result pages",
      "gestion_pacientes_modal_title": "Patient details",
      "gestion_pacientes_page_number_aria": "Page {page}",
      "gestion_pacientes_filter_results": "{count} results found",
      "gestion_pacientes_error_loading": "Could not load the patient list.",
      "gestion_pacientes_unnamed": "Unnamed",
      "gestion_pacientes_no_document": "No document",
      "gestion_pacientes_remove_allergy_aria": "Remove allergy {name}",
      "gestion_pacientes_allergy_max_length": "Each allergy must be 150 characters or fewer.",
      "gestion_pacientes_profile": "Profile",
      "gestion_pacientes_action_view": "View",
      "gestion_pacientes_view_profile": "View full profile",
      "gestion_pacientes_view_profile_aria": "View {name}'s profile",
      "gestion_pacientes_edit_patient": "Edit patient",
      "gestion_pacientes_edit_patient_aria": "Edit {name}",
      "gestion_pacientes_history": "History",
      "gestion_pacientes_history_aria": "Clinical history for {name}",
      "gestion_pacientes_detail_title": "Details for {name}",
      "gestion_pacientes_birth_date": "Date of birth",
      "gestion_pacientes_gender": "Gender",
      "gestion_pacientes_city": "City",
      "gestion_pacientes_blood_type": "Blood type",
      "gestion_pacientes_deactivate": "Deactivate patient",
      "gestion_pacientes_deactivate_aria": "Deactivate {name}",
      "gestion_pacientes_history_title": "Clinical history for {name}",
      "gestion_pacientes_doctor_prefix": "Dr.",
      "gestion_pacientes_no_history": "No history recorded",
      "gestion_pacientes_edit_title": "Edit patient: {name}",
      "gestion_pacientes_form_personal": "Personal information",
      "gestion_pacientes_document_type": "Document type",
      "gestion_pacientes_id_cc": "Citizenship ID",
      "gestion_pacientes_id_ti": "Identity card",
      "gestion_pacientes_id_ce": "Foreign resident ID",
      "gestion_pacientes_id_passport": "Passport",
      "gestion_pacientes_id_nit": "Tax ID",
      "gestion_pacientes_unspecified": "Unspecified",
      "gestion_pacientes_gender_male": "Male",
      "gestion_pacientes_gender_female": "Female",
      "gestion_pacientes_gender_other": "Other",
      "gestion_pacientes_form_contact": "Contact information",
      "gestion_pacientes_emergency_contact": "Emergency contact",
      "gestion_pacientes_emergency_name": "Contact name",
      "gestion_pacientes_full_name_placeholder": "Full name",
      "gestion_pacientes_emergency_phone": "Emergency phone",
      "gestion_pacientes_phone_example": "E.g. 300 123 4567",
      "gestion_pacientes_form_health": "Health information",
      "gestion_pacientes_known_allergies": "Known allergies",
      "gestion_pacientes_allergy_example": "E.g. Penicillin",
      "gestion_pacientes_medical_history": "Medical history",
      "gestion_pacientes_medical_history_placeholder": "Previous illnesses, surgeries, chronic conditions, current treatments...",
      "gestion_pacientes_save_changes": "Save changes",
      "gestion_pacientes_csrf_missing": "The security token was not found.",
      "gestion_pacientes_update_success": "Patient updated successfully.",
      "gestion_pacientes_update_error": "Could not update the patient.",
      "gestion_pacientes_error_deactivate": "Could not deactivate the patient.",
      "gestion_pacientes_confirm_deactivate_title": "Deactivate patient?",
      "gestion_pacientes_confirm_deactivate_message": "Are you sure you want to deactivate {name}?",
      "gestion_pacientes_deactivate_success": "Patient deactivated successfully.",
      "common.add": "Add",
      "common.edit": "Edit",
      "common.not_available": "Not available",
      "common.saving": "Saving...",
      "gestion_facturacion_title": "Billing & Payments",
      "gestion_facturacion_subtitle": "Revenue & collections",
      "gestion_facturacion_kpi_total_facturas": "Total Billed",
      "gestion_facturacion_kpi_por_cobrar": "Pending Collection",
      "gestion_facturacion_kpi_pagadas_hoy": "Paid Today",
      "gestion_facturacion_kpi_anuladas": "Voided",
      "gestion_facturacion_kpi_anio_fiscal": "All periods",
      "gestion_facturacion_kpi_ultimas_24h": "Across all periods",
      "gestion_facturacion_kpi_actualizado_hace": "Based on recorded payments",
      "gestion_facturacion_kpi_vs_mes_anterior_up": "Total outstanding balance",
      "billing.summary": "Billing summary",
      "billing.filters": "Invoice filters",
      "billing.search_placeholder": "Search by number or patient...",
      "billing.search_aria": "Search by invoice number or patient",
      "billing.filter_status": "Filter by status",
      "billing.filter_month": "Filter by month",
      "billing.all_statuses": "All statuses",
      "billing.status_paid": "Paid",
      "billing.status_pending": "Pending",
      "billing.status_partial": "Partial",
      "billing.status_voided": "Voided",
      "billing.all_months": "All months",
      "billing.create_invoice": "New invoice",
      "billing.invoice_number": "Invoice No.",
      "billing.pending_amount": "Pending",
      "billing.pagination_showing": "Showing",
      "billing.pagination_of": "of",
      "billing.pagination_records": "records",
      "billing.pagination": "Result pages",
      "billing.close_panel": "Close panel",
      "billing.tax_19": "VAT (19%):",
      "billing.view_details": "View details",
      "billing.void_invoice": "Void",
      "billing.close_details": "Close details",
      "billing.document": "Document",
      "billing.service": "Service",
      "billing.paid_amount": "Paid",
      "billing.invoice_list": "Invoice list",
      "billing.invoice_details": "Invoice details",
      "billing.fallback_service_cleaning": "Dental cleaning (COP 150,000)",
      "billing.fallback_service_whitening": "Whitening (COP 350,000)",
      "billing.fallback_service_orthodontics": "Orthodontics (COP 800,000)",
      "billing.fallback_service_root_canal": "Root canal treatment (COP 450,000)",
      "billing.fallback_service_implants": "Implants (COP 1,200,000)",
      "billing.fallback_service_pediatric": "Pediatric dentistry (COP 120,000)",
      "billing.fallback_service_orthodontic_visit": "Orthodontic consultation (COP 90,000)",
      "billing.fallback_service_dental_implant": "Dental implant (COP 1,500,000)",
      "billing.payment_history": "Transaction history",
      "billing.close_confirmation": "Close confirmation",
      "billing.payment_success_title": "Payment successful",
      "billing.payment_success_message": "The payment was recorded successfully.",
      "billing.close_form": "Close form",
      "billing.invoice_required_note": "Complete the information to generate the invoice.",
      "billing.select_registered_patient": "Select a registered patient",
      "billing.select_patient_required": "-- Select a patient --",
      "billing.patient_selection_required": "Select a registered patient to create the invoice.",
      "billing.patient_name_required": "Patient name *",
      "billing.patient_name_example": "e.g. Ana Gomez",
      "billing.document_required": "Document *",
      "billing.document_example": "e.g. 1020304050",
      "billing.invoice_date_auto": "Invoice date (automatic)",
      "billing.service_required": "Service or description *",
      "billing.select_service": "Select a service",
      "billing.total_cop_required": "Amount to charge (COP) *",
      "billing.amount_example": "e.g. 150000",
      "billing.generate_invoice": "Generate invoice",
      "billing.empty_title": "No invoices",
      "billing.empty_filtered": "No invoices match the search criteria.",
      "billing.empty_initial": "No invoices have been registered yet.",
      "billing.invoice_for": "Invoice {number} for {patient}",
      "billing.document_short": "Doc.",
      "billing.status_aria": "Status: {status}",
      "billing.view_invoice": "View invoice {number}",
      "billing.view": "View",
      "billing.manage_invoice": "Manage invoice {number}",
      "billing.manage": "Manage",
      "billing.loading_payments": "Loading transactions...",
      "billing.payment_history_error": "Could not load the transaction history.",
      "billing.no_payments": "No transactions recorded.",
      "billing.payment_success_with_amount": "The payment of {amount} for {patient} was recorded successfully.",
      "billing.reminder_sent": "A reminder was sent to {patient}.",
      "billing.already_paid": "This invoice is already paid.",
      "billing.payment_error": "Could not record the payment.",
      "billing.payment_connection_error": "Connection error while recording the payment.",
      "billing.already_voided": "This invoice is already voided.",
      "billing.confirmation_unavailable": "Could not open the confirmation. Please try again.",
      "billing.void_confirmation_title": "Void invoice",
      "billing.void_confirmation_message": "Void invoice {number} for {patient}? This action cannot be undone.",
      "billing.void_reason": "Voided from the administrative dashboard",
      "billing.void_error": "Could not void the invoice.",
      "billing.void_success": "Invoice {number} was voided successfully.",
      "billing.void_connection_error": "Connection error while voiding the invoice.",
      "billing.page_number": "Page {page}",
      "billing.previous_page": "Previous page",
      "billing.next_page": "Next page",
      "billing.toast_success": "Completed",
      "billing.toast_error": "Error",
      "billing.toast_warning": "Notice",
      "billing.toast_info": "Information",
      "billing.patient_catalog_error": "Could not load the patient list.",
      "billing.required_fields_error": "Complete all required fields.",
      "billing.invoice_created": "Invoice {number} was generated successfully.",
      "billing.invoice_create_error": "Could not generate the invoice. Please try again.",
      "billing.invoice_load_error": "Could not load the invoices.",
      "layout_breadcrumb_aria": "Breadcrumb",
      "rec01_title": "Reception Dashboard",
      "rec01_alert_banner_title": "Upcoming appointments — next 30 minutes",
      "profile.edit": "Edit profile",

      // ═══════════════════════════════════════════════════════
      // COMMON
      // ═══════════════════════════════════════════════════════
      "common.smiletrack": "SmileTrack",
      "common.back_to_home": "Back to home",
      "common.loading": "Loading...",
      "common.close": "Close",
      "common.cancel": "Cancel",
      "common.confirm": "Confirm",
      "common.confirmation_unavailable": "Could not open the confirmation. Please try again.",
      "common.save": "Save",
      "common.send": "Send",
      "common.accept": "Accept",
      "common.back": "Back",
      "common.next": "Next",
      "common.yes": "Yes",
      "common.no": "No",
      "common.email": "Email",
      "common.phone": "Phone",
      "common.password": "Password",
      "common.confirm_password": "Confirm password",
      "common.name": "Name",
      "common.lastname": "Lastname",
      "common.document": "Document",
      "common.last_visit": "Last access",
      "common.address": "Address",
      "common.required_field": "This field is required",
      "common.invalid_email": "Enter a valid email",
      "common.invalid_phone": "Enter a valid phone number",
      "common.password_mismatch": "Passwords do not match",
      "common.password_min": "Minimum 8 characters",
      "common.password_upper": "At least one uppercase letter",
      "common.password_number": "At least one number",
      "common.password_symbol": "At least one special symbol",
      "common.terms_accept": "I accept the Terms of Service and Privacy Policy of SmileTrack.",
      "common.terms": "Terms of Service",
      "common.privacy": "Privacy Policy",
      "common.optional": "(optional)",
      "common.select": "Select",
      "common.success": "Success",
      "common.error": "Error",
      "common.warning": "Warning",
      "common.info": "Information",
      "common.notifications": "Notifications",
      "common.read_only": "Read-only",
      "common.no_notifications": "There are no notifications in this category.",
      "common.previous": "Previous",
      "common.view_all": "View all",
      "common_skip_link": "Skip to main content",
      "common_role_patient": "Patient",
      "common_role_auxiliar": "Assistant",
      "common_role_profesional": "Dentist",
      "common_role_recepcionista": "Reception",
      "common.personal_data": "Personal information",
      "common.messages": "Messages",
      "common.today": "Today",
      "common.breadcrumb_nav": "Breadcrumb navigation",
      "rec05.subtitle": "Manage automated and manual appointment reminders for patients",
      "rec05.history_title": "Recently sent",
      "rec05.view_all_aria": "View full history of sent reminders",
      "rec05.history_scroller_aria": "History of sent reminders",
      "rec05.no_history": "No sent reminders recorded yet.",
      "rec05.alerts_title": "Pending alerts",
      "profile.title": "My Profile",
      "common.retry": "Try again",
      "common.connection_error": "Server connection error. Please try again.",
      "common.field_mandatory": "*",

      "help.title": "Help Center",
      "help.subtitle": "Find guides, tutorials, and support for your clinic",
      "help.support_channels": "Support channels",
      "help.support_title": "Support and tickets",
      "help.support_subtitle": "User guides and technical support for SmileTrack",
      "help.guides": "Guides",
      "help.guide_tutorial": "Step-by-step tutorial",
      "help.guide_updated": "Updated on",
      "help.recommended": "Recommended articles",
      "help.direct_support": "+ Direct Support",
      "help.search_placeholder": "How do I schedule an appointment?...",
      "help.view_all": "View all",
      "help.skip_to_content": "Skip to main content",
      "help.chat_hours": "Online chat: Mon–Fri 8 am–6 pm",
      "help.email": "support@smiletrack.local",
      "help.phone": "+57 300 000 0000",
      "help.categories_label": "Help center categories",
      "role.panel_administrador": "Administrator Panel",
      "role.panel_profesional": "Professional Panel",
      "role.panel_paciente": "Patient Panel",
      "role.panel_recepcionista": "Receptionist Panel",
      "role.panel_auxiliar": "Assistant Panel",
      "help.category_citas": "Appointments",
      "help.category_pacientes": "Patients",
      "help.category_facturacion": "Billing",
      "help.category_profesionales": "Professionals",
      "help.category_soporte": "Support",
      "help.category_desc_citas": "Learn step by step how to schedule a new appointment by assigning a patient, professional, office, and service in less than a minute.",
      "help.category_desc_pacientes": "How to register new patients, update data, and view clinical history.",
      "help.category_desc_facturacion": "Guide to generating invoices, recording payments, and viewing billing information.",
      "help.category_desc_profesionales": "How to register professionals, assign specialties, and manage their schedules.",
      "help.category_desc_soporte": "Contact channels and a form for reporting incidents to the support team.",
      "help.article_title_cita": "How to schedule an appointment",
      "help.article_title_pacientes": "Patient management",
      "help.article_title_facturacion": "Billing and payments",
      "help.article_title_profesionales": "Professional management",
      "help.article_title_soporte": "Technical support",
      "help.article_desc_cita": "Learn step by step how to schedule a new appointment by assigning a patient, professional, office, and service in less than a minute.",
      "help.article_desc_pacientes": "How to register new patients, update data, and view clinical history.",
      "help.article_desc_facturacion": "Guide to generating invoices, recording payments, and viewing billing information.",
      "help.article_desc_profesionales": "How to register professionals, assign specialties, and manage their schedules.",
      "help.article_desc_soporte": "Contact channels and a form for reporting incidents to the support team.",
      "nav.dashboard": "Dashboard",
      "nav.patients": "Patients",
      "nav.appointments": "Appointments",
      "nav.billing": "Invoices",
      "nav.agenda": "Schedule",
      "status.attended": "Attended",
      "status.sent": "Sent",
      "audit.level": "Level",
      "audit.not_available": "Not available",
      "audit.live_aria": "Enable or pause live updates",
      "audit.live_label": "Live updates",
      "audit.export_aria": "Export audit log",
      "audit.stats_aria": "Audit log indicators",
      "audit.total_events": "Recent events",
      "audit.latest_records": "Recent records",
      "audit.unique_users": "Users in records",
      "audit.critical_alerts": "Critical alerts",
      "audit.requires_attention": "Require attention",
      "audit.warnings": "Warnings",
      "audit.last_24_hours": "Last 24 hours",
      "audit.system_active": "System active",
      "audit.uptime": "Availability",
      "audit.filters_aria": "Audit log filters",
      "audit.search_ph": "Search audit log...",
      "audit.search_aria": "Search events by description or user",
      "audit.clear_search": "Clear search",
      "audit.level_filter": "Filter by event level",
      "audit.all_levels": "All levels",
      "audit.level_critical": "Critical",
      "audit.level_error": "Error",
      "audit.level_warning": "Warning",
      "audit.level_info": "Information",
      "audit.level_success": "Success",
      "audit.level_debug": "Debug",
      "audit.module": "Module",
      "audit.module_filter": "Filter by module",
      "audit.all_modules": "All modules",
      "audit.module_auth": "Authentication",
      "audit.module_appointments": "Appointments",
      "audit.module_billing": "Billing",
      "audit.module_database": "Database",
      "audit.module_services": "Services",
      "audit.module_system": "System",
      "audit.date": "Date",
      "audit.date_filter": "Filter by date",
      "audit.reset_filters": "Reset filters",
      "audit.bulk_actions": "Bulk actions",
      "audit.selected_count": "{count} selected",
      "audit.mark_read_aria": "Mark selected events as read",
      "audit.mark_read": "Mark as read",
      "audit.archive_aria": "Archive selected events",
      "audit.archive": "Archive",
      "audit.clear_selection": "Clear selection",
      "audit.clear": "Clear",
      "audit.table_title": "System event log",
      "audit.events_registered": "events recorded",
      "audit.items_per_page": "Items per page",
      "audit.per_page_10": "10 per page",
      "audit.per_page_25": "25 per page",
      "audit.per_page_50": "50 per page",
      "audit.per_page_100": "100 per page",
      "audit.refresh": "Refresh audit log",
      "audit.table_aria": "Audit log event table",
      "audit.select_all": "Select all visible events",
      "audit.timestamp": "Date and time",
      "audit.sort_timestamp": "Sort by date and time",
      "audit.description": "Description",
      "audit.user": "User",
      "audit.empty_title": "No events found",
      "audit.empty_description": "Try adjusting the filters or changing the date.",
      "audit.loading": "Loading events...",
      "audit.pagination_aria": "Event pagination",
      "audit.showing": "Showing",
      "audit.of": "of",
      "audit.records": "events",
      "audit.first_page": "First page",
      "audit.previous_page": "Previous page",
      "audit.next_page": "Next page",
      "audit.last_page": "Last page",
      "audit.page_navigation": "Page navigation",
      "audit.jump_page": "Go to page",
      "audit.page_number": "Page number",
      "audit.levels_title": "Event levels",
      "audit.legend_critical": "Critical - Requires immediate attention",
      "audit.legend_error": "Error - Operation failed",
      "audit.legend_warning": "Warning - Unusual situation",
      "audit.legend_info": "Information - Informational event",
      "audit.legend_success": "Success - Operation completed",
      "audit.detail_title": "Event details",
      "audit.event_detail_title": "Event #{id}",
      "audit.ip_address": "IP address",
      "audit.stack_trace": "Stack trace",
      "audit.metadata": "Metadata",
      "audit.copy_aria": "Copy event details",
      "audit.copy": "Copy",
      "audit.report_aria": "Report this event",
      "audit.report": "Report",
      "audit.export_title": "Export audit log",
      "audit.export_format": "Export format",
      "audit.export_pdf_description": "Document for printing or archiving",
      "audit.export_csv_description": "Data for spreadsheets or analysis",
      "audit.export_json_description": "Technical format for developers",
      "audit.export_range": "Event range",
      "audit.export_filtered": "Filtered events only",
      "audit.export_all": "All events",
      "audit.export_preparing": "Preparing file...",
      "audit.export_generating": "Generating file...",
      "audit.export_filtering": "Filtering events...",
      "audit.export_compressing": "Compressing...",
      "audit.export_ready": "Ready",
      "audit.download": "Download",
      "audit.toast_success_title": "Success",
      "audit.toast_error_title": "Error",
      "audit.toast_warning_title": "Warning",
      "audit.toast_info_title": "Information",
      "audit.no_results_announcement": "No events found with the selected filters",
      "audit.events_loaded_announcement": "{count} events loaded, page {page} of {totalPages}",
      "audit.active_filter_count": "{count} active filters",
      "audit.select_event_aria": "Select event {id}",
      "audit.level_named_aria": "Level: {level}",
      "audit.view_details": "View details",
      "audit.view_details_aria": "View details for event {id}",
      "audit.go_to_page_aria": "Go to page {page}",
      "audit.detail_loaded_announcement": "Details for event {id} loaded",
      "audit.copy_success": "Event details copied to the clipboard",
      "audit.copy_error": "Event details could not be copied",
      "audit.report_sent": "Report sent to the support team",
      "audit.mark_read_success": "{count} events marked as read",
      "audit.archive_success": "{count} events archived",
      "audit.filters_reset": "Filters reset",
      "audit.new_event": "New event in audit log",
      "audit.live_enabled": "Live updates enabled",
      "audit.live_paused": "Live updates paused",
      "audit.refreshed": "Audit log refreshed",
      "audit.refresh_error": "The audit log could not be refreshed",
      "audit.loaded": "Audit log loaded",
      "audit.page_out_of_range": "Enter a page between 1 and {totalPages}",
      "audit.export_downloaded": "Prepared {fileName} for {range}",
      "audit.export_error": "The file could not be generated",
      "notifications.reminders": "Reminders",
      "notifications.mark_all": "Mark all as read",
      "citas.procedure_assistance": "Procedure assistance",
      "citas.new_aria": "Create new appointment",
      "citas.appointment_data": "Appointment data",
      "citas.office_status": "Office status",
      "citas.checklist": "Post-care checklist",
      "historia.healthy": "Healthy",
      "historia.treated": "Treated",
      "historia.treatment_followup": "Treatment follow-up",
      "historia.treatment_total": "Total treatments",
      "odo07.active": "Active",
      "odo07.add_plan": "Add treatment to the plan",
      "odo07.all_patients": "All patients",
      "odo07.all_professionals": "All professionals",
      "odo07.all_statuses": "All statuses",
      "odo07.all_types": "All types",
      "odo07.annul": "Void",
      "odo07.annul_reason": "Reason for voiding",
      "odo07.annul_session": "Void session",
      "odo07.annulled_by": "Voided by {name}: {reason}",
      "odo07.antiforgery_missing": "The request could not be validated. Reload the page and try again.",
      "odo07.appointment_date": "Appointment #{number} · {date}",
      "odo07.appointment_number": "Appointment #{number}",
      "odo07.breadcrumb_aria": "Breadcrumb",
      "odo07.cancel": "Cancel",
      "odo07.clear_filters": "Clear filters",
      "odo07.clinical_note_optional": "Clinical note (optional)",
      "odo07.completed": "Completed",
      "odo07.date_locale": "en-US",
      "odo07.document_label": "Document: {document}",
      "odo07.document_unknown": "Not available",
      "odo07.estimated": "Estimated sessions",
      "odo07.estimated_sessions": "Estimated sessions",
      "odo07.fdi_optional": "Permanent tooth FDI (optional)",
      "odo07.fdi_placeholder": "e.g. 16",
      "odo07.filter_patient_aria": "Filter by patient",
      "odo07.filter_professional_aria": "Filter by professional",
      "odo07.filter_status_aria": "Filter by status",
      "odo07.filter_type_aria": "Filter by treatment type",
      "odo07.filters_aria": "Treatment filters",
      "odo07.first_page": "First page",
      "odo07.general": "General",
      "odo07.last_page": "Last page",
      "odo07.list_aria": "Treatment list",
      "odo07.load_error": "Unable to load treatment follow-up.",
      "odo07.next_page": "Next page",
      "odo07.no_appointment": "No appointment",
      "odo07.no_note": "No note",
      "odo07.no_results": "No treatments found",
      "odo07.no_plans": "No treatments have been recorded yet",
      "odo07.no_results_help": "Adjust the filters or search and try again.",
      "odo07.no_sessions": "No sessions have been recorded.",
      "odo07.note_date": "Note #{number} · {date}",
      "odo07.note_number": "Note #{number}",
      "odo07.over_estimate": "Completed sessions exceed the estimate. Adjust the plan.",
      "odo07.page_number": "Page {number}",
      "odo07.page_size": "Per page",
      "odo07.page_size_aria": "Treatments per page",
      "odo07.pagination_aria": "Treatment pagination",
      "odo07.patient": "Patient",
      "odo07.paused": "Paused",
      "odo07.per_page_10": "10 per page",
      "odo07.per_page_15": "15 per page",
      "odo07.per_page_20": "20 per page",
      "odo07.per_page_5": "5 per page",
      "odo07.plan_saved": "Treatment plan saved.",
      "odo07.previous_page": "Previous page",
      "odo07.professional": "Professional",
      "odo07.register": "Record session",
      "odo07.register_session": "Record completed session",
      "odo07.registered_on": "Recorded",
      "odo07.registered_treatments": "Registered treatments",
      "odo07.related_appointment": "Related appointment (optional)",
      "odo07.related_note": "Related clinical note (optional)",
      "odo07.save": "Save",
      "odo07.save_plan_error": "Unable to save the treatment plan.",
      "odo07.save_to_plan": "Save to plan",
      "odo07.search_aria": "Search treatments",
      "odo07.search_placeholder": "Search by patient, document, or treatment...",
      "odo07.select_patient": "Select a patient",
      "odo07.select_patient_service_error": "Select a patient and service.",
      "odo07.select_service": "Select a service",
      "odo07.service": "Service",
      "odo07.session_annulled": "Session annulled with a reason recorded.",
      "odo07.session_date": "Session date",
      "odo07.attended_appointment": "Attended appointment",
      "odo07.select_attended_appointment": "Choose an attended appointment",
      "odo07.no_attended_appointments": "There are no attended appointments available for this treatment yet.",
      "odo07.session_date_help": "Select the date of the session. Choose today or an earlier date.",
      "odo07.session_followup_aria": "Session follow-up",
      "odo07.session_progress_aria": "Session progress: {progress}%",
      "odo07.session_registered": "Completed session recorded.",
      "odo07.session_update_error": "Unable to update the session.",
      "odo07.sessions_completed_count": "Sessions completed: {completed} of {estimated} estimated",
      "odo07.showing_none": "Showing 0 treatments",
      "odo07.showing_range": "Showing {start}-{end} of {total} treatments",
      "odo07.stats_aria": "Treatment statistics",
      "odo07.status": "Status",
      "odo07.status_approved": "Approved",
      "odo07.status_cancelled": "Cancelled",
      "odo07.status_completed": "Completed",
      "odo07.status_in_progress": "In progress",
      "odo07.status_named": "Status: {status}",
      "odo07.status_paused": "Paused",
      "odo07.status_proposed": "Proposed",
      "odo07.submit_button_missing": "The form submit button was not found.",
      "odo07.subtitle": "Review and update your patients' treatment plans.",
      "odo07.tooth_fdi": "FDI tooth",
      "odo07.treatment_type": "Treatment type",
      "odo07.unassigned": "Unassigned",
      "odo07.update_status": "Update status",
      "odo07.view_sessions": "View session log ({count})",
      "pqr.applicant_data": "Applicant information",
      "audit.subtitle": "Audit and operations log",
      "styleguide.development": "Development",
      "styleguide.title": "Style guide",
      "styleguide.design_system": "Design system",
      "pqr.petition_description": "Request information, documents or services related to your care.",
      "pqr.complaint_description": "Report dissatisfaction with the care or service received.",
      "pqr.claim_description": "Request correction of service irregularities or failures.",
      "notifications.title": "Reminders and Notifications",
      "notifications.page_title": "Notifications",
      "notifications.subtitle": "Reminders, confirmations and messages from the clinic",
      "notifications.mark_all_aria": "Mark all notifications as read",
      "notifications.search_ph": "Search notifications…",
      "notifications.search_aria": "Search notifications",
      "notifications.filter_aria": "Filter notifications by type",
      "notifications.filter_all": "All",
      "notifications.filter_reminders": "Reminders",
      "notifications.filter_confirmed": "Confirmations",
      "notifications.filter_cancelled": "Cancellations",
      "notifications.filter_messages": "Messages",
      "notifications.filter_waitlist": "Waitlist",
      "notifications.filter_invoices": "Billing",
      "notifications.list_aria": "Notifications list",
      "notifications.tab_notices": "Notices",
      "servicios.total": "Total Services",
      "pqr.total": "Total PQRs",
      "pqr.management": "PQR Management",
      "patients.active": "Active patients",
      "professionals.active": "Active professionals",

      // Language switcher
      "lang.es": "ES",
      "lang.en": "EN",
      "lang.switch_es": "Spanish",
      "lang.switch_en": "English",

      // ═══════════════════════════════════════════════════════
      // LOGIN
      // ═══════════════════════════════════════════════════════
      "login.page_title": "SmileTrack — Sign In",
      "login.meta_description": "Sign in to SmileTrack to access your account and manage your appointments, history and more.",
      "login.subtitle": "Dental Management System",
      "access_denied.title": "Access denied",
      "access_denied.subtitle": "Your account does not have permission to view this page.",
      "access_denied.message": "You are signed in, but your role does not have access to this resource.",
      "access_denied.home": "Back to home",
      "login.select_role": "Select your role",
      "login.role_label": "ACCOUNT ROLE",
      "login.role_admin": "Administrator",
      "login.role_profesional": "Professional",
      "login.role_paciente": "Patient",
      "login.role_recepcionista": "Receptionist",
      "login.role_auxiliar": "Assistant",
      "login.role_aria_admin": "Sign in as Administrator",
      "login.role_aria_profesional": "Sign in as Professional",
      "login.role_aria_paciente": "Sign in as Patient",
      "login.role_aria_recepcionista": "Sign in as Receptionist",
      "login.role_aria_auxiliar": "Sign in as Assistant",
      "login.email_label": "EMAIL",
      "login.email_placeholder": "dr.smith@smiletrack.com",
      "login.password_label": "PASSWORD",
      "login.password_placeholder": "••••••••",
      "login.show_password": "Show password",
      "login.hide_password": "Hide password",
      "login.remember_me": "Remember me",
      "login.forgot_password": "Forgot your password?",
      "login.button_submit": "Sign In",
      "login.button_loading": "Signing in...",
      "login.no_account": "Don't have an account?",
      "login.register_here": "Register here",
      "login.footer_badge": "Secure Clinical Portal · v2.4.0",
      "login.error_required_email": "Enter your email",
      "login.error_required_password": "Enter your password",
      "login.error_incomplete_data": "Enter your email and password to sign in.",
      "login.error_invalid_format_title": "Invalid format",
      "login.error_invalid_format_msg": "Please enter a valid email address.",
      "login.error_verify_email": "Verify your email",
      "login.error_verify_password": "Verify your password",
      "login.toast_welcome_title": "Welcome!",
      "login.toast_welcome_msg": "Signing you in securely...",
      "login.toast_denied_title": "Access denied",
      "login.toast_connection_title": "Connection error",
      "login.toast_connection_msg": "Could not connect to the server. Please try again.",
      "login.error_incomplete_title": "Incomplete data",

      // Auth error modal
      "login.auth_error_title": "Authentication error",
      "login.auth_error_close_aria": "Close window",
      "login.auth_error_message": "incorrect email or password",
      "login.auth_error_button": "Close",

      // ═══════════════════════════════════════════════════════
      // REGISTER
      // ═══════════════════════════════════════════════════════
      "register.page_title": "Create Account — SmileTrack",
      "register.meta_description": "Create an account in SmileTrack - Dental Clinic Management System",
      "register.tagline": "Join the professional dental network",
      "register.button_submit": "Create account",
      "register.button_loading": "Creating account...",
      "register.have_account": "Already have an account?",
      "register.login_here": "Sign in here",
      "register.footer_badge": "Secure Clinical Portal • v2.4.0",

      // Fieldsets
      "register.fieldset_personal": "Personal Information",
      "register.fieldset_docs": "Documentation",
      "register.fieldset_contact": "Contact",
      "register.fieldset_security": "Security",

      // Fields
      "register.firstname_label": "First name",
      "register.firstname_placeholder": "e.g. John Carlos",
      "register.lastname_label": "Last name",
      "register.lastname_placeholder": "e.g. Smith Johnson",
      "register.doctype_label": "Document Type",
      "register.doctype_select": "Select type...",
      "register.doctype_cc": "Citizenship ID",
      "register.doctype_ti": "Identity card",
      "register.doctype_ce": "Foreigner ID",
      "register.doctype_pas": "Passport",
      "register.docnum_label": "Document Number",
      "register.docnum_placeholder": "e.g. 12345678",
      "register.email_label": "Email",
      "register.email_placeholder": "dentist@dentalclinic.com",
      "register.phone_label": "Phone",
      "register.phone_placeholder": "+57 300 123 456",
      "register.password_label": "Password",
      "register.password_placeholder": "Minimum 8 characters",
      "register.confirm_password_label": "Confirm Password",
      "register.confirm_password_placeholder": "Repeat your password",

      // Password criteria
      "register.pwd_criterion_length": "• Minimum 8 characters",
      "register.pwd_criterion_upper": "• At least one uppercase letter",
      "register.pwd_criterion_number": "• At least one number",
      "register.pwd_criterion_symbol": "• At least one special symbol",

      // Noscript
      "register.noscript_warning": "JavaScript is disabled in your browser. The form can be submitted, but some validations and the enhanced experience will not be available.",

      // Validation / messages
      "register.error_all_fields": "Fill in all required fields before continuing",
      "register.error_password_mismatch": "Passwords do not match. Please review the confirmation field.",
      "register.error_password_weak": "The password must have at least 8 characters, one uppercase letter, one number and one symbol.",
      "register.error_select_role": "Select a valid role before continuing.",
      "register.success_created": "Account created successfully!",
      "register.error_create": "Error creating account. Please review your data.",
      "register.show_password": "Show password",
      "register.hide_password": "Hide password",

      // ═══════════════════════════════════════════════════════
      // RECOVER PASSWORD
      // ═══════════════════════════════════════════════════════
      "recover.page_title": "Reset Password - Smile Track",
      "recover.meta_description": "Reset your SmileTrack password - Dental Clinic Management System",
      "recover.header_title": "Reset Password",
      "recover.footer_badge": "Secure Clinical Portal • v2.4.0",

      // Step descriptions
      "recover.step1_description": "Enter your email and we will send you a verification code.",
      "recover.step2_description": "Check your email and enter the 6-digit code you received.",
      "recover.step3_description": "Create a new secure password for your account.",

      // Step 1 - Email
      "recover.step1_email_label": "Email",
      "recover.step1_email_placeholder": "dentist@dentalclinic.com",
      "recover.step1_button": "Send Code",
      "recover.step1_error_invalid_email": "Enter a valid email address",
      "recover.step1_error_send": "Error sending the code. Please try again.",
      "recover.step1_error_send_later": "Could not send the code. Please try later.",
      "recover.step1_success": "Code sent to your email",

      // Step 2 - OTP Code
      "recover.step2_code_sent_to": "We have sent a 6-digit code to:",
      "recover.step2_resend": "Didn't receive the code? Resend",
      "recover.step2_resend_sending": "Sending...",
      "recover.step2_resend_wait": "Resend in {n}s",
      "recover.step2_otp_label": "Verification Code",
      "recover.step2_otp_digit": "Digit {n} of 6",
      "recover.step2_error_incomplete": "Enter the 6 digits of the code",
      "recover.step2_error_invalid": "Incorrect or expired code.",
      "recover.step2_error_verify": "Verification error. Please try again.",
      "recover.step2_success": "Code verified",
      "recover.step2_cooldown_wait": "Wait {n} seconds before resending.",
      "recover.step2_cooldown_info": "New code sent",
      "recover.step2_cooldown_error": "Could not resend the code.",
      "recover.step2_back_button": "Back",
      "recover.step2_verify_button": "Verify Code",

      // Step 3 - New password
      "recover.step3_new_password_label": "New Password",
      "recover.step3_new_password_placeholder": "Minimum 8 characters",
      "recover.step3_confirm_password_label": "Confirm Password",
      "recover.step3_confirm_password_placeholder": "Repeat your password",
      "recover.step3_criteria_length": "• Minimum 8 characters",
      "recover.step3_criteria_upper": "• At least one uppercase letter",
      "recover.step3_criteria_number": "• At least one number",
      "recover.step3_criteria_symbol": "• At least one symbol (e.g. !#$%)",
      "recover.step3_match_nomatch": "Passwords do not match",
      "recover.step3_match_ok": "✓ Passwords match",
      "recover.step3_back_button": "Back",
      "recover.step3_reset_button": "Reset Password",
      "recover.step3_error_requirements": "Password does not meet requirements",
      "recover.step3_error_mismatch": "Passwords do not match",
      "recover.step3_error_unknown": "An error occurred. Go back to the previous step.",
      "recover.step3_error_invalid_code": "Enter a valid 6-digit code",
      "recover.step3_error_reset": "Error resetting password. Please try again.",
      "recover.step3_success_reset": "Password reset successful!",

      // Success step
      "recover.success_title": "Password reset successful!",
      "recover.success_message": "Your password has been updated successfully. You can now sign in with your new password.",
      "recover.success_goto_login": "Sign In",
      "recover.success_updated_desc": "Your password has been updated successfully.",

      // Footer link
      "recover.back_to_login": "Back to sign in",

      // Toggle password
      "recover.toggle_show": "Show password",
      "recover.toggle_hide": "Hide password",

      // ═══════════════════════════════════════════════════════
      // HOMEPAGE - GENERAL
      // ═══════════════════════════════════════════════════════
      "home.meta_description": "SmileTrack is the dental management platform to book appointments, consult treatments and contact specialists with confidence.",
      "home.meta_keywords": "dentistry, dental appointments, dental clinic, clinical history, appointment booking",
      "home.og_title": "SmileTrack | Book your dental appointment online",
      "home.og_description": "Book your appointment, consult specialists and manage your oral health with security and professional support.",
      "home.page_title": "SmileTrack | Book dental appointments, manage your oral health",
      "home.logo_alt": "Smile Track Logo",
      "home.logo_text": "Smile Track",
      "home.jsonld_description": "Dental clinic in Bogotá offering online appointments, specialized care and clinical history management.",
      "home.jsonld_name": "SmileTrack",

      // Nav
      "home.nav_services": "Services",
      "home.nav_benefits": "The platform",
      "home.nav_professionals": "Professionals",
      "home.nav_testimonials": "Testimonials",
      "home.nav_about": "About Us",
      "home.nav_contact": "Contact",
      "home.nav_help": "Help Center",

      // CTA buttons header
      "home.cta_login": "Sign in",
      "home.cta_book": "Book appointment",

      // Floating buttons
      "home.float_chat_title": "Chat",
      "home.float_phone_title": "Call",

      // Hero badge
      "home.hero_badge": "SmileTrack dental platform",
      "home.hero_title_html": "Dental care, <span>easier to manage</span>",
      "home.hero_title": "Dental care, easier to manage",
      "home.hero_highlight": "easier to manage",
      "home.hero_subtitle": "Browse active services and manage your appointments in one place.",

      // Hero CTAs
      "home.hero_login": "Sign in to my account",
      "home.hero_register": "Create account",

      // Hero tags
      "home.tag_easy_booking": "Service catalog",
      "home.tag_reliable": "Account access",
      "home.tag_followup": "Appointment management",

      // Hero 3 cards
      "home.card_patient_title": "I'm a patient",
      "home.card_patient_desc": "Create your account and book your first appointment.",
      "home.card_pro_title": "I'm a professional",
      "home.card_pro_desc": "Access your schedule and manage your appointments.",
      "home.card_contact_title": "Quick contact",
      "home.card_contact_desc": "Talk to our team to resolve your questions.",

      // Glass card featured services
      "home.glass_services_title": "Featured Services",
      "home.glass_see_specialists": "Consult with specialists →",
      "home.service_cleaning_name": "Dental cleaning",
      "home.service_cleaning_desc": "Prevention · 45 min",
      "home.service_cleaning_price": "From $80k",
      "home.service_whitening_name": "Whitening",
      "home.service_whitening_desc": "Aesthetics · 90 min",
      "home.service_whitening_price": "From $350k",
      "home.service_ortho_name": "Orthodontics",
      "home.service_ortho_desc": "Correction · Monthly plan",
      "home.service_ortho_price": "From $200k/month",

      // Trust strip
      "home.trust_patients_count": "+2,400",
      "home.trust_patients_label": "Patients served",
      "home.trust_rating_count": "4.9/5.0",
      "home.trust_rating_label": "Google rating",
      "home.trust_experience_count": "15 years",
      "home.trust_experience_label": "Of clinical experience",
      "home.trust_invima_count": "INVIMA",
      "home.trust_invima_label": "Licensed and certified",

      // Services section
      "home.services_eyebrow": "What we do",
      "home.services_title": "Dental services",
      "home.services_subtitle": "Information from SmileTrack's active service catalog.",
      "home.active_services_label": "active services in the catalog",
      "home.services_empty_title": "No services have been published yet",
      "home.services_empty": "Services will appear here after they are added and activated in the catalog.",
      "home.view_services": "View catalog",
      "home.live_catalog": "Updated catalog",
      "home.feature_appointments": "Appointment management",
      "home.feature_appointments_desc": "View and manage your requests from your account.",
      "home.feature_account": "Personal portal",
      "home.feature_account_desc": "Sign in to your SmileTrack account.",
      "home.journey_eyebrow": "Get started in a few steps",
      "home.journey_title": "Your dental care, easier to organize",
      "home.journey_desc": "Find a service and use your account to keep track of your appointments.",
      "home.journey_services_title": "Explore services",
      "home.journey_services_desc": "Browse the active catalog, including published descriptions, durations, and prices.",
      "home.journey_account_title": "Create your account",
      "home.journey_account_desc": "Register to access the features available for your profile.",
      "home.journey_appointments_title": "Manage appointments",
      "home.journey_appointments_desc": "Sign in to view and manage your appointment requests.",
      "home.get_started_eyebrow": "Get started",
      "home.get_started_title": "Manage your care with SmileTrack",
      "home.get_started_desc": "Create an account or sign in to access the features available for your profile.",

      "home.svc_cleaning_title": "Dental cleaning",
      "home.svc_cleaning_price": "$80,000",
      "home.svc_cleaning_desc": "Removes tartar and plaque that the toothbrush cannot reach.",
      "home.svc_cleaning_tag": "For the whole family",

      "home.svc_whitening_title": "Whitening",
      "home.svc_whitening_price": "$350,000",
      "home.svc_whitening_desc": "Restore the natural shine of your teeth safely and effectively.",
      "home.svc_whitening_tag": "Advanced aesthetics",

      "home.svc_ortho_title": "Orthodontics",
      "home.svc_ortho_price": "$200k/month",
      "home.svc_ortho_desc": "Dental position correction with fixed and invisible techniques.",
      "home.svc_ortho_tag": "Children and adults",

      "home.svc_endo_title": "Endodontics",
      "home.svc_endo_price": "$450,000",
      "home.svc_endo_desc": "Root canal treatment to save compromised teeth.",
      "home.svc_endo_tag": "High precision",

      "home.svc_implants_title": "Implants",
      "home.svc_implants_price": "$2,800,000",
      "home.svc_implants_desc": "Replace missing teeth with long-lasting titanium implants.",
      "home.svc_implants_tag": "Permanent solution",

      "home.svc_pediatrics_title": "Pediatric Dentistry",
      "home.svc_pediatrics_price": "From $60k",
      "home.svc_pediatrics_desc": "Specialized dental care for the little ones at home.",
      "home.svc_pediatrics_tag": "Children 0–12 years",

      // Professionals section
      "home.pro_eyebrow": "Our team",
      "home.pro_title": "Specialists who inspire trust",
      "home.pro_1_name": "Dr. Carlos Méndez",
      "home.pro_1_specialty": "General Dentistry / Endodontics",
      "home.pro_1_bio": "Expert with over 12 years of experience in complex root canal treatments.",
      "home.pro_1_edu_1": "National U.",
      "home.pro_1_edu_2": "Endodontics Spec.",
      "home.pro_1_alt": "Dr. Carlos Méndez",
      "home.pro_2_name": "Dr. Andrés Torres",
      "home.pro_2_specialty": "Oral Surgery / Implants",
      "home.pro_2_bio": "Specialist in complex restorations and third molar surgery.",
      "home.pro_2_edu_1": "Rosario U.",
      "home.pro_2_edu_2": "Surgery Spec.",
      "home.pro_2_alt": "Dr. Andrés Torres",
      "home.pro_3_name": "Dr. Laura Gómez",
      "home.pro_3_specialty": "Orthodontics / Pediatric Dentistry",
      "home.pro_3_bio": "Dedicated to creating perfect smiles in children and adults with the latest techniques.",
      "home.pro_3_edu_1": "Javeriana U.",
      "home.pro_3_edu_2": "Orthodontics Spec.",
      "home.pro_3_alt": "Dr. Laura Gómez",

      // Testimonials
      "home.test_eyebrow": "What they say",
      "home.test_title": "Patients who already smile with confidence",
      "home.test_1_since": "Patient since 2021",
      "home.test_1_quote": "Dr. Méndez was incredibly professional. My endodontics was painless and the follow-up was excellent. Highly recommended.",
      "home.test_1_name": "Maria P.",
      "home.test_2_since": "Patient since 2020",
      "home.test_2_quote": "I took my children and Dr. Gómez treated them with unique patience and warmth. The clinic is modern and very clean.",
      "home.test_2_name": "Carlos R.",
      "home.test_3_since": "Patient since 2023",
      "home.test_3_quote": "The whitening exceeded my expectations. In a single session I was delighted with the results. I will be back soon.",
      "home.test_3_name": "Luisa F.",

      // About us
      "home.about_eyebrow": "Our story",
      "home.about_left_title": "A clinic with history and results",
      "home.about_left_year_label": "Year of founding, setting a superior quality standard in Bogotá.",
      "home.about_left_patients_label": "Registered patients with 98% guaranteed satisfaction.",
      "home.about_left_digital_label": "Digital: electronic clinical history and AI-assisted diagnosis.",
      "home.about_right_title": "Technology and human warmth in every consultation",
      "home.about_right_para1": "We are a family clinic founded in 2009 with the mission of democratizing access to high-quality dentistry in Bogotá. Our multidisciplinary team works with the highest clinical standards.",
      "home.about_right_para2": "We integrate digital technology — 3D X-rays, AI-assisted diagnosis and cloud-based clinical history management — to offer more precise and safe treatments.",
      "home.about_badge_invima": "INVIMA licensed",
      "home.about_badge_rips": "RIPS active",
      "home.about_badge_registry": "District Registry",

      // FAQ
      "home.faq_eyebrow": "Common Questions",
      "home.faq_title": "Frequently Asked Questions",
      "home.faq_1_q": "Do you accept insurance or prepaid medicine?",
      "home.faq_1_a": "Yes, we have agreements with the main prepaid medicine entities (Colsanitas, Sura, MedPlus). Please contact us to verify the exact coverage of your plan before booking.",
      "home.faq_2_q": "What are the available payment methods?",
      "home.faq_2_a": "We accept credit/debit cards, bank transfers, Nequi, DaviPlata and cash. We also have financing options for complex treatments such as orthodontics or implants.",
      "home.faq_3_q": "What is the process to book a first appointment?",
      "home.faq_3_a": "You can use the contact form on this page or register on our platform (\"Create account\"). Once registered, you can book, reschedule and cancel your appointments 100% digitally, as well as review your clinical history.",

      // Final CTA
      "home.final_cta_title": "Ready to transform your smile?",
      "home.final_cta_subtitle": "Join our platform and manage your oral health in a modern and hassle-free way.",
      "home.final_cta_begin": "Start now",
      "home.final_cta_contact": "Contact us",

      // Contact
      "home.contact_eyebrow": "Find us",
      "home.contact_title": "We are here to serve you",
      "home.contact_address_label": "Address",
      "home.contact_address_value_html": "Cra. 15 #93-47, Office 302<br>Bogotá D.C., Colombia",
      "home.contact_address_value": "Cra. 15 #93-47, Office 302\nBogotá D.C., Colombia",
      "home.contact_schedule_label": "Schedule",
      "home.contact_schedule_value_html": "Monday to Friday: 8am – 6pm<br>Saturdays: 9am – 2pm · Sundays: Closed",
      "home.contact_schedule_value": "Monday to Friday: 8am – 6pm\nSaturdays: 9am – 2pm · Sundays: Closed",
      "home.contact_phone_label": "Phone / WhatsApp",
      "home.contact_phone_value": "+57 300 123 4567",
      "home.contact_map_alt": "Bogotá clinic map",

      // Contact form
      "home.form_title": "Book your appointment here",
      "home.form_name_placeholder": "Full name",
      "home.form_phone_placeholder": "Phone / WhatsApp",
      "home.form_service_select": "Service of interest",
      "home.form_option_cleaning": "Dental cleaning",
      "home.form_option_whitening": "Whitening",
      "home.form_option_ortho": "Orthodontics",
      "home.form_option_endo": "Endodontics",
      "home.form_option_implants": "Implants",
      "home.form_option_general": "General consultation",
      "home.form_submit": "Confirm via WhatsApp",
      "home.form_consent": "I agree to open WhatsApp with these details and accept the",

      // Bottom final CTA
      "home.bottom_cta_title": "Your smile shouldn't wait any longer",
      "home.bottom_cta_subtitle": "First complete assessment included. No commitment.",
      "home.bottom_cta_book": "Book appointment now",
      "home.bottom_cta_whatsapp": "Text via WhatsApp",

      // Footer
      "home.footer_logo_alt": "Smile Track",
      "home.footer_copyright_html": "© 2025 Smile Track. All rights reserved. High Precision Dental Management.<br>INVIMA Licensed · Active RIPS · Valid District Registry.",
      "home.footer_copyright": "© 2025 Smile Track. All rights reserved. High Precision Dental Management.\nINVIMA Licensed · Active RIPS · Valid District Registry.",
      "home.footer_social_fb": "Facebook",
      "home.footer_social_ig": "Instagram",
      "home.footer_social_email": "Email",
      "home.footer_legal_title": "Legal",
      "home.footer_privacy": "Privacy",
      "home.footer_terms": "Terms",
      "home.footer_notice": "Legal Notice",
      "home.footer_company_title": "Company",
      "home.footer_support": "Support",
      "home.footer_blog": "Blog",
      "home.footer_faq": "FAQ",

      // Floating WhatsApp
      "home.whatsapp_aria": "WhatsApp",
      "home.whatsapp_tooltip": "Hi! How can we help you?",

      // Modals
      // Chatbot
      "home.chat_title": "SmileTrack Assistant",
      "home.chat_online": "Online",
      "home.chat_close_aria": "Close",
      "home.chat_placeholder": "Type your question...",
      "home.chat_send_aria": "Send",
      "home.chat_welcome": "Hi! 👋 I'm your SmileTrack assistant. How can I help you?",
      "home.chat_qr_appointments": "📅 Book",
      "home.chat_qr_payments": "💳 Payments",
      "home.chat_qr_services": "🦷 Services",
      "home.chat_qr_location": "📍 Location",
      "home.chat_response_appointments": "📅 To book: 1) Click \"Book appointment\", 2) Fill out the form, 3) We will confirm via WhatsApp.",
      "home.chat_response_payments": "💳 We accept: Cash, cards, Nequi, Daviplata. Check your payments in your account.",
      "home.chat_response_services": "🦷 We offer: Cleaning ($80k), Whitening ($350k), Orthodontics and more. Would you like to book?",
      "home.chat_response_schedule": "🕒 Mon-Fri: 8AM-6PM | Sat: 9AM-2PM | Emergencies: 300 123 4567",
      "home.chat_response_emergency": "🚨 For emergencies: Call NOW at 300 123 4567 or come to our office.",
      "home.chat_response_location": "📍 We are at: Cra. 15 #93-47, Office 302, Bogotá. We also offer virtual consultations.",
      "home.chat_response_fallback": "😅 I didn't understand. Ask me about: appointments, payments, services, schedules, emergencies or location.",
      "home.chat_emergency_toast": "🚨 For emergencies, call 300 123 4567",

      // PQRS
      "home.pqrs_title": "PQRS - SmileTrack",
      "home.pqrs_status": "Response in ≤ 48 business hours",
      "home.pqrs_type_label": "Request type *",
      "home.pqrs_type_p": "🔍 Request",
      "home.pqrs_type_q": "❓ Complaint",
      "home.pqrs_type_r": "🙏 Claim",
      "home.pqrs_type_s": "💡 Suggestion",
      "home.pqrs_name_placeholder": "Full name *",
      "home.pqrs_email_placeholder": "Email *",
      "home.pqrs_phone_placeholder": "Phone",
      "home.pqrs_doc_placeholder": "Document (optional)",
      "home.pqrs_subject_placeholder": "Subject *",
      "home.pqrs_message_placeholder": "Detailed description *",
      "home.pqrs_consent_html": "I accept the <a href=\"#\" class=\"st-link\">data policy</a>",
      "home.pqrs_consent": "I accept the data policy",
      "home.pqrs_btn_cancel": "Cancel",
      "home.pqrs_btn_send": "Send PQRS",
      "home.pqrs_success_title": "Request received!",
      "home.pqrs_success_body1": "Your PQRS #",
      "home.pqrs_success_body2": " has been registered.",
      "home.pqrs_success_body3": "We will respond to ",
      "home.pqrs_success_body4": " in ≤ 48 business hours.",
      "home.pqrs_success_new": "New request",
      "home.pqrs_cancel_confirm": "Cancel this request?",
      "home.pqrs_unavailable_warning": "PQRS is not available from this page. Sign in to register it.",
      "home.pqrs_send_error": "❌ Error sending. Please try again.",
      

      // Home: WhatsApp default message
      "home.whatsapp_message": "Hi! I want to book a dental appointment at Smile Track.",

      // Toast
      "home.toast_success": "✅",
      "home.toast_error": "❌",
      "home.toast_warning": "⚠️",

      // Professional dashboard
      "odo01.breadcrumb_aria": "Breadcrumb",
      "odo01.breadcrumb_professional": "Professional",
      "odo01.breadcrumb_dashboard": "Dashboard",
      "odo01.subtitle": "Overview of your professional activity",
      "odo01.view_reports": "View full reports",
      "odo01.next_urgent": "Urgent appointment!",
      "odo01.next_label": "Next appointment",
      "odo01.consultation": "Consultation",
      "odo01.office_unassigned": "Office not assigned",
      "odo01.last_visit": "Last visit",
      "odo01.view_in_agenda": "View schedule",
      "odo01.no_upcoming": "You have no upcoming appointments. Great work!",
      "odo01.daily_progress_aria": "Daily progress",
      "odo01.daily_progress_label": "{attended} of {total} appointments completed today ({percent}%)",
      "odo01.daily_progress_bar_aria": "Daily progress: {percent}%",
      "odo01.quick_actions": "Quick actions",
      "odo01.my_agenda": "My schedule",
      "odo01.my_agenda_desc": "View and manage appointments",
      "odo01.my_patients": "My patients",
      "odo01.my_patients_desc": "Clinical history",
      "odo01.reports": "Reports",
      "odo01.reports_desc": "Detailed statistics",
      "odo01.my_profile": "My profile",
      "odo01.my_profile_desc": "Personal information",
      "odo01.stats_aria": "Key metrics",
      "odo01.stat_patients": "Patients with appointments this month",
      "odo01.stat_today": "Appointments today",
      "odo01.stat_attended": "Appointments attended today",
      "odo01.stat_revenue": "Estimated value of attended services",
      "odo01.revenue_note": "Estimated using catalog prices; it does not represent invoicing or collections.",
      "odo01.tooltip_patients": "Unique patients seen this month",
      "odo01.tooltip_today": "Total appointments scheduled for today",
      "odo01.tooltip_attended": "Appointments completed today",
      "odo01.tooltip_revenue": "Estimated value using catalog prices for attended appointments",
      "odo01.revenue_title": "Estimated value of attended services in the last 3 months",
      "odo01.revenue_aria": "Estimated value of attended services for the last three months",
      "odo01.revenue_bar_aria": "{month}: {amount}",
      "odo01.revenue_summary": "Estimated from catalog prices for the authenticated professional's attended appointments; it does not represent invoicing or collections.",
      "odo01.revenue_empty": "No attended services in this period.",
      "odo01.recent_patients": "Recently attended patients",
      "odo01.patients_table_aria": "Recently attended patients table",
      "odo01.patient": "Patient",
      "odo01.service": "Service",
      "odo01.date": "Date",
      "odo01.no_recent_patients": "No patients were seen this month.",
      "odo01.performance": "Today's performance",
      "odo01.performance_excellent": "Excellent day! You are meeting your goals.",
      "odo01.performance_good": "Good pace, keep it up.",
      "odo01.performance_low": "Several appointments are pending; organize your schedule.",
      "odo01.performance_attended": "Attended",
      "odo01.notifications": "Notifications",
      "odo01.dismiss_notification": "Dismiss notification",
      "odo01.notification_unconfirmed": "{count} unconfirmed appointment(s)",
      "odo01.review_agenda": "Review your schedule to confirm",
      "odo01.notification_waiting": "{count} patient(s) waiting",
      "odo01.waiting_desc": "Waiting in the treatment room",
      "odo01.notification_records": "{count} clinical record(s) to complete",
      "odo01.complete_records": "Complete the pending clinical records",
      "odo01.notifications_empty": "All caught up",
      "odo01.monthly_appointments": "Your appointments this month",
      "odo01.status_chart_aria": "Status of your appointments this month",
      "odo01.status_attended": "Attended",
      "odo01.status_scheduled": "Scheduled",
      "odo01.status_cancelled": "Cancelled",
      "odo01.status_no_show": "No-show",
      "odo01.status_attended_aria": "Attended: {count} appointments, {percent}%",
      "odo01.status_scheduled_aria": "Scheduled: {count} appointments, {percent}%",
      "odo01.status_cancelled_aria": "Cancelled: {count} appointments, {percent}%",
      "odo01.status_no_show_aria": "No-show: {count} appointments, {percent}%",
      "odo01.upcoming_today": "Upcoming appointments today",
      "odo01.patient_label": "Patient:",
      "odo01.not_available": "Not available",
      "odo01.open_odontogram": "Open odontogram",
      "odo01.no_more_appointments_today": "You have no more appointments scheduled for today.",
      "odo01.countdown_now": "It's now",
      "odo01.countdown_minutes_one": "In {count} minute",
      "odo01.countdown_minutes_other": "In {count} minutes",
      "odo01.date_locale": "en-US",

      // ═══════════════════════════════════════════════════════
      // SIDEBAR — Internal navigation (shared by all roles)
      // ═══════════════════════════════════════════════════════

      // Navigation groups
      "nav.group_inicio":               "Home",
      "nav.group_reportes":             "Reports & Analytics",
      "nav.group_acceso":               "Access & Security",
      "nav.group_pacientes":            "Patient Management",
      "nav.group_historia":             "Clinical History",
      "nav.group_profesionales":        "Professional Management",
      "nav.group_citas":                "Appointment Management",
      "nav.group_facturacion":          "Billing & Payments",
      "nav.group_servicios":            "Services & Resources",
      "nav.group_pqr":                  "PQR Management",
      "nav.group_ayuda":                "Help Center",

      // Navigation items
      "nav.homepage":                   "Homepage",
      "nav.panel_analitica":            "Analytics Panel",
      "nav.gestion_usuarios":           "User Management",
      "nav.gestion_roles":              "Role Management",
      "nav.bitacora":                   "System Log",
      "nav.cambiar_contrasena":         "Change Password",
      "nav.gestion_pacientes":          "Patient Management",
      "nav.historial_clinico":          "Clinical History",
      "nav.gestion_profesionales":      "Professional Management",
      "nav.reportes_clinicos":          "Clinical Reports",
      "nav.dashboard_general":          "General Dashboard",
      "nav.agenda_general":             "General Schedule",
      "nav.gestion_citas":              "Comprehensive Appointment Management",
      "nav.gestion_facturacion":        "Billing Management",
      "nav.catalogo_servicios":         "Service Catalog",
      "nav.configuracion_general":      "General Settings",
      "nav.pqr_lista":                  "Petitions, Complaints & Claims",
      "nav.pqr_nueva":                  "New Request",
      "nav.ayuda_programar_cita":       "How to schedule an appointment",
      "nav.ayuda_guias":                "Guides & Tutorials",
      "nav.ayuda_soporte":              "Support & Tickets",
      "nav.cerrar_sesion":              "Sign out",

      // Sidebar accessibility / aria
      "nav.skip_link":                  "Skip to main content",
      "nav.sidebar_label":              "Main navigation",
      "nav.hamburger_open":             "Open menu",
      "nav.hamburger_close":            "Close menu",

      // Language switcher (sidebar)
      "nav.lang_switcher_label":        "Switch language",

      // ═══════════════════════════════════════════════════════
      // INTERNAL FOOTER (inside authenticated layout)
      // ═══════════════════════════════════════════════════════
      "footer.copyright":               "© 2025 SmileTrack. All rights reserved.",
      "footer.privacy":                 "Privacy",
      "footer.terms":                   "Terms",

      // ═══════════════════════════════════════════════════════
      // TOASTS / COMMON NOTIFICATIONS
      // ═══════════════════════════════════════════════════════
      "toast.success_title":            "Operation successful",
      "toast.error_title":              "Error",
      "toast.warning_title":            "Warning",
      "toast.info_title":               "Information",
      "toast.saving":                   "Saving...",
      "toast.saved":                    "Saved successfully",
      "toast.deleting":                 "Deleting...",
      "toast.deleted":                  "Deleted successfully",
      "toast.loading":                  "Loading...",
      "toast.confirm_delete":           "Confirm deletion?",
      "toast.confirm_delete_msg":       "This action cannot be undone.",
      "toast.session_expired":          "Your session has expired. Please sign in again.",
      "toast.no_permission":            "You do not have permission to perform this action.",
      "toast.network_error":            "Network error. Check your connection and try again.",

      // ═══════════════════════════════════════════════════════
      // TABLES / COMMON LISTS
      // ═══════════════════════════════════════════════════════
      "table.no_data":                  "No data available",
      "table.loading":                  "Loading data...",
      "table.actions":                  "Actions",
      "table.search":                   "Search...",
      "table.filter_all_states":        "All statuses",
      "table.filter":                   "Filter",
      "table.export":                   "Export",
      "table.page_of":                  "Page {current} of {total}",
      "table.rows_per_page":            "Rows per page",
      "table.prev":                     "Previous",
      "table.next":                     "Next",
      "table.first":                    "First",
      "table.last":                     "Last",
      "table.total_records":            "{n} record(s) total",
      "table.selected":                 "{n} selected",
      "table.select_all":               "Select all",

      // ═══════════════════════════════════════════════════════
      // COMMON FORMS
      // ═══════════════════════════════════════════════════════
      "form.save":                      "Save",
      "form.save_changes":              "Save changes",
      "form.cancel":                    "Cancel",
      "form.edit":                      "Edit",
      "form.delete":                    "Delete",
      "form.view":                      "View",
      "form.create":                    "Create",
      "form.add":                       "Add",
      "form.search":                    "Search",
      "form.filter":                    "Filter",
      "form.clear":                     "Clear",
      "form.reset":                     "Reset",
      "form.close":                     "Close",
      "form.confirm":                   "Confirm",
      "form.upload":                    "Upload file",
      "form.download":                  "Download",
      "form.print":                     "Print",
      "form.required_fields":           "Fields marked with * are required.",
      "form.unsaved_changes":           "You have unsaved changes. Do you want to leave?",

      // ═══════════════════════════════════════════════════════
      // STATUSES / COMMON BADGES
      // ═══════════════════════════════════════════════════════
      "status.active":                  "Active",
      "status.inactive":                "Inactive",
      "status.pending":                 "Pending",
      "status.confirmed":               "Confirmed",
      "status.cancelled":               "Cancelled",
      "status.completed":               "Completed",
      "status.in_progress":             "In progress",
      "status.scheduled":               "Scheduled",
      "status.no_show":                 "No show",
      "status.rescheduled":             "Rescheduled",
      "status.enabled":                 "Enabled",
      "status.disabled":                "Disabled",
      "status.blocked":                 "Blocked",
      "status.paid":                    "Paid",
      "status.unpaid":                  "Payment pending",
      "status.partial":                 "Partial payment",
      "status.open":                    "Open",
      "status.closed":                  "Closed",
      "status.resolved":                "Resolved",

      // ═══════════════════════════════════════════════════════
      // MODULES — APPOINTMENTS
      // ═══════════════════════════════════════════════════════
      "citas.title":                    "Appointment Management",
      "citas.new":                      "New appointment",
      "citas.edit":                     "Edit appointment",
      "citas.cancel":                   "Cancel appointment",
      "citas.reschedule":               "Reschedule appointment",
      "citas.confirm":                  "Confirm appointment",
      "citas.date":                     "Date",
      "citas.time":                     "Time",
      "citas.patient":                  "Patient",
      "citas.professional":             "Professional",
      "citas.service":                  "Service",
      "citas.status":                   "Status",
      "citas.notes":                    "Notes",
      "citas.duration":                 "Duration",
      "citas.office":                   "Office",
      "citas.no_appointments":          "No appointments for this period.",
      "citas.filter_date":              "Filter by date",
      "citas.filter_status":            "Filter by status",
      "citas.filter_professional":      "Filter by professional",
      "citas.dashboard_title":          "Appointments Dashboard",
      "citas.total_today":              "Today's appointments",
      "citas.total_week":               "This week",
      "citas.total_month":              "This month",
      "citas.attendance_rate":          "Attendance rate",

      // ═══════════════════════════════════════════════════════
      // MODULES — PATIENTS
      // ═══════════════════════════════════════════════════════
      "pacientes.title":                "Patient Management",
      "pacientes.new":                  "New patient",
      "pacientes.edit":                 "Edit patient",
      "pacientes.view":                 "View patient",
      "pacientes.name":                 "Name",
      "pacientes.document":             "Document",
      "pacientes.birth_date":           "Date of birth",
      "pacientes.age":                  "Age",
      "pacientes.gender":               "Gender",
      "pacientes.phone":                "Phone",
      "pacientes.email":                "Email",
      "pacientes.address":              "Address",
      "pacientes.blood_type":           "Blood type",
      "pacientes.allergies":            "Allergies",
      "pacientes.medical_history":      "Medical history",
      "pacientes.last_visit":           "Last visit",
      "pacientes.no_patients":          "No patients registered.",
      "pacientes.search_placeholder":   "Search by name or document...",

      // ═══════════════════════════════════════════════════════
      // MODULES — CLINICAL HISTORY
      // ═══════════════════════════════════════════════════════
      "odo06.breadcrumb_aria": "Breadcrumb",
      "odo06.breadcrumb_professional": "Professional",
      "odo06.title": "My patients",
      "odo06.subtitle": "View the information of patients you have treated.",
      "odo06.stats_aria": "Patient summary",
      "odo06.stat_total": "Total patients",
      "odo06.stat_active": "Active",
      "odo06.stat_followup": "In follow-up",
      "odo06.stat_discharged": "Discharged",
      "odo06.filters_aria": "Patient filters",
      "odo06.search_placeholder": "Search by name, document, or service...",
      "odo06.search_aria": "Search by name, document, or service",
      "odo06.filter_status": "Status",
      "odo06.filter_status_aria": "Filter by status",
      "odo06.all_statuses": "All statuses",
      "odo06.filter_allergies": "Allergies",
      "odo06.filter_allergies_aria": "Filter by allergies",
      "odo06.all": "All",
      "odo06.with_allergies": "With allergies",
      "odo06.without_allergies": "Without allergies",
      "odo06.filter_appointment": "Next appointment",
      "odo06.filter_appointment_aria": "Filter by next appointment",
      "odo06.with_appointment": "With appointment",
      "odo06.without_appointment": "Without appointment",
      "odo06.filter_service": "Service",
      "odo06.filter_service_aria": "Filter by service",
      "odo06.all_services": "All services",
      "odo06.clear_filters": "Clear all filters",
      "odo06.clear": "Clear",
      "odo06.page_size_aria": "Patients per page",
      "odo06.per_page_5": "5 per page",
      "odo06.per_page_10": "10 per page",
      "odo06.per_page_15": "15 per page",
      "odo06.per_page_100": "100 per page",
      "odo06.patient_list": "Patient list",
      "odo06.patient": "Patient",
      "odo06.last_visit": "Last visit",
      "odo06.service": "Service",
      "odo06.next_appointment": "Next appointment",
      "odo06.status": "Status",
      "odo06.actions": "Actions",
      "odo06.table_aria": "Treated patients table",
      "odo06.pagination_aria": "Page navigation",
      "odo06.pagination_showing": "Showing",
      "odo06.pagination_of": "of",
      "odo06.patient_count": "patients",
      "odo06.pagination_pages_aria": "Result pages",
      "odo06.previous_page": "Previous page",
      "odo06.next_page": "Next page",
      "odo06.page_number": "Page {number}",
      "odo06.result_count": "{count} result(s) found",
      "odo06.patient_row_aria": "View profile for {name}",
      "odo06.action_profile": "Profile",
      "odo06.action_history": "History",
      "odo06.action_profile_aria": "View profile for {name}",
      "odo06.action_history_aria": "View clinical history for {name}",
      "odo06.no_results_title": "No patients found",
      "odo06.no_results_text": "Adjust the filters or search and try again.",
      "odo06.load_error_title": "Unable to load patients",
      "odo06.load_error": "An error occurred while loading the list. Try again.",
      "odo06.service_unavailable": "No service recorded",
      "odo06.no_appointment": "No appointment",
      "odo06.date_locale": "en-US",

      // ═══════════════════════════════════════════════════════
      // MODULES — PROFESSIONAL SERVICES (ODO-05)
      // ═══════════════════════════════════════════════════════
      "odo05.breadcrumb_aria": "Breadcrumbs",
      "odo05.section_title": "Services and Resources",
      "odo05.title": "My services",
      "odo05.subtitle": "View procedures assigned to your professional profile.",
      "odo05.stats_aria": "Services summary",
      "odo05.assigned_services": "Assigned services",
      "odo05.available": "Available",
      "odo05.unavailable": "Unavailable",
      "odo05.description_not_registered": "No description has been registered for this procedure.",
      "odo05.panel_aria": "Services catalog",
      "odo05.catalog_title": "Professional catalog",
      "odo05.catalog_desc": "Explore details of the services assigned to you.",
      "odo05.search_placeholder": "Search service or description...",
      "odo05.search_aria": "Search services",
      "odo05.specialty_label": "Specialty",
      "odo05.specialty_aria": "Filter by specialty",
      "odo05.all_specialties": "All",
      "odo05.status_label": "Status",
      "odo05.status_aria": "Filter by status",
      "odo05.all_statuses": "All",
      "odo05.status_available": "Available",
      "odo05.status_unavailable": "Unavailable",
      "odo05.service_price": "Service price",
      "odo05.view_details": "View details",
      "odo05.no_results": "No services match the selected filters.",
      "odo05.no_results_help": "Adjust the search filters and try again.",
      "odo05.no_services": "You have no assigned services. Contact the administrator to manage assignments.",
      "odo05.no_services_title": "You do not have any assigned services yet.",
      "odo05.no_specialty": "No specialty",
      "odo05.not_specified": "Not specified",
      "odo05.minutes": "minutes",
      "odo05.professional_profile": "Professional profile",
      "odo05.modal_title": "Procedure details",
      "odo05.close_details": "Close details",
      "odo05.desc_heading": "Description",
      "odo05.specs_heading": "Specifications",
      "odo05.category_label": "Category",
      "odo05.duration_label": "Estimated duration",
      "odo05.base_price_label": "Base price",
      "odo05.applicable_price_label": "Applicable price",
      "odo05.custom_price_label": "Custom price",
      "odo05.modal_note": "Prices and assignments are managed from the corresponding administrative module. This view is read-only.",

      "historia.title":                 "Clinical History",
      "historia.new_record":            "New entry",
      "historia.diagnosis":             "Diagnosis",
      "historia.treatment":             "Treatment",
      "historia.observations":          "Observations",
      "historia.date":                  "Consultation date",
      "historia.professional":          "Treating professional",
      "historia.attachments":           "Attachments",
      "historia.no_records":            "No clinical records available.",
      "historia.add_attachment":        "Add file",
      "historia.teeth_chart":           "Odontogram",
      "historia.evolution":             "Evolution",
      "historia_admin.title": "Global Clinical History",
      "historia_admin.subtitle": "Read-only access to the system's clinical histories.",
      "historia_admin.read_only": "Read only",
      "historia_admin.read_only_admin": "Read-only view — Administrator",
      "historia_admin.stats": "Clinical history statistics",
      "historia_admin.total": "Total histories",
      "historia_admin.updated_today": "Updated today",
      "historia_admin.alerts": "With medical alerts",
      "historia_admin.active_dentists": "Active dentists",
      "historia_admin.filters": "Search filters",
      "historia_admin.search_placeholder": "Search by name, ID, or email...",
      "historia_admin.search": "Search patients",
      "historia_admin.filter_dentist": "Filter by dentist",
      "historia_admin.all_dentists": "All dentists",
      "historia_admin.filter_alerts": "Filter by medical alert",
      "historia_admin.all_alerts": "All alerts",
      "historia_admin.with_alert": "With medical alert",
      "historia_admin.without_alert": "No alert",
      "historia_admin.filter_status": "Filter by status",
      "historia_admin.all_statuses": "All statuses",
      "historia_admin.page_size": "Patients per page",
      "historia_admin.page_size_5": "5 per page",
      "historia_admin.page_size_10": "10 per page",
      "historia_admin.page_size_20": "20 per page",
      "historia_admin.page_size_100": "100 per page",
      "historia_admin.list": "Clinical history list",
      "historia_admin.registered_patients": "Registered patients",
      "historia_admin.table": "Clinical histories",
      "historia_admin.patient": "Patient",
      "historia_admin.patient_singular": "patient",
      "historia_admin.patients": "patients",
      "historia_admin.document": "ID",
      "historia_admin.dentist": "Treating dentist",
      "historia_admin.last_visit": "Last visit",
      "historia_admin.alert": "Alert",
      "historia_admin.empty_title": "No patients found",
      "historia_admin.empty_description": "Adjust the filters or search and try again.",
      "historia_admin.showing": "Showing",
      "historia_admin.of": "of",
      "historia_admin.page": "Page",
      "historia_admin.page_navigation": "Result pages",
      "historia_admin.previous": "Previous page",
      "historia_admin.next": "Next page",
      "historia_admin.history_sections": "History sections",
      "historia_admin.consultations": "Consultations",
      "historia_admin.treatments": "Treatments",
      "historia_admin.documents": "Documents",
      "historia_admin.medical_alert": "Medical alert",
      "historia_admin.view": "View history",
      "historia_admin.view_history": "View {name}'s clinical history",
      "historia_admin.birth_date": "Date of birth",
      "historia_admin.phone": "Phone",
      "historia_admin.yes": "Yes",
      "historia_admin.no": "No",
      "historia_admin.sessions": "sessions",
      "historia_admin.no_consultation_notes": "No clinical notes.",
      "historia_admin.no_consultations": "No consultations recorded.",
      "historia_admin.no_treatments": "No treatments recorded.",
      "historia_admin.no_documents": "No documents recorded.",
      "historia_admin.results_count": "Showing {shown} of {total} {patients}",
      "historia_admin.error_title": "Error",
      "historia_admin.load_error": "Could not load patient histories.",

      // ═══════════════════════════════════════════════════════
      // MODULES — PROFESSIONALS
      // ═══════════════════════════════════════════════════════
      "profesionales.title":            "Professional Management",
      "profesionales.new":              "New professional",
      "profesionales.edit":             "Edit professional",
      "profesionales.specialty":        "Specialty",
      "profesionales.license":          "Professional license",
      "profesionales.schedule":         "Schedule",
      "profesionales.availability":     "Availability",
      "profesionales.status":           "Status",
      "profesionales.no_professionals": "No professionals registered.",
      "asignados.title": "Services assigned to professionals",
      "asignados.subtitle": "Assign services based on registered specialties and configure prices in COP.",
      "asignados.back": "Back to professionals",
      "asignados.select_professional": "Select a professional",
      "asignados.professional": "Professional",
      "asignados.choose_professional": "Choose a professional",
      "asignados.consult": "View",
      "asignados.assign_title": "Assign or update a service",
      "asignados.compatible_services_hint": "Only active services matching their specialties are shown.",
      "asignados.registered_specialties": "Registered specialties:",
      "asignados.none": "None",
      "asignados.professional_inactive": "This professional is inactive. New assignments are not allowed.",
      "asignados.no_compatible_services": "There are no compatible active services. Check the professional's specialties.",
      "asignados.service": "Service",
      "asignados.choose_service": "Choose a service to assign or edit",
      "asignados.select_service_hint": "Select a service to check its status.",
      "asignados.custom_price": "Custom price (COP, optional)",
      "asignados.base_price_placeholder": "Blank = base price",
      "asignados.price_help": "Leave blank to use the base price. If the service is already assigned, its current price will be loaded.",
      "asignados.choose_service_button": "Select a service",
      "asignados.assigned_services": "Professional services",
      "asignados.no_assignments": "No assigned services",
      "asignados.no_assignments_hint": "Select a compatible service to create an assignment.",
      "asignados.table_aria": "Services assigned to the professional",
      "asignados.base_price": "Base price",
      "asignados.custom_price_short": "Custom price",
      "asignados.applied_price": "Applied price",
      "asignados.active": "Active",
      "asignados.inactive": "Inactive",
      "asignados.deactivate": "Deactivate",
      "asignados.reactivate": "Reactivate",
      "asignados.new_assignment": "New assignment: this service is not yet linked to the professional.",
      "asignados.active_assignment": "Active assignment: saving will update its price.",
      "asignados.inactive_assignment": "Inactive assignment: saving will update its price and reactivate it.",
      "asignados.assign_service": "Assign service",
      "asignados.update_assignment": "Update assignment",
      "asignados.update_reactivate": "Update and reactivate",
      "asignados.unassigned": "Not assigned",
      "asignados.already_active": "Already assigned (active)",
      "asignados.already_inactive": "Already assigned (inactive)",
      "asignados.error_invalid_pair": "The professional and service must both exist and be active.",
      "asignados.error_specialty": "The professional does not have the specialty required for this service.",
      "asignados.error_invalid_price": "The custom price must be greater than zero and have at most two decimal places; leave it blank to use the base price.",
      "asignados.error_save": "The assignment could not be saved. Check the database constraints.",
      "asignados.error_assignment_missing": "The assignment does not exist.",
      "asignados.error_reactivate": "Cannot reactivate: the professional, service, and specialty must be compatible and active.",
      "asignados.error_update_status": "The status could not be updated.",
      "asignados.success_saved": "Assignment saved successfully.",
      "asignados.success_reactivated": "Assignment reactivated.",
      "asignados.success_deactivated": "Assignment deactivated; its record has been retained.",

      // ═══════════════════════════════════════════════════════
      // MODULES — BILLING
      // ═══════════════════════════════════════════════════════
      "facturacion.title":              "Billing & Payments",
      "facturacion.invoice":            "Invoice",
      "facturacion.new_invoice":        "New invoice",
      "facturacion.payment":            "Payment",
      "facturacion.register_payment":   "Register payment",
      "facturacion.amount":             "Amount",
      "facturacion.date":               "Date",
      "facturacion.method":             "Payment method",
      "facturacion.status":             "Status",
      "facturacion.total":              "Total",
      "facturacion.subtotal":           "Subtotal",
      "facturacion.discount":           "Discount",
      "facturacion.tax":                "Tax",
      "facturacion.no_invoices":        "No invoices available.",
      "facturacion.method_cash":        "Cash",
      "facturacion.method_card":        "Card",
      "facturacion.method_transfer":    "Bank transfer",
      "facturacion.method_nequi":       "Nequi",
      "facturacion.method_daviplata":   "DaviPlata",

      // ═══════════════════════════════════════════════════════
      // MODULES — PQR
      // ═══════════════════════════════════════════════════════
      "pqr.title":                      "PQR Management",
      "pqr.admin_subtitle":             "Manage, review, and respond to patient requests.",
      "pqr.notifications_aria":         "New PQR requests",
      "pqr.no_notifications":           "There are no new PQR requests",
      "pqr.ticket":                     "Ticket",
      "pqr.priority":                   "Priority",
      "pqr.all_types":                  "All types",
      "pqr.all_statuses":               "All statuses",
      "pqr.status_received":            "Received",
      "pqr.status_in_progress":         "In progress",
      "pqr.status_pending_info":         "Pending information",
      "pqr.status_resolved":             "Resolved",
      "pqr.status_closed":               "Closed",
      "pqr.table_aria":                 "PQR requests",
      "pqr.search_aria":                "Search PQR requests",
      "pqr.detail_aria":                "PQR request details",
      "pqr.view_details":               "View details",
      "pqr.view_details_aria":          "View request details for {id}",
      "pqr.close_detail":               "Close details",
      "pqr.select_case":                "Select a case",
      "pqr.select_case_help":            "Select a request to review its details and manage the response.",
      "pqr.unanswered":                 "Unanswered",
      "pqr.in_management":              "In progress",
      "pqr.overdue":                    "Overdue",
      "pqr.general_information":        "General information",
      "pqr.open_time":                  "Time open:",
      "pqr.patient":                    "Patient",
      "pqr.attached_evidence":           "Attached evidence",
      "pqr.view_attachment":             "View attachment",
      "pqr.response_thread":             "Response thread",
      "pqr.no_response":                "This request has not been answered yet.",
      "pqr.reply":                      "Reply",
      "pqr.reply_placeholder":          "Write your response to the patient...",
      "pqr.send_response":              "Send response",
      "pqr.change_status":              "Change status",
      "pqr.received":                   "Received",
      "pqr.resolved":                   "Resolved",
      "pqr.rejected":                   "Rejected",
      "pqr.new":                        "New request",
      "pqr.type":                       "Type",
      "pqr.type_petition":              "Petition",
      "pqr.type_complaint":             "Complaint",
      "pqr.type_claim":                 "Claim",
      "pqr.type_suggestion":            "Suggestion",
      "pqr.subject":                    "Subject",
      "pqr.description":                "Description",
      "pqr.status":                     "Status",
      "pqr.date":                       "Date",
      "pqr.response":                   "Response",
      "pqr.no_requests":                "No requests registered.",
      "pqr.response_time":              "Response time: ≤ 48 business hours",

      // ═══════════════════════════════════════════════════════
      // MODULES — SERVICES & RESOURCES
      // ═══════════════════════════════════════════════════════
      "servicios.title":                "Services & Resources",
      "servicios.new":                  "New service",
      "servicios.edit":                 "Edit service",
      "servicios.name":                 "Name",
      "servicios.description":          "Description",
      "servicios.price":                "Price",
      "servicios.duration":             "Duration",
      "servicios.category":             "Category",
      "servicios.active":               "Active",
      "servicios.no_services":          "No services registered.",
      "servicios.catalog_title":        "Service catalog",
      "servicios.catalog_subtitle":     "Manage prices, categories, and availability.",
      "servicios.summary":              "Service summary",
      "servicios.total_sub":            "Services registered",
      "servicios.active_plural":        "Active services",
      "servicios.active_sub":           "Available for assignment",
      "servicios.inactive_plural":      "Inactive services",
      "servicios.inactive_sub":         "Unavailable for assignment",
      "servicios.filters":              "Service filters",
      "servicios.search_placeholder":   "Search by name or description",
      "servicios.search_aria":          "Search services",
      "servicios.filter_category":      "Filter by category",
      "servicios.all_categories":       "All categories",
      "servicios.category_prevencion":  "Prevention",
      "servicios.category_estetica":    "Aesthetics",
      "servicios.category_cirugia":     "Surgery",
      "servicios.category_ortodoncia":  "Orthodontics",
      "servicios.category_endodoncia":  "Endodontics",
      "servicios.category_periodoncia": "Periodontics",
      "servicios.category_general":     "General",
      "servicios.select_category":      "Select a category",
      "servicios.category_help":        "If the service has an older category, select a valid category before saving.",
      "servicios.filter_status":        "Filter by status",
      "servicios.all_statuses":         "All statuses",
      "servicios.table_title":          "Service list",
      "servicios.column_name":          "Service",
      "servicios.pagination":           "Result pages",
      "servicios.pagination_showing":   "Showing",
      "servicios.pagination_of":        "of",
      "servicios.pagination_records":   "services",
      "servicios.close_drawer":         "Close panel",
      "servicios.specialty":            "Clinical specialty",
      "servicios.select_specialty":     "Select a specialty",
      "servicios.specialty_required_hint": "A specialty is required to save the service.",
      "servicios.no_specialties":       "No specialties are configured; services cannot be saved.",
      "servicios.price_cop":            "Base price (COP)",
      "servicios.duration_minutes":     "Duration (minutes)",
      "servicios.current_status":       "Current status",
      "servicios.status_change_hint":   "Change availability using the table actions.",
      "servicios.save_changes":         "Save changes",
      "servicios.create":               "Create service",
      "servicios.empty_title":          "No services found",
      "servicios.empty_filtered":       "Adjust the filters or search and try again.",
      "servicios.specialty_not_configured": "No specialty configured",
      "servicios.minutes_short":        "min",
      "servicios.status_aria":          "Status: {status}",
      "servicios.service_aria":         "Service {name}",
      "servicios.edit_action":          "Edit service {name}",
      "servicios.activate_action":      "Activate service {name}",
      "servicios.deactivate_action":    "Deactivate service {name}",
      "servicios.activate":             "Activate",
      "servicios.deactivate":           "Deactivate",
      "servicios.page_number":          "Page {page}",
      "servicios.previous_page":        "Previous page",
      "servicios.next_page":            "Next page",
      "servicios.required_fields_error": "Complete the name, category, and specialty.",
      "servicios.price_error":          "Price must be greater than zero.",
      "servicios.duration_error":       "Duration must be between 5 and 480 minutes.",
      "servicios.create_error":         "Could not create the service.",
      "servicios.update_error":         "Could not update the service.",
      "servicios.status_error":         "Could not change the service status.",
      "servicios.created":              "Service “{name}” created successfully.",
      "servicios.updated":              "Service “{name}” updated successfully.",
      "servicios.activated":            "Service “{name}” activated.",
      "servicios.deactivated":          "Service “{name}” deactivated.",
      "servicios.toast_success":        "Success",
      "servicios.toast_error":          "Error",
      "servicios.toast_warning":        "Validation",
      "servicios.toast_info":           "Information",

      // ═══════════════════════════════════════════════════════
      // MODULES — CONFIGURATION
      // ═══════════════════════════════════════════════════════
      "config.title":                   "General Settings",
      "config.clinic_name":             "Clinic name",
      "config.address":                 "Address",
      "config.phone":                   "Phone",
      "config.email":                   "Email",
      "config.schedule":                "Office hours",
      "config.save":                    "Save settings",
      "config.saved":                   "Settings saved successfully.",
      "config.subtitle": "Manage clinic details, appointments, and notifications.",
      "config.appointments": "Appointment settings",
      "config.duration": "Default duration (minutes)",
      "config.opening_time": "Opening time",
      "config.closing_time": "Closing time",
      "config.center_data": "Clinic details",
      "config.center_hint": "Information shown on documents and communications.",
      "config.nit": "Tax ID / Identification number",
      "config.nit_hint": "Format: 000.000.000-0",
      "config.email_reminders": "Email reminders",
      "config.email_reminders_hint": "Send email reminders before appointments.",
      "config.sms_reminders": "SMS reminders",
      "config.sms_reminders_hint": "Send text reminders to the registered phone.",
      "config.system_notifications": "System notifications",
      "config.system_notifications_hint": "Show internal activity alerts.",
      "config.save_title": "Save settings",
      "config.save_hint": "Changes will be applied to the system when you save the form.",
      "config.hours_error": "Opening time must be earlier than closing time.",
      "config.save_error": "Could not save the settings.",
      "config.toast_success": "Settings",
      "config.toast_error": "Error",
      "config.toast_warning": "Validation",

      "nav.item_homepage": "Home",
      "nav.item_panel_analitica": "Analytics Dashboard",
      "nav.item_gestion_usuarios": "User Management",
      "nav.item_gestion_roles": "Role Management",
      "nav.item_bitacora_sistema": "System Log",
      "nav.item_cambiar_contrasena": "Change Password",
      "nav.item_gestion_pacientes": "Patient Management",
      "nav.item_historial_clinico": "Clinical History",
      "nav.item_gestion_profesionales": "Professional Management",
      "nav.item_reportes_clinicos": "Clinical Reports",
      "nav.item_dashboard_general": "General Dashboard",
      "nav.item_agenda_general": "General Schedule",
      "nav.item_gestion_integral_citas": "Appointment Management",
      "nav.item_gestion_facturacion": "Billing Management",
      "nav.item_catalogo_servicios": "Service Catalog",
      "nav.item_configuracion_general": "General Settings",
      "nav.item_peticiones_quejas_reclamos": "PQR management",
      "nav.item_nueva_solicitud": "New Request",
      "nav.item_como_programar_cita": "How to schedule an appointment",
      "nav.item_guias_tutoriales": "Guides and Tutorials",
      "nav.item_soporte_tickets": "Support and Tickets",
      "nav.group_inicio": "Home",
      "nav.group_reportes_analitica": "Reports & Analytics",
      "nav.group_acceso_seguridad": "Access & Security",
      "nav.group_gestion_pacientes": "Patient Management",
      "nav.group_historia_clinica": "Clinical History",
      "nav.group_gestion_profesionales": "Professional Management",
      "nav.group_gestion_citas": "Appointment Management",
      "nav.group_facturacion_pagos": "Billing & Payments",
      "nav.group_servicios_recursos": "Services & Resources",
      "nav.group_gestion_pqr": "PQR Management",
      "nav.group_centro_ayuda": "Help Center",
      "sidebar_logout_text": "Sign out",
      "sidebar_logout_aria": "Sign out and return to login",
      "sidebar_main_nav": "Main navigation",
      "sidebar_change_language": "Change language",
      "sidebar_lang_es": "Spanish",
      "sidebar_lang_en": "English",

      "adm01_bc_admin": "Administration",
      "adm01_bc_aria": "Breadcrumb",
      "adm01_bc_active": "Appointments Dashboard",
      "adm01_title": "Appointments Dashboard",
      "adm01_subtitle": "Operational summary of the dental center",
      "adm01_export_pdf_aria": "Export dashboard as PDF",
      "adm01_export_pdf_btn": "Export PDF",
      "adm01_stat_pacientes": "Active patients",
      "adm01_stat_pacientes_sub": "Total registered",
      "adm01_stat_citas_hoy": "Appointments today",
      "adm01_stat_citas_hoy_sub": "Not cancelled",
      "adm01_stat_profesionales": "Active professionals",
      "adm01_stat_profesionales_sub": "On duty",
      "adm01_stat_valor_atendido": "Services value",
      "adm01_stat_valor_atendido_sub": "Estimated from services",
      "adm01_occupancy_aria": "Monthly schedule occupancy",
      "adm01_occupancy_title": "Schedule occupancy",
      "adm01_occupancy_sub": "Bookings compared with monthly capacity",
      "adm01_distribution_title": "Status distribution — current month",
      "adm01_distribution_aria": "Appointment statuses this month",
      "adm01_dist_attended": "Attended",
      "adm01_dist_attended_sub": "vs same days last week",
      "adm01_dist_confirmed": "Confirmed",
      "adm01_dist_scheduled": "Scheduled",
      "adm01_dist_cancelled": "Cancelled",
      "adm01_dist_other": "Other statuses",
      "adm01_dist_of_total": "of total",
      "adm01_top_prof_title": "Top 3 professionals this month",
      "adm01_top_prof_caption": "The three professionals with the most attended appointments this month",
      "adm01_top_prof_table_aria": "Professional appointment ranking",
      "adm01_th_professional": "Professional",
      "adm01_th_specialty": "Specialty",
      "adm01_th_total_appointments": "Total appointments",
      "adm01_top_prof_empty": "There is not enough data to generate the ranking.",
      "adm01_quick_aria": "Quick actions",
      "adm01_quick_title": "Quick actions",
      "adm01_quick_open_agenda": "Open schedule",
      "adm01_quick_open_agenda_aria": "Go to the general schedule",
      "adm01_quick_manage": "Appointment management",
      "adm01_quick_manage_aria": "Go to appointment management",
      "adm01_notes_title": "System notes",
      "adm01_notes_text": "Use the general schedule or appointment management to modify an appointment or check availability.",
      "adm01_mobile_nav_aria": "Mobile navigation",
      "adm01_mobile_dashboard": "Dashboard",
      "adm01_mobile_agenda": "Schedule",
      "adm01_mobile_citas": "Appointments",
      "adm08_bc_admin": "Administration",
      "adm08_bc_active": "General Schedule",
      "adm08_bc_aria": "Breadcrumb",
      "adm08_title": "General Schedule",
      "adm08_subtitle": "Appointment and schedule management",
      "adm08_controls_aria": "Navigation and filter controls",
      "adm08_weeknav_aria": "Weekly navigation",
      "adm08_prev_week_aria": "Previous week",
      "adm08_next_week_aria": "Next week",
      "adm08_today_aria": "Go to current week",
      "adm08_today": "Today",
      "adm08_filter_prof_label": "Professional",
      "adm08_filter_prof_aria": "Filter by professional",
      "adm08_filter_prof_all": "All professionals",
      "adm08_filter_office_label": "Office",
      "adm08_filter_office_aria": "Filter by office",
      "adm08_filter_office_all": "All offices",
      "adm08_hours_aria": "Clinic hours",
      "adm08_hours_title": "Clinic hours",
      "adm08_hours_min": "min per appointment",
      "adm08_print_aria": "Print weekly schedule",
      "adm08_print_btn": "Print Schedule",
      "adm08_tabla_integral_aria": "Open full appointments table",
      "adm08_tabla_integral_btn": "Full table",
      "adm08_calendar_title": "Weekly appointment calendar",
      "adm08_day_closed": "CLOSED",
      "adm08_no_appointments": "No appointments",
      "adm08_legend_title": "Appointment statuses",
      "adm08_legend_scheduled": "Scheduled",
      "adm08_legend_confirmed": "Confirmed",
      "adm08_legend_attended": "Attended",
      "adm08_legend_cancelled": "Cancelled",
      "adm08_legend_available": "Available",
      "adm08_modal_close_aria": "Close window",
      "adm08_modal_appt_title": "Appointment details",
      "adm08_modal_newappt_title": "Schedule New Appointment",
      "adm08_form_hora_inicio": "Start time",
      "adm08_form_hora_fin": "End time",
      "adm08_form_duracion_hint": "Calculated from the configured duration.",
      "adm08_form_select_paciente": "Select patient...",
      "adm08_form_select_profesional": "Select professional",
      "adm08_form_select_consultorio": "Select office",
      "adm08_form_servicio_label": "Service (reason for visit)",
      "adm08_form_select_servicio": "Select service",
      "adm08_form_observaciones": "Notes",
      "adm08_form_notas_ph": "Additional details...",
      "adm08_form_guardar_cita": "Save Appointment",
      "adm08_mobile_nav_aria": "Mobile navigation",
      "adm08_mobile_dashboard": "Dashboard",
      "adm08_mobile_agenda": "Schedule",
      "adm08_mobile_citas": "Appointments"
      ,"footer_copyright": "All rights reserved."
      ,"footer_project": "ADSO Training Project — SENA"
      ,"reportesadmin_subtitle": "Administrative indicators and clinic activity"
      ,"reportesadmin_updated": "Updated"
      ,"reportesadmin_minutes": "min ago"
      ,"reportesadmin_btn_pdf": "Download PDF"
      ,"reportesadmin_btn_csv": "Export CSV"
      ,"reportesadmin_stat_income": "Total Income"
      ,"reportesadmin_stat_patients": "New Patients"
      ,"reportesadmin_stat_completed": "Completed Appointments"
      ,"reportesadmin_stat_retention": "Retention Rate"
      ,"reportesadmin_stat_no_show": "No-show Rate"
      ,"reportesadmin_stat_period": "Appointments this period"
      ,"reportesadmin_trend": "vs previous month"
      ,"reportesadmin_no_comparison": "No comparison baseline"
      ,"reportesadmin_chart_growth": "Clinic Growth"
      ,"reportesadmin_this_year": "This year"
      ,"reportesadmin_last_year": "Last year"
      ,"reportesadmin_distribution": "Service Distribution"
      ,"reportesadmin_in_period": "appointments this period"
      ,"reportesadmin_total": "Total"
      ,"reportesadmin_highest": "Top"
      ,"reportesadmin_professional_income": "Income by Professional"
      ,"reportesadmin_recent": "Recent Reports"
      ,"reportesadmin_new": "+ New Report"
      ,"reportesadmin_view_all": "View all →"
      ,"reportesadmin_filters_aria": "Filter reports by category"
      ,"reportesadmin_cat_all": "All"
      ,"reportesadmin_cat_financial": "Financial"
      ,"reportesadmin_cat_patients": "Patients"
      ,"reportesadmin_cat_operational": "Operational"
      ,"reportesadmin_cat_logistics": "Logistics"
      ,"reportesadmin_th_name": "Report Name"
      ,"reportesadmin_th_date": "Date Generated"
      ,"reportesadmin_th_category": "Category"
      ,"reportesadmin_th_status": "Status"
      ,"reportesadmin_th_action": "Action"
      ,"reportesadmin_download_csv": "CSV"
      ,"reportesadmin_download_pdf": "PDF"
      ,"reportesadmin_download_csv_title": "Download CSV"
      ,"reportesadmin_download_pdf_title": "Download PDF"
      ,"reportesadmin_empty_title": "No reports found"
      ,"reportesadmin_empty_desc": "Try another category or generate a new report."
      ,"reportesadmin_empty_action": "+ Generate Report"
      ,"reportesadmin_modal_new_title": "New Report"
      ,"reportesadmin_modal_new_desc": "Configure the report to save an aggregate copy in the history."
      ,"reportesadmin_name": "Report name"
      ,"reportesadmin_name_ph": "Monthly income report"
      ,"reportesadmin_category": "Category"
      ,"reportesadmin_period": "Period"
      ,"reportesadmin_notes": "Notes or objectives"
      ,"reportesadmin_notes_ph": "E.g. Compare with the previous quarter"
      ,"reportesadmin_cancel": "Cancel"
      ,"reportesadmin_create": "Create Report"
      ,"reportesadmin_modal_all_title": "All reports"
      ,"reportesadmin_modal_all_desc": "Access generated reports and download the format you need."
      ,"reportesadmin_close": "Close"
      ,"reportesadmin_pdf_title": "Download PDF"
      ,"reportesadmin_pdf_desc": "Select the content to include in the current report PDF."
      ,"reportesadmin_pdf_metrics": "Include indicators and metrics table"
      ,"reportesadmin_pdf_summary": "Include executive summary"
      ,"reportesadmin_pdf_download": "Download PDF"
      ,"reportesadmin_csv_title": "Export CSV"
      ,"reportesadmin_csv_desc": "Choose the data and download aggregate records in CSV format."
      ,"reportesadmin_csv_headers": "Include column headers"
      ,"reportesadmin_csv_all": "Export all reports"
      ,"reportesadmin_csv_export": "Export CSV"
      ,"reportesadmin_ready": "Ready"
      ,"reportesadmin_toast_success": "The report was saved to the history."
      ,"reportesadmin_toast_error": "The report could not be generated."
      ,"reportesadmin_toast_success_title": "Report generated"
      ,"reportesadmin_toast_error_title": "Report generation failed"
      ,"reportesadmin_breadcrumb_admin": "Administration"
      ,"reportesadmin_breadcrumb_current": "Reports & Analytics"
      ,"reportesadmin_stats_aria": "Key performance indicators"
      ,"reportesadmin_growth_aria": "Monthly clinic growth"
      ,"reportesadmin_distribution_aria": "Service distribution"
      ,"reportesadmin_status_title": "Appointment Status"
      ,"reportesadmin_appointments": "appointments"
      ,"reportesadmin_procedures_title": "Most Requested Procedures"
      ,"reportesadmin_view_all_procedures": "View all →"
      ,"reportesadmin_th_procedure": "Procedure"
      ,"reportesadmin_th_quantity": "Quantity"
      ,"reportesadmin_th_income": "Income"
      ,"reportesadmin_th_average_price": "Avg. Price"
      ,"reportesadmin_th_trend": "Trend"
      ,"reportesadmin_all_procedures_title": "All Procedures"
      ,"reportesadmin_all_procedures_desc": "View the complete list of the clinic's most requested procedures."
      ,"reportesadmin_state_completed": "Completed"
      ,"reportesadmin_state_scheduled": "Scheduled"
      ,"reportesadmin_state_in_progress": "In progress"
      ,"reportesadmin_state_cancelled": "Cancelled"
      ,"reportesadmin_state_no_show": "No-shows"
      ,"reportesadmin_state_other": "Other"
      ,"reportesadmin_month_jan": "Jan"
      ,"reportesadmin_month_feb": "Feb"
      ,"reportesadmin_month_mar": "Mar"
      ,"reportesadmin_month_apr": "Apr"
      ,"reportesadmin_month_may": "May"
      ,"reportesadmin_month_jun": "Jun"
      ,"reportesadmin_month_jul": "Jul"
      ,"reportesadmin_month_aug": "Aug"
      ,"reportesadmin_month_sep": "Sep"
      ,"reportesadmin_month_oct": "Oct"
      ,"reportesadmin_month_nov": "Nov"
      ,"reportesadmin_month_dec": "Dec"
      ,"reportesprof.breadcrumb_professional": "Professional"
      ,"reportesprof.breadcrumb_current": "Reports & Analytics"
      ,"reportesprof.title": "Reports & Analytics"
      ,"reportesprof.subtitle": "Clinical activity for the current month and recent appointments."
      ,"reportesprof.open_schedule": "Go to my schedule"
      ,"reportesprof.download_pdf": "Print / save as PDF"
      ,"reportesprof.stats_aria": "Key performance indicators"
      ,"reportesprof.appointments_month": "Appointments this month"
      ,"reportesprof.vs_previous_month": "vs. previous month"
      ,"reportesprof.no_previous_period": "No comparable previous period"
      ,"reportesprof.paid_income_month": "Paid income this month"
      ,"reportesprof.patients_month": "Patients this month"
      ,"reportesprof.unique_patients_month": "Unique patients with appointments"
      ,"reportesprof.cancellations_month": "Cancellations this month"
      ,"reportesprof.cancellation_rate": "of this month's appointments"
      ,"reportesprof.treatments_title": "Treatments completed this month"
      ,"reportesprof.no_treatments_title": "No treatments recorded"
      ,"reportesprof.no_treatments_desc": "There are no completed appointments this month."
      ,"reportesprof.frequent_patients_title": "Patients with most appointments (6 months)"
      ,"reportesprof.no_patients_title": "No recent activity"
      ,"reportesprof.no_patients_desc": "There are no patient appointments in the last six months."
      ,"reportesprof.appointments": "appointments"
      ,"reportesprof.upcoming_title": "Upcoming days"
      ,"reportesprof.upcoming_subtitle": "Appointments for the next three days"
      ,"reportesprof.day_today": "Today"
      ,"reportesprof.day_tomorrow": "Tomorrow"
      ,"reportesprof.day_next": "Next"
      ,"reportesprof.status_completed": "Completed"
      ,"reportesprof.status_scheduled": "Scheduled"
      ,"reportesprof.status_in_progress": "In progress"
      ,"reportesprof.status_cancelled": "Cancelled"
      ,"reportesprof.status_no_show": "No-show"
      ,"reportesprof.status_other": "Other"
      ,"reportesprof.recent_title": "Recent appointments"
      ,"reportesprof.history_link": "Go to clinical history"
      ,"reportesprof.patient": "Patient"
      ,"reportesprof.service": "Service"
      ,"reportesprof.time": "Time"
      ,"reportesprof.status": "Status"
      ,"reportesprof.action": "Action"
      ,"reportesprof.no_recent_title": "No recent appointments"
      ,"reportesprof.no_recent_desc": "Your recorded appointments will appear here."
      ,"reportesprof.view_appointment": "View appointment details"
      ,"reportesprof.appointment_detail": "Appointment details"
      ,"reportesprof.treatment_detail": "Treatment details"
      ,"reportesprof.share_of_treatments": "% of completed treatments"
      ,"status.new": "New"
      ,"status.requested": "Requested"
      ,"status.rejected": "Rejected"
      ,"citas.stat_rejected": "Rejected requests"
      ,"citas.tab_all": "All appointments"
      ,"citas.tab_pending": "Pending"
      ,"citas.tab_completed": "Completed"
      ,"citas.tab_cancelled": "Cancelled"
      ,"citas.tab_rejected": "Rejected"
      ,"citas.loading_progress": "Loading..."
      ,"citas.form_date": "Date"
      ,"citas.form_service": "Service"
      ,"citas.form_professional": "Professional"
      ,"citas.form_schedule": "Recommended available schedule"
      ,"citas.form_notes": "Additional notes"
      ,"citas.select_date_service_first": "Select date and service first"
      ,"citas.select_professional_first": "Select a professional first"
      ,"citas.schedule_help": "Choose a date, service and professional to check available schedules."
      ,"citas.btn_schedule": "Schedule appointment"
      ,"citas.cancel_reason_label": "Cancellation reason *"
      ,"citas.cancel_reason_help": "Briefly describe the reason (minimum 5 characters)."
      ,"citas.list_aria": "Appointments list"
      ,"citas.search_aria": "Search appointments by doctor, service or date"
      ,"notifications.only_unread": "Unread only"
      ,"notifications.only_unread_aria": "Show only unread notifications"
      ,"notifications.show_all": "View all"
      ,"notifications.filter_rejected": "Rejected requests"
      ,"notifications.modal_close_aria": "Close details"
      ,"notifications.modal_detail_title": "Notification Detail"
      ,"notifications.meta_date": "Date / Time:"
      ,"notifications.meta_channel": "Channel:"
      ,"notifications.channel_inapp": "Internal (In-App)"
      ,"notifications.go_to_section": "Go to section"
      ,"pac_complete.breadcrumb_aria": "Breadcrumb"
      ,"pac_complete.breadcrumb_paciente": "Patient"
      ,"pac_complete.breadcrumb_current": "Complete profile"
      ,"pac_complete.title": "Complete your personal data"
      ,"pac_complete.subtitle": "We need your real document and date of birth to complete your profile."
      ,"pac_complete.label_nombre": "Name"
      ,"pac_complete.label_correo": "Email"
      ,"pac_complete.label_tipo_documento": "Document type"
      ,"pac_complete.option_select": "Select a type"
      ,"pac_complete.option_cc": "National ID card"
      ,"pac_complete.option_ti": "Identity card"
      ,"pac_complete.option_ce": "Foreigner ID card"
      ,"pac_complete.option_pas": "Passport"
      ,"pac_complete.label_documento": "Document number"
      ,"pac_complete.label_fecha_nacimiento": "Date of birth"
      ,"pac_complete.btn_save": "Save profile"
      ,"pac_pqr.page_title": "New PQR"
      ,"pac_pqr.page_subtitle": "PQR Module - SmileTrack"
      ,"pac_pqr.label_email": "Email address"
      ,"pac_pqr.ph_email": "email@example.com"
      ,"pac_pqr.label_phone": "Contact phone"
      ,"pac_pqr.ph_phone": "+57 300 000 0000"
      ,"pac_pqr.label_service_date": "Service date"
      ,"pac_pqr.label_dentist": "Treating dentist"
      ,"pac_pqr.ph_dentist": "Professional name"
      ,"pac_pqr.label_subject": "Subject *"
      ,"pac_pqr.ph_subject": "E.g.: Delay in appointment care"
      ,"pac_pqr.label_description": "Detailed description *"
      ,"pac_pqr.ph_description": "Describe your request, complaint or claim in detail. Include relevant dates, names and circumstances."
      ,"pac_pqr.label_attachment": "Attach supporting file (optional)"
      ,"pac_pqr.upload_hint": "Click to attach a file (PDF, JPG, PNG — max. 5 MB)"
      ,"pac_pqr.submit": "Submit request"
      ,"pac_pqr.terms": "By submitting, you accept our personal data processing policy."
      ,"pac_pqr.response_times": "Response times"
      ,"pac_pqr.time_petition": "15 business days"
      ,"pac_pqr.time_complaint": "10 business days"
      ,"pac_pqr.time_claim": "5 business days"
      ,"pac_pqr.contact_channels": "Contact channels"
      ,"pac_pqr.channel_phone_label": "PQR line"
      ,"pac_pqr.channel_phone_hours": "Mon - Fri: 7 am - 6 pm"
      ,"pac_pqr.channel_email_label": "Official email"
      ,"pac_pqr.channel_email_hours": "Response within 24 business hours"
      ,"pac_pqr.how_it_works": "How does it work?"
      ,"pac_pqr.step1": "Fill out the form with your information and case description."
      ,"pac_pqr.step2": "You receive a case number in your email."
      ,"pac_pqr.step3": "Our team analyzes and manages your request."
      ,"pac_pqr.step4": "We notify you of the response according to the legal times."
      ,"pac_pqr.recent_title": "Recent cases"
      ,"pac_pqr.view_all": "View all"
      ,"pac_pqr.detail_title": "PQR Detail"
      ,"pac_pqr.detail_subject": "Subject"
      ,"pac_pqr.detail_description": "Description"
      ,"pac_pqr.detail_response": "SmileTrack Response"
      ,"pac_pqr.empty_recent": "You have no filed requests yet."
      ,"pac_pqr.st_received": "Received"
      ,"pac_pqr.st_in_progress": "In progress"
      ,"pac_pqr.st_resolved": "Resolved"
      ,"pac_pqr.st_closed": "Closed"
      ,"pac_pqr.st_rejected": "Rejected"
      ,"pac_pqr.no_status": "No status"
      ,"pac_pqr.has_response": "Has response"
      ,"pac_pqr.pending_response": "Pending response"
      ,"pac_pqr.filed_on": "Filed on"
      ,"pac_pqr.no_subject": "No subject"
      ,"pac_pqr.no_description": "No description."
      ,"pac_pqr.answered_on": "Answered on"
      ,"pac_pqr.response_registered": "Response recorded"
      ,"pac_pqr.no_response_yet": "This request has no response recorded yet."
      ,"pac_pqr.processing": "Processing..."
      ,"pac_pqr.success_filed": "Request filed successfully. Ticket: "
      ,"pac_pqr.error_filed": "The request could not be filed"
      ,"pac_pqr.error_connection_filed": "Connection error while filing the request"
      ,"pac_pqr.file_too_large": "The file exceeds the maximum allowed size (5 MB)"
      ,"pac_pqr.remove_file": "Remove file"
      ,"pac_miscitas.scheduled_title": "Appointment scheduled!"
      ,"pac_miscitas.progress_of": "of"
      ,"pac_miscitas.progress_completed": "appointments completed"
      ,"pac_profile.hide_password": "Hide password"
      ,"pac_miscitas.error_close_aria": "Close error message"
      ,"pac_miscitas.day_sun": "Sun"
      ,"pac_miscitas.day_mon": "Mon"
      ,"pac_miscitas.day_tue": "Tue"
      ,"pac_miscitas.day_wed": "Wed"
      ,"pac_miscitas.day_thu": "Thu"
      ,"pac_miscitas.day_fri": "Fri"
      ,"pac_miscitas.day_sat": "Sat"
      ,"pac_miscitas.month_jan": "Jan"
      ,"pac_miscitas.month_feb": "Feb"
      ,"pac_miscitas.month_mar": "Mar"
      ,"pac_miscitas.month_apr": "Apr"
      ,"pac_miscitas.month_may": "May"
      ,"pac_miscitas.month_jun": "Jun"
      ,"pac_miscitas.month_jul": "Jul"
      ,"pac_miscitas.month_aug": "Aug"
      ,"pac_miscitas.month_sep": "Sep"
      ,"pac_miscitas.month_oct": "Oct"
      ,"pac_miscitas.month_nov": "Nov"
      ,"pac_miscitas.month_dec": "Dec"
      ,"pac_miscitas.error_load_appointments": "Could not retrieve your appointments. The list will stay empty until the API connection is restored."
      ,"pac_miscitas.select_service": "Select a service"
      ,"pac_miscitas.no_services": "No services available. Try reloading."
      ,"pac_miscitas.error_load_services": "The service catalog could not be loaded. Reload the page before requesting an appointment."
      ,"pac_miscitas.loading_professionals": "Loading professionals..."
      ,"pac_miscitas.error_load_professionals": "The professionals could not be loaded."
      ,"pac_miscitas.select_professional": "Select a professional"
      ,"pac_miscitas.no_professionals_assigned": "No professionals assigned to this service"
      ,"pac_miscitas.no_schedules": "No schedules available"
      ,"pac_miscitas.service_no_professionals_help": "This service has no available professionals. Try another service."
      ,"pac_miscitas.select_professional_help": "Select a professional to see their available schedules."
      ,"pac_miscitas.error_load_professionals_short": "Could not load professionals"
      ,"pac_miscitas.error_availability": "Availability could not be checked."
      ,"pac_miscitas.checking_schedules": "Checking schedules..."
      ,"pac_miscitas.select_date_service_professional_first": "Select date, service and professional first"
      ,"pac_miscitas.error_load_schedules": "Schedules could not be checked."
      ,"pac_miscitas.select_schedule": "Select an available schedule"
      ,"pac_miscitas.schedules_available_help": "These schedules are currently available. Availability will be rechecked when scheduling."
      ,"pac_miscitas.no_schedules_help": "No schedules available for this date and professional. Try another date or professional."
      ,"pac_miscitas.error_load_schedules_short": "Could not load schedules"
      ,"pac_miscitas.cancel_reason_min": "The cancellation reason must be at least 5 characters long."
      ,"pac_miscitas.cancelling": "Cancelling…"
      ,"pac_miscitas.cancel_not_confirmed": "The server did not confirm the cancellation."
      ,"pac_miscitas.error_cancel_connection": "Could not connect to the API to cancel the appointment."
      ,"pac_miscitas.cancel_success": "Appointment cancelled successfully"
      ,"pac_miscitas.error_cancel": "The appointment could not be cancelled. Please try again later or contact reception."
      ,"pac_miscitas.data_incomplete_title": "Incomplete data"
      ,"pac_miscitas.data_incomplete_msg": "Select date, service, professional and available schedule."
      ,"pac_miscitas.no_service_day_title": "No service day"
      ,"pac_miscitas.no_service_day_msg": "The clinic does not provide service that day. Select an available business day."
      ,"pac_miscitas.scheduling": "Scheduling…"
      ,"pac_miscitas.scheduled_msg_prefix": "Your appointment was scheduled for"
      ,"pac_miscitas.at_time": "at"
      ,"pac_miscitas.server_not_confirmed": "The server did not confirm the appointment."
      ,"pac_miscitas.error_schedule": "The appointment could not be scheduled. Please try again."
      ,"pac_miscitas.schedule_taken_alt": "The chosen schedule was taken; select an alternative"
      ,"pac_miscitas.schedule_taken_help": "The schedule was taken while you were confirming. Choose one of these alternatives."
      ,"pac_miscitas.schedule_taken_no_more": "That schedule was taken and there are no more schedules available for this date. Try another date."
      ,"pac_miscitas.error_schedule_title": "Could not schedule"
      ,"pac_miscitas.no_connection_title": "No connection"
      ,"pac_miscitas.no_connection_msg": "Could not connect to the server. Check your connection and try again."
      ,"pac_miscitas.doctor_unassigned": "Unassigned professional"
      ,"pac_miscitas.no_service": "No service"
      ,"pac_miscitas.count_result": "result"
      ,"pac_miscitas.count_results": "results"
      ,"pac_miscitas.empty_filtered": "No appointments match the filters."
      ,"pac_miscitas.cancel_notice": "For cancellations less than 2 hours in advance, please contact reception directly."
      ,"pac_miscitas.view_detail_aria": "View appointment details"
      ,"pac_miscitas.btn_view": "View"
      ,"pac_miscitas.appointment_in": "This appointment is in"
      ,"pac_miscitas.hours": "hours"
      ,"pac_miscitas.cannot_cancel_status": "Cannot cancel: the appointment is in status"
      ,"pac_miscitas.cancel_less_than_2h": "Appointment in less than 2 hours. Contact the clinic to cancel."
      ,"pac_miscitas.appointment_of": "Appointment on"
      ,"pac_miscitas.with_professional": "with"
      ,"pac_miscitas.invalid_date_title": "Invalid date"
      ,"pac_miscitas.invalid_date_msg": "You cannot schedule an appointment on a past date."
      ,"pac_miscitas.sunday_closed_msg": "The clinic does not provide service on Sundays. Select a day from Monday to Saturday."
      ,"pac_miscitas.schedule_selected_help": "Schedule selected. It will be rechecked when scheduling."
      ,"pac_miscitas.error_init": "Error loading \"My Appointments\" module. Please reload."
      ,"pac_notif.badge_read": "Read"
      ,"pac_notif.error_mark_read": "The read status could not be saved."
      ,"pac_notif.error_mark_all_read": "The notifications could not be marked as read."
      ,"pac_notif.error_delete_informative": "This informational notification cannot be deleted."
      ,"pac_notif.error_delete": "The notification could not be deleted."
      ,"pac_notif.unread_aria_prefix": "Unread notification:"
      ,"pac_notif.status_aria_prefix": "Status:"
      ,"pac_notif.view_detail_aria": "View notification details"
      ,"pac_notif.mark_read": "Mark as read"
      ,"pac_notif.delete": "Delete"
      ,"pac_notif.delete_aria": "Delete notification"
      ,"pac_notif.unread_dot_aria": "Unread"
      ,"pac_notif.count_result": "result"
      ,"pac_notif.count_results": "results"
      ,"pac_notif.toast_mark_read_error": "The notification could not be marked as read"
      ,"pac_notif.toast_mark_read_success": "Notification marked as read"
      ,"pac_notif.toast_deleted": "Notification deleted"
      ,"pac_notif.toast_no_unread": "There are no unread notifications"
      ,"pac_notif.toast_all_read": "All notifications marked as read"
      ,"pac_notif.toast_some_failed": "Some notifications could not be marked as read"
      ,"pac_profile.toast_edit_mode": "Edit mode enabled"
      ,"pac_profile.toast_changes_discarded": "Changes discarded"
      ,"pac_profile.toast_need_current_password": "Enter your current password"
      ,"pac_profile.toast_password_min": "The new password must be at least 8 characters long"
      ,"pac_profile.toast_password_mismatch": "The passwords do not match"
      ,"pac_profile.toast_invalid_email": "Enter a valid email address"
      ,"pac_profile.toast_profile_updated": "Profile updated successfully"
      ,"pac_profile.toast_image_too_large": "The image must be smaller than 2MB"
      ,"pac_profile.toast_invalid_image_type": "Only images are allowed (JPG, PNG)"
      ,"pac_profile.toast_photo_updated": "Photo updated"
      ,"pac_profile.photo_alt": "Profile photo"
      ,"pac_profile.status_active": "Active patient"
      ,"pac_profile.status_inactive": "Inactive patient"
      ,"pac_profile.odontologist_label": "Dentist:"
      ,"pac_profile.confirm_data": "Confirm personal data"
      ,"pac_profile.confirm_identity_title": "Confirm your identity data"
      ,"pac_profile.confirm_identity_desc": "We need to validate your document and date of birth to enable appointment requests and access to your clinical history."
      ,"pac_profile.document_type": "Document type"
      ,"pac_profile.select_type": "Select a type"
      ,"pac_profile.doc_cc": "National ID card"
      ,"pac_profile.doc_ti": "Identity card"
      ,"pac_profile.doc_ce": "Foreigner ID card"
      ,"pac_profile.doc_pas": "Passport"
      ,"pac_profile.document_number": "Document number"
      ,"pac_profile.birth_date": "Date of birth"
      ,"pac_profile.save_confirm": "Save and confirm"
      ,"pac_profile.change_photo": "Change photo"
      ,"pac_profile.upload_photo_aria": "Upload profile photo"
      ,"pac_profile.upload_hint": "JPG, PNG. Max 2MB"
      ,"pac_profile.full_name": "Full name"
      ,"pac_profile.id_number": "ID number"
      ,"pac_profile.not_editable": "Not editable"
      ,"pac_profile.gender": "Gender"
      ,"pac_profile.gender_m": "Male"
      ,"pac_profile.gender_f": "Female"
      ,"pac_profile.gender_o": "Other"
      ,"pac_profile.blood_type": "Blood type"
      ,"pac_profile.marital_status": "Marital status"
      ,"pac_profile.marital_single": "Single"
      ,"pac_profile.marital_married": "Married"
      ,"pac_profile.marital_free_union": "Free union"
      ,"pac_profile.marital_divorced": "Divorced"
      ,"pac_profile.marital_widowed": "Widowed"
      ,"pac_profile.contact_info": "Contact information"
      ,"pac_profile.city": "City"
      ,"pac_profile.department": "Department"
      ,"pac_profile.emergency_contact": "Emergency contact"
      ,"pac_profile.relationship": "Relationship"
      ,"pac_profile.medical_info": "Medical information"
      ,"pac_profile.medical_readonly_note": "This information can only be modified by your dentist"
      ,"pac_profile.allergies": "Allergies"
      ,"pac_profile.no_allergies": "No allergies registered"
      ,"pac_profile.conditions": "Diseases or conditions"
      ,"pac_profile.none_registered_feminine": "None registered"
      ,"pac_profile.medications": "Current medications"
      ,"pac_profile.none_registered": "None registered"
      ,"pac_profile.family_history": "Family history"
      ,"pac_profile.insurance": "Insurance / EPS"
      ,"pac_profile.eps": "EPS / Insurer"
      ,"pac_profile.policy_number": "Policy number"
      ,"pac_profile.affiliation_type": "Affiliation type"
      ,"pac_profile.affiliation_contributory": "Contributory regime"
      ,"pac_profile.affiliation_subsidized": "Subsidized regime"
      ,"pac_profile.affiliation_private": "Private"
      ,"pac_profile.security": "Security"
      ,"pac_profile.current_password": "Current password"
      ,"pac_profile.show_password": "Show password"
      ,"pac_profile.new_password": "New password"
      ,"pac_profile.new_password_ph": "Minimum 8 characters"
      ,"pac_profile.confirm_password_ph": "Repeat password"
      ,"pac_hist.breadcrumb_aria": "Breadcrumb"
      ,"pac_hist.breadcrumb_current": "My history"
      ,"pac_hist.hero_kicker": "Clinical history"
      ,"pac_hist.page_title": "My Clinical History"
      ,"pac_hist.page_subtitle": "View the information recorded by your health professionals"
      ,"pac_hist.btn_download_aria": "Download full clinical report"
      ,"pac_hist.btn_download": "Download report"
      ,"pac_hist.agenda_aria": "History content"
      ,"pac_hist.sections_aria": "Clinical history sections"
      ,"pac_hist.tabs_label": "Sections:"
      ,"pac_hist.tab_anamnesis": "1. Anamnesis"
      ,"pac_hist.tab_antecedentes": "2. Background"
      ,"pac_hist.tab_examen": "3. Clinical exam"
      ,"pac_hist.tab_diagnostico": "4. Diagnoses"
      ,"pac_hist.tab_adjuntos": "5. Documents"
      ,"pac_hist.tab_evolucion": "Clinical evolution"
      ,"pac_hist.form_aria": "Read-only clinical form"
      ,"pac_hist.card_anamnesis_title": "Anamnesis and reason for consultation"
      ,"pac_hist.card_anamnesis_sub": "Information recorded by the professional"
      ,"pac_hist.field_reason": "Reason for consultation"
      ,"pac_hist.field_current_illness": "Current illness or condition"
      ,"pac_hist.card_history_title": "Systemic background"
      ,"pac_hist.card_history_sub": "Recorded medical history and alerts"
      ,"pac_hist.field_medical_history": "Medical history"
      ,"pac_hist.field_habits": "Habits and lifestyle"
      ,"pac_hist.field_allergies": "Allergies / medications"
      ,"pac_hist.card_exam_title": "Clinical exam and dental findings"
      ,"pac_hist.card_exam_sub": "Findings recorded during care"
      ,"pac_hist.field_findings": "Exam findings"
      ,"pac_hist.field_observations": "Clinical observations"
      ,"pac_hist.field_complementary": "Complementary exams"
      ,"pac_hist.card_diagnosis_title": "Diagnoses and clinical evolution"
      ,"pac_hist.card_diagnosis_sub": "Recorded clinical notes and procedures"
      ,"pac_hist.field_main_diagnosis": "Main diagnosis"
      ,"pac_hist.field_secondary_diagnosis": "Secondary diagnoses"
      ,"pac_hist.field_procedure": "Procedure performed"
      ,"pac_hist.field_evolution_note": "Clinical evolution note"
      ,"pac_hist.field_prescription": "Prescription and instructions"
      ,"pac_hist.card_documents_title": "Diagnostic documentation and consent"
      ,"pac_hist.card_documents_sub": "Documents available in the clinical history"
      ,"pac_hist.no_documents": "No documents registered."
      ,"pac_hist.workspace_title": "Clinical evolution"
      ,"pac_hist.workspace_sub": "Care records, notes and documents in chronological order."
      ,"pac_hist.records_aria": "Evolution per attended appointment"
      ,"pac_hist.professional_label": "Professional:"
      ,"pac_hist.dt_reason": "Reason"
      ,"pac_hist.dt_illness": "Current illness"
      ,"pac_hist.dt_history": "Background"
      ,"pac_hist.dt_habits": "Habits"
      ,"pac_hist.dt_allergies": "Allergies"
      ,"pac_hist.dt_symptoms": "Symptoms"
      ,"pac_hist.dt_diagnosis": "Diagnosis"
      ,"pac_hist.dt_findings": "Findings"
      ,"pac_hist.dt_odontogram_obs": "Dental observations"
      ,"pac_hist.dt_tests": "Exams"
      ,"pac_hist.dt_evolution": "Evolution"
      ,"pac_hist.dt_procedures": "Procedures"
      ,"pac_hist.dt_prescription": "Prescription"
      ,"pac_hist.empty_history": "There are no attended appointments with registered clinical evolution yet."
      ,"pac_hist.other_records_aria": "Other clinical records"
      ,"pac_hist.other_records_title": "Other clinical records"
      ,"pac_hist.view_document": "View clinical document"
      ,"pac_hist.modal_close_aria": "Close modal"
      ,"pac_hist.modal_detail_title": "Record Detail"
      ,"pac_hist.modal_download_title": "Download history?"
      ,"pac_hist.modal_download_desc": "A PDF will be generated with all your clinical records and available documents."
      ,"pac_hist.btn_back": "Back"
      ,"pac_hist.btn_confirm_download": "Yes, download"
    }
  };

  var i18n = createI18n({
    storageKey: "smiletrack_lang",
    languages: ["es", "en"],
    fallback: "es",
    messages: DICCIONARIO
  });

  window.I18N = i18n;
})();
