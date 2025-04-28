# ScriptDiscovery.ps1
# Script for discovering scripts in network shares and subdirectories

function Get-NetworkScripts {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string]$RootPath,
        
        [Parameter(Mandatory = $false)]
        [string]$ScriptType = "All",
        
        [Parameter(Mandatory = $false)]
        [string]$NameFilter = ""
    )
    
    try {
        Write-Output "Searching for scripts in: $RootPath"
        
        # Verify the network path exists and is accessible
        if (-not (Test-Path -Path $RootPath)) {
            Write-Error "Network path not found or not accessible: $RootPath"
            return @()
        }
        
        # Define script extensions to search for
        $extensions = @("*.ps1", "*.py", "*.bat", "*.cmd", "*.vbs", "*.js")
        
        # Filter by type if specified
        if ($ScriptType -ne "All") {
            switch ($ScriptType) {
                "PowerShell" { $extensions = @("*.ps1") }
                "Python" { $extensions = @("*.py") }
                "Batch" { $extensions = @("*.bat", "*.cmd") }
                "VBScript" { $extensions = @("*.vbs") }
                "JavaScript" { $extensions = @("*.js") }
            }
        }
        
        # Find all script files recursively
        $allFiles = @()
        
        # Try to access the network path with a timeout to avoid hanging
        $accessTimeout = New-TimeSpan -Seconds 10
        $accessTimer = [System.Diagnostics.Stopwatch]::StartNew()
        
        foreach ($ext in $extensions) {
            # Break if the timeout has been reached
            if ($accessTimer.Elapsed -gt $accessTimeout) {
                Write-Warning "Timed out while accessing network path: $RootPath"
                break
            }
            
            # Use -Recurse to search subdirectories, avoid hanging on inaccessible paths
            try {
                $files = Get-ChildItem -Path $RootPath -Filter $ext -Recurse -File -ErrorAction Stop
                $allFiles += $files
            }
            catch {
                Write-Warning "Error accessing path with filter $ext: $_"
            }
        }
        
        # Apply name filter if specified
        if (-not [string]::IsNullOrWhiteSpace($NameFilter)) {
            $allFiles = $allFiles | Where-Object { $_.Name -like "*$NameFilter*" }
        }
        
        # Exit early if no files found
        if ($allFiles.Count -eq 0) {
            Write-Warning "No script files found in $RootPath"
            return @()
        }
        
        # Create script objects with metadata
        $scripts = @()
        foreach ($file in $allFiles) {
            $type = switch ($file.Extension.ToLower()) {
                ".ps1" { "PowerShell" }
                ".py"  { "Python" }
                ".bat" { "Batch" }
                ".cmd" { "Batch" }
                ".vbs" { "VBScript" }
                ".js"  { "JavaScript" }
                default { "Unknown" }
            }
            
            # Basic safe description in case extraction fails
            $safeDescription = "$type script. Created: $(Get-Date -Date $file.CreationTime -Format 'MM/dd/yyyy'). Last modified: $(Get-Date -Date $file.LastWriteTime -Format 'MM/dd/yyyy')."
            
            # Try to extract detailed description with timeout
            $descriptionTimeout = New-TimeSpan -Seconds 5
            $descTimer = [System.Diagnostics.Stopwatch]::StartNew()
            $description = $safeDescription
            
            try {
                # Use a reasonable timeout for description extraction
                $extractTask = [System.Threading.Tasks.Task]::Run({ Get-ScriptDescription -FilePath $file.FullName -ScriptType $type })
                
                if ([System.Threading.Tasks.Task]::WaitAll(@($extractTask), 5000)) {
                    # Description was extracted successfully
                    if (-not [string]::IsNullOrWhiteSpace($extractTask.Result)) {
                        $description = $extractTask.Result
                    }
                }
                else {
                    Write-Warning "Timed out extracting description for: $($file.Name)"
                }
            }
            catch {
                Write-Warning "Error extracting description for $($file.Name): $_"
            }
            
            # Create the script object
            $scripts += [PSCustomObject]@{
                Name = $file.Name
                Type = $type
                Path = $file.FullName
                LastModified = $file.LastWriteTime
                SizeKB = [Math]::Round($file.Length / 1KB, 2)
                Description = $description
            }
        }
        
        return $scripts
    }
    catch {
        Write-Error "Error searching for scripts: $_"
        return @()
    }
}

function Get-ScriptDescription {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        
        [Parameter(Mandatory = $true)]
        [string]$ScriptType
    )
    
    try {
        # Read first 50 lines of the script (or fewer if the file is smaller)
        $content = Get-Content -Path $FilePath -TotalCount 50 -ErrorAction SilentlyContinue
        
        if (-not $content) {
            return "No description available"
        }
        
        $description = ""
        
        switch ($ScriptType) {
            "PowerShell" {
                # Look for comment block or comment-based help
                $inCommentBlock = $false
                $descLines = @()
                
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
            "Python" {
                # Look for docstrings or comments
                $inDocString = $false
                $descLines = @()
                
                foreach ($line in $content) {
                    if ($line -match '^#\s*(.+)$' -and -not $inDocString) {
                        $descLines += $Matches[1].Trim()
                    }
                    elseif ($line -match '"""' -and -not $inDocString) {
                        $inDocString = $true
                        $line = $line -replace '"""', ''
                        if ($line.Trim()) {
                            $descLines += $line.Trim()
                        }
                    }
                    elseif ($line -match '"""' -and $inDocString) {
                        $inDocString = $false
                        $line = $line -replace '"""', ''
                        if ($line.Trim()) {
                            $descLines += $line.Trim()
                        }
                    }
                    elseif ($inDocString) {
                        $descLines += $line.Trim()
                    }
                }
                
                if ($descLines.Count -gt 0) {
                    $description = $descLines -join "`n"
                }
            }
            "Batch" {
                # Look for REM or :: comments
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
            "VBScript" {
                # Look for apostrophe comments
                $descLines = @()
                
                foreach ($line in $content) {
                    if ($line -match "^'\s*(.+)$") {
                        $descLines += $Matches[1].Trim()
                    }
                }
                
                if ($descLines.Count -gt 0) {
                    $description = $descLines -join "`n"
                }
            }
            "JavaScript" {
                # Look for // or /* */ comments
                $inCommentBlock = $false
                $descLines = @()
                
                foreach ($line in $content) {
                    if ($line -match "^//\s*(.+)$" -and -not $inCommentBlock) {
                        $descLines += $Matches[1].Trim()
                    }
                    elseif ($line -match "/\*") {
                        $inCommentBlock = $true
                        $line = $line -replace "/\*", ""
                        if ($line.Trim()) {
                            $descLines += $line.Trim()
                        }
                    }
                    elseif ($line -match "\*/") {
                        $inCommentBlock = $false
                        $line = $line -replace "\*/", ""
                        if ($line.Trim()) {
                            $descLines += $line.Trim()
                        }
                    }
                    elseif ($inCommentBlock) {
                        $descLines += $line.Trim()
                    }
                }
                
                if ($descLines.Count -gt 0) {
                    $description = $descLines -join "`n"
                }
            }
            default {
                # Generic approach for unknown script types
                $descLines = @()
                foreach ($line in $content | Select-Object -First 10) {
                    $descLines += $line.Trim()
                }
                if ($descLines.Count -gt 0) {
                    $description = $descLines -join "`n"
                }
            }
        }
        
        # If no description found, provide a default message
        if ([string]::IsNullOrWhiteSpace($description)) {
            $description = "$ScriptType script.`nCreated: $(Get-Date -Date (Get-Item -Path $FilePath).LastWriteTime -Format 'MM/dd/yyyy')"
        }
        
        return $description
    }
    catch {
        return "Error reading script description: $_"
    }
}

# Export functions
Export-ModuleMember -Function Get-NetworkScripts, Get-ScriptDescription