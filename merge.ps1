# DigiSign Merge PowerShell Script
# Safe merge of master into digisign-prod

param(
    [switch]$VerifyOnly,
    [switch]$ResolveConflicts,
    [switch]$Complete
)

$ErrorActionPreference = "Stop"

function Write-Header($text) {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $text -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Write-Success($text) {
    Write-Host "✓ $text" -ForegroundColor Green
}

function Write-Warning2($text) {
    Write-Host "⚠ $text" -ForegroundColor Yellow
}

function Write-Error2($text) {
    Write-Host "✗ $text" -ForegroundColor Red
}

function Write-Info($text) {
    Write-Host "ℹ $text" -ForegroundColor Blue
}

# Verify we're in the correct directory
if (-not (Test-Path ".git")) {
    Write-Error2 "Not in a git repository!"
    Write-Host "Please run this from D:\Development\DigiSign"
    exit 1
}

# Check if git is accessible
try {
    $null = git --version
} catch {
    Write-Error2 "Git is not accessible from PowerShell!"
    Write-Host "Please ensure Git is installed and in PATH"
    exit 1
}

# Critical files that must be preserved
$CriticalSigningFiles = @(
    "SignatureHelper.cs",
    "DigitalSignatureService.cs",
    "X509Certificate2Extension.cs",
    "SignatureConfiguration.cs",
    "packages.config"
)

function Get-CurrentBranch {
    return git rev-parse --abbrev-ref HEAD
}

function Get-MergeConflicts {
    $conflicts = git diff --name-only --diff-filter=U 2>$null
    return $conflicts
}

function Test-InMergeState {
    return Test-Path ".git/MERGE_HEAD"
}

function Resolve-Conflict {
    param([string]$FilePath, [string]$Strategy)
    
    try {
        if ($Strategy -eq "ours") {
            git checkout --ours $FilePath
            git add $FilePath
            Write-Success "Kept digisign-prod version: $FilePath"
        } elseif ($Strategy -eq "theirs") {
            git checkout --theirs $FilePath
            git add $FilePath
            Write-Success "Accepted master version: $FilePath"
        }
    } catch {
        Write-Error2 "Failed to resolve $FilePath : $_"
    }
}

# ==========================================
# VERIFY MODE
# ==========================================
if ($VerifyOnly) {
    Write-Header "Verification Mode"
    
    # Check current branch
    $currentBranch = Get-CurrentBranch
    Write-Host "Current branch: " -NoNewline
    if ($currentBranch -eq "digisign-prod") {
        Write-Success $currentBranch
    } else {
        Write-Error2 "$currentBranch (Expected: digisign-prod)"
    }
    
    # Check for uncommitted changes
    $status = git status --porcelain
    if ($status) {
        Write-Warning2 "Uncommitted changes detected:"
        git status --short
    } else {
        Write-Success "No uncommitted changes"
    }
    
    # Check if critical files exist
    Write-Host "`nCritical signing files:"
    foreach ($file in $CriticalSigningFiles) {
        if (Test-Path $file) {
            Write-Success $file
        } else {
            Write-Error2 "$file - NOT FOUND!"
        }
    }
    
    # Show branch comparison
    Write-Host "`nCommits in master not in digisign-prod:"
    git log digisign-prod..origin/master --oneline --max-count=10
    
    Write-Host "`nCommits in digisign-prod not in master:"
    git log origin/master..digisign-prod --oneline --max-count=10
    
    exit 0
}

# ==========================================
# RESOLVE CONFLICTS MODE
# ==========================================
if ($ResolveConflicts) {
    Write-Header "Conflict Resolution Mode"
    
    if (-not (Test-InMergeState)) {
        Write-Info "Not currently in a merge state."
        exit 0
    }
    
    $conflicts = Get-MergeConflicts
    if (-not $conflicts) {
        Write-Success "No conflicts detected!"
        exit 0
    }
    
    Write-Warning2 "Found $($conflicts.Count) conflicted file(s):"
    $conflicts | ForEach-Object { Write-Host "  - $_" }
    Write-Host ""
    
    foreach ($file in $conflicts) {
        Write-Host "`nProcessing conflict: $file" -ForegroundColor Yellow
        
        # Check if it's a critical signing file
        if ($CriticalSigningFiles -contains $file) {
            Write-Warning2 "CRITICAL FILE - Keeping digisign-prod version"
            Resolve-Conflict -FilePath $file -Strategy "ours"
        }
        elseif ($file -eq "Program.cs") {
            Write-Warning2 "MAIN PROGRAM - Manual review required"
            Write-Host "Opening in editor..."
            code $file -w  # -w waits for file to close
            
            $resolved = Read-Host "Have you resolved the conflicts in $file? (y/n)"
            if ($resolved -eq "y") {
                git add $file
                Write-Success "Marked as resolved: $file"
            }
        }
        else {
            Write-Host "Choose resolution strategy for $file :"
            Write-Host "  1) Keep digisign-prod version (--ours)"
            Write-Host "  2) Accept master version (--theirs)"
            Write-Host "  3) Manual merge (open in editor)"
            Write-Host "  4) Skip for now"
            
            $choice = Read-Host "Choice (1/2/3/4)"
            
            switch ($choice) {
                "1" { Resolve-Conflict -FilePath $file -Strategy "ours" }
                "2" { Resolve-Conflict -FilePath $file -Strategy "theirs" }
                "3" {
                    code $file -w
                    $resolved = Read-Host "Resolved? (y/n)"
                    if ($resolved -eq "y") {
                        git add $file
                        Write-Success "Marked as resolved: $file"
                    }
                }
                "4" { Write-Info "Skipped: $file" }
                default { Write-Warning2 "Invalid choice. Skipping: $file" }
            }
        }
    }
    
    Write-Host "`n"
    Write-Header "Resolution Summary"
    git status
    
    $remainingConflicts = Get-MergeConflicts
    if ($remainingConflicts) {
        Write-Warning2 "Some conflicts still remain. Please resolve manually."
    } else {
        Write-Success "All conflicts resolved!"
        Write-Host "`nRun with -Complete to finalize the merge."
    }
    
    exit 0
}

# ==========================================
# COMPLETE MODE
# ==========================================
if ($Complete) {
    Write-Header "Complete Merge"
    
    if (-not (Test-InMergeState)) {
        Write-Info "Not in a merge state. Nothing to complete."
        exit 0
    }
    
    $conflicts = Get-MergeConflicts
    if ($conflicts) {
        Write-Error2 "Cannot complete merge - conflicts still exist!"
        Write-Host "Conflicted files:"
        $conflicts | ForEach-Object { Write-Host "  - $_" }
        Write-Host "`nResolve conflicts first with: .\merge.ps1 -ResolveConflicts"
        exit 1
    }
    
    Write-Host "Completing merge..."
    git commit -m "Merge master into digisign-prod - preserved signing implementation"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Merge completed successfully!"
        
        Write-Host "`nNext steps:"
        Write-Host "1. Verify signing files unchanged"
        Write-Host "2. Build solution in Visual Studio"
        Write-Host "3. Test signing functionality"
        Write-Host "4. Push to remote: git push origin digisign-prod"
    } else {
        Write-Error2 "Merge commit failed!"
    }
    
    exit 0
}

# ==========================================
# DEFAULT MODE - Interactive Menu
# ==========================================
Write-Header "DigiSign Merge Script"

$currentBranch = Get-CurrentBranch
Write-Host "Current branch: $currentBranch"

if ($currentBranch -ne "digisign-prod") {
    Write-Warning2 "Not on digisign-prod branch!"
    $switch = Read-Host "Switch to digisign-prod? (y/n)"
    if ($switch -eq "y") {
        git checkout digisign-prod
        Write-Success "Switched to digisign-prod"
    } else {
        exit 1
    }
}

do {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "Select an option:" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "1. Fetch latest changes"
    Write-Host "2. Create backup branch"
    Write-Host "3. Start merge"
    Write-Host "4. Check merge status"
    Write-Host "5. Resolve conflicts"
    Write-Host "6. Verify signing files"
    Write-Host "7. Complete merge"
    Write-Host "8. Abort merge"
    Write-Host "9. Full verification"
    Write-Host "0. Exit"
    Write-Host ""
    
    $choice = Read-Host "Choice (0-9)"
    
    switch ($choice) {
        "1" {
            Write-Info "Fetching latest changes..."
            git fetch --all
            Write-Success "Fetch complete"
        }
        "2" {
            $date = Get-Date -Format "yyyyMMdd"
            $backupBranch = "digisign-prod-backup-$date"
            Write-Info "Creating backup branch: $backupBranch"
            git branch $backupBranch
            Write-Success "Backup created"
        }
        "3" {
            Write-Warning2 "Starting merge of master into digisign-prod"
            $confirm = Read-Host "Continue? (y/n)"
            if ($confirm -eq "y") {
                git merge origin/master --no-ff -m "Merge master updates into digisign-prod"
                if ($LASTEXITCODE -eq 0) {
                    Write-Success "Merge successful (no conflicts)!"
                } else {
                    Write-Warning2 "Merge has conflicts. Use option 5 to resolve."
                }
            }
        }
        "4" {
            git status
            $conflicts = Get-MergeConflicts
            if ($conflicts) {
                Write-Warning2 "Conflicted files:"
                $conflicts | ForEach-Object { Write-Host "  - $_" }
            }
        }
        "5" {
            & $PSCommandPath -ResolveConflicts
        }
        "6" {
            $date = Get-Date -Format "yyyyMMdd"
            $backupBranch = "digisign-prod-backup-$date"
            
            Write-Info "Checking critical files against backup..."
            foreach ($file in $CriticalSigningFiles) {
                Write-Host "`nChecking $file ..."
                $diff = git diff $backupBranch $file 2>$null
                if ($diff) {
                    Write-Warning2 "Changes detected in $file !"
                    Write-Host $diff
                } else {
                    Write-Success "No changes in $file"
                }
            }
        }
        "7" {
            & $PSCommandPath -Complete
        }
        "8" {
            Write-Warning2 "Aborting merge..."
            $confirm = Read-Host "Are you sure? (y/n)"
            if ($confirm -eq "y") {
                git merge --abort
                Write-Success "Merge aborted"
            }
        }
        "9" {
            & $PSCommandPath -VerifyOnly
        }
        "0" {
            Write-Info "Exiting..."
            break
        }
        default {
            Write-Warning2 "Invalid choice"
        }
    }
    
} while ($choice -ne "0")
