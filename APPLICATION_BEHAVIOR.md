# DigiSign Application Behavior Test Scenarios

## ? Application ALWAYS Runs - Regardless of License Status

The DigiSign application is designed to **ALWAYS execute and sign PDFs**, whether or not a valid license exists.

---

## Test Scenarios

### Scenario 1: No license.txt File (First Time User)
**Setup:**
- Delete or remove `license.txt` from application folder
- Run: `DigiSign.exe`

**Expected Behavior:**
1. ? Application starts normally
2. ? Console shows: "?? License file not found — Demo Mode enabled."
3. ? `license.key` file is automatically created
4. ? Application continues to process PDFs from `IP.xml`
5. ? PDFs are signed with "*** DEMO MODE ***" watermark
6. ? Signed PDFs are saved to output folder
7. ? Application exits normally

**Result:** Application runs successfully in Demo Mode

---

### Scenario 2: Invalid or Corrupted license.txt
**Setup:**
- Have an invalid/corrupted `license.txt` in application folder
- Run: `DigiSign.exe`

**Expected Behavior:**
1. ? Application starts normally
2. ? Console shows: "? License invalid or used on a different device — Demo Mode enabled."
3. ? `license.key` file is created (if not exists)
4. ? Application continues to process PDFs from `IP.xml`
5. ? PDFs are signed with "*** DEMO MODE ***" watermark
6. ? Signed PDFs are saved to output folder
7. ? Application exits normally

**Result:** Application runs successfully in Demo Mode

---

### Scenario 3: Valid license.txt Exists
**Setup:**
- Have a valid `license.txt` in application folder
- Run: `DigiSign.exe`

**Expected Behavior:**
1. ? Application starts normally
2. ? Console shows: "? License valid — Full Mode enabled."
3. ? Application processes PDFs from `IP.xml`
4. ? PDFs are signed **WITHOUT** "*** DEMO MODE ***" watermark
5. ? Signed PDFs are saved to output folder
6. ? Application exits normally

**Result:** Application runs successfully in Full Mode

---

### Scenario 4: Admin License Exists (Normal Run)
**Setup:**
- Have `admin.license` in application folder
- Run: `DigiSign.exe` (without /admin flag)

**Expected Behavior:**
1. ? Application starts normally
2. ? Console shows license status (demo or full based on license.txt)
3. ? Console shows hint: "?? Admin license detected. Run with /admin flag to generate licenses."
4. ? Application continues to process PDFs from `IP.xml`
5. ? PDFs are signed (with or without watermark based on license status)
6. ? Application exits normally

**Result:** Application runs successfully, admin mode is NOT activated

---

### Scenario 5: Admin Mode Activated
**Setup:**
- Have `admin.license` in application folder
- Run: `DigiSign.exe /admin`

**Expected Behavior:**
1. ? Application starts in Admin Mode
2. ? Console shows: "?? Admin License Mode"
3. ? Prompts: "Do you want to generate license.txt from a license.key file? (Y/N)"
4. ? If Yes: Prompts for license.key path and generates license.txt
5. ? If No: Exits
6. ? Does NOT process PDFs (admin mode only generates licenses)
7. ? Application waits for key press and exits

**Result:** Admin license generation mode only (no PDF signing)

---

## Key Points

### The Application NEVER Stops Working Due to License Issues
- ? **WRONG:** Application crashes if no license
- ? **WRONG:** Application requires license to run
- ? **WRONG:** Application blocks without license
- ? **CORRECT:** Application always runs
- ? **CORRECT:** Demo mode is activated automatically
- ? **CORRECT:** PDFs are always signed (just with watermark in demo mode)

### Demo Mode vs Full Mode

| Feature | Demo Mode | Full Mode |
|---------|-----------|-----------|
| **Runs Application** | ? Yes | ? Yes |
| **Signs PDFs** | ? Yes | ? Yes |
| **Signature Watermark** | "*** DEMO MODE ***" | None |
| **All Other Features** | ? Full Access | ? Full Access |

---

## What License Controls

The license **ONLY** controls the **watermark** on signatures:
- **No License / Invalid License** = Watermark appears
- **Valid License** = No watermark

**Everything else works the same!**

---

## Troubleshooting

### "Application doesn't run"
**Possible Causes:**
1. Missing `IP.xml` configuration file
2. Invalid XML data in `IP.xml`
3. No input PDF files specified
4. Certificate not found
5. Application crash (check error logs)

**NOT Related to License:**
- License issues NEVER prevent application from running
- Check `application_log.txt` for actual errors

### "Demo watermark appears when I have license.txt"
**Possible Causes:**
1. License is for a different device (DeviceID mismatch)
2. License has expired
3. License file is corrupted
4. DeviceHash validation failed

**Solution:**
- Generate new `license.key` on current device
- Ask admin to generate new `license.txt` from the new `license.key`

---

## Summary

? **The DigiSign application is fully functional without a license**
? **Demo mode enables all features except removes the watermark**
? **License only controls watermark appearance**
? **Application always runs and signs PDFs**
