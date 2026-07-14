# ✅ **WORKING PDF SIGNER IMPLEMENTATION - Production Branch**

## Overview
This document contains the **VERIFIED WORKING** signer implementation from the `digisign-prod` branch that successfully creates PDF signatures validated by Adobe Reader and Microsoft Edge.

---

## 🎯 **Critical Implementation - SafeCertificateSignature Class**

```csharp
public class SafeCertificateSignature : IExternalSignature
{
    private readonly X509Certificate2 _certificate;
    private readonly string _hashAlgorithm;

    public SafeCertificateSignature(X509Certificate2 certificate, string hashAlgorithm)
    {
        _certificate = certificate;
        _hashAlgorithm = hashAlgorithm;
    }

    public string GetHashAlgorithm() => _hashAlgorithm;

    public string GetEncryptionAlgorithm() => "RSA";

    public byte[] Sign(byte[] message)
    {
        using (var rsa = _certificate.GetRSAPrivateKey())
        {
            if (rsa == null)
                throw new InvalidOperationException("RSA private key not found.");

            HashAlgorithmName hashAlgorithm = HashAlgorithmName.SHA256;

            // Use Windows to handle the PIN prompt and signing
            // CRITICAL: Use SignData(), not manual hashing + SignHash()
            return rsa.SignData(message, hashAlgorithm, RSASignaturePadding.Pkcs1);
        }
    }
}
```

---

## 🔑 **Key Signing Code - MakeSignature.SignDetached()**

```csharp
// Create a custom implementation of IExternalSignature
IExternalSignature externalSignature = new SafeCertificateSignature(cert, "SHA-256");

// Convert the certificate to a BouncyCastle certificate
Org.BouncyCastle.X509.X509Certificate bcCert = DotNetUtilities.FromX509Certificate(cert);

// OCSP client for certificate validation
var ocspClient = new OcspClientBouncyCastle();
ITSAClient tsaClient = null;

// Optional: Add timestamp
try
{
    tsaClient = new TSAClientBouncyCastle("http://timestamp.digicert.com");
}
catch (Exception ex)
{
    // Continue without timestamp if TSA unavailable
    LogToFile($"Warning: TSA not available, proceeding without timestamp. {ex.Message}", outputFolderPath);
}

// Sign the PDF
MakeSignature.SignDetached(
    appearance,
    externalSignature,
    new[] { bcCert },      // Certificate chain as array
    null,                  // No CRL
    ocspClient,            // OCSP client for validation
    tsaClient,             // Timestamp (optional)
    0,
    CryptoStandard.CMS
);
```

---

## ⚙️ **PDF Signature Appearance Settings**

```csharp
PdfStamper stamper = PdfStamper.CreateSignature(reader, os, '\0');
PdfSignatureAppearance appearance = stamper.SignatureAppearance;

// CRITICAL SETTINGS
appearance.CertificationLevel = PdfSignatureAppearance.CERTIFIED_NO_CHANGES_ALLOWED;
appearance.Reason = "Digitally signed";
appearance.Acro6Layers = false;  // Important for compatibility

// Set visible signature rectangle
appearance.SetVisibleSignature(
    new iTextSharp.text.Rectangle(adjustedX, adjustedY, adjustedX + adjustedWidth, adjustedY + adjustedHeight), 
    page, 
    $"sig_{page}"
);

// Disable default Layer2 text to avoid double rendering
appearance.Layer2Text = string.Empty;
```

---

## 📦 **Working NuGet Package Versions**

### **packages.config**
```xml
<?xml version="1.0" encoding="utf-8"?>
<packages>
  <package id="BouncyCastle" version="1.8.9" targetFramework="net472" />
  <package id="BouncyCastle.Cryptography" version="2.4.0" targetFramework="net472" />
  <package id="iTextSharp" version="5.5.13.4" targetFramework="net472" />
  <package id="Microsoft.Bcl.Cryptography" version="9.0.9" targetFramework="net472" />
  <package id="Pkcs11Interop" version="5.3.0" targetFramework="net472" />
  <package id="Spire.PDF" version="11.9.17" targetFramework="net472" />
  <package id="SkiaSharp" version="3.119.1" targetFramework="net472" />
  <package id="System.Management" version="9.0.9" targetFramework="net472" />
  <package id="System.Security.Cryptography.Pkcs" version="9.0.9" targetFramework="net472" />
  <!-- Additional support packages -->
  <package id="System.Buffers" version="4.6.1" targetFramework="net472" />
  <package id="System.CodeDom" version="9.0.9" targetFramework="net472" />
  <package id="System.Formats.Asn1" version="9.0.9" targetFramework="net472" />
  <package id="System.Memory" version="4.6.3" targetFramework="net472" />
  <package id="System.Numerics.Vectors" version="4.6.1" targetFramework="net472" />
  <package id="System.Runtime.CompilerServices.Unsafe" version="6.1.2" targetFramework="net472" />
  <package id="System.Text.Encoding.CodePages" version="9.0.9" targetFramework="net472" />
  <package id="System.ValueTuple" version="4.6.1" targetFramework="net472" />
</packages>
```

### **Key Package Notes**
- ✅ **BouncyCastle 1.8.9**: Legacy version (stable, proven)
- ✅ **BouncyCastle.Cryptography 2.4.0**: Newer version (both coexist)
- ✅ **iTextSharp 5.5.13.4**: PDF manipulation library
- ⚠️ **Note**: Both BouncyCastle packages are present in production

---

## 🚀 **Complete Signing Method**

```csharp
static void SignPdfWithITextSharp(
    string inputPath, 
    string outputPath, 
    X509Certificate2 cert, 
    float x, float y, 
    float width, float height, 
    string signOnPage, 
    string certPassword, 
    string outputFolderPath)
{
    try
    {
        // Extract CN from certificate
        string cn = cert.Subject
            .Split(',')
            .Select(p => p.Trim())
            .FirstOrDefault(p => p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            ?.Substring(3) ?? "Unknown";

        string signatureText = 
            $"{cn}\nDigitally signed by {cn}\nDate: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";

        // Setup PDF reader
        PdfReader reader = new PdfReader(inputPath);
        int pageCount = reader.NumberOfPages;

        // Determine which pages to sign
        var pagesToSign = new List<int>();
        switch (signOnPage?.ToUpper())
        {
            case "F":
                pagesToSign.Add(1); // First page
                break;
            case "E":
                pagesToSign.AddRange(Enumerable.Range(1, pageCount)); // Each page
                break;
            case "L":
            default:
                pagesToSign.Add(pageCount); // Last page
                break;
        }

        using (FileStream os = new FileStream(outputPath, FileMode.Create))
        {
            PdfStamper stamper = PdfStamper.CreateSignature(reader, os, '\0');
            PdfSignatureAppearance appearance = stamper.SignatureAppearance;
            appearance.CertificationLevel = PdfSignatureAppearance.CERTIFIED_NO_CHANGES_ALLOWED;
            appearance.Reason = "Digitally signed";
            appearance.Acro6Layers = false;

            // Sign each specified page
            foreach (int page in pagesToSign)
            {
                iTextSharp.text.Rectangle pageSize = reader.GetPageSize(page);
                float pageWidth = pageSize.Width;
                float pageHeight = pageSize.Height;

                // Validate coordinates
                float adjustedX = x;
                float adjustedY = y;
                float adjustedWidth = width;
                float adjustedHeight = height;

                if (x < 0 || y < 0 || x + width > pageWidth || y + height > pageHeight)
                {
                    adjustedX = Math.Max(50, x);
                    adjustedY = Math.Max(50, y);
                    adjustedWidth = Math.Min(width, pageWidth - adjustedX - 50);
                    adjustedHeight = Math.Min(height, pageHeight - adjustedY - 50);
                }

                // Define visible signature area
                appearance.SetVisibleSignature(
                    new iTextSharp.text.Rectangle(adjustedX, adjustedY, adjustedX + adjustedWidth, adjustedY + adjustedHeight), 
                    page, 
                    $"sig_{page}"
                );

                // Disable default Layer2 text
                appearance.Layer2Text = string.Empty;

                // Draw signature text on page
                PdfContentByte over = stamper.GetOverContent(page);
                DrawSignatureText(over, cn, signatureText, adjustedX, adjustedY, adjustedWidth, adjustedHeight);
            }

            // Create signature
            IExternalSignature externalSignature = new SafeCertificateSignature(cert, "SHA-256");
            Org.BouncyCastle.X509.X509Certificate bcCert = DotNetUtilities.FromX509Certificate(cert);

            var ocspClient = new OcspClientBouncyCastle();
            ITSAClient tsaClient = null;

            try
            {
                tsaClient = new TSAClientBouncyCastle("http://timestamp.digicert.com");
            }
            catch (Exception ex)
            {
                // Continue without timestamp
            }

            MakeSignature.SignDetached(
                appearance,
                externalSignature,
                new[] { bcCert },
                null,
                ocspClient,
                tsaClient,
                0,
                CryptoStandard.CMS
            );
        }
    }
    catch (Exception ex)
    {
        LogToFile($"ERROR | Failed to sign '{inputPath}'. Exception: {ex.Message}", outputFolderPath);
    }
}
```

---

## ❌ **Common Mistakes to Avoid**

### 1. **Manual Hashing (WRONG)**
```csharp
// ❌ DON'T DO THIS
byte[] hash = sha256.ComputeHash(message);
byte[] signature = rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
```

### 2. **Correct Way (RIGHT)**
```csharp
// ✅ DO THIS
return rsa.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
```

**Why?** 
- `SignData()` performs **both hashing and signing** in one operation
- `SignHash()` expects **already-hashed data**, leading to double-hashing when you hash manually
- For CMS signatures with authenticated attributes, iTextSharp passes the **raw authenticated attributes**, not a hash
- `SignData()` will hash them internally and create the correct PKCS#1 v1.5 signature structure

---

## 🔍 **Why This Works**

### **The Signature Flow:**
1. iTextSharp creates **CMS authenticated attributes** (includes content type, message digest, signing time)
2. Passes these attributes (77 bytes typically) to `Sign(byte[] message)`
3. `SignData()` internally:
   - Hashes the message with SHA-256 → 32 bytes
   - Creates DigestInfo structure (PKCS#1 v1.5)
   - Signs with RSA private key → 256 bytes (for 2048-bit key)
4. Returns signature to iTextSharp
5. iTextSharp embeds in PKCS#7/CMS container with certificate and timestamp

### **Result:**
- ✅ Valid CMS signature structure
- ✅ Single SignerInfo (not multiple)
- ✅ Verifies in Adobe Reader
- ✅ Verifies in Microsoft Edge
- ✅ Verifies in our BouncyCastle validator

---

## 📋 **Required Using Statements**

```csharp
using System;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Linq;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Security;
```

---

## ✅ **Verification Checklist**

Before deployment, ensure:

- [ ] Using `SafeCertificateSignature` class exactly as shown
- [ ] `Sign()` method uses `rsa.SignData()` not manual hashing
- [ ] Package versions match the production config
- [ ] `Acro6Layers = false` is set
- [ ] `CertificationLevel = CERTIFIED_NO_CHANGES_ALLOWED`
- [ ] `Layer2Text = string.Empty`
- [ ] OCSP client is included
- [ ] Timestamp is optional (catches exceptions)
- [ ] Certificate chain uses array syntax: `new[] { bcCert }`

---

## 🎯 **Summary**

**The critical difference:**
```csharp
// PRODUCTION (WORKS ✅)
return rsa.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

// vs

// BROKEN ATTEMPTS (FAILS ❌)
byte[] hash = sha256.ComputeHash(message);
return rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
```

**One method call vs. two makes all the difference!**

---

## 📝 **Notes**

- This implementation works with **hardware tokens (USB tokens)** via Windows CSP
- PIN prompts are handled automatically by Windows
- Works with both `RSACryptoServiceProvider` and `RSACng`
- Compatible with .NET Framework 4.7.2
- Tested and verified in production environment
- Signatures validate in Adobe Reader DC, Microsoft Edge, and other PDF viewers

---

**Status**: ✅ **PRODUCTION-READY**  
**Branch**: `digisign-prod`  
**Last Verified**: 2026-02-13
