; Instalador local de AbogApp2 (backend + frontend en un solo Servicio de
; Windows). Compilado por package-local-install.ps1, no a mano.
;
; MyAppVersion se pasa por linea de comandos: ISCC "/DMyAppVersion=1.0.0" installer.iss
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName "AbogApp2"
#define MyServiceName "AbogApp2Service"
#define MyExeName "Login.WebApi.exe"
#define MyPublishDir "publish"

[Setup]
AppId={{B7B6F1B0-7C4C-4D4E-9C1A-ABOGAPP2LOCAL}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=AbogApp2-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "AbogApp2.url"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\AbogApp2.url"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\AbogApp2.url"

; Detiene/borra una instalación previa del servicio antes de copiar archivos
; nuevos (permite reinstalar/actualizar sin pasos manuales).
[Run]
Filename: "{sys}\sc.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden skipifdoesntexist; StatusMsg: "Deteniendo AbogApp2 (si ya estaba instalado)..."
Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden skipifdoesntexist

Filename: "{sys}\sc.exe"; Parameters: "create {#MyServiceName} binPath= ""{app}\{#MyExeName}"" start= auto DisplayName= ""AbogApp2 - Servicio local"""; Flags: runhidden; StatusMsg: "Registrando el servicio de AbogApp2..."
Filename: "{sys}\sc.exe"; Parameters: "description {#MyServiceName} ""Backend + frontend de AbogApp2 (localhost:8090). No cerrar."""; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "start {#MyServiceName}"; Flags: runhidden; StatusMsg: "Iniciando AbogApp2..."
Filename: "http://localhost:8090"; Description: "Abrir AbogApp2 ahora"; Flags: postinstall shellexec nowait skipifsilent

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden skipifdoesntexist
Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden skipifdoesntexist
