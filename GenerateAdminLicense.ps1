# Quick Admin Key Generator for DigiSign
# Run this in PowerShell to generate the correct AdminKey

$adminId = "TENINFOTECH"
$secret = "DIGISIGN_ADMIN_SECRET"
$data = "$adminId|$secret"

# Calculate SHA256 hash
$sha256 = [System.Security.Cryptography.SHA256]::Create()
$bytes = [System.Text.Encoding]::UTF8.GetBytes($data)
$hash = $sha256.ComputeHash($bytes)

# Convert to hex string (without dashes)
$adminKey = [BitConverter]::ToString($hash).Replace("-", "")

Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host "       Admin License Generator for DigiSign" -ForegroundColor Cyan
Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Admin ID: $adminId" -ForegroundColor Yellow
Write-Host "Admin Key: $adminKey" -ForegroundColor Green
Write-Host ""
Write-Host "Complete admin.license file content:" -ForegroundColor Cyan
Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host "AdminID=$adminId"
Write-Host "AdminKey=$adminKey"
Write-Host "ValidUntil=2030-12-31"
Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host ""

# Save to file
$content = @"
AdminID=$adminId
AdminKey=$adminKey
ValidUntil=2030-12-31
"@

$outputPath = Join-Path $PSScriptRoot "admin.license"
$content | Out-File -FilePath $outputPath -Encoding ASCII -NoNewline

Write-Host "? File saved to: $outputPath" -ForegroundColor Green
Write-Host ""
Write-Host "Copy this file to your DigiSign directory:" -ForegroundColor Yellow
Write-Host "   D:\Development\DigiSign\" -ForegroundColor Yellow
Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
