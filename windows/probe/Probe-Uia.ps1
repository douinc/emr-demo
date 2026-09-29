<#
.SYNOPSIS
  Dumps the UI Automation tree of a target window, captures it two ways, and (for EmrDemo)
  checks what each UI mode exposes.

.DESCRIPTION
  Against any program (e.g. a real EMR): pass -ProcessName or -ProcessId. Writes uia-tree.txt,
  capture-screen.png (CopyFromScreen) and capture-printwindow.png (PrintWindow) to -OutDir.

  Against EmrDemo: pass -ExePath and -Mode. The script launches the app with a new data dir
  (<OutDir>\data-<timestamp>; nothing is deleted), then asserts the mode's contract:
    standard: chartText / vitalGrid / orderGrid are reachable by AutomationId, chartText exposes
              ValuePattern, values written through UIA (text box + one order grid cell) show up in
              the next save snapshot and once each as field_commit; switching patients discards an
              unsaved edit without logging load-time commits; reset restores the seed on screen only.
    custom:   none of those AutomationIds exist; the chart/vitals/orders areas are panes with no
              name, patterns or children (AutomationId only a per-run numeric id); neither UIA Name/Value nor WM_GETTEXT of any child window
              contains chart text; text typed with the keyboard (chart + first order cell) is saved
              and logged, and is still not readable through UIA or WM_GETTEXT.
  Exits 1 if any check fails.

  Probing a real EMR writes its screen content (patient data) to -OutDir. Keep -OutDir outside
  the repository or under an ignored folder.

  Kept ASCII-only on purpose: Windows PowerShell 5.1 reads BOM-less scripts as the ANSI code page.
#>
param(
    [string]$ExePath,
    [ValidateSet('standard', 'custom')][string]$Mode,
    [string]$ProcessName,
    [int]$ProcessId,
    [string]$OutDir = (Join-Path (Get-Location) 'probe-out'),
    [int]$MaxDepth = 12
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class ProbeNative {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc proc, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, StringBuilder lParam,
        uint flags, uint timeout, out IntPtr result);

    public static void Click(int x, int y) {
        SetCursorPos(x, y);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }

    // WM_GETTEXT of every descendant window (GetWindowText does not send it across processes).
    public static List<string> ChildTexts(IntPtr parent) {
        var texts = new List<string>();
        EnumChildWindows(parent, (hWnd, _) => {
            var buffer = new StringBuilder(65536);
            IntPtr result;
            SendMessageTimeout(hWnd, 0x000D, (IntPtr)buffer.Capacity, buffer, 0x0002, 1000, out result);
            texts.Add(buffer.ToString());
            return true;
        }, IntPtr.Zero);
        return texts;
    }
}
'@

$AE = [System.Windows.Automation.AutomationElement]
$script:Failures = 0

function Check([bool]$ok, [string]$message) {
    if ($ok) { Write-Host "PASS  $message" } else { Write-Host "FAIL  $message"; $script:Failures++ }
}

function Wait-MainWindow([System.Diagnostics.Process]$process) {
    for ($i = 0; $i -lt 100; $i++) {
        $process.Refresh()
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { return $process.MainWindowHandle }
        Start-Sleep -Milliseconds 200
    }
    throw "main window did not appear for pid $($process.Id)"
}

function Get-Patterns($element) {
    ($element.GetSupportedPatterns() | ForEach-Object { $_.ProgrammaticName -replace 'Identifiers\.Pattern', '' }) -join ','
}

function Get-ValueText($element) {
    $pattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) {
        return $pattern.Current.Value
    }
    return $null
}

function Dump-Tree($root, [string]$path) {
    $walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
    $lines = New-Object System.Collections.Generic.List[string]
    $all = New-Object System.Collections.Generic.List[object]
    function Visit($element, [int]$depth) {
        $c = $element.Current
        $value = Get-ValueText $element
        $shown = if ($null -ne $value -and $value.Length -gt 60) { $value.Substring(0, 60) + '...' } else { $value }
        $lines.Add(('{0}{1} name="{2}" id="{3}" class="{4}" patterns=[{5}] value="{6}"' -f
            ('  ' * $depth), $c.ControlType.ProgrammaticName, $c.Name, $c.AutomationId, $c.ClassName,
            (Get-Patterns $element), $shown))
        $all.Add([pscustomobject]@{ Element = $element; Name = $c.Name; Id = $c.AutomationId; Value = $value })
        if ($depth -ge $MaxDepth) { return }
        $child = $walker.GetFirstChild($element)
        while ($null -ne $child) {
            Visit $child ($depth + 1)
            $child = $walker.GetNextSibling($child)
        }
    }
    $started = Get-Date
    Visit $root 0
    $elapsed = ((Get-Date) - $started).TotalMilliseconds
    [System.IO.File]::WriteAllLines($path, $lines, (New-Object System.Text.UTF8Encoding $true))
    Write-Host ("INFO  UIA tree: {0} elements, full walk {1:N0} ms -> {2}" -f $all.Count, $elapsed, $path)
    return $all
}

function Save-Captures([IntPtr]$hwnd, [string]$suffix = '') {
    $rect = New-Object ProbeNative+RECT
    [void][ProbeNative]::GetWindowRect($hwnd, [ref]$rect)
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top

    $screen = New-Object System.Drawing.Bitmap $width, $height
    $g = [System.Drawing.Graphics]::FromImage($screen)
    $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $screen.Size)
    $g.Dispose()
    $name = if ($suffix) { "-$suffix" } else { '' }
    $screen.Save((Join-Path $OutDir "capture-screen$name.png"))
    $screen.Dispose()

    $printed = New-Object System.Drawing.Bitmap $width, $height
    $g = [System.Drawing.Graphics]::FromImage($printed)
    $hdc = $g.GetHdc()
    $ok = [ProbeNative]::PrintWindow($hwnd, $hdc, 2)
    $g.ReleaseHdc($hdc)
    $g.Dispose()
    $printed.Save((Join-Path $OutDir "capture-printwindow$name.png"))
    $printed.Dispose()
    Write-Host "INFO  captures ${width}x${height}, PrintWindow returned $ok"
}

function Find-ById($root, [string]$id) {
    $condition = New-Object System.Windows.Automation.PropertyCondition $AE::AutomationIdProperty, $id
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Read-LastSave([string]$dataDir) {
    $log = Join-Path $dataDir 'events.jsonl'
    if (-not (Test-Path $log)) { return $null }
    $saves = Get-Content $log -Encoding UTF8 | ForEach-Object { $_ | ConvertFrom-Json } | Where-Object { $_.type -eq 'save' }
    return @($saves)[-1]
}

function Read-Events([string]$dataDir) {
    return @(Get-Content (Join-Path $dataDir 'events.jsonl') -Encoding UTF8 | ForEach-Object { $_ | ConvertFrom-Json })
}

function Find-Text($root, [string]$fragment) {
    $condition = New-Object System.Windows.Automation.PropertyCondition $AE::ControlTypeProperty, ([System.Windows.Automation.ControlType]::Text)
    foreach ($element in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)) {
        if ($element.Current.Name.Contains($fragment)) { return $element }
    }
    return $null
}

function Test-OpaquePane($element) {
    if ($null -eq $element) { return $false }
    $c = $element.Current
    $walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
    # A nameless WinForms control still reports a per-run numeric window id as AutomationId.
    return $c.ControlType -eq [System.Windows.Automation.ControlType]::Pane -and -not $c.Name -and $c.AutomationId -match '^\d*$' -and
        $element.GetSupportedPatterns().Count -eq 0 -and $null -eq $walker.GetFirstChild($element)
}

function Test-Leak($root, [IntPtr]$hwnd, [string[]]$markers, [string]$label) {
    $all = Dump-Tree $root (Join-Path $OutDir "uia-tree-$label.txt")
    $childTexts = [ProbeNative]::ChildTexts($hwnd)
    foreach ($marker in $markers) {
        $uia = @($all | Where-Object { ($_.Name -and $_.Name.Contains($marker)) -or ($_.Value -and $_.Value.Contains($marker)) })
        Check ($uia.Count -eq 0) "[$label] '$marker' is not readable in the UIA tree"
        $win = @($childTexts | Where-Object { $_.Contains($marker) })
        Check ($win.Count -eq 0) "[$label] '$marker' is not readable through WM_GETTEXT"
    }
}

function Click-In($element, [int]$dx, [int]$dy) {
    $r = $element.Current.BoundingRectangle
    $x = if ($dx -ge 0) { [int]$r.Left + $dx } else { [int]($r.Left + $r.Width / 2) }
    $y = if ($dy -ge 0) { [int]$r.Top + $dy } else { [int]($r.Top + $r.Height / 2) }
    [ProbeNative]::Click($x, $y)
    Start-Sleep -Milliseconds 300
}

function Send-Keys([string]$keys) {
    [System.Windows.Forms.SendKeys]::SendWait($keys)
    Start-Sleep -Milliseconds 200
}

function Select-Patient($root, [string]$id) {
    $condition = New-Object System.Windows.Automation.PropertyCondition $AE::ControlTypeProperty, ([System.Windows.Automation.ControlType]::ListItem)
    foreach ($item in (Find-ById $root 'patientList').FindAll([System.Windows.Automation.TreeScope]::Children, $condition)) {
        if ($item.Current.Name.StartsWith($id)) {
            $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
            Start-Sleep -Milliseconds 500
            return $true
        }
    }
    return $false
}

function Read-FormLayout([string]$dataDir, [string]$formId) {
    $path = Join-Path $dataDir "layout-$formId.json"
    if (-not (Test-Path $path)) { return $null }
    return Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Get-FormValue($save, [string]$key) {
    if ($null -eq $save) { return $null }
    $property = $save.patient.formValues.PSObject.Properties[$key]
    if ($null -eq $property) { return $null }
    return $property.Value
}

# Picks one input of each kind from the form layout: an editable text box, option 1 of the first radio
# group, option 0 of the first check group, the first select, and the first grid cell.
function Get-FormTargets($layout) {
    $items = @($layout.items)
    return [pscustomobject]@{
        Text   = $items | Where-Object { $_.kind -eq 'text' -and -not $_.readOnly } | Select-Object -First 1
        Radio  = $items | Where-Object { $_.group -eq 'radio' -and $_.option -eq 1 } | Select-Object -First 1
        Check  = $items | Where-Object { $_.group -eq 'check' -and $_.option -eq 0 } | Select-Object -First 1
        Select = $items | Where-Object { $_.kind -eq 'select' } | Select-Object -First 1
        Cell   = $items | Where-Object { $_.kind -eq 'gridCell' } | Select-Object -First 1
    }
}

function Test-FormSave($dataDir, $targets, [string]$textValue, [string]$selectValue, [string]$cellValue, [string]$label) {
    $last = Read-LastSave $dataDir
    $expected = [ordered]@{
        $targets.Text.key   = $textValue
        $targets.Radio.key  = $targets.Radio.text
        $targets.Check.key  = $targets.Check.text
        $targets.Select.key = $selectValue
        $targets.Cell.key   = $cellValue
    }
    $commits = @(Read-Events $dataDir | Where-Object { $_.type -eq 'field_commit' })
    foreach ($key in $expected.Keys) {
        Check ((Get-FormValue $last $key) -eq $expected[$key]) "[$label] form value $key = '$($expected[$key])' in save snapshot"
        Check (@($commits | Where-Object { $_.field -eq "form.$key" -and $_.value -eq $expected[$key] }).Count -eq 1) "[$label] form.$key field_commit logged once"
    }
}

function Get-GridCell($grid, [int]$row, [int]$column) {
    $gridPattern = $null
    if ($grid.TryGetCurrentPattern([System.Windows.Automation.GridPattern]::Pattern, [ref]$gridPattern)) {
        return $gridPattern.GetItem($row, $column)
    }
    return $null
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$process = $null
$dataDir = $null

if ($ExePath) {
    if (-not $Mode) { throw '-Mode is required with -ExePath' }
    $dataDir = Join-Path (Resolve-Path $OutDir) ('data-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    # Windows PowerShell 5.1 joins -ArgumentList with spaces without quoting.
    $process = Start-Process -FilePath $ExePath -ArgumentList "--ui=$Mode", "`"--data-dir=$dataDir`"" -PassThru
} elseif ($ProcessId) {
    $process = Get-Process -Id $ProcessId
} elseif ($ProcessName) {
    $process = Get-Process -Name $ProcessName | Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } | Select-Object -First 1
    if (-not $process) { throw "no windowed process named $ProcessName" }
} else {
    throw 'pass -ExePath/-Mode, -ProcessName, or -ProcessId'
}

try {
    $hwnd = Wait-MainWindow $process
    Start-Sleep -Milliseconds 800
    [void][ProbeNative]::SetForegroundWindow($hwnd)
    $root = $AE::FromHandle($hwnd)
    Write-Host ("INFO  window pid={0} title=""{1}"" class={2}" -f $process.Id, $root.Current.Name, $root.Current.ClassName)

    $all = Dump-Tree $root (Join-Path $OutDir 'uia-tree.txt')
    Save-Captures $hwnd

    if ($Mode) {
        $chartMarker = 'manual squeezing'
        $ids = 'chartText', 'vitalGrid', 'orderGrid'
        if ($Mode -eq 'standard') {
            foreach ($id in ($ids + @('medicalMemo', 'patientMemo', 'saveButton', 'patientList'))) {
                Check ($null -ne (Find-ById $root $id)) "AutomationId '$id' is reachable"
            }
            $chart = Find-ById $root 'chartText'
            $chartValue = Get-ValueText $chart
            Check ($null -ne $chartValue -and $chartValue.Contains($chartMarker)) 'chartText ValuePattern returns the chart text'

            $saerok = [string]::Concat([char]0xC0C8, [char]0xB85D)
            $written = "PROBE $([guid]::NewGuid().ToString('N').Substring(0, 8)) $saerok"
            $chart.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($written)

            $cellValue = "ORDER $saerok"
            $cell = Get-GridCell (Find-ById $root 'orderGrid') 0 0
            Check ($null -ne $cell) 'orderGrid exposes GridPattern cell (0,0)'
            if ($null -ne $cell) {
                $cell.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($cellValue)
            }

            $save = Find-ById $root 'saveButton'
            $save.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
            Start-Sleep -Milliseconds 800
            $last = Read-LastSave $dataDir
            Check ($null -ne $last -and $last.patient.chartText -eq $written) 'UIA-written chart text is in the save snapshot'
            Check ($null -ne $last -and $last.patient.orders[0].name -eq $cellValue) 'UIA-written order cell is in the save snapshot'
            $commits = @(Read-Events $dataDir | Where-Object { $_.type -eq 'field_commit' })
            Check (@($commits | Where-Object { $_.field -eq 'chartText' -and $_.value -eq $written }).Count -eq 1) 'chartText field_commit logged once'
            Check (@($commits | Where-Object { $_.field -eq 'orders[0].name' -and $_.value -eq $cellValue }).Count -eq 1) 'orders[0].name field_commit logged once'

            $unsaved = "UNSAVED $saerok"
            $chart.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($unsaved)
            Check (Select-Patient $root 'DEMO-02') 'patientList item DEMO-02 selectable through UIA'
            Check (-not (Get-ValueText (Find-ById $root 'chartText')).Contains($written)) 'switching to DEMO-02 shows its own chart'
            [void](Select-Patient $root 'DEMO-01')
            Check ((Get-ValueText (Find-ById $root 'chartText')) -eq $written) 'switching back shows the saved chart, unsaved edit discarded'
            $commits = @(Read-Events $dataDir | Where-Object { $_.type -eq 'field_commit' })
            Check ($commits.Count -eq 3) "patient loads log no field_commit (got $($commits.Count), expected 3)"

            $reset = Find-ById $root 'resetButton'
            $reset.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
            Start-Sleep -Milliseconds 500
            Check ((Get-ValueText (Find-ById $root 'chartText')).Contains($chartMarker)) 'reset shows the seed chart'
            $last = Read-LastSave $dataDir
            Check ($null -ne $last -and $last.patient.chartText -eq $written) 'reset does not touch the store'

            Check (Select-Patient $root 'DEMO-10') 'form record DEMO-10 selectable'
            Start-Sleep -Milliseconds 800
            [void](Dump-Tree $root (Join-Path $OutDir 'uia-tree-form.txt'))
            Save-Captures $hwnd 'form'
            $layout = Read-FormLayout $dataDir 'csec'
            Check ($null -ne $layout) 'layout-csec.json written'
            # DEMO-10 is the csec form (FormCatalog order); it has every input kind near the top.
            $t = if ($layout) { Get-FormTargets $layout } else { $null }
            $complete = $t -and $t.Text -and $t.Radio -and $t.Check -and $t.Select -and $t.Cell
            Check $complete 'csec layout has text, radio, check, select and grid targets'
            if ($complete) {
                $text = Find-ById $root $t.Text.key
                Check ($null -ne $text) "text $($t.Text.key) reachable"
                if ($text) { $text.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('PROBE FORM') }
                $radio = Find-ById $root "$($t.Radio.key)#1"
                Check ($null -ne $radio) "radio option $($t.Radio.key)#1 reachable"
                if ($radio) { $radio.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
                $check = Find-ById $root "$($t.Check.key)#0"
                Check ($null -ne $check) "check option $($t.Check.key)#0 reachable"
                if ($check) { $check.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle() }
                $combo = Find-ById $root $t.Select.key
                Check ($null -ne $combo) "select $($t.Select.key) reachable"
                if ($combo) {
                    $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
                    Start-Sleep -Milliseconds 300
                    $listCondition = New-Object System.Windows.Automation.PropertyCondition $AE::ControlTypeProperty, ([System.Windows.Automation.ControlType]::ListItem)
                    $choice = @($combo.FindAll([System.Windows.Automation.TreeScope]::Descendants, $listCondition)) |
                        Where-Object { $_.Current.Name -eq $t.Select.options[0] } | Select-Object -First 1
                    Check ($null -ne $choice) "select $($t.Select.key) lists '$($t.Select.options[0])'"
                    if ($choice) { $choice.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
                    $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
                }
                $gridKey = $t.Cell.key.Substring(0, $t.Cell.key.IndexOf('['))
                $gridElement = Find-ById $root $gridKey
                # Column 0 of the standard grid is the row-number column, so form column 0 is grid column 1.
                $cell = if ($gridElement) { Get-GridCell $gridElement 0 1 } else { $null }
                Check ($null -ne $cell) "form grid $gridKey exposes cell (0,1)"
                if ($cell) { $cell.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('PROBECELL') }
                $save.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
                Start-Sleep -Milliseconds 800
                Test-FormSave $dataDir $t 'PROBE FORM' $t.Select.options[0] 'PROBECELL' 'standard'
            }
        } else {
            foreach ($id in $ids) {
                Check ($null -eq (Find-ById $root $id)) "AutomationId '$id' is not exposed"
            }
            $walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
            $panes = @{}
            foreach ($pair in @(@('chart', '(DEMO-01)'), @('vitals', 'VitalSign'), @('orders', 'Routine Order'))) {
                $title = Find-Text $root $pair[1]
                $pane = if ($title) { $walker.GetNextSibling($title) } else { $null }
                $panes[$pair[0]] = $pane
                Check (Test-OpaquePane $pane) "$($pair[0]) area is a pane with no name, stable id, patterns or children"
            }
            Test-Leak $root $hwnd @($chartMarker) 'initial'
            Check ($null -ne (Find-ById $root 'saveButton')) 'saveButton is still reachable'

            if ($panes['chart'] -and $panes['orders']) {
                [void][ProbeNative]::SetForegroundWindow($hwnd)
                Click-In $panes['chart'] -1 -1
                Send-Keys '^a'
                Send-Keys 'PROBE CUSTOM{ENTER}LINE2'
                Click-In $panes['orders'] 40 30
                Send-Keys 'TYLENOL{ENTER}'
                Send-Keys '^s'
                Start-Sleep -Milliseconds 800
                $last = Read-LastSave $dataDir
                Check ($null -ne $last -and $last.patient.chartText -eq "PROBE CUSTOM`nLINE2") 'typed chart text is in the save snapshot'
                Check ($null -ne $last -and $last.patient.orders[0].name -eq 'TYLENOL') 'typed order cell is in the save snapshot'
                $commits = @(Read-Events $dataDir | Where-Object { $_.type -eq 'field_commit' })
                Check (@($commits | Where-Object { $_.field -eq 'chartText' }).Count -eq 1) 'chartText field_commit logged once'
                Check (@($commits | Where-Object { $_.field -eq 'orders[0].name' -and $_.value -eq 'TYLENOL' }).Count -eq 1) 'orders[0].name field_commit logged once'
                Test-Leak $root $hwnd @('PROBE CUSTOM', 'TYLENOL') 'after-typing'
            }

            Check (Select-Patient $root 'DEMO-10') 'form record DEMO-10 selectable'
            Start-Sleep -Milliseconds 800
            Save-Captures $hwnd 'form'
            $layout = Read-FormLayout $dataDir 'csec'
            Check ($null -ne $layout) 'layout-csec.json written'
            $formTitle = Find-Text $root '(DEMO-10)'
            $canvas = if ($formTitle) { $walker.GetNextSibling($formTitle) } else { $null }
            Check (Test-OpaquePane $canvas) 'form canvas is a pane with no name, stable id, patterns or children'
            $t = if ($layout) { Get-FormTargets $layout } else { $null }
            $complete = $t -and $t.Text -and $t.Radio -and $t.Check -and $t.Select -and $t.Cell
            Check $complete 'csec layout has text, radio, check, select and grid targets'
            if ($complete -and $canvas) {
                $origin = $canvas.Current.BoundingRectangle
                $hidden = @(@($t.Text, $t.Radio, $t.Check, $t.Cell, $t.Select) |
                    Where-Object { $_.y + $_.h + 10 -gt $origin.Height -or $_.x + $_.w + 10 -gt $origin.Width })
                $dropdownBottom = $t.Select.y + $t.Select.h + 20 * (@($t.Select.options).Count + 1)
                Check ($hidden.Count -eq 0 -and $dropdownBottom -lt $origin.Height) 'form probe targets are inside the visible canvas'
                function Click-Form($item, [int]$dx = -1, [int]$dy = -1) {
                    $x = if ($dx -ge 0) { $item.x + $dx } else { $item.x + [int]($item.w / 2) }
                    $y = if ($dy -ge 0) { $item.y + $dy } else { $item.y + [int]($item.h / 2) }
                    [ProbeNative]::Click([int]$origin.Left + $x, [int]$origin.Top + $y)
                    Start-Sleep -Milliseconds 300
                }
                [void][ProbeNative]::SetForegroundWindow($hwnd)
                Click-Form $t.Text
                Send-Keys '^a'
                Send-Keys 'PROBE FORM'
                Click-Form $t.Radio 6
                Click-Form $t.Check 6
                Click-Form $t.Select
                # The drop-down opens under the select (CustomFormCanvas.DropdownBounds): 1px border, 20px rows,
                # row 0 is the blank choice and row 1 the first option.
                Click-Form $t.Select 10 ($t.Select.h + 1 + 20 + 10)
                Click-Form $t.Cell
                Send-Keys 'PROBECELL'
                Send-Keys '^s'
                Start-Sleep -Milliseconds 800
                Test-FormSave $dataDir $t 'PROBE FORM' $t.Select.options[0] 'PROBECELL' 'custom'
                Test-Leak $root $hwnd @('PROBE FORM', 'PROBECELL') 'form'
            }
        }
    }
} finally {
    if ($ExePath -and $process -and -not $process.HasExited) { $process.Kill() }
}

if ($script:Failures -gt 0) {
    Write-Host "RESULT  $($script:Failures) check(s) failed"
    exit 1
}
Write-Host 'RESULT  ok'
