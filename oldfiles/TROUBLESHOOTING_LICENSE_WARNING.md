# License Expiration Warning - Troubleshooting Guide

## Issue: Warning Dialog Not Showing

If the license expiration warning dialog is not appearing when expected, follow this troubleshooting guide.

## Step 1: Check License File

### Verify license.txt exists and contains ValidUntil

Open your `license.txt` file and verify it has this format:

```
CustomerID=teninfotech
ValidUntil=2025-02-10
DeviceID=BFEBFBFF000906A4_AA000000000000001631
LicenseNumber=1234567890
DeviceHash=829E62144A40058DDA78021DB7D7B187B31DB60652656CA639D8A31AF67519D1
```

**Key Check:**
- ? `ValidUntil` field exists
- ? Date format is `yyyy-MM-dd` (e.g., 2025-02-10)
- ? Date is within 15 days from today

### Test with Different Dates

To test the warning, temporarily edit `ValidUntil` to trigger it:

**For testing (10 days warning):**
```
ValidUntil=2025-02-10  (if today is 2025-01-31)
```

?? **IMPORTANT:** After testing, you MUST regenerate the license because changing the date will invalidate the hash!

## Step 2: Check Application Logs

### Open application_log.txt

Look for these log entries:

**Expected logs when checking expiry:**
```
DEBUG    | Checking license expiry for: D:\Path\to\license.txt
DEBUG    | ValidUntil found: 2025-02-10
DEBUG    | License expires on: 2025-02-10
DEBUG    | Current date: 2025-01-31
DEBUG    | Days remaining: 10
INFO     | License expires in 10 days - showing warning dialog
WARNING  | Showing license expiration warning: 10 days remaining
INFO     | License expiration warning displayed to user
```

**If you see this instead:**
```
DEBUG    | License has 20 days remaining - no warning needed
```
? License has more than 15 days, warning is correctly NOT shown.

**If you don't see expiry check logs at all:**
? The method is not being called. Check that license validation succeeded.

## Step 3: Verify Days Remaining Calculation

### Manual Calculation

Calculate days remaining manually:

1. Find ValidUntil date in license.txt: `2025-02-10`
2. Check today's date: `2025-01-31`
3. Calculate: `2025-02-10 - 2025-01-31 = 10 days`

### Check Log Output

Search application_log.txt for:
```
DEBUG    | Days remaining: 10
```

If this shows a different number, verify:
- System date/time is correct
- ValidUntil date in license.txt is correct
- Date format is correct (yyyy-MM-dd)

## Step 4: Test Warning Display

### Create Test License for Exact Testing

To test the warning for specific scenarios:

**10 Days Warning:**
```powershell
# Calculate date 10 days from now
$testDate = (Get-Date).AddDays(10).ToString("yyyy-MM-dd")
Write-Host "Set ValidUntil to: $testDate"
```

**1 Day Warning:**
```powershell
# Calculate date tomorrow
$testDate = (Get-Date).AddDays(1).ToString("yyyy-MM-dd")
Write-Host "Set ValidUntil to: $testDate"
```

?? **After testing, regenerate the license with correct hash!**

## Step 5: Common Issues and Solutions

### Issue 1: Warning Never Shows

**Symptoms:**
- License has < 15 days remaining
- No warning dialog appears
- No error in logs

**Solutions:**
1. Check if license validation is failing (won't reach warning code)
2. Check logs for "License validation failed"
3. Verify `licenseExpiryDaysRemaining` is being set

**Debug in logs:**
```
INFO     | License validation successful - Full Mode enabled
INFO     | License expires in X days - showing warning dialog  ? This line should appear
```

### Issue 2: Wrong Days Displayed

**Symptoms:**
- Warning shows wrong number of days

**Solutions:**
1. Verify system date/time is correct: `echo %date% %time%`
2. Check ValidUntil date in license.txt
3. Look for date parsing errors in logs

### Issue 3: Warning Shows When It Shouldn't

**Symptoms:**
- License has > 15 days remaining
- Warning still appears

**Solutions:**
1. Check application_log.txt for actual days remaining
2. Verify ValidUntil date is correct
3. Check for time zone issues

### Issue 4: Dialog Appears Behind Other Windows

**Symptoms:**
- Warning dialog is shown but not visible

**Solutions:**
1. Check taskbar for flashing DigiSign window
2. Alt+Tab to find the dialog
3. Check logs confirm dialog was shown:
   ```
   INFO     | License expiration warning displayed to user
   ```

## Step 6: Detailed Testing Procedure

### Full Test Scenario

1. **Backup original license:**
   ```
   copy license.txt license.txt.backup
   ```

2. **Set ValidUntil to 10 days from now:**
   - Calculate: Current date + 10 days
   - Edit license.txt: `ValidUntil=2025-02-10`
   - Note: This will break the hash, but that's OK for testing

3. **Run application:**
   ```
   DigiSign.exe
   ```

4. **Expected behavior:**
   - License validation FAILS (hash mismatch due to date change)
   - Application exits with error
   - **This is correct!** The hash includes the date

5. **Proper test method:**
   - Use `/admin` mode to regenerate license with test date
   - This creates valid hash with the test date
   - Then run normal mode to see warning

### Proper Testing with Admin Mode

1. **Use admin mode to create test license:**
   ```
   DigiSign.exe /admin
   ```

2. **In the license generation form:**
   - Select user's license.key
   - Set expiration to 10 days from now
   - Generate license.txt

3. **Run normal mode:**
   ```
   DigiSign.exe
   ```

4. **Expected result:**
   - ? License validates successfully
   - ?? Warning dialog appears
   - Application continues normally

## Step 7: Verify Code is Active

### Check code was compiled

Run:
```powershell
Select-String -Path "Program.cs" -Pattern "ShowLicenseExpirationWarning" -Context 0,1
```

Should show:
```
Program.cs:464:    ShowLicenseExpirationWarning(licenseExpiryDaysRemaining);
Program.cs:465:}
--
Program.cs:1009:static void ShowLicenseExpirationWarning(int daysRemaining)
Program.cs:1010:{
```

### Verify method is called

Add temporary console output to verify:
```csharp
if (licenseExpiryDaysRemaining >= 0 && licenseExpiryDaysRemaining <= 15)
{
    Console.WriteLine($"DEBUG: About to show warning for {licenseExpiryDaysRemaining} days");
    ShowLicenseExpirationWarning(licenseExpiryDaysRemaining);
}
```

## Quick Test Command

Run this to see all expiry-related logs:
```powershell
Select-String -Path "application_log.txt" -Pattern "expir" -CaseSensitive:$false
```

Should show entries like:
```
DEBUG    | Checking license expiry for: ...
DEBUG    | Days remaining: 10
INFO     | License expires in 10 days - showing warning dialog
WARNING  | Showing license expiration warning: 10 days remaining
```

## Summary Checklist

- [ ] License file has ValidUntil field
- [ ] Date format is yyyy-MM-dd
- [ ] Days remaining is 0-15
- [ ] License validation succeeds
- [ ] GetLicenseExpiryDays returns correct value
- [ ] ShowLicenseExpirationWarning is called
- [ ] Dialog shows with correct days
- [ ] Logs show warning was displayed

## Need More Help?

Check these log sections:
1. **License Validation:** Search for "License validation"
2. **Expiry Check:** Search for "Days remaining"
3. **Warning Display:** Search for "expiration warning"

All three should show success entries for the warning to appear.
