# Logger.ps1
# Provides logging functionality for the application

function Write-Log {
    param (
        [Parameter(Mandatory=$true)]
        [string]$Message,
        
        [Parameter(Mandatory=$false)]
        [ValidateSet("INFO", "WARNING", "ERROR")]
        [string]$Level = "INFO",
        
        [Parameter(Mandatory=$false)]
        [string]$LogFilePath = $null
    )
    
    try {
        # Use default log file if not specified
        if ([string]::IsNullOrWhiteSpace($LogFilePath)) {
            $logDir = Join-Path -Path $env:LOCALAPPDATA -ChildPath "ScriptSchedulerApp\Logs"
            
            if (-not (Test-Path -Path $logDir)) {
                New-Item -Path $logDir -ItemType Directory -Force | Out-Null
            }
            
            $LogFilePath = Join-Path -Path $logDir -ChildPath "Log_$(Get-Date -Format 'yyyy-MM-dd').log"
        }
        
        # Ensure log directory exists
        $logDir = Split-Path -Path $LogFilePath -Parent
        if (-not (Test-Path -Path $logDir)) {
            New-Item -Path $logDir -ItemType Directory -Force | Out-Null
        }
        
        # Format the log message
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $logMessage = "[$timestamp] [$Level] $Message"
        
        # Write to log file
        $logMessage | Out-File -FilePath $LogFilePath -Append -Encoding UTF8 -Force
        
        # Output to console
        switch ($Level) {
            "INFO" { Write-Host $logMessage }
            "WARNING" { Write-Host $logMessage -ForegroundColor Yellow }
            "ERROR" { Write-Host $logMessage -ForegroundColor Red }
        }
        
        return $true
    }
    catch {
        Write-Error "Failed to write to log file: $_"
        return $false
    }
}

function Get-LogContent {
    param (
        [Parameter(Mandatory=$false)]
        [string]$LogFilePath = $null,
        
        [Parameter(Mandatory=$false)]
        [int]$MaxLines = 1000
    )
    
    try {
        # Use default log file if not specified
        if ([string]::IsNullOrWhiteSpace($LogFilePath)) {
            $logDir = Join-Path -Path $env:LOCALAPPDATA -ChildPath "ScriptSchedulerApp\Logs"
            $LogFilePath = Join-Path -Path $logDir -ChildPath "Log_$(Get-Date -Format 'yyyy-MM-dd').log"
        }
        
        # Check if log file exists
        if (-not (Test-Path -Path $LogFilePath -PathType Leaf)) {
            return @()
        }
        
        # Get content with specified line limit
        $content = Get-Content -Path $LogFilePath -Tail $MaxLines -ErrorAction Stop
        return $content
    }
    catch {
        Write-Error "Failed to read log file: $_"
        return @()
    }
}

# Export functions
Export-ModuleMember -Function Write-Log, Get-LogContent