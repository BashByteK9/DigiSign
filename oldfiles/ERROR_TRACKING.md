# Error Tracking in Verbose Mode

## Overview

The verbose mode tracks **actual processing errors**, not warnings. Demo mode (invalid/missing license) is considered a **warning** because the application still functions - it just adds visual overlays instead of cryptographic signatures.

## Important Distinction: Errors vs Warnings

### ?? **Warnings (Do NOT trigger 15-second countdown)**
- **Invalid License** - Application runs in demo mode
- **Missing License** - Application runs in demo mode
- Demo mode is fully functional, just without cryptographic signing

### ? **Errors (DO trigger 15-second countdown)**
- Invalid XML configuration
- Certificate not found
- No valid PDF files
- PDF signing failures

## Tracked Error Types

### 1. **License Validation Errors**
```csharp
// Invalid license
if (!ValidateLicense(licensePath))
{
    totalErrorCount++; // Counted!
    isDemoMode = true;
}

// Missing license file
if (!File.Exists(licensePath))
{
    totalErrorCount++; // Counted!
    isDemoMode = true;
}
```

**Triggers:**
- Invalid license file
- License used on different device
- Missing license.txt file
- Corrupted license data

**User Impact:**
- ? Warning message: "DEMO - Visual text overlay only"
- ?? Auto-close timer: 15 seconds

---

### 2. **XML Configuration Errors**
```csharp
if (xmlData == null || 
    !xmlData.InputFilePaths.Any() || 
    string.IsNullOrEmpty(xmlData.OutputFolderPath) || 
    string.IsNullOrEmpty(xmlData.CommonName))
{
    totalErrorCount++; // Counted!
}
```

**Triggers:**
- Missing IP.xml file
- Corrupted XML data
- No input file paths specified
- Missing output folder path
- Missing certificate common name

**User Impact:**
- ? Error message: "Invalid XML configuration"
- ?? Auto-close timer: 15 seconds

---

### 3. **Certificate Errors**
```csharp
if (cert == null)
{
    totalErrorCount++; // Counted!
}
```

**Triggers:**
- Certificate not found in certificate store
- Certificate CN doesn't match specified name
- Certificate expired
- Certificate not accessible

**User Impact:**
- ? Error message: "Certificate not found: {commonName}"
- ?? Auto-close timer: 15 seconds

---

### 4. **No Valid PDF Files** ?
```csharp
if (!validPdfFiles.Any())
{
    totalErrorCount++; // Counted!
}
```

**Triggers:**
- No PDF files in input list
- All input files are missing
- All input files are invalid/corrupted
- Files don't have .pdf extension

**User Impact:**
- ? Error message: "No valid PDF files found"
- ?? Auto-close timer: 15 seconds

---

### 5. **PDF Signing Failures**
```csharp
try
{
    SignPdfWithITextSharp(...);
    successCount++;
}
catch (Exception ex)
{
    failCount++;
    // Later: totalErrorCount += failCount;
}
```

**Triggers:**
- Corrupted PDF file
- Insufficient permissions
- Invalid signature parameters
- Out of memory
- PDF is password protected
- Certificate private key not accessible

**User Impact:**
- ? Error message: "FAILED - Error: {error message}"
- ?? Auto-close timer: 15 seconds (if any PDF fails)

---

## Error Count Accumulation

The `totalErrorCount` variable accumulates only **actual errors** throughout the application lifecycle:

```csharp
int totalErrorCount = 0; // Initialized at start

// Demo mode - NOT counted (it's a warning)
if (license invalid) { } // No increment - demo mode works fine
if (license missing) { } // No increment - demo mode works fine

// During XML validation  
if (xml invalid) totalErrorCount++; // ? Counted

// During certificate loading
if (cert not found) totalErrorCount++; // ? Counted

// During PDF validation
if (no PDFs found) totalErrorCount++; // ? Counted

// During PDF signing
totalErrorCount += failCount; // ? Add all signing failures

// At completion
verboseForm.ProcessingComplete(true, totalErrorCount);
```

## Auto-Close Logic

```csharp
public void ProcessingComplete(bool autoClose = true, int errorCount = 0)
{
    // Set hasErrors based on errorCount parameter
    hasErrors = errorCount > 0;
    
    // Use 15 seconds if there are errors, 2 seconds for success
    autoCloseCountdown = hasErrors ? 15 : 2;
    
    if (hasErrors)
    {
        // Shows: "? Errors detected (X errors) - Auto-closing in 15 seconds..."
        AppendText($"\n? Errors detected ({errorCount} error{(errorCount > 1 ? "s" : "")}) - Auto-closing in {autoCloseCountdown} seconds...\n", Color.Orange);
    }
    else
    {
        // Shows: "Auto-closing in 2 seconds..."
        AppendText($"\nAuto-closing in {autoCloseCountdown} seconds...\n", Color.Gray);
    }
}
```

## Examples

### Example 1: Invalid License
```
[3/10] Validating license...
    ? DEMO - Visual text overlay only (no cryptographic signature)

[Processing continues in demo mode...]

???????????????????????????????????????????????????????????
Application completed.
???????????????????????????????????????????????????????????

? Errors detected (1 error) - Auto-closing in 15 seconds...
Click 'Stop' button to prevent auto-close.

[Stop [15]] [Close]
```

### Example 2: Certificate Not Found (Real Error)
```
[7/10] Loading certificate...
    • Searching for: John Doe
    ? Certificate not found: John Doe

???????????????????????????????????????????????????????????
Application completed.
???????????????????????????????????????????????????????????

? Errors detected (1 error) - Auto-closing in 15 seconds...
Click 'Stop' button to prevent auto-close.

[Stop [15]] [Close]
```

### Example 3: Demo Mode + PDF Signing Failure
```
[3/10] Validating license...
    ? DEMO - Visual text overlay only

[8/10] Processing PDF files...
    PDF 1/3: document1.pdf
        ? SUCCESS
    
    PDF 2/3: corrupted.pdf
        ? FAILED
        Error: PDF file is corrupted
    
    PDF 3/3: document3.pdf
        ? SUCCESS

[9/10] Processing complete

???????????????????????????????????????????????????????????
SUMMARY:
  ? Successful: 2
  ? Failed: 1
???????????????????????????????????????????????????????????

? Errors detected (2 errors) - Auto-closing in 15 seconds...
Click 'Stop' button to prevent auto-close.

[Stop [15]] [Close]
```

### Example 4: No Errors (Success)
```
[3/10] Validating license...
    ? LICENSED - Full cryptographic signing enabled

[8/10] Processing PDF files...
    PDF 1/3: document1.pdf
        ? SUCCESS
    PDF 2/3: document2.pdf
        ? SUCCESS
    PDF 3/3: document3.pdf
        ? SUCCESS

[9/10] Processing complete

???????????????????????????????????????????????????????????
SUMMARY:
  ? Successful: 3
???????????????????????????????????????????????????????????

Auto-closing in 2 seconds...
Click 'Stop' button to prevent auto-close.

[Stop [2]] [Close]
```

## Benefits

### ? **Comprehensive Error Detection**
- Catches ALL error types, not just signing failures
- No error goes unnoticed
- Proper error counting in summary

### ?? **Smart Timing**
- 2 seconds for 100% success (fast automation)
- 15 seconds for ANY error (time to review)
- No guessing if something went wrong

### ?? **Clear User Feedback**
- Error count shown: "(3 errors)"
- Singular/plural grammar: "1 error" vs "2 errors"
- Orange warning color for visibility

### ?? **Better Troubleshooting**
- See exactly what failed
- Multiple error types clearly identified
- Full log available in application_log.txt

## Tracked vs Not Tracked

### ? **Tracked as Errors (15-second countdown):**
| Error Type | Status |
|-----------|--------|
| Invalid XML Configuration | ? Counted |
| Certificate Not Found | ? Counted |
| No Valid PDF Files | ? Counted |
| PDF Signing Failures | ? Counted |

### ?? **NOT Tracked as Errors (2-second countdown):**
| Warning Type | Status |
|-----------|--------|
| Invalid License (Demo Mode) | ?? Warning only |
| Missing License (Demo Mode) | ?? Warning only |

**Reason**: Demo mode is fully functional - PDFs are still processed and marked with visual overlays. It's not a failure, just a different mode of operation.

### Variable Scope
```csharp
// Declared at method start - accessible throughout
int totalErrorCount = 0;

// Accumulates through entire process
// License check -> XML validation -> Cert loading -> PDF signing

// Finally passed to form
verboseForm.ProcessingComplete(true, totalErrorCount);
```

### Thread Safety
All error tracking happens on the main thread, so no synchronization needed for `totalErrorCount`.

### Logging
Every error increment is accompanied by a log entry:
```csharp
totalErrorCount++;
Logger.Error("Specific error message");
```

## Testing Scenarios

### Test Case 1: Invalid License
1. Rename/delete license.txt
2. Run `DigiSign.exe /verbose`
3. Expected: 15-second countdown, "1 error"

### Test Case 2: Missing Certificate
1. Use wrong CN in IP.xml
2. Run `DigiSign.exe /verbose`
3. Expected: 15-second countdown, "1 error"

### Test Case 3: Corrupted PDF
1. Create invalid PDF file
2. Add to input list in IP.xml
3. Run `DigiSign.exe /verbose`
4. Expected: 15-second countdown, shows failed count

### Test Case 4: Multiple Errors
1. Remove license.txt
2. Use corrupted PDF
3. Run `DigiSign.exe /verbose`
4. Expected: 15-second countdown, "2+ errors"

### Test Case 5: Perfect Success
1. Valid license
2. Valid PDFs
3. Run `DigiSign.exe /verbose`
4. Expected: 2-second countdown, no errors

## Conclusion

The verbose mode now provides **comprehensive error tracking** across all stages of PDF signing:
- ? License validation
- ? XML configuration
- ? Certificate loading
- ? PDF file validation
- ? Signing operations

Any error triggers the 15-second countdown, giving users time to review what went wrong before the form auto-closes.
