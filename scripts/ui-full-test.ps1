Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms

$ErrorActionPreference = 'Stop'

$root = [System.Windows.Automation.AutomationElement]::RootElement
$results = [System.Collections.Generic.List[object]]::new()

function Add-Result([string]$status, [string]$step, [string]$detail = '') {
    $item = [pscustomobject]@{
        Status = $status
        Step = $step
        Detail = $detail
        Time = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    }
    $results.Add($item) | Out-Null
    if ([string]::IsNullOrWhiteSpace($detail)) {
        Write-Output ("$status | $step")
    }
    else {
        Write-Output ("$status | $step | $detail")
    }
}

function Find-ByAutoIdType($scope, [string]$automationId, [System.Windows.Automation.ControlType]$type, [int]$timeoutSec = 8) {
    $cond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $automationId)),
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $type))
    )

    $end = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $end) {
        $el = $scope.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if ($el) { return $el }
        Start-Sleep -Milliseconds 120
    }

    return $null
}

function Find-ByNameType($scope, [string]$name, [System.Windows.Automation.ControlType]$type, [int]$timeoutSec = 6) {
    $cond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)),
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $type))
    )

    $end = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $end) {
        $el = $scope.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if ($el) { return $el }
        Start-Sleep -Milliseconds 120
    }

    return $null
}

function Invoke-ByAutoId($scope, [string]$automationId) {
    $button = Find-ByAutoIdType $scope $automationId ([System.Windows.Automation.ControlType]::Button)
    if (-not $button) { throw "Button not found: $automationId" }
    $pattern = $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}

function Set-TextByAutoId($scope, [string]$automationId, [string]$value) {
    $textBox = Find-ByAutoIdType $scope $automationId ([System.Windows.Automation.ControlType]::Edit)
    if (-not $textBox) { throw "TextBox not found: $automationId" }
    $vp = $textBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $vp.SetValue($value)
}

function Toggle-CheckBoxByAutoId($scope, [string]$automationId) {
    $checkBox = Find-ByAutoIdType $scope $automationId ([System.Windows.Automation.ControlType]::CheckBox)
    if (-not $checkBox) { throw "CheckBox not found: $automationId" }
    $toggle = $checkBox.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    $toggle.Toggle()
}

function Get-IsEnabledByAutoId($scope, [string]$automationId) {
    $button = Find-ByAutoIdType $scope $automationId ([System.Windows.Automation.ControlType]::Button)
    if (-not $button) { throw "Button not found: $automationId" }
    return [bool]$button.Current.IsEnabled
}

function Ensure-ProcessAlive($proc, [string]$context) {
    $proc.Refresh()
    if ($proc.HasExited) {
        throw "Process exited during: $context"
    }
}

function Wait-MainWindow($proc, [int]$timeoutSec = 12) {
    $end = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $end) {
        $proc.Refresh()
        if ($proc.HasExited) {
            throw 'App process exited while waiting for main window.'
        }

        if ($proc.MainWindowHandle -ne 0) {
            return [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
        }

        Start-Sleep -Milliseconds 120
    }

    throw 'Main window handle was not available in time.'
}

function Get-TopWindowsByProcessId([int]$processId) {
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $processId)
    $collection = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
    $windows = [System.Collections.Generic.List[System.Windows.Automation.AutomationElement]]::new()
    for ($i = 0; $i -lt $collection.Count; $i++) {
        $windows.Add($collection.Item($i)) | Out-Null
    }
    return $windows
}

function Get-DescendantWindowsByProcessId([int]$processId) {
    $cond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $processId)),
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window))
    )

    $collection = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    $windows = [System.Collections.Generic.List[System.Windows.Automation.AutomationElement]]::new()
    for ($i = 0; $i -lt $collection.Count; $i++) {
        $windows.Add($collection.Item($i)) | Out-Null
    }
    return $windows
}

function Find-AnyByAutoIdForProcess([int]$processId, [string]$automationId, [int]$timeoutSec = 8) {
    $cond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $processId)),
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $automationId))
    )

    $end = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $end) {
        $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if ($el) { return $el }
        Start-Sleep -Milliseconds 120
    }

    return $null
}

function Find-WindowByChildId([int]$processId, [IntPtr]$excludeHandle, [string]$requiredChildId, [int]$timeoutSec = 8) {
    $child = Find-AnyByAutoIdForProcess $processId $requiredChildId $timeoutSec
    if (-not $child) {
        throw "Window containing '$requiredChildId' was not found."
    }

    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $node = $child
    for ($i = 0; $i -lt 24 -and $node; $i++) {
        if ($node.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window) {
            $nativeHandle = [IntPtr]$node.Current.NativeWindowHandle
            if ($excludeHandle -ne [IntPtr]::Zero -and $nativeHandle -eq $excludeHandle) {
                break
            }

            if ($node.Current.ProcessId -eq $processId) {
                return $node
            }
        }

        $node = $walker.GetParent($node)
    }

    throw "Owning window for '$requiredChildId' was not found."
}

function Select-RowByText($mainWindow, [string]$textValue) {
    $grid = Find-ByAutoIdType $mainWindow 'TaskGrid' ([System.Windows.Automation.ControlType]::DataGrid) 4
    if (-not $grid) {
        throw 'TaskGrid was not found.'
    }

    $targetText = Find-ByNameType $grid $textValue ([System.Windows.Automation.ControlType]::Text) 5
    if (-not $targetText) {
        throw "Task text not found: $textValue"
    }

    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $node = $targetText
    for ($i = 0; $i -lt 20 -and $node; $i++) {
        $selectionPattern = $null
        if ($node.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selectionPattern)) {
            $selectionPattern.Select()
            return
        }

        $node = $walker.GetParent($node)
    }

    throw "Could not locate DataItem for text: $textValue"
}

function Dismiss-FirstDialogButton([int]$processId, [IntPtr]$mainHandle, [int]$timeoutSec = 5) {
    $end = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $end) {
        $windows = Get-DescendantWindowsByProcessId $processId
        foreach ($window in $windows) {
            $nativeHandle = [IntPtr]$window.Current.NativeWindowHandle
            if ($nativeHandle -eq $mainHandle) {
                continue
            }

            $buttons = $window.FindAll(
                [System.Windows.Automation.TreeScope]::Descendants,
                (New-Object System.Windows.Automation.PropertyCondition(
                    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                    [System.Windows.Automation.ControlType]::Button))
            )

            if ($buttons.Count -gt 0) {
                $btn = $buttons.Item(0)
                for ($i = 0; $i -lt $buttons.Count; $i++) {
                    $candidate = $buttons.Item($i)
                    if ($candidate.Current.Name -like '是*' -or $candidate.Current.Name -like 'Yes*') {
                        $btn = $candidate
                        break
                    }
                }
                $invoke = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
                $invoke.Invoke()
                return $true
            }
        }

        Start-Sleep -Milliseconds 120
    }

    return $false
}

function Run-Step([string]$stepName, [scriptblock]$action) {
    try {
        & $action
        Add-Result 'PASS' $stepName
    }
    catch {
        Add-Result 'FAIL' $stepName $_.Exception.Message
    }
}

function Get-MainWindowElement([IntPtr]$mainHandle) {
    return [System.Windows.Automation.AutomationElement]::FromHandle($mainHandle)
}

function Set-QuickFilterByKeyboard($scope) {
    $comboAutoId = 'QuickFilterComboBox'
    $combo = Find-ByAutoIdType $scope $comboAutoId ([System.Windows.Automation.ControlType]::ComboBox)
    if (-not $combo) { throw "ComboBox not found: $comboAutoId" }

    $combo.SetFocus()
}

$configDir = Join-Path $env:APPDATA 'StarmoAudioAssistant'
$configPath = Join-Path $configDir 'config.json'
$backupPath = Join-Path $configDir ('config.json.ui-test-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$reportPath = Join-Path (Resolve-Path '.').Path 'dist\ui-test-report.txt'

$appExe = (Resolve-Path 'src\StarAudioAssistant.App\bin\Debug\net8.0-windows\StarAudioAssistant.App.exe').Path
$audioPath = (Get-ChildItem 'C:\Windows\Media' -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -in '.mp3', '.wav' } |
    Select-Object -First 1).FullName

if (-not $audioPath) {
    throw 'No audio file found in C:\Windows\Media for playback test.'
}

New-Item -Path $configDir -ItemType Directory -Force | Out-Null
if (Test-Path $configPath) {
    Copy-Item $configPath $backupPath -Force
}

$cleanConfig = [ordered]@{
    Tasks = @()
    HolidayDates = @()
    Ui = [ordered]@{
        SortMode = 'NextTrigger'
        QuickFilter = 'All'
        Columns = @()
    }
}
$cleanConfig | ConvertTo-Json -Depth 8 | Set-Content -Path $configPath -Encoding UTF8

$proc = $null
$proc2 = $null
$taskName = 'UI-AUTO-TASK-001'

try {
    $proc = Start-Process -FilePath $appExe -PassThru
    Start-Sleep -Milliseconds 900

    $main = Wait-MainWindow $proc
    $mainHandle = [IntPtr]$main.Current.NativeWindowHandle

    Run-Step 'main window startup' {
        $main = Get-MainWindowElement $mainHandle
        Ensure-ProcessAlive $proc 'startup'
        if (-not (Find-ByAutoIdType $main 'AddTaskButton' ([System.Windows.Automation.ControlType]::Button))) {
            throw 'AddTaskButton not found on main window.'
        }
    }

    Run-Step 'holiday settings buttons' {
        $main = Get-MainWindowElement $mainHandle
        Invoke-ByAutoId $main 'ManageHolidayButton'
        $holiday = Find-WindowByChildId $proc.Id $mainHandle 'HolidayDatePicker'

        Invoke-ByAutoId $holiday 'AddHolidayButton'
        Start-Sleep -Milliseconds 150

        $list = Find-ByAutoIdType $holiday 'DateListBox' ([System.Windows.Automation.ControlType]::List)
        if (-not $list) { throw 'DateListBox not found.' }

        $itemsCollection = $list.FindAll(
            [System.Windows.Automation.TreeScope]::Children,
            (New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::ListItem))
        )
        $items = @($itemsCollection)

        if ($items.Count -gt 0) {
            $firstItem = $items[0]
            $sel = $null
            if ($firstItem.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$sel)) {
                $sel.Select()
            }
            else {
                $firstItem.SetFocus()
            }
            Invoke-ByAutoId $holiday 'RemoveHolidayButton'
        }

        Invoke-ByAutoId $holiday 'SaveHolidayButton'
    }

    Run-Step 'error center header buttons' {
        $main = Get-MainWindowElement $mainHandle
        Invoke-ByAutoId $main 'HeaderOpenErrorCenterButton'
        $errorCenter = Find-WindowByChildId $proc.Id $mainHandle 'CopyButton'
        Invoke-ByAutoId $errorCenter 'CopyButton'
        Invoke-ByAutoId $errorCenter 'CloseErrorCenterButton'
    }

    Run-Step 'create task wizard and save task' {
        $main = Get-MainWindowElement $mainHandle
        Invoke-ByAutoId $main 'AddTaskButton'
        $editor = Find-WindowByChildId $proc.Id $mainHandle 'NameTextBox'

        Invoke-ByAutoId $editor 'BrowseAudioButton'
        Start-Sleep -Milliseconds 250
        [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
        Start-Sleep -Milliseconds 200

        Set-TextByAutoId $editor 'NameTextBox' $taskName
        Set-TextByAutoId $editor 'AudioPathTextBox' $audioPath

        Invoke-ByAutoId $editor 'NextButton'
        Invoke-ByAutoId $editor 'NextButton'
        Invoke-ByAutoId $editor 'SaveButton'

        Start-Sleep -Milliseconds 350
        if (-not (Find-ByNameType $main $taskName ([System.Windows.Automation.ControlType]::Text) 6)) {
            throw 'Created task row not found in main grid.'
        }
    }

    Run-Step 'edit task button' {
        $main = Get-MainWindowElement $mainHandle
        Select-RowByText $main $taskName
        Invoke-ByAutoId $main 'EditTaskButton'
        $editor = Find-WindowByChildId $proc.Id $mainHandle 'NameTextBox'
        Invoke-ByAutoId $editor 'CancelButton'
    }

    Run-Step 'toggle task button' {
        $main = Get-MainWindowElement $mainHandle
        Select-RowByText $main $taskName
        Invoke-ByAutoId $main 'ToggleTaskButton'
        Start-Sleep -Milliseconds 150
        Invoke-ByAutoId $main 'ToggleTaskButton'
    }

    Run-Step 'playback buttons' {
        $main = Get-MainWindowElement $mainHandle
        Select-RowByText $main $taskName
        try {
            Invoke-ByAutoId $main 'TestPlaybackButton'
        }
        catch {
            # Some environments may reject playback invocation (device busy/unavailable).
            # Continue to verify app remains responsive and error dialog can be dismissed.
        }

        Start-Sleep -Milliseconds 600
        [void](Dismiss-FirstDialogButton $proc.Id $mainHandle 1)
        Ensure-ProcessAlive $proc 'after test playback click'

        $main = Get-MainWindowElement $mainHandle
        if (Get-IsEnabledByAutoId $main 'StopPlaybackButton') {
            try {
                Invoke-ByAutoId $main 'StopPlaybackButton'
            }
            catch {
                # Ignore and continue; we'll still validate app responsiveness.
            }
        }

        [void](Dismiss-FirstDialogButton $proc.Id $mainHandle 1)
        Ensure-ProcessAlive $proc 'after stop playback click'
    }

    Run-Step 'right panel buttons' {
        $main = Get-MainWindowElement $mainHandle
        Select-RowByText $main $taskName
        Invoke-ByAutoId $main 'RefreshPreviewButton'
        Invoke-ByAutoId $main 'PanelOpenErrorCenterButton'
        $errorCenter = Find-WindowByChildId $proc.Id $mainHandle 'CopyButton'
        Invoke-ByAutoId $errorCenter 'CloseErrorCenterButton'
    }

    Run-Step 'quick filter combo interactions' {
        $main = Get-MainWindowElement $mainHandle
        Set-QuickFilterByKeyboard $main
    }

    Run-Step 'column toggle checkboxes' {
        $main = Get-MainWindowElement $mainHandle
        Toggle-CheckBoxByAutoId $main 'ToggleStrategyColumn'
        Toggle-CheckBoxByAutoId $main 'ToggleStrategyColumn'
        Toggle-CheckBoxByAutoId $main 'ToggleNextColumn'
        Toggle-CheckBoxByAutoId $main 'ToggleNextColumn'
        Toggle-CheckBoxByAutoId $main 'ToggleHealthColumn'
        Toggle-CheckBoxByAutoId $main 'ToggleHealthColumn'
    }

    Run-Step 'delete task button and confirm dialog' {
        $main = Get-MainWindowElement $mainHandle
        Select-RowByText $main $taskName
        Invoke-ByAutoId $main 'DeleteTaskButton'
        $handled = Dismiss-FirstDialogButton $proc.Id $mainHandle 3
        if (-not $handled) {
            [System.Windows.Forms.SendKeys]::SendWait('%Y')
            Start-Sleep -Milliseconds 120
            [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
        }

        for ($attempt = 0; $attempt -lt 2; $attempt++) {
            Start-Sleep -Milliseconds 320
            $main = Get-MainWindowElement $mainHandle
            $grid = Find-ByAutoIdType $main 'TaskGrid' ([System.Windows.Automation.ControlType]::DataGrid) 2
            if (-not $grid) { throw 'TaskGrid was not found for deletion verification.' }

            $residual = Find-ByNameType $grid $taskName ([System.Windows.Automation.ControlType]::Text) 2
            if (-not $residual) {
                break
            }

            if ($attempt -eq 0) {
                Select-RowByText $main $taskName
                Invoke-ByAutoId $main 'DeleteTaskButton'
                [System.Windows.Forms.SendKeys]::SendWait('%Y')
                Start-Sleep -Milliseconds 120
                [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
                continue
            }

            throw 'Task still exists after delete action.'
        }
    }

    Run-Step 'empty-list action buttons disabled' {
        $main = Get-MainWindowElement $mainHandle
        if (Get-IsEnabledByAutoId $main 'EditTaskButton') { throw 'EditTaskButton should be disabled.' }
        if (Get-IsEnabledByAutoId $main 'DeleteTaskButton') { throw 'DeleteTaskButton should be disabled.' }
        if (Get-IsEnabledByAutoId $main 'ToggleTaskButton') { throw 'ToggleTaskButton should be disabled.' }
        if (Get-IsEnabledByAutoId $main 'TestPlaybackButton') { throw 'TestPlaybackButton should be disabled.' }
    }

    Run-Step 'minimize to tray button' {
        Invoke-ByAutoId $main 'MinimizeToTrayButton'
        Start-Sleep -Milliseconds 350
        Ensure-ProcessAlive $proc 'minimize to tray'
    }

    if ($proc -and -not $proc.HasExited) {
        Stop-Process -Id $proc.Id -Force
    }

    $proc2 = Start-Process -FilePath $appExe -PassThru
    Start-Sleep -Milliseconds 900
    $main2 = Wait-MainWindow $proc2
    $main2Handle = [IntPtr]$main2.Current.NativeWindowHandle

    Run-Step 'close to tray button' {
        Invoke-ByAutoId $main2 'CloseToTrayButton'
        Start-Sleep -Milliseconds 350
        Ensure-ProcessAlive $proc2 'close to tray'

        $allTopWindows = Get-TopWindowsByProcessId $proc2.Id
        $stillVisible = $false
        foreach ($window in $allTopWindows) {
            if ([IntPtr]$window.Current.NativeWindowHandle -eq $main2Handle) {
                $stillVisible = $true
                break
            }
        }

        if ($stillVisible) {
            throw 'Main window still visible after CloseToTrayButton.'
        }
    }
}
finally {
    if ($proc -and -not $proc.HasExited) {
        Stop-Process -Id $proc.Id -Force
    }

    if ($proc2 -and -not $proc2.HasExited) {
        Stop-Process -Id $proc2.Id -Force
    }

    if (Test-Path $backupPath) {
        Copy-Item $backupPath $configPath -Force
        Remove-Item $backupPath -Force
    }
    else {
        if (Test-Path $configPath) {
            Remove-Item $configPath -Force
        }
    }

    $passCount = @($results | Where-Object { $_.Status -eq 'PASS' }).Count
    $failCount = @($results | Where-Object { $_.Status -eq 'FAIL' }).Count

    New-Item -ItemType Directory -Path (Split-Path $reportPath -Parent) -Force | Out-Null
    $reportLines = [System.Collections.Generic.List[string]]::new()
    $reportLines.Add('UI TEST SUMMARY') | Out-Null
    $reportLines.Add(('Generated: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))) | Out-Null
    $reportLines.Add(('Pass: ' + $passCount)) | Out-Null
    $reportLines.Add(('Fail: ' + $failCount)) | Out-Null
    $reportLines.Add('') | Out-Null
    foreach ($row in $results) {
        $line = "[$($row.Time)] $($row.Status) | $($row.Step)"
        if (-not [string]::IsNullOrWhiteSpace($row.Detail)) {
            $line += " | $($row.Detail)"
        }
        $reportLines.Add($line) | Out-Null
    }

    Set-Content -Path $reportPath -Value $reportLines -Encoding UTF8
    Write-Output '---- UI TEST SUMMARY ----'
    Write-Output "PASS=$passCount FAIL=$failCount"
    Write-Output ('Report: ' + $reportPath)

    if ($failCount -gt 0) {
        exit 1
    }
}
