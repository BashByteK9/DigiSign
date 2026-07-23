param([switch]$Clean)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path "$PSScriptRoot\.."

Push-Location $repoRoot
try {
    if ($Clean) { dotnet clean DigiSign.csproj -c Release }
    dotnet build DigiSign.csproj -c Release
} finally {
    Pop-Location
}

$candidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)
$iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup 6 (ISCC.exe) not found. Install it from https://jrsoftware.org/isdl.php or `winget install JRSoftware.InnoSetup`."
}

& $iscc "$PSScriptRoot\DigiSign.iss"
