# Demo Mode Verification Guide

## Problem Fixed
The demo mode watermark was not clearly visible on signed PDFs. The following enhancements have been made:

### ? Changes Made

1. **Added Debug Logging**
   - Logs when demo mode is active
   - Logs the signature text being applied
   - Logs each line being drawn on the PDF
   - Logs when the DEMO MODE watermark is drawn

2. **Enhanced Visual Appearance**
   - Demo mode watermark ("*** DEMO MODE ***") is now **RED**
   - Clearly distinguishable from normal signature text
   - Maintains same font size as other text

3. **Improved Logging**
   - Check `application_log.txt` to verify demo mode status
   - See exact signature text being applied
   - Trace each line of text being drawn

---

## How to Verify Demo Mode

### Step 1: Remove License File
```bash
# Navigate to application directory
cd D:\Development\DigiSign

# Remove or rename license.txt
del license.txt
# OR
ren license.txt license.txt.backup
```

### Step 2: Run the Application
```bash
DigiSign.exe
```

### Step 3: Check Console Output
You should see:
```
?? License file not found — Demo Mode enabled.
?? License Key File Generated
```

### Step 4: Check application_log.txt
Look for these entries:
```
INFO     | License file not found - Demo Mode enabled
INFO     | Application mode: DEMO
INFO     | Starting PDF signing - Demo Mode: True
DEBUG    | Signature text: John Doe | Digitally signed by John Doe | Date: 20.01.2025 14:30:00 | *** DEMO MODE ***
DEBUG    | Drawing 3 signature text lines (excluding CN)
DEBUG    | Processing signature line: Digitally signed by John Doe
DEBUG    | Processing signature line: Date: 20.01.2025 14:30:00
DEBUG    | Processing signature line: *** DEMO MODE ***
DEBUG    | Drawing DEMO MODE watermark in RED at Y=...
```

### Step 5: Verify the Signed PDF
Open the signed PDF and check the signature:

**Expected Result:**
```
John Doe
Digitally signed by John Doe
Date: 20.01.2025 14:30:00
*** DEMO MODE ***     ? This should be in RED
```

---

## Troubleshooting

### Issue: "*** DEMO MODE ***" text not visible

**Check 1: Verify Demo Mode is Active**
```
Look in application_log.txt for:
"Application mode: DEMO"
```

**Check 2: Verify Signature Text is Correct**
```
Look in application_log.txt for:
"Signature text: ... | *** DEMO MODE ***"
```

**Check 3: Verify Text is Being Drawn**
```
Look in application_log.txt for:
"Processing signature line: *** DEMO MODE ***"
"Drawing DEMO MODE watermark in RED at Y=..."
```

**Check 4: Verify Signature Box Size**
The signature box must be large enough to fit all lines:
- CN line
- "Digitally signed by..." line
- Date line
- "*** DEMO MODE ***" line

If the box is too small, the last line may be cut off.

**Solution:** Increase the signature box height in IP.xml

---

### Issue: Demo mode active but text is black (not red)

**Check:**
```
Look in application_log.txt for:
"Drawing DEMO MODE watermark in RED"
```

If this message is NOT present, the color change code is not executing.

**Possible Causes:**
1. The line doesn't contain "DEMO MODE" (check exact text)
2. `isDemoMode` is false when it should be true

---

### Issue: License.txt is missing but app runs in Full Mode

**Check application_log.txt:**
```
Should show:
"License file not found - Demo Mode enabled"
"Application mode: DEMO"

Should NOT show:
"License validation successful"
"Application mode: FULL"
```

**If showing Full Mode:**
- Check if license.txt still exists in the directory
- Check if there's a license.txt in a parent directory
- Verify the file path in logs

---

## Demo Mode Signature Appearance

### Demo Mode (license.txt missing or invalid):
```
??????????????????????????????????????
? John Doe                           ?
? Digitally signed by John Doe       ?
? Date: 20.01.2025 14:30:00         ?
? *** DEMO MODE ***    ? RED TEXT    ?
??????????????????????????????????????
```

### Full Mode (valid license.txt):
```
??????????????????????????????????????
? John Doe                           ?
? Digitally signed by John Doe       ?
? Date: 20.01.2025 14:30:00         ?
??????????????????????????????????????
```

---

## Quick Test Procedure

1. **Delete license.txt**
2. **Run:** `DigiSign.exe`
3. **Sign a PDF**
4. **Open signed PDF**
5. **Look for RED "*** DEMO MODE ***" text in signature**
6. **Check application_log.txt** for confirmation

---

## Log Messages to Look For

### Demo Mode Activation
```
2025-01-20 14:30:01 | INFO     | License file not found - Demo Mode enabled
2025-01-20 14:30:01 | INFO     | Application mode: DEMO
```

### Signature Generation
```
2025-01-20 14:30:05 | INFO     | Starting PDF signing - Demo Mode: True
2025-01-20 14:30:05 | DEBUG    | Signature text: John Doe | Digitally signed by John Doe | Date: 20.01.2025 14:30:00 | *** DEMO MODE ***
```

### Watermark Drawing
```
2025-01-20 14:30:05 | DEBUG    | Drawing 3 signature text lines (excluding CN)
2025-01-20 14:30:05 | DEBUG    | Processing signature line: Digitally signed by John Doe
2025-01-20 14:30:05 | DEBUG    | Processing signature line: Date: 20.01.2025 14:30:00
2025-01-20 14:30:05 | DEBUG    | Processing signature line: *** DEMO MODE ***
2025-01-20 14:30:05 | DEBUG    | Drawing DEMO MODE watermark in RED at Y=75.5
```

---

## Signature Box Size Recommendations

To ensure all text is visible, use these minimum dimensions in IP.xml:

```xml
<FILENAME>200</FILENAME> <!-- Width: minimum 200 -->
<FILENAME>100</FILENAME> <!-- Height: minimum 100 for 4 lines -->
```

**Formula for Height:**
```
Minimum Height = (Number of Lines × Line Height) + (2 × Padding)
                = (4 lines × 20 pixels) + (2 × 10 pixels)
                = 100 pixels
```

For longer CN names or dates, increase accordingly.

---

## Expected Behavior Summary

| Scenario | Demo Mode | Watermark Color | Text Content |
|----------|-----------|-----------------|--------------|
| No license.txt | ? Yes | ?? Red | "*** DEMO MODE ***" |
| Invalid license.txt | ? Yes | ?? Red | "*** DEMO MODE ***" |
| Expired license.txt | ? Yes | ?? Red | "*** DEMO MODE ***" |
| Device mismatch | ? Yes | ?? Red | "*** DEMO MODE ***" |
| Valid license.txt | ? No | ? N/A | (no watermark) |

---

## Contact Points for Debugging

If demo mode is still not working after verification:

1. Share `application_log.txt` - Contains detailed execution trace
2. Share `plf.txt` - Contains simple success/failure status
3. Share screenshot of signed PDF signature
4. Share IP.xml configuration (signature box coordinates and dimensions)

---

## Known Limitations

1. **Signature Box Too Small**
   - If height is insufficient, last line (demo watermark) may be cut off
   - Solution: Increase height in IP.xml

2. **Font Size**
   - Demo watermark uses same font size as other text (9pt)
   - If you want it larger, we can make it a different size

3. **Position**
   - Demo watermark is always the last line
   - Always drawn at the bottom of the signature box

---

## Success Indicators

? **Demo mode is working if you see:**
1. Console: "?? License file not found — Demo Mode enabled"
2. Log: "Application mode: DEMO"
3. Log: "Starting PDF signing - Demo Mode: True"
4. Log: "Drawing DEMO MODE watermark in RED"
5. PDF: "*** DEMO MODE ***" in red color in signature

? **Demo mode is NOT working if:**
1. No demo watermark appears in PDF signature
2. Log shows: "Application mode: FULL"
3. No "DEMO MODE" entries in application_log.txt
