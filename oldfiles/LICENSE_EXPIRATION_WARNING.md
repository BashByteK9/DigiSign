# License Expiration Warning Feature

## Overview

DigiSign now includes an automatic license expiration warning system that alerts users when their license is about to expire within 15 days.

## How It Works

### Automatic Detection
- Checked automatically during license validation
- Only applies to PDF signing mode (not admin mode)
- Triggers when license has **15 days or less** remaining
- Shows a warning dialog box with exact days remaining

### Warning Dialog

When your license is expiring soon, you'll see:

```
?????????????????????????????????????????????????????????????
?        ?? LICENSE EXPIRATION WARNING ??                   ?
?????????????????????????????????????????????????????????????
?                                                            ?
?  Your license will expire in X days.                      ?
?                                                            ?
?  Please contact your administrator to renew your license  ?
?  before it expires to avoid service interruption.         ?
?                                                            ?
?                       [   OK   ]                          ?
?????????????????????????????????????????????????????????????
```

## When the Warning Appears

| Days Remaining | Warning Shown? | Application Behavior |
|----------------|----------------|---------------------|
| > 15 days | ? No | Normal operation, no warning |
| 1-15 days | ? Yes | Warning dialog shown, then continues |
| 0 days (expired) | ? No | Application exits with error |
| < 0 (expired) | ? No | Application exits with error |

## User Experience

### Scenario 1: License Expires in 10 Days
```
1. User runs DigiSign.exe
2. License validation succeeds
3. ?? Warning dialog appears: "Your license will expire in 10 days"
4. User clicks OK
5. Application continues normally
6. PDFs are signed successfully
```

### Scenario 2: License Expires in 30 Days
```
1. User runs DigiSign.exe
2. License validation succeeds
3. ? No warning (more than 15 days remaining)
4. Application continues normally
5. PDFs are signed successfully
```

### Scenario 3: License Already Expired
```
1. User runs DigiSign.exe
2. ? License validation fails
3. Error: "License expired"
4. Application exits
5. No PDF signing occurs
```

## Logging

All expiration warnings are logged to `application_log.txt`:

```
2025-01-30 10:00:00 | WARNING  | License expiring soon: 10 days remaining
2025-01-30 10:00:01 | WARNING  | Showing license expiration warning: 10 days remaining
2025-01-30 10:00:05 | INFO     | License expiration warning displayed to user
```

## Admin Mode Behavior

**Admin mode does NOT show this warning** because:
- Admin mode only uses `admin.license` (not `license.txt`)
- Admin mode is for license generation only
- No PDF signing occurs in admin mode
- Admin licenses have their own expiration handling

## Implementation Details

### Detection Logic
```csharp
1. License file is read and validated
2. ValidUntil date is parsed
3. Days remaining = ValidUntil - CurrentDate
4. If days remaining <= 15 AND > 0:
   - Log warning
   - Show dialog
   - Continue operation
```

### Code Flow
```
Main()
??? ValidateLicense(licensePath)
?   ??? Parse license file
?   ??? Validate all fields
?   ??? Log days remaining if <= 15
??? GetLicenseExpiryDays(licensePath)
?   ??? Calculate days until expiration
??? ShowLicenseExpirationWarning(daysRemaining)
    ??? Create warning message
    ??? Show MessageBox dialog
    ??? Log to application_log.txt
```

## Benefits

### ? **Proactive Notification**
- Users get advance warning before expiration
- 15-day window gives time to request renewal
- Prevents unexpected service interruption

### ? **Clear Communication**
- Exact days remaining displayed
- Actionable instructions provided
- Professional warning dialog

### ? **Non-Intrusive**
- Single OK button to dismiss
- Application continues normally after warning
- Only shows once per session

### ? **Comprehensive Logging**
- All warnings logged for audit trail
- Admin can track license expiration patterns
- Helps with proactive license management

## User Actions

### When You See the Warning

1. **Note the days remaining**
   - Check how many days until expiration

2. **Contact your administrator**
   - Request license renewal
   - Provide your current license details

3. **Continue working**
   - Click OK to dismiss warning
   - Application will continue normally
   - PDFs can still be signed

4. **Plan ahead**
   - Don't wait until last day
   - Allow time for admin to process renewal

### Renewing Your License

1. **Contact Admin** - Request renewal before expiration
2. **Provide Info** - Share your customer ID and license number
3. **Receive New License** - Admin generates new `license.txt`
4. **Replace File** - Copy new `license.txt` to application folder
5. **Verify** - Run application to confirm new expiration date

## Testing

### Test Case 1: License Expiring in 5 Days
**Setup:**
```
ValidUntil=2025-02-04  (5 days from today)
```
**Expected:**
- ? Warning dialog appears
- Message: "Your license will expire in 5 days"
- Application continues after OK clicked

### Test Case 2: License Expiring in 20 Days
**Setup:**
```
ValidUntil=2025-02-19  (20 days from today)
```
**Expected:**
- ? No warning dialog
- Application runs normally

### Test Case 3: License Expiring Today
**Setup:**
```
ValidUntil=2025-01-30  (0 days - today)
```
**Expected:**
- ? License validation fails
- Application exits with error
- No warning dialog (expired)

### Test Case 4: License Expiring Tomorrow
**Setup:**
```
ValidUntil=2025-01-31  (1 day from today)
```
**Expected:**
- ? Warning dialog appears
- Message: "Your license will expire in 1 day"
- Application continues after OK clicked

## Troubleshooting

### Warning Doesn't Appear
**Possible Causes:**
- License has more than 15 days remaining
- License validation failed (check logs)
- Dialog was shown but closed quickly

**Solution:**
- Check `application_log.txt` for warning messages
- Verify `ValidUntil` date in `license.txt`
- Look for "License expiring soon" log entries

### Wrong Days Displayed
**Possible Causes:**
- System date/time is incorrect
- License file has wrong date format

**Solution:**
- Verify system date: `echo %date%`
- Check `ValidUntil` format in `license.txt`: `yyyy-MM-dd`
- Review logs for date parsing errors

### Warning Shows in Admin Mode
**This should NOT happen** - Admin mode skips license.txt validation.

**If it occurs:**
- Check that `/admin` flag is being used
- Verify admin.license exists and is valid
- Review application logs for mode detection

## Summary

The license expiration warning feature provides:
- ? **15-day advance notice**
- ?? **Exact days remaining**
- ?? **Clear user instructions**
- ?? **Complete audit logging**
- ? **Non-blocking operation**

This ensures users have sufficient time to renew their licenses before expiration, preventing unexpected service interruptions.

**The warning only applies to PDF signing mode - admin mode is unaffected.**
