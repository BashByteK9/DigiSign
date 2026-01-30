# Settings Dialog Feature

## Overview
A new Settings dialog has been added to allow administrators to easily edit the IP.xml configuration file through a user-friendly graphical interface.

## Features

### Settings Button
- A new **"? Settings"** button has been added to the License Generation Form
- Located at the bottom-left corner of the form
- Opens the Settings dialog when clicked
- Confirmation message displayed when settings are saved successfully

### Settings Dialog

The Settings dialog provides a tabbed interface with two sections:

#### General Tab
- **Input PDF File**: Browse and select the input PDF file to sign
- **Output Folder**: Browse and select where signed PDFs will be saved
- **Certificate Common Name (CN)**: The common name of the certificate to use for signing
- **Smart Card/Token PIN**: The PIN for the smart card or USB token (with show/hide option)

#### Signature Tab
- **X Coordinate**: Horizontal position of the signature (in pixels)
- **Y Coordinate**: Vertical position of the signature (in pixels)
- **Signature Width**: Width of the signature box (in pixels)
- **Signature Height**: Height of the signature box (in pixels)
- **Sign On Page**: Choose where to place the signature
  - F - First Page
  - E - Each Page
  - L - Last Page (default)
- **Open Output Folder After Signing**: Automatically open the output folder
  - Y - Yes (default)
  - N - No
- **Use Self-Signed Certificate**: Allow using self-signed certificates
  - Y - Yes
  - N - No (default)

### Dialog Actions

#### Save Button
- Validates all required fields
- Saves settings to IP.xml
- Displays success confirmation
- Closes the dialog

#### Reset Button
- Resets all fields to default values
- Requires user confirmation
- Does not save until "Save" is clicked

#### Cancel Button
- Closes the dialog without saving
- Discards any changes made

## Default Values

When no IP.xml exists or on reset:
- **Output Folder**: C:\Users\Public
- **X Coordinate**: 400
- **Y Coordinate**: 75
- **Signature Width**: 150
- **Signature Height**: 50
- **Sign On Page**: L (Last Page)
- **Open Output Folder**: Y (Yes)
- **Use Self-Signed**: N (No)

## Usage

### From License Generation Form
1. Open the License Generation Form (run DigiSign.exe with /admin flag)
2. Click the **"? Settings"** button at the bottom-left
3. The Settings dialog will open
4. Make your changes in the appropriate tab
5. Click **"Save"** to apply changes or **"Cancel"** to discard

### Direct Access
The Settings dialog can also be opened programmatically:
```csharp
using (var settingsForm = new SettingsForm())
{
    var result = settingsForm.ShowDialog();
    if (result == DialogResult.OK)
    {
        // Settings saved successfully
    }
}
```

## File Structure

The settings are saved to IP.xml in the following format:
```xml
<ENVELOPE>
    <FILENAMELIST>
        <FILENAMELIST>
            <FILENAME>input_file.pdf</FILENAME>
        </FILENAMELIST>
        <FILENAMELIST>
            <FILENAME>C:\Users\Public</FILENAME>
        </FILENAMELIST>
        <FILENAMELIST>
            <FILENAME>CommonName</FILENAME>
        </FILENAMELIST>
        <FILENAMELIST>
            <FILENAME>PIN</FILENAME>
        </FILENAMELIST>
        <FILENAMELIST>
            <FILENAME>400</FILENAME>
        </FILENAMELIST>
        <FILENAMELIST>
            <FILENAME>75</FILENAME>
        </FILENAMELIST>
        <FILENAMELIST>
            <FILENAME>150</FILENAME>
        </FILENAMELIST>
        <FILENAMELIST>
            <FILENAME>50</FILENAME>
        </FILENAMELIST>
        <FILENAMELIST>
            <FILENAME>L</FILENAME>
            <!-- SignOnPage F=FIRST Page, E=Each Page, L=Last Page, default value=L -->
        </FILENAMELIST>
        <FILENAMELIST>
            <FILENAME>Y</FILENAME>
            <!-- Open Output folder after signing: Y=output folder will be open after signing, N=Output folder will not open after signing, default value=Y -->
        </FILENAMELIST>
        <FILENAMELIST>
            <FILENAME>N</FILENAME>
            <!-- USESELFSIGNED -->
        </FILENAMELIST>
    </FILENAMELIST>
</ENVELOPE>
```

## Security Features

- PIN field is masked by default (shows as asterisks)
- "Show PIN" checkbox allows temporary visibility for verification
- PIN is saved in plain text in IP.xml (consider using encryption in production)

## Error Handling

- Validates that Certificate Common Name is provided before saving
- Shows friendly error messages if IP.xml cannot be loaded
- Falls back to default values if configuration is corrupted
- Displays confirmation before resetting to defaults

## Benefits

? User-friendly interface for configuration  
? No need to manually edit XML files  
? Validation to prevent invalid configurations  
? Tab-based organization for better UX  
? Browse buttons for file/folder selection  
? Reset option to restore defaults  
? Integrated with License Generation workflow
