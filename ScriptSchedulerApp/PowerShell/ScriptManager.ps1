# ScriptManager.ps1
# Handles script discovery and management operations

function Get-ScriptList {
    param (
        [Parameter(Mandatory=$true)]
        [string]$NetworkSharePath,
        
        [Parameter(Mandatory=$false)]
        [string]$ScriptType = "All",
        
        [Parameter(Mandatory=$false)]
        [string]$NameFilter = ""
    )
    
    try {
        # Check if network path exists
        if (-not (Test-Path -Path $NetworkSharePath)) {
            Write-Error "Network share path not found: $NetworkSharePath"
            return $null
        }
        
        # Get all scripts from network share
        $supportedExtensions = @('.ps1', '.py', '.bat', '.cmd')
        $allFiles = Get-ChildItem -Path $NetworkSharePath -File | 
                    Where-Object { $_.Extension -in $supportedExtensions }
        
        # Filter by script type if specified
        if ($ScriptType -ne "All") {
            switch ($ScriptType) {
                "PowerShell" { $allFiles = $allFiles | Where-Object { $_.Extension -eq '.ps1' } }
                "Python" { $allFiles = $allFiles | Where-Object { $_.Extension -eq '.py' } }
                "Batch" { $allFiles = $allFiles | Where-Object { $_.Extension -in @('.bat', '.cmd') } }
            }
        }
        
        # Filter by name if specified
        if (-not [string]::IsNullOrWhiteSpace($NameFilter)) {
            $allFiles = $allFiles | Where-Object { $_.Name -like "*$NameFilter*" }
        }
        
        # Create script objects with metadata
        $scripts = @()
        foreach ($file in $allFiles) {
            $type = switch ($file.Extension) {
                ".ps1" { "PowerShell" }
                ".py"  { "Python" }
                ".bat" { "Batch" }
                ".cmd" { "Batch" }
                default { "Unknown" }
            }
            
            $scripts += [PSCustomObject]@{
                Name = $file.Name
                Type = $type
                Path = $file.FullName
                LastModified = $file.LastWriteTime
                SizeKB = [Math]::Round($file.Length / 1KB, 2)
            }
        }
        
        return $scripts
    }
    catch {
        Write-Error "Error retrieving script list: $_"
        return $null
    }
}

function Get-ScriptDetails {
    param (
        [Parameter(Mandatory=$true)]
        [string]$ScriptPath
    )
    
    try {
        # Check if file exists
        if (-not (Test-Path -Path $ScriptPath -PathType Leaf)) {
            Write-Error "Script file not found: $ScriptPath"
            return $null
        }
        
        $file = Get-Item -Path $ScriptPath
        
        # Determine script type
        $type = switch ($file.Extension) {
            ".ps1" { "PowerShell" }
            ".py"  { "Python" }
            ".bat" { "Batch" }
            ".cmd" { "Batch" }
            default { "Unknown" }
        }
        
        # Read first 50 lines of script to extract description
        $content = Get-Content -Path $ScriptPath -TotalCount 50 -ErrorAction SilentlyContinue
        
        # Extract description from script headers
        $description = ""
        if ($type -eq "PowerShell") {
            # Look for PowerShell comment-based help or simple comments
            $descLines = @()
            $inCommentBlock = $false
            
            foreach ($line in $content) {
                if ($line -match "^#\s*(.+)$" -and -not $inCommentBlock) {
                    $descLines += $Matches[1].Trim()
                }
                elseif ($line -match "<#") {
                    $inCommentBlock = $true
                }
                elseif ($line -match "#>") {
                    $inCommentBlock = $false
                }
                elseif ($inCommentBlock) {
                    $descLines += $line.Trim()
                }
            }
            
            if ($descLines.Count -gt 0) {
                $description = $descLines -join "`n"
            }
        }
        elseif ($type -eq "Python") {
            # Look for Python docstrings or comments
            $descLines = @()
            $inDocString = $false
            
            foreach ($line in $content) {
                if ($line -match '^#\s*(.+)$' -and -not $inDocString) {
                    $descLines += $Matches[1].Trim()
                }
                elseif ($line -match '"""' -and -not $inDocString) {
                    $inDocString = $true
                    $descLines += $line.Replace('"""', '').Trim()
                }
                elseif ($line -match '"""' -and $inDocString) {
                    $inDocString = $false
                    $descLines += $line.Replace('"""', '').Trim()
                }
                elseif ($inDocString) {
                    $descLines += $line.Trim()
                }
            }
            
            if ($descLines.Count -gt 0) {
                $description = $descLines -join "`n"
            }
        }
        elseif ($type -eq "Batch") {
            # Look for REM comments or :: comments
            $descLines = @()
            
            foreach ($line in $content) {
                if ($line -match "^(?:REM|::)\s*(.+)$") {
                    $descLines += $Matches[1].Trim()
                }
            }
            
            if ($descLines.Count -gt 0) {
                $description = $descLines -join "`n"
            }
        }
        
        # Create script details object
        $scriptDetails = [PSCustomObject]@{
            Name = $file.Name
            Type = $type
            Path = $file.FullName
            Description = $description
            LastModified = $file.LastWriteTime
            SizeKB = [Math]::Round($file.Length / 1KB, 2)
            DirectoryName = $file.DirectoryName
        }
        
        return $scriptDetails
    }
    catch {
        Write-Error "Error retrieving script details: $_"
        return $null
    }
}

# Export functions
Export-ModuleMember -Function Get-ScriptList, Get-ScriptDetails