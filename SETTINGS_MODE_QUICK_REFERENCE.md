# Settings Mode - Quick Reference

## ? New Feature: /settings Option

### Command
```bash
DigiSign.exe /settings
```

### What It Does
Opens the **settings panel** without requiring an admin license - allows users to configure PDF signing settings easily.

## Quick Comparison

| Mode | Command | License? | Shows | Purpose |
|------|---------|----------|-------|---------|
| **Settings** | `/settings` | ? None | Settings only | Configure signing |
| **Admin** | `/admin` | ? admin.license | License + Settings | Generate licenses |
| **Signing** | (none) | ? license.txt | Console/Verbose | Sign PDFs |

## Visual Differences

### Settings Mode (/settings)
```
???????????????????????????????
? DigiSign - Settings         ?
???????????????????????????????
? ?? PDF Signing Settings ?? ?
? ? • General               ? ?
? ? • Signature             ? ?
? ? • Preview               ? ?
? ??????????????????????????? ?
?                             ?
? [Reset] [Save Settings]     ?
???????????????????????????????

No License Required ?
Only 1 tab visible
```

### Admin Mode (/admin)
```
???????????????????????????????
? DigiSign - Admin Panel      ?
???????????????????????????????
? ?? License ?? ?? Settings ???
? ? Generate  ? ? PDF Signing??
? ? Licenses  ? ? Config     ??
? ????????????? ???????????????
?                             ?
? [Cancel] [Generate]         ?
???????????????????????????????

admin.license Required ?
2 tabs visible
```

## What You Can Do

### In Settings Mode ?
- ? Configure input PDF location
- ? Set output folder
- ? Enter certificate CN
- ? Set PIN
- ? Configure signature position (X, Y, Width, Height)
- ? Choose sign on page (First/Each/Last)
- ? Enable/disable verbose mode
- ? Preview signature placement
- ? Drag and resize signature box
- ? Save all settings to IP.xml

### In Settings Mode ?
- ? Generate user licenses
- ? Access license generation tab
- ? Sign PDFs (use normal mode)

## Usage Examples

### Example 1: First-Time Setup
```bash
# Configure settings without admin privileges
DigiSign.exe /settings

? Settings window opens
? Configure all signing parameters
? Save settings
? Close window

# Now ready to sign
DigiSign.exe
? Signs PDFs with configured settings
```

### Example 2: Change Signature Position
```bash
# Quick settings adjustment
DigiSign.exe /settings

? Go to Signature tab
? Change X, Y coordinates
? Or use Preview tab to drag signature
? Save settings
? Done!
```

### Example 3: Enable Verbose Mode
```bash
# Enable detailed logging
DigiSign.exe /settings

? Go to General tab
? Check "Enable Verbose Mode"
? Save settings
? Close

# Next signing will show verbose window
DigiSign.exe
? Verbose UI appears automatically
```

## Key Benefits

? **No Admin License**: Anyone can configure settings
? **Simple Interface**: Only shows relevant options
? **Quick Access**: Fast configuration changes
? **User-Friendly**: No technical knowledge required
? **Safe**: Cannot generate licenses or bypass security

## Console Output

When you run `/settings`:
```
???????????????????????????????????????????????????????????
? Settings Configuration Mode
???????????????????????????????????????????????????????????

Opening settings panel...

Configure your PDF signing settings without requiring admin privileges.

Settings panel closed.
```

## Troubleshooting

### Settings Not Saving
- **Check:** Do you have write permissions to IP.xml?
- **Solution:** Run as administrator or check file permissions

### Settings Window Doesn't Open
- **Check:** Any error messages in console?
- **Check:** Is .NET Framework 4.7.2 installed?
- **Solution:** Check application_log.txt for details

### Changed Settings Not Applied
- **Check:** Did you click "Save Settings"?
- **Solution:** Always save before closing window

## Integration with Other Modes

### Settings ? Signing
```bash
# 1. Configure
DigiSign.exe /settings
# ? Set all parameters

# 2. Sign
DigiSign.exe
# ? Uses saved settings
```

### Settings ? Admin (For License Generation)
```bash
# 1. Configure signing settings
DigiSign.exe /settings
# ? Anyone can do this

# 2. Generate licenses (requires admin.license)
DigiSign.exe /admin
# ? Only admins can do this
```

## Settings Saved to IP.xml

All settings configured in `/settings` mode are saved to `IP.xml`:
- Input PDF path
- Output folder
- Certificate CN
- PIN (encrypted)
- Signature coordinates (X, Y, Width, Height)
- Sign on page option
- Open output folder option
- Use self-signed option
- **Verbose mode option** ? New!

## Command Reference

```bash
# Open settings (no license required)
DigiSign.exe /settings

# Open admin panel (requires admin.license)
DigiSign.exe /admin

# Sign PDFs (requires license.txt)
DigiSign.exe

# Sign PDFs with verbose output
DigiSign.exe /verbose
```

## Build Information

**Status:** ? Implemented and tested
**Files Modified:** Program.cs, LicenseGenerationForm.cs
**Breaking Changes:** None
**Backward Compatible:** Yes

## Summary

**New Command:** `DigiSign.exe /settings`

**Purpose:** Configure PDF signing settings without admin license

**Key Features:**
- No license required
- Settings-only interface
- Same settings as admin mode
- User-friendly and focused

**When to Use:**
- First-time setup
- Change signing parameters
- Enable/disable verbose mode
- Adjust signature placement
- Regular configuration updates

**When NOT to Use:**
- To generate licenses ? Use `/admin`
- To sign PDFs ? Use normal mode
- For admin tasks ? Use `/admin`

Try it now: `DigiSign.exe /settings` ??
