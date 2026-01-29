# Demo Mode Behavior - Important Update

## ?? CRITICAL CHANGE: Demo Mode Does NOT Sign PDFs

### What Changed

**BEFORE:** Demo mode signed PDFs with a cryptographic signature + watermark  
**AFTER:** Demo mode does NOT sign PDFs - only adds visual text overlay

---

## ?? New Behavior

### Demo Mode (No license.txt)
- ? **NO cryptographic signature applied**
- ? **Visual text overlay added** at signature location
- ?? **Red warning text:** "NOT DIGITALLY SIGNED" and "*** DEMO MODE ***"
- ?? **PDF remains unsigned** (no digital certificate embedded)

### Full Mode (Valid license.txt)
- ? **Full cryptographic signature applied**
- ? **Certificate embedded in PDF**
- ? **Timestamped** (if available)
- ? **Legally valid digital signature**

---

## ?? Visual Appearance

### Demo Mode Text
```
??????????????????????????????????????????
? John Doe                               ?
? NOT DIGITALLY SIGNED          ?? RED   ?
? Date: 20.01.2025 14:30:00             ?
? *** DEMO MODE ***             ?? RED   ?
? *** NO CRYPTOGRAPHIC          ?? RED   ?
?     SIGNATURE ***             ?? RED   ?
??????????????????????????????????????????
```

### Full Mode Signature
```
??????????????????????????????????????????
? John Doe                               ?
? Digitally signed by John Doe           ?
? Date: 20.01.2025 14:30:00             ?
? [Digital Certificate Embedded]         ?
??????????????????????????????????????????
```

---

## ?? Technical Details

### Demo Mode Process
1. Read input PDF
2. Create output PDF using `PdfStamper` (not `CreateSignature`)
3. Add visual text overlay at specified coordinates
4. Save PDF **without** cryptographic signature
5. Result: Unsigned PDF with visual demo text

### Full Mode Process
1. Read input PDF
2. Create signature stamper using `PdfStamper.CreateSignature`
3. Add visual signature appearance
4. Apply cryptographic signature using `MakeSignature.SignDetached`
5. Embed certificate and timestamp
6. Result: Legally signed PDF with embedded certificate

---

## ?? Important Implications

### Demo Mode PDFs Are NOT Signed
- **Cannot be verified** by PDF readers as signed
- **No legal validity** as digital signatures
- **Can be modified** without breaking signature (because there is none)
- **No certificate embedded** in PDF
- **For demonstration purposes only**

### Full Mode PDFs Are Fully Signed
- **Can be verified** by Adobe Reader, PDF readers
- **Legally valid** digital signatures
- **Tamper-evident** - modifications break signature
- **Certificate embedded** for verification
- **Production-ready** documents

---

## ?? How to Verify

### Test Demo Mode (No Signature)
1. Delete `license.txt`
2. Run `DigiSign.exe`
3. Sign a PDF
4. **Open in Adobe Reader**
5. **Expected:** No signature panel, no certificate, just text overlay
6. **Verify:** Right-click document ? Document Properties ? Security ? "No security"

### Test Full Mode (With Signature)
1. Have valid `license.txt`
2. Run `DigiSign.exe`
3. Sign a PDF
4. **Open in Adobe Reader**
5. **Expected:** Signature panel appears, shows certificate
6. **Verify:** Right-click signature ? Show Signature Properties ? Certificate details visible

---

## ?? Log Messages

### Demo Mode Logs
```
INFO     | License file not found - Demo Mode enabled
INFO     | Application mode: DEMO
INFO     | Starting PDF processing - Demo Mode: True
INFO     | Demo mode: Adding visual text overlay WITHOUT cryptographic signature
DEBUG    | Drawing DEMO MODE text in RED
INFO     | PDF processed in demo mode (no signature): document.pdf
```

### Full Mode Logs
```
INFO     | License validation successful - Full Mode enabled
INFO     | Application mode: FULL
INFO     | Starting PDF processing - Demo Mode: False
INFO     | Full mode: Applying cryptographic digital signature
INFO     | Timestamp service connected successfully
INFO     | PDF digitally signed successfully: document.pdf
```

---

## ?? Code Architecture

### New Method: `DrawSignatureText()`
Separated the text drawing logic into a reusable method:
- Used in **both** demo and full modes
- Handles text wrapping
- Applies color coding (RED for demo warnings)
- Calculates line positions

### Modified: `SignPdfWithITextSharp()`
Now has two distinct paths:
```csharp
if (isDemoMode)
{
    // Use PdfStamper - no signature
    // Just add text overlay
}
else
{
    // Use PdfStamper.CreateSignature
    // Apply full cryptographic signature
}
```

---

## ?? Configuration

### Signature Box Size (IP.xml)
Demo mode requires **more height** for additional warning lines:

```xml
<!-- Minimum height for demo mode (6 lines) -->
<FILENAME>150</FILENAME>

<!-- Formula: -->
<!-- Height = (Lines × 20px) + Padding -->
<!-- Height = (6 × 20) + 30 = 150 pixels -->
```

**Lines in Demo Mode:**
1. CN (e.g., "John Doe")
2. "NOT DIGITALLY SIGNED"
3. Date
4. "*** DEMO MODE ***"
5. "*** NO CRYPTOGRAPHIC"
6. "SIGNATURE ***"

---

## ?? Security Considerations

### Why Demo Mode Doesn't Sign

1. **Prevents Misuse** - Demo PDFs cannot be mistaken for legally signed documents
2. **Clear Indication** - Obvious visual difference between demo and production
3. **No Certificate Abuse** - Prevents unauthorized use of certificates in demo mode
4. **License Enforcement** - Encourages users to obtain proper license for production use

### Legal Implications

?? **Demo Mode PDFs:**
- Have **NO legal validity** as signed documents
- Should **NOT be used** for official/legal purposes
- Are **NOT tamper-evident**
- Do **NOT prove** authenticity or integrity

? **Full Mode PDFs:**
- Have **full legal validity** (subject to jurisdiction)
- Are **legally binding** digital signatures
- Are **tamper-evident** and **verifiable**
- **Prove** document integrity and signer identity

---

## ?? Comparison Table

| Feature | Demo Mode | Full Mode |
|---------|-----------|-----------|
| **Cryptographic Signature** | ? No | ? Yes |
| **Certificate Embedded** | ? No | ? Yes |
| **Timestamp** | ? No | ? Yes (if available) |
| **Legal Validity** | ? No | ? Yes |
| **Tamper Detection** | ? No | ? Yes |
| **Adobe Reader Verification** | ? No | ? Yes |
| **Visual Appearance** | ?? Red warnings | ? Standard signature |
| **PDF Modification** | ? Allowed | ? Breaks signature |
| **Use Case** | ?? Testing/Demo | ?? Production |

---

## ?? Upgrade Path

### From Demo to Full Mode
1. Obtain valid `license.txt` from administrator
2. Place `license.txt` in application directory
3. Run application
4. Re-sign all PDFs
5. PDFs will now have **full cryptographic signatures**

### No Re-signing Needed for Demo
- Demo mode PDFs remain unsigned
- Can be re-processed in full mode later
- Original unsign status clearly indicated

---

## ?? Support & Questions

### Common Questions

**Q: Why don't demo PDFs show signature in Adobe Reader?**  
A: Demo mode does not apply cryptographic signatures, only visual text.

**Q: Can I use demo PDFs for legal purposes?**  
A: No, demo PDFs are not digitally signed and have no legal validity.

**Q: How do I get real signatures?**  
A: Obtain a valid license.txt from your administrator.

**Q: Can demo PDFs be verified?**  
A: No, there's no signature to verify. They're just text overlays.

---

## ? Summary

### Key Takeaways
1. ? Demo mode **does not sign** PDFs
2. ? Demo mode **adds visual text** only
3. ? Full mode **applies real signatures**
4. ? Clear visual distinction between modes
5. ? Prevents misuse of demo mode for production

### Best Practices
1. **Always test** PDFs in Adobe Reader to verify signature
2. **Use demo mode** only for testing and demonstrations
3. **Use full mode** for all production/legal documents
4. **Check logs** to confirm which mode was used
5. **Verify license** status before processing important documents

---

**Last Updated:** 2025-01-20  
**Version:** 2.0 - Major Behavior Change  
**Breaking Change:** Demo mode no longer signs PDFs
