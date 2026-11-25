<#
.SYNOPSIS
  Restablece de forma segura los datos de prueba de LogiMarket 360.
.DESCRIPTION
  Cierra la aplicación si está abierta y la relanza con --restablecer. Solo se elimina y recrea la base
  SQLite de la carpeta indicada (o la predeterminada); los datos semilla se cargan de nuevo. No afecta a
  las bases temporales de las pruebas automatizadas.
.PARAMETER Datos
  Carpeta de datos a restablecer. Por defecto, %LOCALAPPDATA%\LogiMarket360.
#>
param([string]$Datos)
$ErrorActionPreference = 'Stop'

if ($Datos) { $carpeta = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Datos) } else { $carpeta = Join-Path $env:LOCALAPPDATA 'LogiMarket360' }
Write-Host "Se recreará la base de datos en: $carpeta"
$respuesta = Read-Host '¿Continuar? (s/N)'
if ($respuesta -notin @('s', 'S', 'si', 'sí')) { Write-Host 'Cancelado.'; return }

Get-Process LogiMarket360 -ErrorAction SilentlyContinue | ForEach-Object { $_.CloseMainWindow() | Out-Null; $_.WaitForExit(5000) | Out-Null }
& (Join-Path $PSScriptRoot 'ejecutar-demo.ps1') -Datos $carpeta -Restablecer
