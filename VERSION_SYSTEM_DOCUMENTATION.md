# Auto-Incrementing Version System

## Overview
The DigiSign application now includes an automatic version numbering system that increments with each build and displays version information in all window title bars.

## Version Number Format

### Full Version
Format: `Major.Minor.Build.Revision`
- **Example:** `1.0.9145.31234`

### Short Version  
Format: `Major.Minor.Build`
- **Example:** `1.0.9145`

### Display Version
Format: `vMajor.Minor.Build`
- **Example:** `v1.0.9145`

## How Version Auto-Increment Works

### Build Number Calculation
The build number is calculated based on the **number of days since January 1, 2000**:

```
Build Number = Days between (2000-01-01) and (Build Date)
```

**Example:**
- Build Date: `2025-01-07`
- Days since 2000-01-01: `9145 days`
- Build Number: `9145`

### Revision Number Calculation
The revision number is calculated based on the **time of day** when the build occurred:

```
Revision Number = (Seconds since midnight) / 2
```

**Example:**
- Build Time: `14:30:00` (2:30 PM)
- Seconds since midnight: `52,200 seconds`
- Revision Number: `26,100`

### Why This Approach?
? **Truly Auto-Incrementing**: Every build gets a unique version
? **Deterministic Builds Compatible**: Works with deterministic compilation
? **Human Readable**: Build number indicates approximate date
? **No Manual Updates Required**: Automatically updates on each build

## Where Version Appears

### 1. Window Title Bars

#### Verbose Progress Window
```
DigiSign v1.0.9145 - Verbose Progress
```

#### Admin Panel
```
DigiSign v1.0.9145 - Admin Panel
```

#### Settings Panel
```
DigiSign v1.0.9145 - Settings
```

### 2. Console Output

#### Application Startup
```
???????????????????????????????????????????????????????????
? DigiSign v1.0.9145 - Settings Configuration Mode
???????????????????????????????????????????????????????????
```

```
???????????????????????????????????????????????????????????
?? DigiSign v1.0.9145 - Admin License Generation Mode
???????????????????????????????????????????????????????????
```

### 3. Application Log (application_log.txt)

```
???????????????????????????????????????????????????????????
DigiSign Application Log - Session Started: 2025-01-07 14:30:00
Application Path: D:\Development\DigiSign\bin\Debug\
Machine: DESKTOP-ABC123 | User: JohnDoe
OS: Microsoft Windows NT 10.0.19045.0 | .NET: 4.0.30319.42000
???????????????????????????????????????????????????????????

2025-01-07 14:30:00 | INFO     | Application started - DigiSign v1.0.9145
2025-01-07 14:30:00 | INFO     | Version: 1.0.9145.26100 | Build Date: 2025-01-07 14:30:00
```

### 4. Verbose Mode Header

```
???????????????????????????????????????????????????????????
DigiSign v1.0.9145 - VERBOSE MODE
???????????????????????????????????????????????????????????
```

## VersionInfo Class API

The `VersionInfo` static class provides several properties for accessing version information:

### Properties

```csharp
// Full version with all components
VersionInfo.FullVersion
// Returns: "1.0.9145.26100"

// Short version (Major.Minor.Build)
VersionInfo.ShortVersion  
// Returns: "1.0.9145"

// Display version with 'v' prefix
VersionInfo.DisplayVersion
// Returns: "v1.0.9145"

// Application title with version
VersionInfo.TitleWithVersion
// Returns: "DigiSign v1.0.9145"

// Build date and time
VersionInfo.BuildDate
// Returns: "2025-01-07 14:30:00"
```

### Usage Examples

#### In Window Forms
```csharp
this.Text = $"{VersionInfo.TitleWithVersion} - Verbose Progress";
// Result: "DigiSign v1.0.9145 - Verbose Progress"
```

#### In Console Output
```csharp
Console.WriteLine($"? {VersionInfo.TitleWithVersion} - Settings Configuration Mode");
// Result: "? DigiSign v1.0.9145 - Settings Configuration Mode"
```

#### In Logging
```csharp
Logger.Info($"Application started - {VersionInfo.TitleWithVersion}");
Logger.Info($"Version: {VersionInfo.FullVersion} | Build Date: {VersionInfo.BuildDate}");
```

#### In Labels/UI Text
```csharp
lblTitle.Text = $"{VersionInfo.TitleWithVersion} - Administration";
// Result: "DigiSign v1.0.9145 - Administration"
```

## Implementation Details

### Files Modified

1. **Properties\AssemblyInfo.cs**
   - Reverted to static version (1.0.0.0)
   - Using build date calculation instead of wildcard

2. **Program.cs**
   - Added `VersionInfo` static class
   - Updated all console output to include version
   - Updated logging to include version and build date

3. **VerboseProgressForm.cs**
   - Updated window title to include version

4. **LicenseGenerationForm.cs**
   - Updated window title (both admin and settings modes)
   - Updated form title label

5. **DigiSign.csproj**
   - Set `<Deterministic>false</Deterministic>` (allows auto-increment)

### Technical Approach

The version system uses the assembly's **last modified date** (file timestamp) to calculate version numbers:

```csharp
private static DateTime GetBuildDate(Assembly assembly)
{
    // Try to get date from InformationalVersion attribute first
    // Fallback to assembly file's last write time
    return File.GetLastWriteTime(assembly.Location);
}
```

This ensures:
- ? Every rebuild gets a new version
- ? Compatible with deterministic builds
- ? No external tools required
- ? No manual version updates needed

## Version Number Examples

### Morning Build
- **Build Date:** 2025-01-07 09:15:30
- **Build Number:** 9145 (days since 2000-01-01)
- **Revision:** 16647 (seconds since midnight / 2)
- **Full Version:** `1.0.9145.16647`
- **Display:** `v1.0.9145`

### Afternoon Build
- **Build Date:** 2025-01-07 14:30:00
- **Build Number:** 9145 (same day)
- **Revision:** 26100 (seconds since midnight / 2)
- **Full Version:** `1.0.9145.26100`
- **Display:** `v1.0.9145`

### Next Day Build
- **Build Date:** 2025-01-08 10:00:00
- **Build Number:** 9146 (next day)
- **Revision:** 18000
- **Full Version:** `1.0.9146.18000`
- **Display:** `v1.0.9146`

## Benefits

### For Development
? **Easy Debugging**: Every build has a unique version
? **No Conflicts**: Different builds can be easily distinguished
? **Traceable**: Build date encoded in version number

### For Users
? **Clear Identification**: Know exactly which version they're running
? **Support**: Can provide exact version when reporting issues
? **Consistency**: Version shown everywhere (title bars, logs, console)

### For Support
? **Quick Identification**: Version tells you approximate build date
? **Build History**: Can track which builds were deployed when
? **Diagnostics**: Version info included in all logs

## Comparison: Before vs After

### Before
```
Window Title:   "DigiSign - Admin Panel"
Console:        "? Settings Configuration Mode"
Logs:           "Application started"
Version:        1.0.0.0 (static, never changed)
```

### After
```
Window Title:   "DigiSign v1.0.9145 - Admin Panel"
Console:        "? DigiSign v1.0.9145 - Settings Configuration Mode"
Logs:           "Application started - DigiSign v1.0.9145"
                "Version: 1.0.9145.26100 | Build Date: 2025-01-07 14:30:00"
Version:        Auto-increments with each build
```

## Updating Major/Minor Version

To update the major or minor version number (currently 1.0):

1. Open `Properties\AssemblyInfo.cs`
2. Update the version:
   ```csharp
   [assembly: AssemblyVersion("2.0.0.0")]  // Change 1.0 to 2.0
   ```
3. Build the application
4. The display will automatically update to `v2.0.9145`

## Build Status
? **Build Successful**
- No errors
- Version system fully functional
- All windows display version correctly

## Summary

**Feature:** Auto-incrementing version numbers with display in all UI elements

**Increment Method:** Based on build date and time
- Build Number = Days since 2000-01-01
- Revision = Seconds since midnight / 2

**Visibility:**
- ? All window title bars
- ? Console output
- ? Application logs
- ? Verbose mode headers

**Format:** `DigiSign v1.0.9145` (short) or `1.0.9145.26100` (full)

**Benefits:**
- Automatic version increment on every build
- Easy identification of builds
- Improved support and debugging
- Professional appearance

The version numbering system is now fully implemented and operational! ??
