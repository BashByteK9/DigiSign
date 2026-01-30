# License Requirements and Admin Mode

## ?? License Types and Usage

DigiSign uses two distinct license types with specific purposes:

### 1. **User License** (`license.txt`)
- ? **Used for**: PDF signing (cryptographic signatures)
- ?? **Location**: Application base directory
- ?? **Validation**: Must be valid and not used on different device
- ? **Required for**: Normal PDF signing mode
- ? **Not required for**: `/admin` mode

### 2. **Admin License** (`admin.license`)
- ? **Used for**: Generating user licenses via `/admin` mode
- ?? **Location**: Application base directory
- ?? **Validation**: Must be valid admin license
- ? **Required for**: `/admin` mode only
- ? **Cannot be used**: For PDF signing (security restriction)

## Mode-Specific License Requirements

| Mode | Requires `license.txt` | Requires `admin.license` | Purpose |
|------|----------------------|------------------------|---------|
| Normal PDF Signing | ? YES | ? NO | Sign PDFs with cryptographic signatures |
| `/admin` Mode | ? NO | ? YES | Generate user licenses only |

## What Changed

### Before (OLD Behavior)
- ? Missing `license.txt` ? Demo mode (visual overlay only)
- ? Invalid `license.txt` ? Demo mode (visual overlay only)
- ? `/admin` mode required both licenses
- ?? PDFs were processed without cryptographic signatures

### After (NEW Behavior)
- ? Missing `license.txt` ? **Application exits with error** (PDF signing mode)
- ? Invalid `license.txt` ? **Application exits with error** (PDF signing mode)
- ? `/admin` mode ? **Only requires admin.license**, no `license.txt` needed
- ? Only valid user license ? PDF signing allowed

## Admin Mode (`/admin`)

### Purpose
Generate user licenses (`license.txt`) from license keys (`license.key`) for distribution to end users.

### Requirements
- ? `admin.license` file must exist
- ? `admin.license` must be valid
- ? `license.txt` is NOT required (this mode doesn't sign PDFs)

### Usage
```cmd
DigiSign.exe /admin
```

### Process
1. Validates `admin.license`
2. Opens license generation form
3. User fills in:
   - License key path (`license.key` file)
   - Customer ID
   - License number
   - Expiration date
4. Generates `license.txt` file
5. Exits (no PDF signing)

## Error Messages

### PDF Signing Mode - Missing License File
```
? License file not found.

???????????????????????????????????????????????????????????
ERROR: Valid user license required for PDF signing!
???????????????????????????????????????????????????????????

Please ensure you have a valid license.txt file.
Contact support for a license if you don't have one.

?? Admin license detected. Use /admin flag to generate user licenses.
   Example: DigiSign.exe /admin

Note: Admin licenses cannot be used for PDF signing.
```

### PDF Signing Mode - Invalid License
```
? License invalid or used on a different device.

???????????????????????????????????????????????????????????
ERROR: Valid user license required for PDF signing!
???????????????????????????????????????????????????????????

Please ensure you have a valid license.txt file.
Contact support for a license if you don't have one.
```

### Admin Mode - Missing Admin License
```
???????????????????????????????????????????????????????????
ERROR: Admin license not found!
???????????????????????????????????????????????????????????

Please ensure 'admin.license' file exists in: D:\YourPath\

Contact support for an admin license if you don't have one.

Press any key to exit...
```

### Admin Mode - Invalid Admin License
```
???????????????????????????????????????????????????????????
ERROR: Invalid admin license!
???????????????????????????????????????????????????????????

The admin.license file is invalid or corrupted.
Contact support for a valid admin license.

Press any key to exit...
```

### Verbose Mode Output (PDF Signing)
```
[3/10] Validating license...
    ? LICENSE MISSING - PDF signing not allowed

???????????????????????????????????????????????????????????
ERROR: Valid user license required for PDF signing!
???????????????????????????????????????????????????????????
    ? Application cannot continue without valid user license

? Errors detected (1 error) - Auto-closing in 15 seconds...
[Stop [15]] [Close]
```

## Security Benefits

### ?? **Prevents Unauthorized Use**
- No more demo mode bypass
- Must have valid license to sign
- Protects cryptographic signing capability

### ?? **License Separation**
- User license: For PDF signing only
- Admin license: For license generation only
- Clear separation of responsibilities

### ? **Compliance**
- Ensures all PDF signatures are properly licensed
- Prevents unauthorized cryptographic operations
- Audit trail for license usage

## Migration Guide

### If You Were Using Demo Mode

**Before:**
```cmd
# Missing license ? Demo mode (worked but no real signature)
DigiSign.exe
```

**After:**
```cmd
# Missing license ? Error (application exits)
DigiSign.exe
# Output: ERROR: Valid user license required for PDF signing!
```

**Solution:**
1. Obtain a valid `license.txt` file
2. Place it in the application directory
3. Run DigiSign.exe

### If You Have Admin License Only

**Before:**
```cmd
# Admin license could be used for signing (insecure)
DigiSign.exe
```

**After:**
```cmd
# Admin license cannot be used for signing
DigiSign.exe
# Output: ERROR: Valid user license required for PDF signing!
```

**Solution:**
1. Use admin license to generate user licenses:
   ```cmd
   DigiSign.exe /admin
   ```
2. Generate `license.txt` from `license.key`
3. Use the generated `license.txt` for PDF signing

## Code Changes

### Removed Features
- ? Demo mode completely removed
- ? `isDemoMode` variable removed
- ? Visual-only text overlay mode
- ? `GenerateLicenseKeyFile()` method removed
- ? Demo mode yellow warning box removed
- ? Red "DEMO MODE" text in signatures

### Added Features
- ? License validation exit on failure
- ? Admin license detection and message
- ? Clear error messages for missing/invalid license
- ? Error counting for license failures (15-second countdown in verbose mode)

### Modified Behavior
- **SignPdfWithITextSharp()**: No longer accepts `isDemoMode` parameter
- **DrawSignatureText()**: No longer accepts `isDemoMode` parameter
- **License validation**: Now exits application instead of falling back to demo mode

## Exit Codes

| Scenario | Exit Code | Verbose Mode Countdown |
|----------|-----------|----------------------|
| Valid license + Success | 0 | 2 seconds |
| Missing license | Non-zero | 15 seconds then exits |
| Invalid license | Non-zero | 15 seconds then exits |
| Admin license only | Non-zero | 15 seconds then exits |

## Testing

### Test Case 1: No License File
```cmd
# Delete/rename license.txt
DigiSign.exe /verbose
```
**Expected:**
- ? Error: "LICENSE MISSING - PDF signing not allowed"
- Application exits after 15 seconds (or immediately if Stop clicked)
- Exit code: Non-zero

### Test Case 2: Invalid License
```cmd
# Corrupt license.txt content
DigiSign.exe /verbose
```
**Expected:**
- ? Error: "LICENSE INVALID - PDF signing not allowed"
- Application exits after 15 seconds
- Exit code: Non-zero

### Test Case 3: Valid User License
```cmd
# With valid license.txt
DigiSign.exe /verbose
```
**Expected:**
- ? Success: "LICENSED - Full cryptographic signing enabled"
- PDFs signed with cryptographic signatures
- Exit code: 0 (if all PDFs signed successfully)

### Test Case 4: Admin License Only
```cmd
# Only admin.license exists, no license.txt
DigiSign.exe
```
**Expected:**
- ? Error: "LICENSE MISSING - PDF signing not allowed"
- Message: "?? Admin license detected. Use /admin flag to generate user licenses."
- Application exits

## Backwards Compatibility

?? **BREAKING CHANGE** - This update is NOT backwards compatible:

- Applications that relied on demo mode will now fail
- Users must obtain valid `license.txt` to continue
- Batch scripts may need updating to handle exit codes

## Support

If you need a license:
1. Contact support team
2. Provide device information
3. Receive `license.txt` file
4. Place in application directory

If you have admin license:
1. Use `/admin` mode to generate user licenses
2. Follow the prompt to create `license.txt` from `license.key`

## Summary

This update enforces proper licensing for PDF signing operations:
- ? **More Secure**: No unauthorized signing
- ? **Clear Requirements**: Must have valid user license
- ? **Better Separation**: Admin license only for license generation
- ?? **Breaking Change**: Demo mode removed

**All PDF signing now requires a valid user license. No exceptions.**
