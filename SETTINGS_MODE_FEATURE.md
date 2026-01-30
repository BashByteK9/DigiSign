# Settings Mode Feature - /settings Option

## Overview
Added a new `/settings` command-line option that allows users to access PDF signing settings without requiring an admin license. This provides a license-free way to configure signing parameters.

## Feature Details

### Command-Line Option
```bash
DigiSign.exe /settings
```

### Purpose
- **Settings Management**: Configure PDF signing settings
- **No License Required**: Unlike `/admin`, no admin.license file needed
- **User-Friendly**: Simple configuration without admin privileges
- **PDF Signing Only**: Focused on signing configuration, not license generation

## Differences Between Modes

| Feature | `/admin` | `/settings` | Normal Mode |
|---------|----------|-------------|-------------|
| **Requires Admin License** | ? Yes | ? No | ? No |
| **Shows License Generation Tab** | ? Yes | ? No | ? No |
| **Shows Settings Tab** | ? Yes | ? Yes | ? No |
| **Can Generate Licenses** | ? Yes | ? No | ? No |
| **Can Configure Signing** | ? Yes | ? Yes | ? No |
| **Performs PDF Signing** | ? No | ? No | ? Yes |
| **Shows Verbose Window** | ? No | ? No | ? Optional |

## Visual Interface

### Settings Mode Window
```
???????????????????????????????????????????????????????
? DigiSign - PDF Signing Settings                     ?
???????????????????????????????????????????????????????
?                                                     ?
? ?? PDF Signing Settings ???????????????????????   ?
? ? ? General ? ? Signature ? ? Preview ?       ?   ?
? ? ???????????????????????????????????????????? ?   ?
? ? ? Input PDF File:                          ? ?   ?
? ? ? [________________________] [Browse]      ? ?   ?
? ? ?                                          ? ?   ?
? ? ? Output Folder:                           ? ?   ?
? ? ? [________________________] [Browse]      ? ?   ?
? ? ?                                          ? ?   ?
? ? ? Certificate Common Name (CN):            ? ?   ?
? ? ? [__________________________________]     ? ?   ?
? ? ?                                          ? ?   ?
? ? ? Smart Card/Token PIN:                    ? ?   ?
? ? ? [__________________________________]     ? ?   ?
? ? ? ? Show PIN                               ? ?   ?
? ? ?                                          ? ?   ?
? ? ? ? Enable Verbose Mode                    ? ?   ?
? ? ?                                          ? ?   ?
? ? ???????????????????????????????????????????? ?   ?
? ?                                              ?   ?
? ? [Reset to Defaults]  [Save Settings]        ?   ?
? ????????????????????????????????????????????????   ?
???????????????????????????????????????????????????????
```

### Admin Mode Window (For Comparison)
```
???????????????????????????????????????????????????????
? DigiSign Administration                              ?
???????????????????????????????????????????????????????
?                                                     ?
? ? License Generation ? ? PDF Signing Settings ?   ?
? ??????????????????????????????????????????????????? ?
? ? License Key File (*.key):                       ? ?
? ? [________________________] [Browse Key File]    ? ?
? ?                                                 ? ?
? ? ... (license generation controls) ...          ? ?
? ?                                                 ? ?
? ? [Cancel]  [Generate License]                   ? ?
? ??????????????????????????????????????????????????? ?
???????????????????????????????????????????????????????
```

## Implementation Details

### Code Changes

#### 1. Program.cs Main Method
```csharp
// Check if admin mode or settings mode is requested FIRST
bool isAdminMode = args.Length > 0 && args[0].Equals("/admin", StringComparison.OrdinalIgnoreCase);
bool isSettingsMode = args.Length > 0 && args[0].Equals("/settings", StringComparison.OrdinalIgnoreCase);

// Only enable verbose mode if NOT in admin mode or settings mode
if (!isAdminMode && !isSettingsMode)
{
    // Verbose mode handling...
}

// Handle settings mode (no admin license required)
if (isSettingsMode)
{
    Logger.Info("Settings mode requested - showing settings panel (no license required)");
    RunSettingsMode();
    return; // Exit after settings mode completes
}
```

#### 2. RunSettingsMode Method
```csharp
static void RunSettingsMode()
{
    Logger.Info("Entering settings mode - no license required");
    
    Console.WriteLine("Opening settings panel...");
    Console.WriteLine("Configure your PDF signing settings without requiring admin privileges.");
    
    using (var form = new LicenseGenerationForm(settingsOnly: true))
    {
        form.ShowDialog();
    }
    
    Console.WriteLine("Settings panel closed.");
    Logger.Info("Settings mode completed");
}
```

#### 3. LicenseGenerationForm Constructor
```csharp
private bool settingsOnlyMode = false;

public LicenseGenerationForm(bool settingsOnly = false)
{
    settingsOnlyMode = settingsOnly;
    xmlFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IP.xml");
    InitializeComponents();
    LoadSettings();
    WasCancelled = true;
}
```

#### 4. InitializeComponents Updates
```csharp
// Form title changes based on mode
this.Text = settingsOnlyMode ? "DigiSign - Settings" : "DigiSign - Admin Panel";

lblTitle.Text = settingsOnlyMode ? 
    "DigiSign - PDF Signing Settings" : 
    "DigiSign Administration";

// Only include License tab if NOT in settings-only mode
if (!settingsOnlyMode)
{
    CreateLicenseGenerationTab();
}
CreateSettingsTab();

// Select Settings tab by default in settings-only mode
if (settingsOnlyMode && tabControl.TabPages.Count > 0)
{
    tabControl.SelectedIndex = 0;
}
```

## User Workflows

### Workflow 1: Configure Settings (No Admin License)
```
User ? DigiSign.exe /settings
    ?
Settings window opens
    ?
User configures:
  - Input PDF location
  - Output folder
  - Certificate CN
  - PIN
  - Signature coordinates
  - Verbose mode
    ?
User clicks Save Settings
    ?
Settings saved to IP.xml
    ?
User closes window
    ?
Ready to sign PDFs
```

### Workflow 2: Admin Mode (Requires Admin License)
```
User ? DigiSign.exe /admin
    ?
Admin license validation
    ?
Admin panel opens (2 tabs)
    ?
Tab 1: License Generation
Tab 2: PDF Signing Settings
    ?
User can generate licenses AND configure settings
```

### Workflow 3: Normal Signing Mode
```
User ? DigiSign.exe
    ?
License validation
    ?
PDF signing begins
    ?
(No settings UI shown)
```

## Console Output

### Settings Mode Launch
```
???????????????????????????????????????????????????????????
? Settings Configuration Mode
???????????????????????????????????????????????????????????

Opening settings panel...

Configure your PDF signing settings without requiring admin privileges.

Settings panel closed.
```

### Admin Mode Launch (For Comparison)
```
???????????????????????????????????????????????????????????
?? Admin License Generation Mode
???????????????????????????????????????????????????????????

? Admin license validated

This mode is ONLY for generating user licenses.
No PDF signing will be performed.
```

## Benefits

### For Users
? **No Admin License Required**: Can configure settings without special privileges
? **Easy Access**: Simple command-line option
? **Focused Interface**: Only shows relevant settings, not license generation
? **Immediate Configuration**: Quick setup before signing PDFs

### For Administrators
? **Separation of Concerns**: License generation vs. settings configuration
? **Controlled Access**: Admin mode still requires admin.license
? **User Empowerment**: Users can self-configure signing parameters
? **Reduced Support**: Users don't need admin help for basic settings

## Security Considerations

### Settings Mode
- ? **Cannot generate licenses** (License Generation tab hidden)
- ? **Can configure signing settings** (IP.xml)
- ? **No license validation** (settings only)
- ? **Cannot bypass license checks** for PDF signing

### Admin Mode
- ? **Can generate licenses** (with admin.license)
- ? **Can configure signing settings**
- ? **Requires admin.license validation**
- ? **Full administrative access**

## Testing

### Test Case 1: Launch Settings Mode
**Command:**
```bash
DigiSign.exe /settings
```

**Expected:**
- ? Console shows "Settings Configuration Mode"
- ? Settings window opens
- ? Only "PDF Signing Settings" tab visible
- ? No "License Generation" tab
- ? No admin license check
- ? Can save settings

### Test Case 2: Verify No Verbose Window
**Setup:** Enable VerboseMode in IP.xml
**Command:**
```bash
DigiSign.exe /settings
```

**Expected:**
- ? No verbose progress window appears
- ? Only settings panel shows
- ? Clean interface

### Test Case 3: Save Settings in Settings Mode
**Command:**
```bash
DigiSign.exe /settings
```

**Actions:**
1. Change signature coordinates
2. Enable verbose mode
3. Click Save Settings

**Expected:**
- ? Settings saved to IP.xml
- ? Success message shown
- ? Can close window
- ? Settings persist

### Test Case 4: Compare with Admin Mode
**Admin Mode:**
```bash
DigiSign.exe /admin
```

**Expected:**
- ? Requires admin.license
- ? Shows 2 tabs (License + Settings)
- ? Can generate licenses

**Settings Mode:**
```bash
DigiSign.exe /settings
```

**Expected:**
- ? No admin.license required
- ? Shows 1 tab (Settings only)
- ? Cannot generate licenses

## Command Reference

| Command | Purpose | License Required | Interface |
|---------|---------|------------------|-----------|
| `DigiSign.exe` | Sign PDFs | license.txt | Console/Verbose |
| `DigiSign.exe /verbose` | Sign PDFs (verbose) | license.txt | Verbose Window |
| `DigiSign.exe /settings` | Configure settings | None | Settings Panel |
| `DigiSign.exe /admin` | License generation | admin.license | Admin Panel |

## File Changes Summary

**Files Modified:**
1. `Program.cs`
   - Added `isSettingsMode` check
   - Added `RunSettingsMode()` method
   - Updated verbose mode logic to exclude settings mode

2. `LicenseGenerationForm.cs`
   - Added `settingsOnlyMode` flag
   - Updated constructor to accept `settingsOnly` parameter
   - Updated `InitializeComponents()` to conditionally create tabs
   - Updated form title based on mode

**Lines Added:** ~60 lines
**Lines Modified:** ~20 lines

## Build Status
? **Build Successful**
- No errors
- No warnings
- Ready to test

## Documentation
- Feature overview: This document
- User guide: See "User Workflows" section
- Testing guide: See "Testing" section

## Summary

**Feature:** `/settings` command-line option for license-free settings configuration

**Purpose:** Allow users to configure PDF signing settings without admin license

**Key Points:**
- ? No admin license required
- ? Settings-only interface
- ? Same settings as admin mode, but focused
- ? Separate from license generation
- ? Simple and user-friendly

**Usage:**
```bash
DigiSign.exe /settings
```

The settings mode is now fully implemented and ready for use! ??
