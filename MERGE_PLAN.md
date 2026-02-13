# DigiSign Merge Plan: Master → DigiSign-Prod

**Date**: Generated  
**Repository**: D:\Development\DigiSign  
**Source Branch**: master  
**Target Branch**: digisign-prod (ACTIVE)  

---

## ⚠️ CRITICAL PROTECTION REQUIREMENTS

### Files That MUST NOT Change
The following files contain the complete digital signing implementation and **MUST** be preserved from digisign-prod in case of conflicts:

1. **SignatureHelper.cs**
   - Contains `SafeCertificateSignature` class
   - Implements `IExternalSignature` for certificate-based signing
   - Uses RSA private key signing with SHA-256

2. **DigitalSignatureService.cs**
   - Certificate loading logic (USB token + self-signed fallback)
   - PDF signing with iTextSharp
   - Signature validation
   - Visual signature appearance rendering

3. **X509Certificate2Extension.cs**
   - PIN handling for private keys
   - P/Invoke methods for Windows Crypto API
   - Critical for USB token authentication

4. **SignatureConfiguration.cs**
   - Configuration model for signature placement
   - Page selection logic (First/Each/Last)

5. **Program.cs** (PARTIAL - signing logic only)
   - Main signing workflow
   - XML data reading
   - Certificate handling
   - **Note**: May have new features from master, but preserve all signing-related code

6. **packages.config**
   - All 19 NuGet package versions must remain unchanged
   - See detailed list below

---

## 📦 NuGet Packages (LOCKED VERSIONS)

These package versions are critical for signing functionality and **MUST NOT** be upgraded:

```xml
<package id="BouncyCastle" version="1.8.9" targetFramework="net472" />
<package id="BouncyCastle.Cryptography" version="2.4.0" targetFramework="net472" />
<package id="iTextSharp" version="5.5.13.4" targetFramework="net472" />
<package id="Microsoft.Bcl.Cryptography" version="9.0.9" targetFramework="net472" />
<package id="Pkcs11Interop" version="5.3.0" targetFramework="net472" />
<package id="SkiaSharp" version="3.119.1" targetFramework="net472" />
<package id="SkiaSharp.NativeAssets.macOS" version="3.119.1" targetFramework="net472" />
<package id="SkiaSharp.NativeAssets.Win32" version="3.119.1" targetFramework="net472" />
<package id="Spire.PDF" version="11.9.17" targetFramework="net472" />
<package id="System.Buffers" version="4.6.1" targetFramework="net472" />
<package id="System.CodeDom" version="9.0.9" targetFramework="net472" />
<package id="System.Formats.Asn1" version="9.0.9" targetFramework="net472" />
<package id="System.Management" version="9.0.9" targetFramework="net472" />
<package id="System.Memory" version="4.6.3" targetFramework="net472" />
<package id="System.Numerics.Vectors" version="4.6.1" targetFramework="net472" />
<package id="System.Runtime.CompilerServices.Unsafe" version="6.1.2" targetFramework="net472" />
<package id="System.Security.Cryptography.Pkcs" version="9.0.9" targetFramework="net472" />
<package id="System.Text.Encoding.CodePages" version="9.0.9" targetFramework="net472" />
<package id="System.ValueTuple" version="4.6.1" targetFramework="net472" />
```

---

## 🔄 Merge Execution Steps

Execute these commands in **Git Bash** or **Command Prompt** from `D:\Development\DigiSign`:

### Step 1: Verify Current State
```bash
# Ensure you're on digisign-prod
git branch --show-current
# Should output: digisign-prod

# Check for uncommitted changes
git status
# Commit any changes before proceeding
```

### Step 2: Fetch Latest Changes
```bash
# Get latest from all branches
git fetch --all

# View commits in master not in digisign-prod
git log digisign-prod..origin/master --oneline

# View commits in digisign-prod not in master
git log origin/master..digisign-prod --oneline
```

### Step 3: Create Safety Backup
```bash
# Create backup branch
git branch digisign-prod-backup-$(date +%Y%m%d)

# Verify backup created
git branch -a
```

### Step 4: Attempt Merge
```bash
# Merge master into digisign-prod
git merge origin/master --no-ff -m "Merge master updates into digisign-prod"
```

---

## 🚨 Conflict Resolution Strategy

### If Merge Has NO Conflicts
```bash
# Proceed to Step 5 (Verification)
```

### If Merge Has Conflicts

#### A. List All Conflicts
```bash
git status
# Look for files marked "both modified"
```

#### B. Resolve Each Conflict

For **signing-related files** (SignatureHelper.cs, DigitalSignatureService.cs, etc.):
```bash
# Keep digisign-prod version completely
git checkout --ours <filename>
git add <filename>
```

For **packages.config**:
```bash
# Keep digisign-prod version
git checkout --ours packages.config
git add packages.config
```

For **Program.cs** (requires manual review):
```bash
# Open in editor to manually merge
code Program.cs

# Look for conflict markers:
# <<<<<<< HEAD (digisign-prod)
# ...digisign-prod code...
# =======
# ...master code...
# >>>>>>> origin/master

# KEEP all signing logic from digisign-prod
# ACCEPT new features from master (if they don't conflict with signing)
# After manual resolution:
git add Program.cs
```

For **other files** (new features from master):
```bash
# Accept master version if they're new features
git checkout --theirs <filename>
git add <filename>

# OR manually merge if needed
code <filename>
# Resolve and save
git add <filename>
```

#### C. Complete the Merge
```bash
# After resolving all conflicts
git commit -m "Merge master into digisign-prod - preserved signing implementation"
```

---

## ✅ Post-Merge Verification

### Step 5: Verify Signing Files
```bash
# Check that signing files weren't changed
git diff digisign-prod-backup-<date> SignatureHelper.cs
git diff digisign-prod-backup-<date> DigitalSignatureService.cs
git diff digisign-prod-backup-<date> X509Certificate2Extension.cs
git diff digisign-prod-backup-<date> SignatureConfiguration.cs
git diff digisign-prod-backup-<date> packages.config

# Should show NO differences or only additions from master
```

### Step 6: Verify NuGet Packages
```bash
# Ensure packages.config is unchanged
git diff digisign-prod-backup-<date> packages.config

# Restore packages
nuget restore
# OR in Visual Studio: Right-click solution → Restore NuGet Packages
```

### Step 7: Build Solution
```bash
# In PowerShell (from VS Developer Command Prompt)
msbuild DigiSign.sln /t:Rebuild /p:Configuration=Debug

# OR in Visual Studio
# Build → Rebuild Solution
```

### Step 8: Verify Signing Logic Integrity

**Manual Test Checklist**:
- [ ] Open solution in Visual Studio
- [ ] Verify all 5 signing files are present and unchanged
- [ ] Check that all 19 NuGet packages are restored
- [ ] Build succeeds without errors
- [ ] Review Program.cs for signing workflow integrity
- [ ] Test run with sample PDF (if possible)

### Step 9: Review New Features from Master
```bash
# View what was added from master
git log --oneline --graph --all

# Review new files added
git diff --name-status digisign-prod-backup-<date> digisign-prod
```

---

## 🔙 Rollback Plan (If Needed)

If the merge causes issues:

```bash
# Option 1: Reset to backup
git reset --hard digisign-prod-backup-<date>

# Option 2: Abort merge (only if merge not committed)
git merge --abort

# Option 3: Revert the merge commit
git log --oneline
# Find the merge commit SHA
git revert -m 1 <merge-commit-sha>
```

---

## 📋 Conflict Resolution Quick Reference

| File Type | Strategy | Command |
|-----------|----------|---------|
| SignatureHelper.cs | Keep digisign-prod | `git checkout --ours SignatureHelper.cs` |
| DigitalSignatureService.cs | Keep digisign-prod | `git checkout --ours DigitalSignatureService.cs` |
| X509Certificate2Extension.cs | Keep digisign-prod | `git checkout --ours X509Certificate2Extension.cs` |
| SignatureConfiguration.cs | Keep digisign-prod | `git checkout --ours SignatureConfiguration.cs` |
| packages.config | Keep digisign-prod | `git checkout --ours packages.config` |
| Program.cs | Manual merge | Edit manually, preserve signing logic |
| New feature files | Accept master | `git checkout --theirs <file>` |
| Other modified files | Review case-by-case | Manual resolution |

---

## 📊 Expected Outcomes

### After Successful Merge:
✅ digisign-prod has all new features from master  
✅ All signing implementation files remain unchanged  
✅ All 19 NuGet packages remain at current versions  
✅ Solution builds without errors  
✅ Signing functionality works as before  
✅ New features are integrated and functional  

### Files That Should Show Changes:
- Files added/modified in master (new features)
- Possibly Program.cs (with signing logic preserved)

### Files That Should NOT Show Changes:
- SignatureHelper.cs
- DigitalSignatureService.cs
- X509Certificate2Extension.cs
- SignatureConfiguration.cs
- packages.config

---

## 🆘 Support Commands

```bash
# View current merge status
git status

# View conflicts
git diff --name-only --diff-filter=U

# See what branch contains what commits
git log --all --oneline --graph --decorate

# Compare branches
git diff digisign-prod..origin/master

# List all files different between branches
git diff --name-status digisign-prod..origin/master

# View specific file from different branch
git show origin/master:Program.cs
git show digisign-prod:Program.cs
```

---

## ⚠️ Important Notes

1. **Never force push** to digisign-prod after merge
2. **Always test** the merged code before deploying
3. **Keep the backup branch** until merge is verified in production
4. **Document any manual changes** made during conflict resolution
5. **Test signing functionality** thoroughly after merge
6. If uncertain about a conflict, **choose digisign-prod version** for signing files

---

## 📝 Merge Completion Checklist

- [ ] Step 1: Verified current branch (digisign-prod)
- [ ] Step 2: Fetched latest changes from remote
- [ ] Step 3: Created backup branch
- [ ] Step 4: Executed merge command
- [ ] Step 5: Resolved all conflicts (if any)
- [ ] Step 6: Verified signing files unchanged
- [ ] Step 7: Verified packages.config unchanged
- [ ] Step 8: Restored NuGet packages
- [ ] Step 9: Built solution successfully
- [ ] Step 10: Tested signing functionality
- [ ] Step 11: Reviewed new features from master
- [ ] Step 12: Committed merge (if not auto-committed)
- [ ] Step 13: Pushed to remote (optional)
- [ ] Step 14: Verified in production environment

---

**End of Merge Plan**
