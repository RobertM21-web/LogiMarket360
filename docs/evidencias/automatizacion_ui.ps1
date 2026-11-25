# Ayudante de automatización UI (UI Automation + PrintWindow) para capturas reales de LogiMarket 360.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class W32 {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hh, bool r);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, IntPtr e);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
"@
$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]

function Wait-Ventana([int]$procId, [string]$titulo, [int]$seg = 30) {
  $fin = (Get-Date).AddSeconds($seg)
  while ((Get-Date) -lt $fin) {
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $procId)
    foreach ($w in $AE::RootElement.FindAll($TS::Children, $c)) {
      if ($w.Current.Name -like $titulo) { return $w }
      foreach ($m in $w.FindAll($TS::Children, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)))) {
        if ($m.Current.Name -like $titulo) { return $m }
      }
    }
    Start-Sleep -Milliseconds 250
  }
  throw "No apareció la ventana '$titulo'"
}

function Find-El($raiz, [string]$nombre, [int]$seg = 10) {
  $fin = (Get-Date).AddSeconds($seg)
  while ((Get-Date) -lt $fin) {
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $nombre)
    $e = $raiz.FindFirst($TS::Descendants, $c)
    if ($e) { return $e }
    Start-Sleep -Milliseconds 200
  }
  throw "No se encontró '$nombre'"
}

function Click-El($raiz, [string]$nombre, $tipo = $null) {
  $e = if ($tipo) { Find-Tipo $raiz $nombre $tipo } else { Find-El $raiz $nombre }
  $p = $null
  $h = [IntPtr]$e.Current.NativeWindowHandle
  if ($e.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and $h -ne [IntPtr]::Zero) {
    # BM_CLICK asíncrono: no bloquea si el botón abre un cuadro de diálogo modal.
    [W32]::PostMessage($h, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 700
    return
  }
  if ($e.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$p)) { $p.Invoke() }
  elseif ($e.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$p)) { $p.Select() }
  elseif ($e.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$p)) { $p.Toggle() }
  else { throw "'$nombre' no admite clic" }
  Start-Sleep -Milliseconds 400
}

function Find-Tipo($raiz, [string]$nombre, $tipo, [int]$seg = 10) {
  $fin = (Get-Date).AddSeconds($seg)
  while ((Get-Date) -lt $fin) {
    $c = New-Object System.Windows.Automation.AndCondition((New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $nombre)), (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $tipo)))
    $e = $raiz.FindFirst($TS::Descendants, $c)
    if ($e) { return $e }
    Start-Sleep -Milliseconds 200
  }
  throw "No se encontró '$nombre' del tipo indicado"
}

function Set-Texto($raiz, [string]$nombre, [string]$valor) {
  $e = Find-Tipo $raiz $nombre ([System.Windows.Automation.ControlType]::Edit)
  $p = $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
  $p.SetValue($valor)
  Start-Sleep -Milliseconds 150
}

# Selecciona un elemento de un ComboBox por texto (coincidencia parcial).
function Select-Combo($raiz, [string]$nombre, [string]$texto) {
  $combo = Find-Tipo $raiz $nombre ([System.Windows.Automation.ControlType]::ComboBox)
  $exp = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
  $exp.Expand(); Start-Sleep -Milliseconds 300
  $items = $combo.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)))
  foreach ($i in $items) {
    if ($i.Current.Name -like "*$texto*") {
      $i.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
      $exp.Collapse(); Start-Sleep -Milliseconds 500; return
    }
  }
  $exp.Collapse()
  throw "No hay opción '$texto' en '$nombre'"
}


# Busca un cuadro de diálogo (#32770) del proceso: de nivel superior o como hijo de una ventana propietaria.
function Find-Dialogo([int]$procId) {
  $c = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $procId)
  $clase = New-Object System.Windows.Automation.PropertyCondition($AE::ClassNameProperty, "#32770")
  foreach ($w in $AE::RootElement.FindAll($TS::Children, $c)) {
    if ($w.Current.ClassName -eq "#32770") { return $w }
    $d = $w.FindFirst($TS::Children, $clase)
    if ($d) { return $d }
  }
  return $null
}

# Responde un MessageBox (Sí / Aceptar) y devuelve su texto.
function Answer-Dialogo([int]$procId, [string]$boton = "Aceptar", [int]$seg = 15) {
  $fin = (Get-Date).AddSeconds($seg)
  while ((Get-Date) -lt $fin) {
    $w = Find-Dialogo $procId
    if ($w) {
      $txt = ($w.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ClassNameProperty, "Static"))) | ForEach-Object { $_.Current.Name } | Where-Object { $_ }) -join " | "
      foreach ($nb in @($boton, "Sí", "Yes", "Aceptar", "OK")) {
        $b = $w.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $nb)))
        if ($b) { [W32]::PostMessage([IntPtr]$b.Current.NativeWindowHandle, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null; Start-Sleep -Milliseconds 900; return $txt }
      }
    }
    Start-Sleep -Milliseconds 250
  }
  throw "No apareció el diálogo"
}

# Captura una ventana o diálogo abierto del proceso por título.
function Save-Captura($ventana, [string]$ruta) {
  $h = [IntPtr]$ventana.Current.NativeWindowHandle
  [W32]::SetForegroundWindow($h) | Out-Null
  Start-Sleep -Milliseconds 700
  $r = New-Object W32+RECT
  [W32]::GetWindowRect($h, [ref]$r) | Out-Null
  $bmp = New-Object System.Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $g.GetHdc()
  [W32]::PrintWindow($h, $hdc, 2) | Out-Null
  $g.ReleaseHdc($hdc); $g.Dispose()
  $bmp.Save($ruta, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  Write-Output "captura: $ruta"
}

function Save-DialogoCaptura([int]$procId, [string]$ruta) {
  $fin = (Get-Date).AddSeconds(15)
  while ((Get-Date) -lt $fin) {
    $d = Find-Dialogo $procId
    if ($d) { Save-Captura $d $ruta; return }
    Start-Sleep -Milliseconds 250
  }
  throw "No hay diálogo para capturar"
}

# Fija el valor de un NumericUpDown (Spinner) por nombre accesible.
function Set-Numero($raiz, [string]$nombre, [int]$valor) {
  $e = try { Find-Tipo $raiz $nombre ([System.Windows.Automation.ControlType]::Spinner) 2 } catch { Find-El $raiz $nombre }
  $p = $null
  if ($e.TryGetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern, [ref]$p)) { $p.SetValue($valor); Start-Sleep -Milliseconds 200; return }
  if ($e.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$p)) { $p.SetValue("$valor"); Start-Sleep -Milliseconds 200; return }
  $ed = $e.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)))
  $ed.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("$valor"); Start-Sleep -Milliseconds 200
}

# Selecciona la fila de una grilla cuyo texto contiene $texto con un clic real del ratón sobre su primera celda.
function Select-Fila($raiz, [string]$grilla, [string]$texto) {
  $g = Find-Tipo $raiz $grilla ([System.Windows.Automation.ControlType]::DataGrid)
  [W32]::SetForegroundWindow([IntPtr]$raiz.Current.NativeWindowHandle) | Out-Null
  foreach ($f in $g.FindAll($TS::Children, [System.Windows.Automation.Condition]::TrueCondition)) {
    $celdas = @($f.FindAll($TS::Children, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::DataItem))))
    $valores = ($celdas | ForEach-Object { $_.GetCurrentPropertyValue([System.Windows.Automation.ValuePattern]::ValueProperty) }) -join " "
    if ($celdas.Count -gt 0 -and $valores -like "*$texto*") {
      $r = $celdas[0].Current.BoundingRectangle
      [W32]::SetCursorPos([int]($r.X + [Math]::Min(20, $r.Width / 2)), [int]($r.Y + $r.Height / 2)) | Out-Null
      [W32]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero); [W32]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
      Start-Sleep -Milliseconds 900; return
    }
  }
  throw "No hay fila con '$texto' en '$grilla'"
}

# Captura el diálogo abierto (si lo hay), lo responde y devuelve su texto.
function Dialogo([int]$procId, [string]$ruta = $null, [string]$boton = "Aceptar") {
  if ($ruta) { Save-DialogoCaptura $procId $ruta | Out-Null }
  return Answer-Dialogo $procId $boton
}
