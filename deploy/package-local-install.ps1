<#
    Arma el paquete de instalación local de AbogApp2 (backend + frontend en
    un solo Servicio de Windows, apuntando a la base MySQL remota de cPanel).

    Pasos:
      1. Build de Angular (configuración "local", apiUrl relativo /api).
      2. Publish self-contained del backend (no requiere .NET instalado en
         la PC destino).
      3. Copia el build de Angular dentro de wwwroot del publish.
      4. Inyecta la cadena de conexión real (desde user-secrets) SOLO en la
         copia publicada — nunca toca el repo.
      5. Compila el instalador .exe con Inno Setup.

    Uso: .\package-local-install.ps1 [-Version 1.0.0]
#>
param(
    [string]$Version = (Get-Date -Format "yyyy.MM.dd.HHmm")
)

$ErrorActionPreference = "Stop"

$backendRoot   = "D:\VisualStudio\TemplateLogin"
$frontendRoot  = "d:\Angular\Abog2\Aboga2"
$webApiProject = "$backendRoot\Login.WebApi\Login.WebApi.csproj"
$publishDir    = "$backendRoot\deploy\publish"
$innoScript    = "$backendRoot\deploy\installer.iss"
$innoCompiler  = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
$secretsPath   = "$env:APPDATA\Microsoft\UserSecrets\4c2df804-4d72-43f5-90a4-8f1b1c84fc55\secrets.json"

Write-Host "=== 1/5 Build Angular (local) ===" -ForegroundColor Cyan
Push-Location $frontendRoot
npm run build:local
if ($LASTEXITCODE -ne 0) { throw "Fallo el build de Angular" }
Pop-Location

Write-Host "=== 2/5 Publish backend (self-contained win-x64) ===" -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $webApiProject -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "Fallo el publish del backend" }

Write-Host "=== 3/5 Copiar Angular a wwwroot ===" -ForegroundColor Cyan
$wwwroot = "$publishDir\wwwroot"
New-Item -ItemType Directory -Force -Path $wwwroot | Out-Null
Copy-Item "$frontendRoot\dist\Aboga2\browser\*" $wwwroot -Recurse -Force

Write-Host "=== 4/5 Inyectar cadena de conexion real (solo en el publish) ===" -ForegroundColor Cyan
if (-not (Test-Path $secretsPath)) {
    throw "No se encontraron user-secrets en $secretsPath. Configura ConnectionStrings:DefaultConection primero."
}
$secrets = Get-Content -Raw $secretsPath | ConvertFrom-Json
$connString = $secrets.'ConnectionStrings:DefaultConection'
if ([string]::IsNullOrWhiteSpace($connString)) {
    throw "ConnectionStrings:DefaultConection esta vacio en user-secrets."
}

$prodSettingsPath = "$publishDir\appsettings.Production.json"
$prodSettings = Get-Content -Raw $prodSettingsPath | ConvertFrom-Json
$prodSettings | Add-Member -NotePropertyName "ConnectionStrings" -NotePropertyValue ([ordered]@{ DefaultConection = $connString }) -Force
$prodSettings | ConvertTo-Json -Depth 10 | Set-Content -Path $prodSettingsPath -Encoding UTF8
Write-Host "  Cadena de conexion inyectada (longitud $($connString.Length) chars)."

Write-Host "=== 5/5 Compilar instalador con Inno Setup ===" -ForegroundColor Cyan
if (-not (Test-Path $innoCompiler)) { throw "No se encontro ISCC.exe en $innoCompiler" }
& $innoCompiler "/DMyAppVersion=$Version" $innoScript
if ($LASTEXITCODE -ne 0) { throw "Fallo la compilacion del instalador" }

Write-Host "`nListo. Instalador generado en $backendRoot\deploy\output\" -ForegroundColor Green
