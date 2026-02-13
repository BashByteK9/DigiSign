# 🧪 **TESTING GUIDE - Production Signer Validation**

## ✅ **Pre-Test Checklist**

Before testing, ensure:
- [x] Build successful
- [x] SafeCertificateSignature class updated
- [x] Using `SignData()` not manual hashing
- [x] Hardware token (USB token) connected
- [x] Test PDF file available
- [x] IP.xml configured correctly

---

## 🚀 **Test Procedure**

### **Test 1: Basic Signature Creation**

#### **Steps:**
1. Open command prompt
2. Navigate to build directory:
   ```cmd
   cd D:\Development\DigiSign\Build\Digisign
   ```
3. Run the application:
   ```cmd
   DigiSign.exe
   ```

#### **Expected Results:**
- ✅ Application runs without errors
- ✅ Certificate loaded successfully
- ✅ PIN prompt appears (if configured)
- ✅ PDF signed successfully
- ✅ Output file created

#### **Log Messages to Look For:**
```
INFO: Certificate loaded successfully
DEBUG: SafeCertificateSignature.Sign called - Message length: 77 bytes
DEBUG: Message (hex): 314B301806092A864886F70D010903...
DEBUG: Signature created - 256 bytes
INFO: Timestamp service connected successfully
INFO: PDF digitally signed successfully
```

---

### **Test 2: Adobe Reader Validation**

#### **Steps:**
1. Open the signed PDF in Adobe Reader DC
2. Look for the signature panel on the left
3. Click on the signature

#### **Expected Results:**
- ✅ Signature panel shows signature
- ✅ Blue ribbon: "Signed and all signatures are valid"
- ✅ Signature properties show:
  - Valid signature
  - Certificate details
  - Timestamp information
- ✅ **NO "PKCS7 parsing error"**

#### **Screenshot Comparison:**

**BEFORE (Failed):**
```
❌ Error during signature verification
   PKCS7 parsing error: Error encountered while BER decoding
```

**AFTER (Success):**
```
✅ Signed and all signatures are valid
   Signature is valid
   Signer: [Certificate CN]
   Timestamp: 2026-02-13 [time] from DigiCert
```

---

### **Test 3: Microsoft Edge Validation**

#### **Steps:**
1. Open the signed PDF in Microsoft Edge
2. Click on the signature icon in the PDF

#### **Expected Results:**
- ✅ Signature icon appears
- ✅ Clicking shows "Valid signature"
- ✅ Certificate details visible
- ✅ No error messages

---

### **Test 4: Verbose Mode Testing**

#### **Steps:**
1. Edit IP.xml, set VerboseMode to "Y":
   ```xml
   <FILENAMELIST>
       <FILENAME>Y</FILENAME>  <!-- Index 11: VerboseMode -->
   </FILENAMELIST>
   ```
2. Run application
3. Watch the verbose dialog

#### **Expected Results:**
- ✅ Progress dialog appears
- ✅ Shows "Creating signature..."
- ✅ Shows "Requesting timestamp..."
- ✅ Shows "Timestamp acquired" or "Timestamp unavailable"
- ✅ Shows "SUCCESS"
- ✅ Auto-closes after 2 seconds (or 10 if errors)

---

### **Test 5: Log File Verification**

#### **Check application_log.txt:**

##### **Required Entries:**
```log
INFO: Application started
INFO: Certificate loaded successfully
DEBUG: PDF has [N] pages
INFO: Full mode: Applying cryptographic digital signature
DEBUG: Creating hardware token signature handler
DEBUG: SafeCertificateSignature.Sign called - Message length: 77 bytes
DEBUG: Signature created - 256 bytes
INFO: Timestamp service connected successfully
INFO: PDF digitally signed successfully
```

##### **What Should NOT Appear:**
```log
❌ "Using RSACryptoServiceProvider for signing (supports PIN caching)"
❌ "Computed SHA-256 hash of authenticated attributes"
❌ "PKCS7 parsing error"
❌ "multiple SignerInfos"
❌ "illegal object in GetInstance"
```

---

### **Test 6: Signature Structure Validation**

#### **Check File Sizes:**
```cmd
dir /B OutputFolder\signed.pdf
```

#### **Expected PKCS7 Size:**
- **Without timestamp**: ~8KB structure
- **With timestamp**: ~12KB structure

#### **Use hex editor** (optional):
1. Open signed PDF in hex editor
2. Search for "Contents" (signature)
3. Verify size is 8-12KB

---

### **Test 7: Multiple Page Signing**

#### **Test SignOnPage Options:**

1. **First Page (F)**:
   ```xml
   <FILENAME>F</FILENAME>  <!-- Index 8: SignOnPage -->
   ```
   - ✅ Signature on page 1 only

2. **Last Page (L)**:
   ```xml
   <FILENAME>L</FILENAME>
   ```
   - ✅ Signature on last page only

3. **Each Page (E)**:
   ```xml
   <FILENAME>E</FILENAME>
   ```
   - ✅ Signature on every page

---

## 🔍 **Validation Checklist**

After testing, verify:

- [ ] ✅ PDF created successfully
- [ ] ✅ Adobe Reader: "Signed and all signatures are valid"
- [ ] ✅ Microsoft Edge: Valid signature
- [ ] ✅ Logs show: "SafeCertificateSignature.Sign called"
- [ ] ✅ Logs show: "Signature created - 256 bytes"
- [ ] ✅ Timestamp included (if TSA available)
- [ ] ✅ No PKCS7 parsing errors
- [ ] ✅ No multiple SignerInfos errors
- [ ] ✅ File size appropriate (~8-12KB PKCS7)

---

## 🐛 **Troubleshooting Tests**

### **Test Failed: Adobe Reader Shows Error**

#### **Diagnostic Steps:**
1. Check logs for exact error
2. Verify `SignData()` is being called:
   ```log
   DEBUG: SafeCertificateSignature.Sign called - Message length: 77 bytes
   ```
3. Check signature size:
   ```log
   DEBUG: Signature created - 256 bytes
   ```
4. If different, check code for manual hashing

---

### **Test Failed: No Signature Created**

#### **Diagnostic Steps:**
1. Check certificate loading:
   ```log
   INFO: Certificate loaded successfully
   ```
2. Check for errors:
   ```log
   ERROR: [error message]
   ```
3. Verify USB token is connected
4. Verify PIN is correct in IP.xml

---

### **Test Failed: Signature Invalid**

#### **Diagnostic Steps:**
1. Check timestamp:
   ```log
   INFO: Timestamp service connected successfully
   ```
2. Verify OCSP client is included (already is)
3. Check for certificate expiration
4. Verify certificate chain is valid

---

## 📊 **Test Results Template**

```
==========================================
TEST RESULTS - Production Signer
==========================================
Date: 2026-02-13
Tester: [Your Name]
Build: [Build Number]
==========================================

Test 1: Basic Signature Creation
Status: [ ] PASS [ ] FAIL
Notes: 

Test 2: Adobe Reader Validation  
Status: [ ] PASS [ ] FAIL
Notes:

Test 3: Microsoft Edge Validation
Status: [ ] PASS [ ] FAIL
Notes:

Test 4: Verbose Mode
Status: [ ] PASS [ ] FAIL
Notes:

Test 5: Log File Verification
Status: [ ] PASS [ ] FAIL
Notes:

Test 6: Signature Structure
Status: [ ] PASS [ ] FAIL
Notes:

Test 7: Multiple Pages
Status: [ ] PASS [ ] FAIL
Notes:

==========================================
OVERALL RESULT: [ ] PASS [ ] FAIL
==========================================

Issues Found:


Recommendations:


==========================================
```

---

## ✅ **Success Criteria**

### **Minimum Requirements:**
- ✅ All 7 tests pass
- ✅ Adobe Reader validates signature
- ✅ Microsoft Edge validates signature
- ✅ No PKCS7 parsing errors
- ✅ Logs show correct SignData() usage

### **Optional (Nice to Have):**
- ✅ Timestamp included
- ✅ Verbose mode works
- ✅ Multiple page signing works

---

## 🎯 **Final Verification**

Before declaring success:

1. **Create fresh test PDF**
2. **Sign with production code**
3. **Open in Adobe Reader** → Must show "Valid"
4. **Open in Microsoft Edge** → Must show "Valid"
5. **Check logs** → Must use SignData()
6. **No error messages** → Must be clean

If ALL checks pass → ✅ **READY FOR PRODUCTION**

---

**Test Plan Version**: 1.0  
**Last Updated**: 2026-02-13  
**Based On**: Production branch `digisign-prod`
