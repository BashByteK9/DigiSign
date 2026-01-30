# PIN Dialog Fix - Use PIN from IP.xml

## Problem
During the PDF signing process, a PIN dialog was being displayed to the user even though the PIN was configured in the `IP.xml` file (index 3 in FILENAMELIST).

## Root Cause
The application was reading the PIN from `IP.xml` and attempting to cache it using `SetPinForPrivateKey()`, but the signing code was using the modern .NET API (`GetRSAPrivateKey()`) which doesn't respect the PIN caching done by the legacy cryptographic API.

## Solution
Modified the signing implementation to use the legacy `RSACryptoServiceProvider` which properly supports PIN caching:

### Changes Made

1. **LoadCertificateFromUSBToken method (Lines 1566-1587)**
   - Changed to access `cert.PrivateKey` directly instead of using `GetRSAPrivateKey()`
   - This allows proper PIN caching for hardware tokens
   - Added check to only set PIN when it's provided in IP.xml
   - Added warning log when hardware token is detected but no PIN provided

2. **SafeCertificateSignature.Sign method (Lines 1928-1951)**
   - Modified to use `RSACryptoServiceProvider.SignHash()` when available (legacy API)
   - This method respects the PIN cached via `SetPinForPrivateKey()`
   - Falls back to modern API (`GetRSAPrivateKey()`) for non-hardware certificates
   - Uses `SignHash()` instead of `SignData()` to work with the cached PIN

## How It Works

1. PIN is read from IP.xml at index 3: `xmlData.Pin = fileNameLists[3].Element("FILENAME")?.Value.Trim();`
2. During certificate loading, if hardware token is detected, PIN is cached using `cert.SetPinForPrivateKey(pin)`
3. During signing, the `SafeCertificateSignature` class uses the legacy `RSACryptoServiceProvider` API
4. The cached PIN is automatically used - no dialog is shown to the user

## IP.xml Configuration

The PIN should be configured in the 4th FILENAMELIST element (index 3):

```xml
<FILENAMELIST>
    <FILENAME>123456789</FILENAME>  <!-- Your PIN here -->
</FILENAMELIST>
```

## Benefits

- ? No PIN dialog interruption during signing
- ? Fully automated signing process when PIN is configured
- ? Backward compatible - still works if PIN is not provided (shows dialog)
- ? Proper logging to track PIN usage

## Testing

To test the fix:
1. Ensure your PIN is correctly set in IP.xml at index 3
2. Run the signing process
3. The PDF should be signed without showing any PIN dialog
4. Check the logs for: "PIN set for hardware token certificate from IP.xml"
