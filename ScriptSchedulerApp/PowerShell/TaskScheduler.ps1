# TaskScheduler.ps1
# Handles scheduled task creation and management

function New-ScheduledScriptTask {
    param (
        [Parameter(Mandatory=$true)]
        [string]$TaskName,
        
        [Parameter(Mandatory=$false)]
        [string]$TaskDescription = "",
        
        [Parameter(Mandatory=$true)]
        [string]$ScriptPath,
        
        [Parameter(Mandatory=$true)]
        [string]$ScriptType,
        
        [Parameter(Mandatory=$false)]
        [string]$ScriptArguments = "",
        
        [Parameter(Mandatory=$false)]
        [string]$WorkingDirectory = "",
        
        [Parameter(Mandatory=$false)]
        [bool]$CreateVbsWrapper = $true,
        
        [Parameter(Mandatory=$false)]
        [string]$VbsWrapperDir = "",
        
        [Parameter(Mandatory=$false)]
        [string]$TriggerType = "Once",
        
        [Parameter(Mandatory=$false)]
        [hashtable]$TriggerSettings = @{},
        
        [Parameter(Mandatory=$false)]
        [string]$RunAsUser = "SYSTEM",
        
        [Parameter(Mandatory=$false)]
        [bool]$RunWithHighestPrivileges = $true,
        
        [Parameter(Mandatory=$false)]
        [bool]$Hidden = $false
    )
    
    try {
        # Validate inputs
        if (-not (Test-Path -Path $ScriptPath -PathType Leaf)) {
            Write-Error "Script file not found: $ScriptPath"
            return $false
        }
        
        if ($CreateVbsWrapper -and -not (Test-Path -Path $VbsWrapperDir -PathType Container)) {
            Write-Error "VBS wrapper directory not found: $VbsWrapperDir"
            return $false
        }
        
        # Create the VBS wrapper if requested
        $programPath = $ScriptPath
        $arguments = $ScriptArguments
        
        if ($CreateVbsWrapper) {
            $vbsPath = Join-Path -Path $VbsWrapperDir -ChildPath "$TaskName.vbs"
            $success = New-VbsScriptWrapper -VbsPath $vbsPath -ScriptPath $ScriptPath -ScriptType $ScriptType -Arguments $ScriptArguments -WorkingDirectory $WorkingDirectory
            
            if (-not $success) {
                Write-Error "Failed to create VBS wrapper script"
                return $false
            }
            
            # Update the program path and arguments for the scheduled task
            $programPath = "wscript.exe"
            $arguments = "`"$vbsPath`""
        }
        else {
            # Set up direct script execution
            switch ($ScriptType) {
                "PowerShell" {
                    $programPath = "powershell.exe"
                    $arguments = "-ExecutionPolicy Bypass -NoProfile -File `"$ScriptPath`" $ScriptArguments"
                }
                "Python" {
                    $programPath = "python"
                    $arguments = "`"$ScriptPath`" $ScriptArguments"
                }
                "Batch" {
                    $programPath = "cmd.exe"
                    $arguments = "/c `"$ScriptPath`" $ScriptArguments"
                }
            }
        }
        
        # Set up the action
        $action = New-ScheduledTaskAction -Execute $programPath -Argument $arguments
        if (-not [string]::IsNullOrWhiteSpace($WorkingDirectory)) {
            $action.WorkingDirectory = $WorkingDirectory
        }
        
        # Set up the principal (security context)
        $principal = New-ScheduledTaskPrincipal -UserId $RunAsUser -LogonType S4U -RunLevel Highest
        
        # Set up the trigger based on trigger type
        $trigger = $null
        
        switch ($TriggerType) {
            "Once" {
                $startTime = $TriggerSettings.StartDateTime
                if (-not $startTime) {
                    $startTime = (Get-Date).AddMinutes(5)
                }
                $trigger = New-ScheduledTaskTrigger -Once -At $startTime
            }
            "Daily" {
                $startTime = $TriggerSettings.StartDateTime
                if (-not $startTime) {
                    $startTime = "09:00"
                }
                $daysInterval = $TriggerSettings.DaysInterval
                if (-not $daysInterval) {
                    $daysInterval = 1
                }
                $trigger = New-ScheduledTaskTrigger -Daily -At $startTime -DaysInterval $daysInterval
            }
            "Weekly" {
                $startTime = $TriggerSettings.StartDateTime
                if (-not $startTime) {
                    $startTime = "09:00"
                }
                $daysOfWeek = $TriggerSettings.DaysOfWeek
                if (-not $daysOfWeek) {
                    $daysOfWeek = "Monday"
                }
                $weeksInterval = $TriggerSettings.WeeksInterval
                if (-not $weeksInterval) {
                    $weeksInterval = 1
                }
                $trigger = New-ScheduledTaskTrigger -Weekly -At $startTime -DaysOfWeek $daysOfWeek -WeeksInterval $weeksInterval
            }
            "AtStartup" {
                $trigger = New-ScheduledTaskTrigger -AtStartup
            }
            "AtLogon" {
                $trigger = New-ScheduledTaskTrigger -AtLogon
            }
        }
        
        # Set trigger repetition if specified
        if ($TriggerSettings.RepeatInterval) {
            $repetitionDuration = "P1D" # Default to 1 day
            if ($TriggerSettings.RepeatDuration) {
                $repetitionDuration = $TriggerSettings.RepeatDuration
            }
            
            $trigger.Repetition.Interval = $TriggerSettings.RepeatInterval
            $trigger.Repetition.Duration = $repetitionDuration
        }
        
        # Set task settings
        $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
        
        if ($TriggerSettings.ExecutionTimeLimit) {
            $settings.ExecutionTimeLimit = $TriggerSettings.ExecutionTimeLimit
        }
        else {
            $settings.ExecutionTimeLimit = "PT72H" # Default to 72 hours
        }
        
        if ($Hidden) {
            $settings.Hidden = $true
        }
        
        # Create the scheduled task
        try {
            $task = New-ScheduledTask -Action $action -Principal $principal -Trigger $trigger -Settings $settings -Description $TaskDescription
            Register-ScheduledTask -TaskName $TaskName -InputObject $task -Force
            
            Write-Output "Successfully created scheduled task: $TaskName"
            return $true
        }
        catch {
            Write-Error "Error creating scheduled task: $_"
            return $false
        }
    }
    catch {
        Write-Error "Error in New-ScheduledScriptTask: $_"
        return $false
    }
}

function New-VbsScriptWrapper {
    param (
        [Parameter(Mandatory=$true)]
        [string]$VbsPath,
        
        [Parameter(Mandatory=$true)]
        [string]$ScriptPath,
        
        [Parameter(Mandatory=$true)]
        [string]$ScriptType,
        
        [Parameter(Mandatory=$false)]
        [string]$Arguments = "",
        
        [Parameter(Mandatory=$false)]
        [string]$WorkingDirectory = ""
    )
    
    try {
        # Create VBS content based on script type
        $vbsContent = @"
' VBS Wrapper for hidden script execution
' Generated by Script Scheduler on $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

Option Explicit

Dim WshShell, strCommand, scriptPath, scriptArgs, workingDir

Set WshShell = CreateObject("WScript.Shell")

' Script configuration
scriptPath = "$ScriptPath"
scriptArgs = "$Arguments"
workingDir = "$WorkingDirectory"

' Prepare command based on script type
"@

        # Add script type specific command
        switch ($ScriptType) {
            "PowerShell" {
                $vbsContent += @"

' PowerShell execution
strCommand = "powershell.exe -ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File """ & scriptPath & """ " & scriptArgs

"@
            }
            "Python" {
                $vbsContent += @"

' Python execution
strCommand = "python """ & scriptPath & """ " & scriptArgs

"@
            }
            "Batch" {
                $vbsContent += @"

' Batch execution
strCommand = "cmd.exe /c """ & scriptPath & """ " & scriptArgs

"@
            }
        }

        # Add execution code
        $vbsContent += @"

' Execute the script hidden
If workingDir <> "" Then
    WshShell.CurrentDirectory = workingDir
End If

WshShell.Run strCommand, 0, True
"@

        # Write the VBS file
        try {
            $vbsContent | Out-File -FilePath $VbsPath -Encoding utf8 -Force
            Write-Output "Successfully created VBS wrapper: $VbsPath"
            return $true
        }
        catch {
            Write-Error "Error writing VBS file: $_"
            return $false
        }
    }
    catch {
        Write-Error "Error in New-VbsScriptWrapper: $_"
        return $false
    }
}

function Test-TaskSchedulerAccess {
    try {
        # Try to get a list of scheduled tasks to test access
        Get-ScheduledTask -TaskPath "\" -ErrorAction Stop | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Invoke-ElevatedCommand {
    param (
        [Parameter(Mandatory=$true)]
        [string]$Command
    )
    
    try {
        # Create a temporary script file
        $tempScriptPath = [System.IO.Path]::GetTempFileName() -replace '\.tmp$', '.ps1'
        
        # Write the command to the temp script
        $Command | Out-File -FilePath $tempScriptPath -Encoding utf8 -Force
        
        # Execute the command with elevated privileges
        $processInfo = New-Object System.Diagnostics.ProcessStartInfo
        $processInfo.FileName = "powershell.exe"
        $processInfo.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$tempScriptPath`""
        $processInfo.Verb = "runas"
        $processInfo.UseShellExecute = $true
        
        # Start the process
        $process = [System.Diagnostics.Process]::Start($processInfo)
        $process.WaitForExit()
        
        # Clean up the temp file
        if (Test-Path -Path $tempScriptPath) {
            Remove-Item -Path $tempScriptPath -Force
        }
        
        return ($process.ExitCode -eq 0)
    }
    catch {
        Write-Error "Error in Invoke-ElevatedCommand: $_"
        return $false
    }
}

# Export functions
Export-ModuleMember -Function New-ScheduledScriptTask, New-VbsScriptWrapper, Test-TaskSchedulerAccess, Invoke-ElevatedCommand