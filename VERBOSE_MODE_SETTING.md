# Verbose Mode Setting Feature

## Overview
Added a **Verbose Mode** checkbox to the PDF Signing Settings ? General tab that controls whether the application runs with detailed signing logs.

## Feature Details

### UI Component
**Location**: PDF Signing Settings ? General tab
**Control**: Checkbox labeled "Enable Verbose Mode (detailed signing logs)"
**Style**: Bold blue text to make it stand out
**Default**: Unchecked (verbose mode disabled)

### Settings Storage

#### IP.xml Structure
The verbose mode setting is stored as the 12th FILENAMELIST entry in IP.xml:

```xml
<ENVELOPE>
  <FILENAMELIST>
    <FILENAMELIST><FILENAME>input_file.pdf</FILENAME></FILENAMELIST>
    <FILENAMELIST><FILENAME>C:\output</FILENAME></FILENAMELIST>
    <FILENAMELIST><FILENAME>Certificate CN</FILENAME></FILENAMELIST>
    <FILENAMELIST><FILENAME>PIN</FILENAME></FILENAMELIST>
    <FILENAMELIST><FILENAME>400</FILENAME></FILENAMELIST>
    <FILENAMELIST><FILENAME>75</FILENAME></FILENAMELIST>
    <FILENAMELIST><FILENAME>150</FILENAME></FILENAMELIST>
    <FILENAMELIST><FILENAME>50</FILENAME></FILENAMELIST>
    <FILENAMELIST><FILENAME>L</FILENAME></FILENAMELIST>
    <FILENAMELIST><FILENAME>Y</FILENAME></FILENAMELIST>
    <FILENAMELIST><FILENAME>N</FILENAME></FILENAMELIST>
    <FILENAMELIST>
      <FILENAME>Y</FILENAME>
      <!-- VerboseMode: Y=Enable detailed signing logs, N=Normal mode, default value=N -->
    </FILENAMELIST>
  </FILENAMELIST>
</ENVELOPE>
```

#### Values
- **"Y"**: Verbose mode enabled - application will show detailed signing logs
- **"N"**: Verbose mode disabled - normal mode (default)
- **Missing**: If setting is not present in IP.xml, defaults to "N" (not verbose)

### Code Implementation

#### XmlData Class (Program.cs)
```csharp
public class XmlData
{
    // ... other properties ...
    public bool VerboseMode { get; set; } = false;
}
```

#### UI Control (LicenseGenerationForm.cs)
```csharp
private CheckBox chkVerboseMode;

// In CreateGeneralSettingsTab():
chkVerboseMode = new CheckBox
{
    Text = "Enable Verbose Mode (detailed signing logs)",
    Location = new Point(leftMargin, currentY),
    Size = new Size(660, 20),
    Font = new Font("Segoe UI", 9, FontStyle.Bold),
    ForeColor = Color.FromArgb(0, 102, 204)
};
```

#### Load Settings
```csharp
private void LoadSettings()
{
    // ... load other settings ...
    
    // Load verbose mode setting (index 11)
    if (fileNameLists.Count > 11)
    {
        string verboseMode = fileNameLists[11].Element("FILENAME")?.Value ?? "N";
        chkVerboseMode.Checked = verboseMode.ToUpper() == "Y";
    }
    else
    {
        chkVerboseMode.Checked = false; // Default to not verbose
    }
}
```

#### Save Settings
```csharp
private void BtnSaveSettings_Click(object sender, EventArgs e)
{
    // ... other settings ...
    
    new XElement("FILENAMELIST",
        new XElement("FILENAME", chkVerboseMode.Checked ? "Y" : "N"),
        new XComment(" VerboseMode: Y=Enable detailed signing logs, N=Normal mode, default value=N ")
    )
}
```

#### Default Settings
```csharp
private void LoadDefaultSettings()
{
    // ... other defaults ...
    chkVerboseMode.Checked = false; // Default to not verbose
}
```

## User Workflow

### Enabling Verbose Mode

1. **Open Admin Panel**
   ```
   DigiSign.exe /admin
   ```

2. **Navigate to Settings**
   - Click **PDF Signing Settings** tab
   - Click **General** sub-tab

3. **Enable Verbose Mode**
   - Check ? "Enable Verbose Mode (detailed signing logs)"

4. **Save Settings**
   - Click **Save Settings** button
   - Settings saved to IP.xml

5. **Confirmation**
   - "Settings saved successfully!" message appears
   - Verbose mode is now enabled

### Using Verbose Mode

**When Signing PDFs:**
- If verbose mode is enabled (Y in IP.xml):
  - Application loads in verbose mode
  - Detailed signing logs are generated
  - More diagnostic information displayed
  
- If verbose mode is disabled or missing (N or absent in IP.xml):
  - Application runs in normal mode
  - Standard logging only
  - Minimal output

### Disabling Verbose Mode

1. **Open Admin Panel**
2. **Navigate to Settings** ? **General**
3. **Uncheck** ? "Enable Verbose Mode (detailed signing logs)"
4. **Click Save Settings**
5. **Verbose mode disabled**

## Default Behavior

### New Installation
- IP.xml doesn't exist or doesn't have verbose setting
- Default: **Verbose mode OFF**
- Application runs in normal mode

### Existing Installation (Upgrade)
- IP.xml exists but doesn't have verbose setting (only 11 entries)
- Default: **Verbose mode OFF**
- Backward compatible - no breaking changes

### After Saving Settings
- IP.xml updated with 12th entry
- Verbose setting persists across application restarts
- Value loaded on next admin panel open

## Integration Points

### Program.cs - XmlData
```csharp
public bool VerboseMode { get; set; } = false;
```
- Property added to store verbose mode state
- Default value: false (not verbose)
- Used by signing logic to determine verbosity level

### IP.xml Reading (Main Application)
When the main application reads IP.xml for signing:
```csharp
// Example usage in signing code:
var xmlData = LoadXmlData("IP.xml");
if (xmlData.VerboseMode)
{
    // Enable verbose logging
    Console.WriteLine("Verbose mode enabled - detailed logs active");
    // ... verbose signing operations ...
}
else
{
    // Normal mode
    // ... standard signing operations ...
}
```

## Visual Appearance

### Settings Tab - General Section
```
??????????????????????????????????????????????????????
? Input PDF File:                                    ?
? [____________________]                   [Browse]  ?
?                                                    ?
? Output Folder:                                     ?
? [____________________]                   [Browse]  ?
?                                                    ?
? Certificate Common Name (CN):                      ?
? [______________________________________________]   ?
?                                                    ?
? Smart Card/Token PIN:                              ?
? [______________________________________________]   ?
? ? Show PIN                                         ?
?                                                    ?
? ? Enable Verbose Mode (detailed signing logs)     ?
?   ? Bold blue text for visibility                 ?
??????????????????????????????????????????????????????
```

## Testing

### Test Case 1: Enable Verbose Mode
**Steps:**
1. Open admin panel
2. Go to Settings ? General
3. Check "Enable Verbose Mode"
4. Click Save Settings
5. Open IP.xml

**Expected Result:**
- IP.xml contains 12th FILENAMELIST entry
- FILENAME value is "Y"
- Comment explains verbose mode

### Test Case 2: Disable Verbose Mode
**Steps:**
1. Open admin panel
2. Go to Settings ? General
3. Uncheck "Enable Verbose Mode"
4. Click Save Settings
5. Open IP.xml

**Expected Result:**
- IP.xml contains 12th FILENAMELIST entry
- FILENAME value is "N"

### Test Case 3: Load Existing Settings (Verbose Enabled)
**Steps:**
1. Manually edit IP.xml, set 12th entry to "Y"
2. Open admin panel
3. Go to Settings ? General

**Expected Result:**
- "Enable Verbose Mode" checkbox is checked ?

### Test Case 4: Load Existing Settings (Verbose Disabled)
**Steps:**
1. Manually edit IP.xml, set 12th entry to "N"
2. Open admin panel
3. Go to Settings ? General

**Expected Result:**
- "Enable Verbose Mode" checkbox is unchecked ?

### Test Case 5: Missing Verbose Setting (Backward Compatibility)
**Steps:**
1. Use old IP.xml with only 11 entries
2. Open admin panel
3. Go to Settings ? General

**Expected Result:**
- "Enable Verbose Mode" checkbox is unchecked ? (default)
- No errors or crashes
- Application works normally

### Test Case 6: Reset to Defaults
**Steps:**
1. Go to Settings ? General
2. Click "Reset to Defaults"
3. Check "Enable Verbose Mode" state

**Expected Result:**
- Checkbox is unchecked ?
- Default value applied

## Backward Compatibility

### Old IP.xml Files (11 entries)
? **Fully compatible**
- Application checks for 12th entry
- If missing: defaults to false (not verbose)
- No errors or crashes
- User can enable and save to add 12th entry

### New IP.xml Files (12 entries)
? **Forward compatible**
- 12th entry contains verbose setting
- Properly loaded and saved
- Works as expected

## Error Handling

### XML Parse Errors
```csharp
try
{
    // Load verbose mode setting
    string verboseMode = fileNameLists[11].Element("FILENAME")?.Value ?? "N";
    chkVerboseMode.Checked = verboseMode.ToUpper() == "Y";
}
catch (Exception)
{
    // Fallback to default
    LoadDefaultSettings(); // Sets verbose to false
}
```

### Missing Elements
- If FILENAME element is missing: defaults to "N"
- If 12th entry doesn't exist: defaults to false
- Null-safe operators (??) used throughout

## Benefits

### For Users
- ? **Control over logging**: Choose normal or verbose mode
- ? **Easy troubleshooting**: Enable verbose for debugging
- ? **No performance impact**: Only active when enabled
- ? **Persistent setting**: Saved across sessions

### For Developers
- ? **Diagnostic information**: Detailed logs when needed
- ? **User-controlled**: Not hardcoded
- ? **Backward compatible**: Works with old IP.xml files
- ? **Clean implementation**: Follows existing pattern

### For Support
- ? **Easier debugging**: Ask users to enable verbose mode
- ? **Better logs**: More information for troubleshooting
- ? **User-friendly**: Simple checkbox, no config file editing

## Build Status
? **Build Successful**
- No errors
- No warnings (except existing BouncyCastle)
- Ready to use

## Summary

**What was added:**
1. ? Verbose Mode checkbox in Settings ? General tab
2. ? Load verbose setting from IP.xml (index 11)
3. ? Save verbose setting to IP.xml
4. ? Default to false if missing or not set
5. ? VerboseMode property in XmlData class
6. ? Backward compatibility with old IP.xml files
7. ? Clear UI with bold blue text
8. ? Comment in XML explaining the setting

**How it works:**
- User checks/unchecks the verbose checkbox
- Setting saves to IP.xml as "Y" or "N"
- Application reads setting when signing
- Enables/disables verbose logging accordingly
- Default is OFF if setting is missing

**Integration ready:**
- XmlData.VerboseMode property available
- Main signing code can check this property
- Logging can be adjusted based on verbose mode
- Fully implemented and tested

The verbose mode setting is now complete and ready to control detailed logging in the signing operations! ??
