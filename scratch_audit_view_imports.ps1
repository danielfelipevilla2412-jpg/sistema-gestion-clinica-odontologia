 = @(
    @{ Module="Citas"; Code="st-adm-01-dashboard"; Path="Views/Gestion_De_Citas/st-adm-01-dashboard/index.cshtml"; Js="wwwroot/js/Gestion_De_Citas/st-adm-01-dashboard" },
    @{ Module="Citas"; Code="st-adm-08-agenda"; Path="Views/Gestion_De_Citas/st-adm-08-agenda/index.cshtml"; Js="wwwroot/js/Gestion_De_Citas/st-adm-08-agenda" },
    @{ Module="Citas"; Code="st-adm-09-citas"; Path="Views/Gestion_De_Citas/st-adm-09-citas/index.cshtml"; Js="wwwroot/js/Gestion_De_Citas/st-adm-09-citas" },
    @{ Module="Citas"; Code="st-aux-01-panel-operativo"; Path="Views/Gestion_De_Citas/st-aux-01-panel-operativo/panel-operativo.cshtml"; Js="wwwroot/js/Gestion_De_Citas/st-aux-01-panel-operativo" },
    @{ Module="Citas"; Code="st-aux-02-agenda-apoyo"; Path="Views/Gestion_De_Citas/st-aux-02-agenda-apoyo/agenda-apoyo.cshtml"; Js="wwwroot/js/Gestion_De_Citas/st-aux-02-agenda-apoyo" },
    @{ Module="Citas"; Code="st-aux-05-historial-parcial"; Path="Views/Gestion_De_Citas/st-aux-05-historial-parcial/historial-parcial.cshtml"; Js="wwwroot/js/Gestion_De_Citas/st-aux-05-historial-parcial" },
    @{ Module="Citas"; Code="st-aux-06-asistencia-procedi"; Path="Views/Gestion_De_Citas/st-aux-06-asistencia-procedi/asistencia-procedi.cshtml"; Js="wwwroot/js/Gestion_De_Citas/st-aux-06-asistencia-procedi" },
    @{ Module="Citas"; Code="st-aux-09-estado-consultorio"; Path="Views/Gestion_De_Citas/st-aux-09-estado-consultorio/estado-consultorio.cshtml"; Js="wwwroot/js/Gestion_De_Citas/st-aux-09-estado-consultorio" },
    @{ Module="Citas"; Code="st-aux-10-citas-finalizadas"; Path="Views/Gestion_De_Citas/st-aux-10-citas-finalizadas/citas-finalizadas.cshtml"; Js="wwwroot/js/Gestion_De_Citas/st-aux-10-citas-finalizadas" },
    @{ Module="Citas"; Code="st-odo-02-agenda"; Path="Views/Gestion_De_Citas/st-odo-02-agenda/index.cshtml"; Js="wwwroot/js/Gestion_De_Citas/st-odo-02-agenda" },
    @{ Module="Citas"; Code="st-pac-01-mis-citas"; Path="Views/Gestion_De_Citas/st-pac-01-mis-citas/index.cshtml"; Js="wwwroot/js/Gestion_De_Citas/st-pac-01-mis-citas" },
    @{ Module="Citas"; Code="st-pac-03-notificaciones"; Path="Views/Gestion_De_Citas/st-pac-03-notificaciones/index.cshtml"; Js="wwwroot/js/Gestion_De_Citas/st-pac-03-notificaciones" },
    @{ Module="Citas"; Code="st-rec-01-dashboard"; Path="Views/Gestion_De_Citas/st-rec-01-dashboard/index.cshtml"; Js="wwwroot/js/Gestion_De_Citas/st-rec-01-dashboard" },
    @{ Module="Citas"; Code="st-rec-03-gestion-citas"; Path="Views/Gestion_De_Citas/st-rec-03-gestion-citas/index.cshtml"; Js="wwwroot/js/Gestion_De_Citas/st-rec-03-gestion-citas" },
    @{ Module="Citas"; Code="st-rec-05-recordatorios"; Path="Views/Gestion_De_Citas/st-rec-05-recordatorios/index.cshtml"; Js="wwwroot/js/Gestion_De_Citas/st-rec-05-recordatorios" },
    @{ Module="Profesionales"; Code="st-adm-07-gestion-profesionales"; Path="Views/Gestion_De_Profesionales/st-adm-07-gestion-profesionales/index.cshtml"; Js="wwwroot/js/Gestion_De_Profesionales/st-adm-07-gestion-profesionales" },
    @{ Module="Profesionales"; Code="st-adm-14-reportes-clinicos"; Path="Views/Gestion_De_Profesionales/st-adm-14-reportes-clinicos/index.cshtml"; Js="wwwroot/js/Gestion_De_Profesionales/st-adm-14-reportes-clinicos" },
    @{ Module="Profesionales"; Code="st-odo-01-dashboard"; Path="Views/Gestion_De_Profesionales/st-odo-01-dashboard/index.cshtml"; Js="wwwroot/js/Gestion_De_Profesionales/st-odo-01-dashboard" },
    @{ Module="Profesionales"; Code="st-odo-09-perfil-profesional"; Path="Views/Gestion_De_Profesionales/st-odo-09-perfil-profesional/index.cshtml"; Js="wwwroot/js/Gestion_De_Profesionales/st-odo-09-perfil-profesional" }
)

foreach ( in ) {
    Write-Host "=================================================="
    Write-Host "VISTA: "
    
    # Check script tags in cshtml
    if (Test-Path .Path) {
         = Get-Content .Path -Raw
         = [regex]::Matches(, '<script[^>]*src=["'']([^"'']+)["'']') | ForEach-Object { .Groups[1].Value }
        Write-Host "CSHTML Scripts: "
    } else {
        Write-Host "CSHTML Path not found: "
    }
    
    # Check JS files
    if (Test-Path .Js) {
         = Get-ChildItem .Js -Filter "*.js"
        foreach ( in ) {
            Write-Host "  JS File: "
             = Get-Content .FullName -Raw
            
            # Check usages
             =  -match 'CommonUtils'
             =  -match 'ToastService'
             =  -match 'ValidationUtils'
             =  -match 'AppointmentUtils'
            
            Write-Host "    Uses CommonUtils:  | ToastService:  | ValidationUtils:  | AppointmentUtils: "
        }
    } else {
        Write-Host "  JS Directory not found: "
    }
}
