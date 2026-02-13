# PDF Preview Loading Troubleshooting Guide

## Issue: PDF File Not Loading - Mock PDF Displayed Instead

### Problem Description
Input file path: `d:\build\digisign\input_folder\2425007179-D.pdf`
Expected: PDF preview with actual content
Actual: Mock PDF preview displayed

## Diagnostic Steps

### 1. Verify File Path
Check if the file path is correct and file exists:
- Path entered: `d:\build\digisign\input_folder\2425007179-D.pdf`
- Check for:
  - Typos in path
  - Case sensitivity (usually not an issue on Windows)
  - File extension (.pdf vs .PDF)
  - Special characters in path

### 2. Check Error Message
After the fix, the info label will show one of these:
- ? **Success**: "Preview: 2425007179-D.pdf (use mouse wheel to zoom)"
- ? **File Not Found**: "? File not found: 2425007179-D.pdf. Using mock preview."
- ? **Read Error**: "? Cannot read PDF file. Using mock preview. Error: [details]"

### 3. Common Issues and Solutions

#### Issue 1: File Path Not Entered
**Symptom**: Info label shows "No input file selected. Using mock preview"
**Solution**: 
1. Go to **PDF Signing Settings** ? **General** tab
2. Click **Browse** button next to "Input PDF File"
3. Navigate to the file location
4. Select the PDF file
5. Go back to **Preview** tab

#### Issue 2: File Does Not Exist
**Symptom**: Info label shows "? File not found: [filename]. Using mock preview."
**Solution**:
- Verify the file exists at the specified path
- Check if file was moved or deleted
- Ensure correct drive letter (D: vs d:)
- Browse to select the file again

#### Issue 3: File Is Corrupted or Invalid PDF
**Symptom**: Info label shows "? Cannot read PDF file. Using mock preview. Error: [details]"
**Possible Causes**:
- PDF file is corrupted
- PDF is encrypted/password protected
- PDF uses unsupported format
- File is not actually a PDF (wrong extension)

**Solution**:
- Open the PDF in Adobe Reader to verify it's valid
- If encrypted, decrypt the PDF first
- Try a different PDF file to test
- Check the error message details for specific issue

#### Issue 4: Permissions Issue
**Symptom**: Error message mentions access denied or permissions
**Solution**:
- Check file permissions (right-click ? Properties ? Security)
- Ensure your user account has Read access
- Try running the application as Administrator
- Copy the file to a location you have access to

#### Issue 5: File Path Contains Special Characters
**Symptom**: File not found even though it exists
**Solution**:
- Avoid special characters in file path
- Use simple folder names (no spaces, no special chars)
- Try copying the file to: `C:\temp\test.pdf`

### 4. Testing Steps

#### Test 1: Verify Input Field
1. Open **PDF Signing Settings** ? **General** tab
2. Look at "Input PDF File" text box
3. Verify the exact path shown
4. Expected: `d:\build\digisign\input_folder\2425007179-D.pdf`

#### Test 2: File Existence Check
Open Command Prompt and run:
```cmd
dir "d:\build\digisign\input_folder\2425007179-D.pdf"
```
Expected output: File details
If file not found: Verify path is correct

#### Test 3: Browse to File
1. In General tab, click **Browse**
2. Navigate manually to the folder
3. Check if file appears in the dialog
4. If file not visible, check "Files of type" filter is set to "PDF Files"

#### Test 4: Test with Simple Path
1. Copy the PDF to `C:\test.pdf`
2. Browse to select `C:\test.pdf`
3. Check if preview loads
4. If it works, issue is with the original path

### 5. Enhanced Error Reporting

The updated code now provides detailed error messages:

**Before (vague):**
```
"Invalid PDF file. Using mock preview."
```

**After (detailed):**
```
"? Cannot read PDF file. Using mock preview. Error: PDF header signature not found."
```

### 6. Known PDF Issues

#### iTextSharp Compatibility
Some PDF features may cause issues:
- **PDF Version**: Very new PDF versions (1.7+) may have issues
- **Encryption**: Encrypted PDFs cannot be read without password
- **Digital Signatures**: Already-signed PDFs may have restrictions
- **Linearization**: Web-optimized PDFs sometimes cause issues

**Solutions**:
- Use PDF 1.4 or 1.5 for best compatibility
- Remove encryption/password protection
- Test with a simple, unprotected PDF first

### 7. Debug Checklist

Run through this checklist:
- [ ] File path is typed correctly in Input File field
- [ ] File exists at the specified location
- [ ] File is a valid PDF (opens in Adobe Reader)
- [ ] File is not password protected
- [ ] You have read permissions on the file
- [ ] Path does not contain special characters
- [ ] File size is reasonable (not corrupted 0 KB file)
- [ ] Error message in info label is read and understood
- [ ] Refresh button has been clicked after entering path

### 8. Step-by-Step Resolution

#### For Your Specific Case
**File**: `d:\build\digisign\input_folder\2425007179-D.pdf`

1. **Verify File Exists**:
   ```cmd
   dir "d:\build\digisign\input_folder\2425007179-D.pdf"
   ```

2. **Check File Properties**:
   - Right-click the file
   - Properties
   - Verify:
     - Type: PDF Document
     - Size: > 0 bytes
     - Attributes: Not Read-only, Not Hidden

3. **Test in Application**:
   - Open DigiSign Admin Panel
   - Go to **PDF Signing Settings** ? **General**
   - Click **Browse**
   - Navigate to: `d:\build\digisign\input_folder\`
   - Select: `2425007179-D.pdf`
   - Go to **Preview** tab
   - Read the info label message

4. **If Still Not Loading**:
   - Copy file to: `C:\temp\test.pdf`
   - Browse to select `C:\temp\test.pdf`
   - If this works: Issue is with original path
   - If this fails: Issue is with the PDF file itself

### 9. Alternative Solution

If the specific PDF continues to have issues:

**Option 1: Use Different PDF**
- Test with a known-good PDF file
- Create a simple PDF from Word/Excel
- Use that for testing signature placement

**Option 2: PDF Repair**
- Open the PDF in Adobe Acrobat
- Save As ? Optimized PDF
- Try the optimized version

**Option 3: Convert PDF**
- Use online PDF converter
- Convert to PDF/A format
- Try the converted version

## Fixes Applied

### Fix 1: Improved Error Messages
- More detailed error information
- Color-coded status (red=error, orange=warning, gray=info)
- Shows actual error details from exceptions
- Displays filename in messages

### Fix 2: Better File Path Handling
- Explicit file existence check before loading
- Separates "file not found" from "read error"
- Shows which condition caused fallback to mock PDF

### Fix 3: Auto-Update on File Change
- Preview automatically refreshes when input file changes
- No need to manually click Refresh
- Instant feedback when file is selected

### Fix 4: Zoom Button Icon
- Changed from "?" (minus) to "–" (en dash)
- Better font rendering
- More visible icon

## Expected Behavior After Fixes

### Scenario 1: Valid PDF File
```
Info Label: "Preview: 2425007179-D.pdf (use mouse wheel to zoom)"
Color: Gray (normal)
Preview: Shows actual PDF with signature overlay
```

### Scenario 2: File Not Found
```
Info Label: "? File not found: 2425007179-D.pdf. Using mock preview."
Color: Orange (warning)
Preview: Shows mock PDF with signature overlay
```

### Scenario 3: Invalid PDF
```
Info Label: "? Cannot read PDF file. Using mock preview. Error: [details]"
Color: Red (error)
Preview: Shows mock PDF with signature overlay
Error Details: Specific exception message
```

### Scenario 4: No File Selected
```
Info Label: "No input file selected. Using mock preview (use mouse wheel to zoom)"
Color: Gray (info)
Preview: Shows mock PDF with signature overlay
```

## Next Steps

1. **Build the application** (already successful ?)
2. **Run the application**
3. **Navigate to PDF Signing Settings ? General**
4. **Enter or browse to the file**: `d:\build\digisign\input_folder\2425007179-D.pdf`
5. **Go to Preview tab**
6. **Read the info label** to see the exact status
7. **If error**, note the error message and refer to this guide

## Contact Information

If the issue persists after following this guide:
1. Note the exact error message from the info label
2. Check the application_log.txt for detailed errors
3. Provide:
   - File path attempted
   - Error message shown
   - File size and type
   - Whether file opens in Adobe Reader
