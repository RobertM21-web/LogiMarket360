<#
.SYNOPSIS
  Genera el ZIP limpio de entrega y lo verifica compilando y probando desde cero.
.DESCRIPTION
  1. Exporta con `git archive` solo los archivos versionados de la rama indicada: excluye bin/, obj/, .vs/,
     bases *.db, la guía de referencia y cualquier archivo ignorado.
  2. Revisa que el ZIP no contenga artefactos prohibidos.
  3. Lo descomprime en una carpeta temporal, ejecuta restore, build y test, y guarda el resultado en
     docs/evidencias/pruebas/build_limpio.txt (o en la ruta indicada con -Registro).
.EXAMPLE
  .\scripts\empaquetar.ps1 -Apellidos MONTESDEOCA_CHAFLA -Nombres ROBERT_ALEXANDER
#>
param(
    [Parameter(Mandatory)] [string]$Apellidos,
    [Parameter(Mandatory)] [string]$Nombres,
    [string]$Rama = 'HEAD',
    [string]$Salida,
    [string]$Registro
)
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$nombre = "APE04_LogiMarket360_${Apellidos}_${Nombres}"
if (-not $Salida) { $Salida = Join-Path $raiz "$nombre.zip" }
if (-not $Registro) { $Registro = Join-Path $raiz 'docs\evidencias\pruebas\build_limpio.txt' }

Push-Location $raiz
try {
    git archive --format=zip --prefix="$nombre/" -o $Salida $Rama
    if ($LASTEXITCODE -ne 0) { throw 'git archive falló.' }
} finally { Pop-Location }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($Salida)
try {
    $entradas = $zip.Entries | ForEach-Object FullName
    $prohibidas = $entradas | Where-Object { $_ -match '(^|/)(bin|obj|\.vs|TestResults)/|\.(db|db-wal|db-shm|user|suo)$|Documento\.pdf$|MASTER_PROMPT' }
    if ($prohibidas) { throw "El ZIP contiene artefactos prohibidos:`n$($prohibidas -join "`n")" }
    $tamano = (Get-Item $Salida).Length
} finally { $zip.Dispose() }

$temporal = Join-Path ([IO.Path]::GetTempPath()) ("LogiMarket360_verificacion_" + [Guid]::NewGuid().ToString('N'))
Expand-Archive -Path $Salida -DestinationPath $temporal
$solucion = Join-Path $temporal "$nombre\LogiMarket360.sln"

$log = New-Object System.Collections.Generic.List[string]
$log.Add("Verificación del ZIP limpio — $(Get-Date -Format 'yyyy-MM-dd HH:mm')")
$log.Add("Archivo: $(Split-Path -Leaf $Salida) · $([math]::Round($tamano / 1KB)) KB · $($entradas.Count) entradas · sin bin/obj/.vs/*.db")
$log.Add("SDK: $(dotnet --version) · $([Environment]::OSVersion.VersionString)")
foreach ($paso in @(
        @('restore', @('restore', $solucion, '--nologo')),
        @('build', @('build', $solucion, '-c', 'Release', '--no-restore', '--nologo')),
        @('test', @('test', $solucion, '-c', 'Release', '--no-build', '--nologo')))) {
    $salidaPaso = & dotnet @($paso[1]) 2>&1 | Out-String
    $codigo = $LASTEXITCODE
    $resumen = ($salidaPaso -split "`r?`n") | Where-Object { $_ -match 'Build succeeded|Compilación correcta|Warning\(s\)|Error\(s\)|Advertencia|error|Passed!|Failed!|Correcto!|Con error' } | Select-Object -Last 4
    $log.Add("[$($paso[0])] código de salida $codigo")
    $resumen | ForEach-Object { $log.Add('    ' + $_.Trim()) }
    if ($codigo -ne 0) { $log | Set-Content -Encoding utf8 $Registro; throw "Falló el paso $($paso[0]). Ver $Registro" }
}
$log | Set-Content -Encoding utf8 $Registro
Remove-Item -Recurse -Force $temporal
$log | ForEach-Object { Write-Host $_ }
Write-Host "ZIP listo: $Salida"
