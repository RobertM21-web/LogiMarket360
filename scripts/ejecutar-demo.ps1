<#
.SYNOPSIS
  Compila (si hace falta) y ejecuta LogiMarket 360.
.PARAMETER Datos
  Carpeta de datos alternativa (base SQLite y comprobantes). Por defecto se usa %LOCALAPPDATA%\LogiMarket360.
.PARAMETER Restablecer
  Recrea la base de esa carpeta con los datos semilla antes de abrir la aplicación.
.EXAMPLE
  .\scripts\ejecutar-demo.ps1 -Datos .\datos-demo -Restablecer
#>
param(
    [string]$Datos,
    [switch]$Restablecer
)
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $raiz 'src\LogiMarket.WinForms\bin\Release\net8.0-windows\LogiMarket360.exe'

if (-not (Test-Path $exe)) {
    dotnet build (Join-Path $raiz 'LogiMarket360.sln') -c Release --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'La compilación falló.' }
}

$argumentos = @()
if ($Datos) { $argumentos += @('--datos', (New-Item -ItemType Directory -Force $Datos).FullName) }
if ($Restablecer) { $argumentos += '--restablecer' }
if ($argumentos.Count -gt 0) { Start-Process -FilePath $exe -ArgumentList $argumentos } else { Start-Process -FilePath $exe }
