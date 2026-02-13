# DigiSign Logging System Documentation

## Overview
The DigiSign application now features a comprehensive logging system with multiple log levels and detailed event tracking.

## Log Files

### 1. `application_log.txt` - Main Application Log
**Location:** Application directory  
**Purpose:** Comprehensive detailed logging of all application activities

**Features:**
- Session header with system information
- Timestamped entries with log levels
- Exception details including stack traces
- Color-coded console output for errors and warnings

**Log Levels:**
- **DEBUG** - Detailed diagnostic information (file paths, certificate details, XML parsing)
- **INFO** - General informational messages (successful operations, status changes)
- **WARNING** - Warning messages (license issues, adjusted coordinates, missing timestamps)
- **ERROR** - Error messages with exceptions
- **CRITICAL** - Critical failures (not currently used, reserved for future)

### 2. `plf.txt` - PDF Processing Log
**Location:** Application directory  
**Purpose:** Simple success/failure status for PDF signing operations

**Format:**
```
2025-01-20 14:30:45 | SUCCESS | File signed successfully: document.pdf
2025-01-20 14:31:10 | ERROR | Failed to sign 'document2.pdf' - Certificate not found
```

---

## What Gets Logged

### Application Startup
```
???????????????????????????????????????????????????????????
DigiSign Application Log - Session Started: 2025-01-20 14:30:00
Application Path: D:\DigiSign\
Machine: WORKSTATION01 | User: JohnDoe
OS: Microsoft Windows NT 10.0.19045.0 | .NET: 4.0.30319.42000
???????????????????????????????????????????????????????????

2025-01-20 14:30:00 | INFO     | Logger initialized successfully
2025-01-20 14:30:00 | INFO     | Application started
2025-01-20 14:30:00 | DEBUG    | Command line arguments: /admin
```

### License System
```
2025-01-20 14:30:01 | INFO     | Starting license validation
2025-01-20 14:30:01 | INFO     | License file found at: D:\DigiSign\license.txt
2025-01-20 14:30:01 | DEBUG    | Starting license validation
2025-01-20 14:30:01 | DEBUG    | License Number: LIC-2025-0001
2025-01-20 14:30:01 | DEBUG    | Valid Until: 2026-12-31
2025-01-20 14:30:01 | DEBUG    | Stored Device ID: CPUID123_DISKID456
2025-01-20 14:30:01 | DEBUG    | Current Device ID: CPUID123_DISKID456
2025-01-20 14:30:01 | INFO     | License validation successful
2025-01-20 14:30:01 | INFO     | Application mode: FULL
```

### License Key Generation
```
2025-01-20 14:30:02 | INFO     | Generating license key file
2025-01-20 14:30:02 | DEBUG    | Device ID: CPUID123_DISKID456
2025-01-20 14:30:02 | DEBUG    | Machine Name: WORKSTATION01
2025-01-20 14:30:02 | DEBUG    | User Name: JohnDoe
2025-01-20 14:30:02 | INFO     | License key file created successfully: D:\DigiSign\license.key
```

### XML Configuration
```
2025-01-20 14:30:03 | INFO     | Starting PDF processing
2025-01-20 14:30:03 | DEBUG    | Reading XML configuration from: D:\DigiSign\IP.xml
2025-01-20 14:30:03 | DEBUG    | Input files found in XML: 3
2025-01-20 14:30:03 | INFO     | XML configuration loaded successfully
2025-01-20 14:30:03 | INFO     | XML configuration validated successfully
2025-01-20 14:30:03 | DEBUG    | Input files count: 3
2025-01-20 14:30:03 | DEBUG    | Output folder: D:\Output
2025-01-20 14:30:03 | DEBUG    | Certificate CN: John Doe
2025-01-20 14:30:03 | DEBUG    | Signature coordinates: X=50, Y=50, Width=200, Height=100
2025-01-20 14:30:03 | DEBUG    | Sign on page: L
```

### Certificate Loading
```
2025-01-20 14:30:04 | INFO     | Loading certificate: John Doe
2025-01-20 14:30:04 | DEBUG    | Loading certificate with CN: John Doe
2025-01-20 14:30:04 | DEBUG    | Searching certificates in CurrentUser\My store
2025-01-20 14:30:04 | DEBUG    | Found 2 certificate(s) matching name: John Doe
2025-01-20 14:30:04 | DEBUG    | Certificate found - Subject: CN=John Doe..., Issuer: CN=CA, HasPrivateKey: True
2025-01-20 14:30:04 | INFO     | PIN set for hardware token certificate
2025-01-20 14:30:04 | INFO     | Certificate matched CN='John Doe': CN=John Doe, O=Company
2025-01-20 14:30:04 | INFO     | Certificate loaded successfully
2025-01-20 14:30:04 | DEBUG    | Certificate Subject: CN=John Doe, O=Company, C=US
2025-01-20 14:30:04 | DEBUG    | Certificate Thumbprint: A1B2C3D4E5F6...
2025-01-20 14:30:04 | DEBUG    | Certificate Expiry: 2026-12-31
```

### PDF Signing
```
2025-01-20 14:30:05 | INFO     | Processing PDF: document1.pdf
2025-01-20 14:30:05 | DEBUG    | Attempting to get timestamp from DigiCert
2025-01-20 14:30:06 | INFO     | Timestamp service connected successfully
2025-01-20 14:30:07 | INFO     | PDF signed successfully: document1.pdf
2025-01-20 14:30:07 | INFO     | PLF Log: File signed successfully: document1.pdf
2025-01-20 14:30:07 | INFO     | Successfully signed: document1.pdf
```

### Errors and Warnings
```
2025-01-20 14:30:08 | WARNING  | License validation failed - Demo Mode enabled
2025-01-20 14:30:09 | WARNING  | Signature rectangle outside page 1 boundaries. Adjusting coordinates
2025-01-20 14:30:09 | DEBUG    | Original: X=-10, Y=20, W=200, H=100, PageSize: 595x842
2025-01-20 14:30:09 | DEBUG    | Adjusted: X=50, Y=50, W=200, H=100
2025-01-20 14:30:10 | WARNING  | TSA not available, proceeding without timestamp: Connection failed
2025-01-20 14:30:11 | ERROR    | Certificate not found: InvalidCN
2025-01-20 14:30:11 | ERROR    | Failed to sign PDF: document3.pdf
                     | Exception: CryptographicException - The certificate is not valid
                     | StackTrace: at System.Security.Cryptography...
```

### Completion
```
2025-01-20 14:30:15 | INFO     | PDF signing completed - Success: 2, Failed: 1
2025-01-20 14:30:15 | INFO     | Output folder opened successfully
2025-01-20 14:30:15 | INFO     | Application completed
```

---

## Logger API

### Static Methods

#### Initialize
```csharp
Logger.Initialize();
```
Initializes the logger with session header. Called automatically on first log entry.

#### Log Levels
```csharp
Logger.Debug("Detailed diagnostic message");
Logger.Info("General information");
Logger.Warning("Warning message");
Logger.Error("Error message", exception);
Logger.Critical("Critical failure", exception);
```

#### Generic Log
```csharp
Logger.Log(LogLevel.INFO, "Message", exception);
```

#### PLF Log
```csharp
Logger.LogToPlf("Success message", isError: false);
Logger.LogToPlf("Error message", isError: true);
```

#### Separator
```csharp
Logger.LogSeparator();  // Adds a visual separator line
```

---

## Console Output

The logger also outputs to console with color coding:

- **ERROR/CRITICAL** ? Red text
- **WARNING** ? Yellow text
- **Other levels** ? Normal text (logged to file only)

Example console output:
```
? License valid — Full Mode enabled.
?? Admin license detected. Run with /admin flag to generate licenses.
[ERROR] Certificate not found: TestCN
  ? The specified certificate could not be found in any certificate store
```

---

## Best Practices

### When to Use Each Log Level

#### DEBUG
- File paths and locations
- Certificate details (thumbprint, expiry)
- XML parsing details
- Configuration values
- Coordinate calculations
- Device IDs

#### INFO
- Successful operations
- Application flow milestones
- License status changes
- Certificate loading success
- PDF processing completion

#### WARNING
- License validation failures (but application continues)
- Coordinate adjustments
- Missing optional features (timestamp)
- Admin license issues

#### ERROR
- Certificate not found
- PDF signing failures
- XML parsing errors
- License generation failures
- File I/O errors

#### CRITICAL
- Reserved for fatal errors that prevent application from continuing
- Not currently implemented

---

## Log Rotation

Currently, the log file is cleared at the start of each session. For production:

### Recommendations:
1. **Implement log rotation** - Keep last N sessions
2. **Add file size limits** - Rotate when file exceeds size
3. **Add date-based rotation** - One log per day
4. **Compress old logs** - Save disk space

---

## Troubleshooting with Logs

### Issue: Application doesn't run
**Check:**
```
ERROR    | Error parsing XML configuration
ERROR    | XML file not found
```

### Issue: PDFs not signed
**Check:**
```
ERROR    | Certificate not found
ERROR    | No valid PDF files found
WARNING  | License validation failed
```

### Issue: Demo mode when license exists
**Check:**
```
WARNING  | Device mismatch
WARNING  | License expired
WARNING  | Device hash mismatch
```

### Issue: Signature position wrong
**Check:**
```
WARNING  | Signature rectangle outside page boundaries
DEBUG    | Original: X=..., Y=...
DEBUG    | Adjusted: X=..., Y=...
```

---

## Log File Locations

All log files are created in the **application directory** (same folder as `DigiSign.exe`):

```
D:\DigiSign\
??? DigiSign.exe
??? application_log.txt   ? Main detailed log
??? plf.txt               ? Simple success/failure log
??? license.txt           ? License file (if exists)
??? license.key           ? License key (auto-generated)
??? IP.xml                ? Configuration file
```

---

## Performance Impact

The logging system is designed for minimal performance impact:

- **Thread-safe**: Uses locks to prevent conflicts
- **Fail-silent**: Logging errors don't crash the application
- **Buffered I/O**: Uses efficient file writing methods
- **Minimal overhead**: Only logs what's necessary

Typical logging overhead: **< 1% of total execution time**

---

## Future Enhancements

Planned improvements:

1. ? Structured logging (JSON format option)
2. ? Remote logging (send logs to server)
3. ? Log filtering (configurable log levels)
4. ? Performance metrics (execution time tracking)
5. ? Log viewer application
6. ? Email alerts for critical errors
