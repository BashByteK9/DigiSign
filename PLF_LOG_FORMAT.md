# PLF.txt File Format - Host Application Integration

## Purpose
The `plf.txt` file provides a simple status log for the host application to determine if PDF processing was successful or failed. The format is intentionally kept simple and consistent across demo and licensed modes to ensure reliable parsing.

---

## File Format

### Success Format
```
File(s) Signed Successfully - [filename.pdf]
```

### Error Format
```
ERROR [error message]
```

---

## Example Content

### Successful Processing
```
File(s) Signed Successfully - document.pdf
```

### Failed Processing
```
ERROR Failed to sign 'document2.pdf' - Certificate not found
```

---

## Important: Consistent Across Modes

### ? BOTH Demo Mode AND Licensed Mode Use Same Format

**Demo Mode (No cryptographic signature applied):**
```
File(s) Signed Successfully - document.pdf
```

**Licensed Mode (Real cryptographic signature applied):**
```
File(s) Signed Successfully - document.pdf
```

### Why?
The host application logic depends on a consistent `plf.txt` format. The host application:
- ? Checks for "Successfully" for success status
- ? Checks for "ERROR" for failure status
- ? Extracts filename from the message
- ? Determines if processing completed

The host application **does not need to know** if the PDF was cryptographically signed or just had demo text added. It only needs to know if the file was successfully processed.

---

## Parsing Logic for Host Application

### Success Detection
```csharp
string plfContent = File.ReadAllText("plf.txt").Trim();
bool isSuccess = plfContent.Contains("Successfully");
```

### Error Detection
```csharp
string plfContent = File.ReadAllText("plf.txt").Trim();
bool isError = plfContent.StartsWith("ERROR");
```

### Extract Filename
```csharp
// Format: File(s) Signed Successfully - filename.pdf
string plfContent = File.ReadAllText("plf.txt").Trim();
if (plfContent.Contains("Successfully"))
{
    string[] parts = plfContent.Split('-');
    if (parts.Length >= 2)
    {
        string filename = parts[parts.Length - 1].Trim();
    }
}
```

---

## Status Values

| Content Pattern | Meaning | Use Case |
|----------------|---------|----------|
| Contains "Successfully" | File processed successfully | Both demo and licensed mode |
| Starts with "ERROR" | File processing failed | Certificate not found, invalid PDF, etc. |

---

## Detailed Logging vs PLF Logging

### PLF Log (plf.txt)
- **Purpose:** Simple status for host application
- **Format:** Plain text message only
- **Content:** SUCCESS or ERROR message
- **Updated:** Once per execution (overwritten)

### Application Log (application_log.txt)
- **Purpose:** Detailed diagnostic information
- **Format:** Timestamped with multiple log levels (DEBUG, INFO, WARNING, ERROR)
- **Content:** Full execution trace with details
- **Updated:** Appended for each session

### Comparison

| Aspect | plf.txt | application_log.txt |
|--------|---------|---------------------|
| **Audience** | Host application (automated) | Developers/Support (manual) |
| **Format** | Simple message | Timestamped, multi-level |
| **Demo Mode Info** | Not included | Fully detailed |
| **Signature Status** | Not included | Fully detailed |
| **Timestamp** | Not included | Included |
| **File Operation** | Overwrite | Append |
| **Line Count** | 1 line | Hundreds of lines |

---

## Example Usage in Host Application

### C# Example
```csharp
public class PdfProcessingResult
{
    public bool Success { get; set; }
    public string Filename { get; set; }
    public string ErrorMessage { get; set; }
}

public static PdfProcessingResult ReadPlfLog(string plfPath)
{
    try
    {
        string content = File.ReadAllText(plfPath).Trim();
        
        var result = new PdfProcessingResult();
        
        if (content.Contains("Successfully"))
        {
            result.Success = true;
            // Extract filename from: "File(s) Signed Successfully - filename.pdf"
            var parts = content.Split('-');
            if (parts.Length >= 2)
                result.Filename = parts[parts.Length - 1].Trim();
        }
        else if (content.StartsWith("ERROR"))
        {
            result.Success = false;
            result.ErrorMessage = content;
        }
        else
        {
            result.Success = false;
            result.ErrorMessage = "Unknown status";
        }
        
        return result;
    }
    catch (Exception ex)
    {
        return new PdfProcessingResult 
        { 
            Success = false, 
            ErrorMessage = ex.Message 
        };
    }
}

// Usage
var result = ReadPlfLog("plf.txt");
if (result.Success)
{
    Console.WriteLine($"Successfully processed: {result.Filename}");
}
else
{
    Console.WriteLine($"Error: {result.ErrorMessage}");
}
```

### VB.NET Example
```vb
Public Function ReadPlfLog(plfPath As String) As Boolean
    Try
        Dim content As String = File.ReadAllText(plfPath).Trim()
        Return content.Contains("Successfully")
    Catch ex As Exception
        Return False
    End Try
End Function

' Usage
If ReadPlfLog("plf.txt") Then
    MsgBox "PDF processing successful"
Else
    MsgBox "PDF processing failed"
End If
```

---

## Error Messages Format

### Common Errors
```
ERROR Certificate not found: [CN Name]
ERROR No valid PDF files found
ERROR Invalid XML data in [path]
ERROR Failed to sign '[filename]' - [exception message]
```

### Error Detection Pattern
```csharp
string content = File.ReadAllText("plf.txt").Trim();
if (content.StartsWith("ERROR"))
{
    // Extract error message (everything after "ERROR ")
    string errorMsg = content.Substring(6); // Skip "ERROR "
    
    // Log or display error
    Console.WriteLine($"Processing failed: {errorMsg}");
}
```

---

## File Location

**Fixed Location:**
```
[Application Directory]\plf.txt
```

**Example:**
```
D:\Development\DigiSign\plf.txt
```

---

## Best Practices for Host Application

1. **Always Check File Exists**
   ```csharp
   if (!File.Exists("plf.txt"))
   {
       // DigiSign may not have run yet
       return;
   }
   ```

2. **Handle Parse Errors**
   ```csharp
   try
   {
       var result = ReadPlfLog("plf.txt");
   }
   catch (Exception ex)
   {
       // Handle parsing errors
   }
   ```

3. **Don't Assume Format Details**
   - Check for "SUCCESS" or "ERROR" keywords
   - Don't parse implementation-specific details
   - Format may be enhanced in future versions

4. **Use Timeout for File Access**
   ```csharp
   // Wait for file to be written and closed
   Thread.Sleep(100);
   using (var fs = new FileStream("plf.txt", FileMode.Open, FileAccess.Read, FileShare.Read))
   {
       // Read file
   }
   ```

---

## Version History

### Version 1.0 (Original)
```
[message]
```
Simple message without timestamp or status.

### Version 2.0 (Current)
```
File(s) Signed Successfully - [filename.pdf]
```
Simple message format for easy parsing.

### Compatibility
- Success indicated by "Successfully" in message
- Errors indicated by "ERROR" prefix
- No timestamp needed for simple status checking
- Host application can easily parse filename

---

## Summary

### ? Key Points

1. **Simple Format:** plf.txt contains only the message, no timestamp or status prefix
2. **Consistent Content:** Identical for demo and licensed modes
3. **Easy Parsing:** Just check for "Successfully" or "ERROR"
4. **Host Integration:** Designed for straightforward integration
5. **One Line:** Single line per execution (file overwritten)

### ? What NOT to Expect

1. ? Timestamp in plf.txt
2. ? Status prefix (SUCCESS/ERROR label)
3. ? Demo mode indicator
4. ? Signature validation status
5. ? Multiple lines per execution

### ?? For Detailed Information

Use `application_log.txt` for:
- Timestamps for each operation
- Demo vs licensed mode status
- Signature details
- Certificate information
- Full error stack traces
- Execution flow details

---

**File:** plf.txt  
**Format:** Simple message only (no timestamp, no status prefix)  
**Example Success:** `File(s) Signed Successfully - document.pdf`  
**Example Error:** `ERROR Certificate not found`  
**Compatibility:** Both demo and licensed modes use identical format
