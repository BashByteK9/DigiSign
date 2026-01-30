# Verbose Mode - Windows Forms UI Documentation

## Overview

The `/verbose` mode displays detailed, step-by-step progress information in a **professional Windows Forms dialog** during PDF signing operations. The form automatically closes after completion, making it perfect for automation while providing visual feedback.

## Features

### ? Windows Forms GUI
- **Professional dialog window** - Modern, resizable form with progress tracking
- **Rich text display** - Color-coded output with formatted text
- **Progress bar** - Visual indicator of current step (1-10)
- **Real-time updates** - Live updates as each step completes
- **Auto-scroll** - Automatically scrolls to show latest progress

### ? Detailed Progress Display
- **10-step progress tracking** - Shows current step and total steps
- **Per-file progress** - Individual status for each PDF being signed
- **Color-coded output**:
  - **Green (?)** - Success messages
  - **Red (?)** - Errors and failures
  - **Orange (?)** - Warnings
  - **Blue (•)** - Information
  - **Gray** - Details and separators

### ? Auto-Close Functionality
- **Smart timing** - 2 seconds for success, 15 seconds if errors detected
- **Wait button** - Click to prevent auto-close and review details
- **Countdown timer** - Shows remaining time before close
- **Perfect for automation** - Auto-closes on success for unattended operation
- **Manual review option** - Extended time and Wait button when errors occur
- **Manual close option** - Close button always available for immediate exit

## Usage

### Basic Usage
```cmd
DigiSign.exe /verbose
```

When you run this command:
1. A Windows Forms dialog appears immediately
2. Progress updates in real-time as signing proceeds
3. Summary is displayed when complete
4. **If successful:** Form auto-closes after 2 seconds
5. **If errors occurred:** Form shows "Wait" button and waits 15 seconds
6. Click "Wait" button to cancel auto-close and review details

### Example Screenshot Description

The form contains:
```
???????????????????????????????????????????????????
? DigiSign - Verbose Progress                  [_][?][X]?
???????????????????????????????????????????????????
? [8/10] Processing PDF files...                 ?
? ?????????????????????????????? 80%            ?
? Processing PDF 2 of 3...                        ?
???????????????????????????????????????????????????
? ????????????????????????????????????????????????
? ?[1/10] Initializing application...          ??
? ?    • Base Directory: D:\Development\...    ??
? ?[2/10] Loading configuration...             ??
? ?    • License file: license.txt             ??
? ?[3/10] Validating license...                ??
? ?    ? LICENSED - Full cryptographic...     ??
? ?[4/10] Validating XML configuration...      ??
? ?    ? Configuration valid                   ??
? ?[5/10] Creating output directory...         ??
? ?    ? Already exists: D:\Output            ??
? ?[6/10] Filtering PDF files...               ??
? ?    ? Found 3 valid PDF(s)                  ??
? ?[7/10] Loading certificate...               ??
? ?    ? Certificate loaded                    ??
? ?[8/10] Processing PDF files...              ??
? ?                                             ??
? ?    PDF 1/3: document1.pdf                  ??
? ?        • Reading PDF file...               ??
? ?        • Pages: 5                           ??
? ?        ? SUCCESS                            ??
? ?                                             ??
? ?    PDF 2/3: document2.pdf                  ??
? ?        • Reading PDF file...               ??
? ????????????????????????????????????????????????
?                                   [Wait] [Close] ?
???????????????????????????????????????????????????
```

**Note:** The "Wait" button appears when processing completes and auto-close is enabled. Click it to cancel the countdown and review results.

## Progress Steps

### Step 1: Initialization
```
[1/10] Initializing application...
    • Base Directory: D:\Development\DigiSign\
```

### Step 2: Configuration Loading
```
[2/10] Loading configuration...
    • License file: license.txt
    • Config file: IP.xml
```

### Step 3: License Validation  
```
[3/10] Validating license...
    ? LICENSED - Full cryptographic signing enabled
```
or in demo mode:
```
    ? DEMO - Visual text overlay only (no cryptographic signature)
```

### Step 4: XML Validation
```
[4/10] Validating XML configuration...
    ? Configuration valid
    • Input files: 3
    • Output folder: D:\Output
    • Certificate CN: John Doe
```

### Step 5: Output Directory
```
[5/10] Creating output directory...
    ? Created: D:\Output
```

### Step 6: PDF Filtering
```
[6/10] Filtering PDF files...
    ? Found 3 valid PDF(s)
```

### Step 7: Certificate Loading
```
[7/10] Loading certificate...
    • Searching for: John Doe
    ? Certificate loaded
    • Subject: CN=John Doe, O=Company
    • Expiry: 2026-12-31
```

### Step 8: PDF Processing (Detailed)
```
[8/10] Processing PDF files...

    PDF 1/3: document1.pdf
        • Reading PDF file...
        • Pages: 5
        • Signature mode: FULL (cryptographic)
        • Signing: Last page only
        • Creating signature...
        • Applying cryptographic signature...
        • Requesting timestamp...
        ? Timestamp acquired
        • Saving signed PDF: document1.pdf
        ? SUCCESS
        • Output: document1.pdf
```

### Step 9: Summary
```
[9/10] Processing complete

???????????????????????????????????????????????????????????
SUMMARY:
  ? Successful: 3
???????????????????????????????????????????????????????????
```

### Step 9: Summary
```
[9/10] Processing complete

???????????????????????????????????????????????????????????
SUMMARY:
  ? Successful: 3
???????????????????????????????????????????????????????????
```

**If errors occurred:**
```
[9/10] Processing complete

???????????????????????????????????????????????????????????
SUMMARY:
  ? Successful: 2
  ? Failed: 1
???????????????????????????????????????????????????????????
```

### Step 10: Folder Opening
```
[10/10] Opening output folder...
    • D:\Output
    ? Folder opened
```

### Completion
```
???????????????????????????????????????????????????????????
Application completed.
???????????????????????????????????????????????????????????

Auto-closing in 2 seconds...
```

## Form Features

### Progress Bar
- **Range:** 0-10 steps
- **Updates:** After each major step
- **Visual feedback:** Shows completion percentage

### Rich Text Output
- **Color coding:** Different colors for different message types
- **Bold text:** Headers and important messages
- **Monospace font:** Consolas 9pt for code-like output
- **Auto-scroll:** Always shows latest messages

### Status Label
- Shows current operation
- Updates in real-time
- Shows countdown timer when auto-closing

### Close Button
- **Disabled** during processing (prevents accidental closure)
- **Enabled** when complete
- Clicking closes the form immediately

## Comparison: Console vs Windows Forms

### Old Console-Based Verbose Mode
? Text-only console output  
? No visual progress indicator  
? Harder to read  
? No color in some terminals  
? Can't scroll back easily  

### New Windows Forms Verbose Mode
? Professional GUI dialog  
? Visual progress bar  
? Rich formatted text  
? Full color support  
? Scrollable history  
? Resizable window  
? Can maximize  

## Use Cases

### 1. **Automated Batch Processing**
```batch
@echo off
echo Starting PDF signing...
DigiSign.exe /verbose
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Signing failed!
    pause
    exit /b 1
)
echo All PDFs signed successfully!
```

### 2. **Scheduled Tasks**
- Add to Windows Task Scheduler
- Set to run DigiSign.exe with `/verbose` flag  
- Form shows progress, then auto-closes
- Check logs for details if needed

### 3. **Build/Deployment Scripts**
```powershell
Write-Host "Signing release PDFs..."
Start-Process -FilePath "DigiSign.exe" -ArgumentList "/verbose" -Wait -NoNewWindow
if ($LASTEXITCODE -eq 0) {
    Write-Host "? Signing completed" -ForegroundColor Green
} else {
    Write-Host "? Signing failed" -ForegroundColor Red
    exit 1
}
```

### 4. **Visual Monitoring**
- Watch signing progress in real-time
- See which file is currently being processed
- Monitor for warnings or errors
- Verify timestamp acquisition

## Technical Details

### Form Class: `VerboseProgressForm`

**Key Components:**
- `RichTextBox txtProgress` - Scrollable output display
- `ProgressBar progressBar` - 10-step visual progress
- `Label lblCurrentStep` - Current operation
- `Label lblStatus` - Status messages
- `Button btnClose` - Manual close option
- `Button btnWait` - Cancel auto-close (yellow button)
- `Timer autoCloseTimer` - Auto-close countdown
- `bool hasErrors` - Tracks if errors occurred

**Key Methods:**
- `UpdateProgress(step, stepName)` - Update progress bar and title
- `UpdateStatus(status)` - Update status label
- `AppendText(text, color, bold)` - Add formatted text
- `AppendSuccess(text)` - Add green success message
- `AppendError(text)` - Add red error message
- `AppendWarning(text)` - Add orange warning
- `AppendInfo(text)` - Add blue info message
- `ShowSummary(success, fail)` - Display final summary (tracks hasErrors)
- `ProcessingComplete(autoClose, errorCount)` - Finish and optionally auto-close with error count
- `BtnWait_Click()` - Cancel auto-close timer
- `AutoCloseTimer_Tick()` - Update button countdown text every second

### Thread Safety
- All form updates use `Invoke` for thread safety
- `Application.DoEvents()` ensures UI responsiveness
- No blocking operations on UI thread

### Auto-Close Mechanism
```csharp
// In ShowSummary method
hasErrors = failCount > 0;

// In ProcessingComplete method
if (shouldAutoClose)
{
    // Use 15 seconds if there are errors, 2 seconds for success
    autoCloseCountdown = hasErrors ? 15 : 2;
    
    btnWait.Enabled = true; // Enable Wait button
    
    if (hasErrors)
    {
        AppendText($"\n? Errors detected - Auto-closing in {autoCloseCountdown} seconds...\n", Color.Orange);
        AppendText("Click 'Wait' to prevent auto-close.\n", Color.Gray);
    }
    else
    {
        AppendText($"\nAuto-closing in {autoCloseCountdown} seconds...\n", Color.Gray);
        AppendText("Click 'Wait' to prevent auto-close.\n", Color.Gray);
    }
    
    autoCloseTimer.Start();
}
```

### Wait Button Handler
```csharp
private void BtnWait_Click(object sender, EventArgs e)
{
    // Stop auto-close timer
    if (autoCloseTimer.Enabled)
    {
        autoCloseTimer.Stop();
        shouldAutoClose = false;
        btnWait.Enabled = false;
        UpdateStatus("Auto-close cancelled - Click Close to exit");
        AppendText("\nAuto-close cancelled by user.\n", Color.Orange);
    }
}
```

### Helper Method
```csharp
private static void VerboseLog(string message, VerboseLogType type)
{
    switch (type)
    {
        case VerboseLogType.Success:
            verboseForm.AppendSuccess(message);
            break;
        case VerboseLogType.Error:
            verboseForm.AppendError(message);
            break;
        case VerboseLogType.Warning:
            verboseForm.AppendWarning(message);
            break;
        // ...
    }
}
```

## Logging

All verbose mode operations are still logged to `application_log.txt`:

```
2026-01-30 10:00:00 | INFO     | Verbose mode enabled
2026-01-30 10:00:01 | INFO     | Loading certificate: John Doe
2026-01-30 10:00:02 | INFO     | Certificate loaded successfully
2026-01-30 10:00:03 | INFO     | Processing PDF: document.pdf
2026-01-30 10:00:05 | INFO     | PDF digitally signed successfully
2026-01-30 10:00:05 | INFO     | Auto-closing (verbose mode)
```

## Benefits

### ?? **Better User Experience**
- Professional appearance
- Clear visual feedback
- Easy to read and follow

### ?? **Automation-Friendly**
- No user interaction required
- Auto-closes when done
- Returns proper exit codes

### ?? **Transparent Operations**
- See exactly what's happening
- Real-time progress updates
- Color-coded status messages

### ?? **Progress Tracking**
- Visual progress bar
- Step indicators (1/10, 2/10, etc.)
- Per-file status

### ? **Clear Results**
- Summary section with counts
- Success/failure indicators
- Detailed error messages

## Troubleshooting

### Form Doesn't Appear
- Check if application has GUI permissions
- Verify Windows Forms references are present
- Check logs for initialization errors

### Form Appears Behind Other Windows
- Click taskbar to bring to front
- Or add `form.TopMost = true` to force front

### Form Doesn't Auto-Close
- Check if timer started correctly
- Verify `ProcessingComplete(true)` was called
- Look for exceptions in logs
- **Clicking "Stop [X]" cancels auto-close** - this is by design

### Want to Review Errors Longer
- Click "Stop [X]" button when countdown appears
- Auto-close will be cancelled immediately
- Button changes to "Stopped" and becomes disabled
- Review log at your own pace
- Click "Close" when ready

### Button Shows Wrong Countdown
- This is updated every second by the timer
- If countdown seems stuck, check timer is running
- Button text updates: "Stop [15]" ? "Stop [14]" ? etc.

### Text Not Visible
- Resize window (it's resizable)
- Maximize window for more space
- Scroll down to see recent messages

## Summary

The Windows Forms verbose mode transforms DigiSign into a visually appealing, professional PDF signing tool with:

- ?? **Modern GUI** - Professional Windows Forms dialog
- ?? **Visual Progress** - Real-time progress bar and step tracking
- ?? **Color Coding** - Easy to identify success, errors, warnings
- ?? **Auto-Close** - Perfect for automation
- ?? **Comprehensive Logging** - All output also saved to log file
- ?? **Resizable** - Adjustable window size with maximize option

**Use `/verbose` whenever you need transparent, automated PDF signing with beautiful visual feedback!** ??

## Overview

The `/verbose` mode provides detailed, step-by-step progress information during PDF signing operations and automatically closes the application after completion.

## Features

### ? Detailed Progress Display
- **10-step progress tracking** - Shows current step and total steps
- **Real-time status updates** - Updates for each operation
- **Per-file progress** - Individual status for each PDF being signed
- **Color-coded output** - Green for success, red for errors, yellow for warnings

### ? Auto-Close Functionality
- **No manual interaction required** - Automatically closes after completion
- **2-second delay** - Brief pause before closing to see final summary
- **Perfect for automation** - Ideal for batch scripts and scheduled tasks

## Usage

### Basic Usage
```cmd
DigiSign.exe /verbose
```

### Combined with Other Operations
The verbose mode works with normal PDF signing operations. Simply add `/verbose` to the command line:

```cmd
DigiSign.exe /verbose
```

**Note:** `/verbose` mode is for PDF signing only. It does not work with `/admin` mode.

## Progress Steps

When running in verbose mode, you'll see a 10-step progress indicator:

### Step 1: Initialization
```
[1/10] Initializing application...
        Base Directory: D:\Development\DigiSign\
```

### Step 2: Configuration Loading
```
[2/10] Loading configuration...
        License file: license.txt
        Config file: IP.xml
```

### Step 3: License Validation
```
[3/10] Validating license...
        Status: LICENSED - Full cryptographic signing enabled
```
or
```
        Status: DEMO - Visual text overlay only (no cryptographic signature)
```

### Step 4: XML Validation
```
[4/10] Validating XML configuration...
        ? Configuration valid
        Input files: 3
        Output folder: D:\Output
        Certificate CN: John Doe
```

### Step 5: Output Directory
```
[5/10] Creating output directory...
        ? Created: D:\Output
```
or
```
        ? Already exists: D:\Output
```

### Step 6: PDF Filtering
```
[6/10] Filtering PDF files...
        ? Found 3 valid PDF(s)
```

### Step 7: Certificate Loading
```
[7/10] Loading certificate...
        Searching for: John Doe
        ? Certificate loaded
        Subject: CN=John Doe, O=Company
        Expiry: 2026-12-31
```

### Step 8: PDF Processing
```
[8/10] Processing PDF files...

    PDF 1/3: document1.pdf
        • Reading PDF file...
        • Pages: 5
        • Signature mode: FULL (cryptographic)
        • Signing: Last page only
        • Creating signature...
        • Applying cryptographic signature...
        • Requesting timestamp...
        • Timestamp acquired
        • Saving signed PDF: document1.pdf
        ? SUCCESS
        Output: document1.pdf

    PDF 2/3: document2.pdf
        • Reading PDF file...
        • Pages: 3
        • Signature mode: FULL (cryptographic)
        • Signing: Last page only
        • Creating signature...
        • Applying cryptographic signature...
        • Requesting timestamp...
        • Timestamp acquired
        • Saving signed PDF: document2.pdf
        ? SUCCESS
        Output: document2.pdf

    PDF 3/3: document3.pdf
        • Reading PDF file...
        • Pages: 8
        • Signature mode: FULL (cryptographic)
        • Signing: Last page only
        • Creating signature...
        • Applying cryptographic signature...
        • Requesting timestamp...
        ? Timestamp unavailable (continuing without)
        • Saving signed PDF: document3.pdf
        ? SUCCESS
        Output: document3.pdf
```

### Step 9: Summary
```
[9/10] Processing complete

???????????????????????????????????????????????????????????
SUMMARY:
  ? Successful: 3
???????????????????????????????????????????????????????????
```

If there are errors:
```
SUMMARY:
  ? Successful: 2
  ? Failed: 1
```

### Step 10: Folder Opening
```
[10/10] Opening output folder...
         D:\Output
         ? Folder opened
```

### Completion
```
???????????????????????????????????????????????????????????
Application completed.
???????????????????????????????????????????????????????????
Auto-closing in 2 seconds...
```

## Color Coding

The verbose mode uses color-coded output for easy visualization:

- **Green (?)** - Successful operations
- **Red (?)** - Errors and failures
- **Yellow (?)** - Warnings (e.g., timestamp unavailable)
- **Cyan** - Headers and titles
- **White** - Standard information

## Output Examples

### Example 1: Successful Signing (Demo Mode)

```
???????????????????????????????????????????????????????????
DigiSign - VERBOSE MODE
???????????????????????????????????????????????????????????

[1/10] Initializing application...
        Base Directory: D:\Development\DigiSign\
[2/10] Loading configuration...
        License file: license.txt
        Config file: IP.xml
[3/10] Validating license...
        Status: DEMO - Visual text overlay only (no cryptographic signature)
[4/10] Validating XML configuration...
        ? Configuration valid
        Input files: 1
        Output folder: D:\Output
        Certificate CN: Test User
[5/10] Creating output directory...
        ? Already exists: D:\Output
[6/10] Filtering PDF files...
        ? Found 1 valid PDF(s)
[7/10] Loading certificate...
        Searching for: Test User
        ? Certificate loaded
        Subject: CN=Test User
        Expiry: 2027-01-30
[8/10] Processing PDF files...

    PDF 1/1: sample.pdf
        • Reading PDF file...
        • Pages: 2
        • Signature mode: DEMO (visual only)
        • Signing: Last page only
        • Adding visual text overlay...
        • Saving to: sample.pdf
        ? SUCCESS
        Output: sample.pdf

[9/10] Processing complete

???????????????????????????????????????????????????????????
SUMMARY:
  ? Successful: 1
???????????????????????????????????????????????????????????

[10/10] Opening output folder...
         D:\Output
         ? Folder opened

???????????????????????????????????????????????????????????
Application completed.
???????????????????????????????????????????????????????????
Auto-closing in 2 seconds...
```

### Example 2: Error During Signing

```
[8/10] Processing PDF files...

    PDF 1/2: valid.pdf
        • Reading PDF file...
        • Pages: 1
        • Signature mode: FULL (cryptographic)
        • Signing: Last page only
        • Creating signature...
        • Applying cryptographic signature...
        • Requesting timestamp...
        • Timestamp acquired
        • Saving signed PDF: valid.pdf
        ? SUCCESS
        Output: valid.pdf

    PDF 2/2: corrupted.pdf
        • Reading PDF file...
        ? FAILED
        Error: PDF file is corrupted or invalid

[9/10] Processing complete

???????????????????????????????????????????????????????????
SUMMARY:
  ? Successful: 1
  ? Failed: 1
???????????????????????????????????????????????????????????
```

## Use Cases

### 1. **Automated Batch Processing**
```batch
@echo off
for %%f in (*.pdf) do (
    echo Processing %%f...
    DigiSign.exe /verbose
)
echo All PDFs processed!
```

### 2. **Scheduled Tasks**
- Add to Windows Task Scheduler
- Set to run DigiSign.exe with `/verbose` flag
- No manual intervention needed - auto-closes

### 3. **Build/Deployment Scripts**
```powershell
# Sign PDFs as part of deployment
Write-Host "Signing release PDFs..."
& "DigiSign.exe" /verbose
if ($LASTEXITCODE -eq 0) {
    Write-Host "Signing completed successfully"
} else {
    Write-Host "Signing failed" -ForegroundColor Red
    exit 1
}
```

### 4. **Debugging and Troubleshooting**
- See exactly which step fails
- Identify certificate or file issues
- Monitor timestamp service availability

## Comparison: Normal vs Verbose Mode

### Normal Mode
```
? License valid — Full Mode enabled.

?? Admin license detected. Run with /admin flag to generate licenses.
   Example: DigiSign.exe /admin

[Minimal output, waits for user to press a key]
```

### Verbose Mode
```
???????????????????????????????????????????????????????????
DigiSign - VERBOSE MODE
???????????????????????????????????????????????????????????

[1/10] Initializing application...
[2/10] Loading configuration...
[3/10] Validating license...
[4/10] Validating XML configuration...
[5/10] Creating output directory...
[6/10] Filtering PDF files...
[7/10] Loading certificate...
[8/10] Processing PDF files...
    PDF 1/3: file1.pdf
        • Reading PDF file...
        • Pages: 5
        • Creating signature...
        ? SUCCESS
[9/10] Processing complete
[10/10] Opening output folder...

[Detailed output, auto-closes after 2 seconds]
```

## Logging

All verbose mode operations are also logged to `application_log.txt` with full details:

```
2026-01-30 08:00:00 | INFO     | Verbose mode enabled
2026-01-30 08:00:01 | INFO     | Loading certificate: John Doe
2026-01-30 08:00:02 | INFO     | Certificate loaded successfully
2026-01-30 08:00:03 | INFO     | Processing PDF: document.pdf
2026-01-30 08:00:05 | INFO     | PDF digitally signed successfully: document.pdf
2026-01-30 08:00:05 | INFO     | Auto-closing (verbose mode)
```

## Tips and Best Practices

### ? **DO:**
- Use `/verbose` for automated scripts and scheduled tasks
- Monitor the summary section for success/failure counts
- Check `application_log.txt` for detailed error information
- Use verbose mode when troubleshooting signing issues

### ? **DON'T:**
- Use `/verbose` when you need to interact with the application
- Expect user prompts in verbose mode (it auto-closes)
- Use `/verbose` with `/admin` mode (not supported)

## Exit Codes

When running in verbose mode, the application returns standard exit codes:

- **0** - Success (all PDFs signed successfully)
- **Non-zero** - Error occurred during processing

This allows batch scripts to detect failures:

```batch
DigiSign.exe /verbose
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Signing failed!
    exit /b 1
)
echo Success!
```

## Technical Details

### Auto-Close Mechanism
- Checks `shouldAutoClose` flag (set when `/verbose` is detected)
- Waits 2 seconds before exit
- No "Press any key" prompts
- Returns appropriate exit code

### Thread Safety
- All console writes are synchronized
- Logger operations are thread-safe
- No race conditions in verbose output

### Performance Impact
- Minimal overhead (< 100ms total)
- Console writes are buffered
- Does not affect signing speed

## Summary

The `/verbose` mode transforms DigiSign into a fully automated, self-documenting PDF signing tool perfect for:
- ?? **Automation** - Batch scripts and scheduled tasks
- ?? **Debugging** - Detailed step-by-step progress
- ?? **Monitoring** - Real-time status updates
- ? **Validation** - Clear success/failure indicators

**Use it whenever you need transparent, automated PDF signing with no manual intervention!**
