# License System Documentation

## Overview
The DigiSign application uses a two-tier licensing system with automatic license key generation and admin-controlled license activation.

## Workflow

### For Regular Users

1. **First Run Without License**
   - Application checks for `license.txt` in the application directory
   - If not found or invalid, application automatically generates `license.key` file
   - Application runs in **Demo Mode** (watermark added to signatures)

2. **License Key File Generated**
   - Location: Same directory as the application executable
   - Filename: `license.key`
   - Contains:
     - DeviceID (unique hardware identifier)
     - MachineName
     - UserName
     - GeneratedOn timestamp

3. **Share License Key**
   - User sends the `license.key` file to the administrator
   - Administrator uses their admin license to generate `license.txt`

4. **Receive License File**
   - Administrator sends back the generated `license.txt` file
   - User places `license.txt` in the application directory
   - Application runs in **Full Mode** (no watermark)

### For Administrators

1. **Admin License Setup**
   - Administrator must have `admin.license` file in their application directory
   - This file contains:
     - AdminID
     - AdminKey (cryptographic hash)
     - ValidUntil (expiration date)

2. **Activate Admin Mode**
   - Run the application with `/admin` flag:
     ```
     DigiSign.exe /admin
     ```
   - This enables license generation mode

3. **Generate License for Users**
   - When admin runs with `/admin` flag and valid `admin.license`:
     - Application prompts: "Do you want to generate license.txt from a license.key file? (Y/N)"
   - Admin selects Yes and provides:
     - Path to user's `license.key` file
     - Customer ID
     - License Number
     - Expiration Date

4. **License Output**
   - `license.txt` is generated in the same directory as the `license.key`
   - Admin sends this file back to the user

**Note:** If admin runs the application normally (without `/admin` flag), it will work like a regular user application for signing PDFs.

## File Formats

### license.key (User's Device Key)
```
# License Key File
# Share this file with your administrator to generate a license

DeviceID=CPUID_DISKID
MachineName=COMPUTERNAME
UserName=USERNAME
GeneratedOn=2025-01-01 12:00:00
```

### license.txt (Activated License)
```
CustomerID=CUST123456
ValidUntil=2026-12-31
DeviceID=CPUID_DISKID
LicenseNumber=LIC-2025-0001
DeviceHash=7F23C4AD9D8A6BDEB4187A0A9F8F3E4D
```

### admin.license (Administrator Key)
```
# Admin License File
# This file enables license generation capabilities

AdminID=ADMIN001
AdminKey=E8B3F5D9C4A1B2E6F7D8C9A0B1E2F3D4C5A6B7E8F9D0A1B2C3D4E5F6A7B8C9D0
ValidUntil=2030-12-31
```

## Security Features

1. **Device Binding**: License is tied to specific hardware (CPU ID + Disk Serial)
2. **Hash Verification**: DeviceHash prevents tampering
3. **Admin Authentication**: Only valid admin licenses can generate user licenses
4. **Expiration Dates**: Both admin and user licenses have expiration dates
5. **Automatic Demo Mode**: Invalid/missing licenses automatically enable demo mode

## Usage

### Regular User Mode
```bash
# Just run the application normally
DigiSign.exe

# If no license.txt exists:
# - license.key is automatically generated
# - Application runs in Demo Mode
# - PDFs are signed with "*** DEMO MODE ***" watermark
```

### Admin License Generation Mode
```bash
# Run with /admin flag to generate licenses
DigiSign.exe /admin

# Then follow the prompts:
# 1. Choose Y to generate a license
# 2. Enter path to user's license.key file
# 3. Enter Customer ID
# 4. Enter License Number
# 5. Enter Expiration Date
# 6. license.txt is generated in the same folder as license.key
```

## Creating Admin License

To generate the AdminKey for a new admin:

### PowerShell Method
```powershell
$adminId = "ADMIN001"  # Your admin ID
$data = "$adminId|DIGISIGN_ADMIN_SECRET"
$bytes = [System.Text.Encoding]::UTF8.GetBytes($data)
$hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
$adminKey = [BitConverter]::ToString($hash).Replace("-", "")
Write-Host "AdminKey=$adminKey"
```

### C# Method
```csharp
using System.Security.Cryptography;
using System.Text;

string adminId = "ADMIN001";
string data = adminId + "|DIGISIGN_ADMIN_SECRET";
using (SHA256 sha = SHA256.Create())
{
    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(data));
    string adminKey = BitConverter.ToString(hash).Replace("-", "");
    Console.WriteLine($"AdminKey={adminKey}");
}
```

## Troubleshooting

### User Issues
- **License not activating**: Ensure `license.txt` is in the same directory as the executable
- **Device mismatch error**: License was generated for a different device
- **License expired**: Contact administrator for renewal

### Admin Issues
- **Cannot generate licenses**: Check that `admin.license` exists and is valid
- **Admin license expired**: Update ValidUntil date in `admin.license`
- **Invalid admin key**: Regenerate AdminKey using the correct formula

## Demo Mode vs Full Mode

| Feature | Demo Mode | Full Mode |
|---------|-----------|-----------|
| PDF Signing | ? Yes | ? Yes |
| Signature Watermark | "*** DEMO MODE ***" | None |
| License Required | ? No | ? Yes |
| Expiration | Never | Based on license |

## Files Reference

- `license.key` - Generated automatically for users (shareable)
- `license.txt` - User's activated license (device-specific)
- `admin.license` - Administrator's license (enables license generation)
- `admin.license.template` - Template for creating new admin licenses
- `admin.license.example` - Example admin license file
