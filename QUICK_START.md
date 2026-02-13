# DigiSign Merge - Quick Start Guide

## 📋 Overview
This guide helps you merge updates from the `master` branch into `digisign-prod` while preserving all digital signing functionality and NuGet packages.

---

## 🚀 Quick Start (Recommended)

### Option 1: Use PowerShell Script (Easiest)
```powershell
# Navigate to repository
cd D:\Development\DigiSign

# Run interactive script
.\merge.ps1
```

The script provides a menu with options:
1. Fetch latest changes
2. Create backup branch
3. Start merge
4. Check merge status
5. Resolve conflicts (automated)
6. Verify signing files
7. Complete merge
8. Abort merge
9. Full verification

### Option 2: Use Batch Script
```cmd
cd D:\Development\DigiSign
merge-helper.bat
```

### Option 3: Manual Git Commands
See **MERGE_PLAN.md** for detailed step-by-step instructions.

---

## ⚡ Express Merge (No Conflicts Expected)

If you expect a clean merge:

```bash
cd D:\Development\DigiSign
git fetch --all
git branch digisign-prod-backup-$(date +%Y%m%d)
git merge origin/master --no-ff -m "Merge master into digisign-prod"
```

If successful:
```bash
# Verify
git status

# Build
msbuild DigiSign.sln /t:Rebuild

# Push
git push origin digisign-prod
```

---

## 🔥 If You Get Conflicts

### Automated Resolution
```powershell
.\merge.ps1 -ResolveConflicts
```

### Manual Resolution

#### For Signing Files (Keep digisign-prod version):
```bash
git checkout --ours SignatureHelper.cs
git checkout --ours DigitalSignatureService.cs
git checkout --ours X509Certificate2Extension.cs
git checkout --ours SignatureConfiguration.cs
git checkout --ours packages.config
git add .
```

#### For Program.cs (Manual Review):
```bash
# Open in Visual Studio Code
code Program.cs

# Look for conflict markers:
# <<<<<<< HEAD (digisign-prod)
# ...keep all signing logic...
# =======
# ...review new features from master...
# >>>>>>> origin/master

# After resolving:
git add Program.cs
```

#### Complete Merge:
```bash
git commit -m "Merge master into digisign-prod - preserved signing implementation"
```

---

## ✅ Verification Checklist

After merge, verify:

- [ ] **No changes to signing files**
  ```bash
  git diff digisign-prod-backup-<date> SignatureHelper.cs
  git diff digisign-prod-backup-<date> DigitalSignatureService.cs
  git diff digisign-prod-backup-<date> X509Certificate2Extension.cs
  git diff digisign-prod-backup-<date> SignatureConfiguration.cs
  ```

- [ ] **No changes to packages**
  ```bash
  git diff digisign-prod-backup-<date> packages.config
  ```

- [ ] **Solution builds successfully**
  - Open in Visual Studio
  - Build → Rebuild Solution
  - Check for errors

- [ ] **Restore NuGet packages**
  - Right-click solution → Restore NuGet Packages
  - Or: `nuget restore DigiSign.sln`

- [ ] **Test signing functionality**
  - Run with test PDF
  - Verify signature appears correctly
  - Check certificate loading

---

## 🔒 Protected Files

These files MUST NOT change (always keep digisign-prod version):

1. **SignatureHelper.cs** - Core signing implementation
2. **DigitalSignatureService.cs** - Certificate and PDF signing
3. **X509Certificate2Extension.cs** - PIN handling
4. **SignatureConfiguration.cs** - Configuration model
5. **packages.config** - All 19 NuGet packages

---

## 📦 Protected NuGet Packages

All 19 packages must remain at current versions:
- BouncyCastle 1.8.9
- BouncyCastle.Cryptography 2.4.0
- iTextSharp 5.5.13.4
- Microsoft.Bcl.Cryptography 9.0.9
- Pkcs11Interop 5.3.0
- Spire.PDF 11.9.17
- System.Security.Cryptography.Pkcs 9.0.9
- ... (+ 12 more, see MERGE_PLAN.md)

---

## 🆘 Emergency Rollback

If something goes wrong:

### Abort Merge (Before Commit)
```bash
git merge --abort
```

### Reset to Backup (After Commit)
```bash
git reset --hard digisign-prod-backup-<date>
```

### Revert Merge Commit
```bash
git log --oneline  # Find merge commit SHA
git revert -m 1 <merge-commit-sha>
```

---

## 📁 Generated Files

| File | Purpose |
|------|---------|
| **MERGE_PLAN.md** | Complete detailed merge guide with all commands and explanations |
| **merge.ps1** | PowerShell interactive script with automated conflict resolution |
| **merge-helper.bat** | Windows batch script for step-by-step merge |
| **resolve-conflicts.sh** | Bash script for Git Bash conflict resolution |
| **QUICK_START.md** | This file - quick reference guide |

---

## 🎯 Common Scenarios

### Scenario 1: Clean Merge (No Conflicts)
```bash
git fetch --all
git branch digisign-prod-backup-$(date +%Y%m%d)
git merge origin/master --no-ff
# Success! Verify and push
```

### Scenario 2: Conflicts in Signing Files
```bash
git merge origin/master --no-ff
# CONFLICT in SignatureHelper.cs, packages.config
git checkout --ours SignatureHelper.cs
git checkout --ours packages.config
git add .
git commit -m "Merge master - preserved signing logic"
```

### Scenario 3: Conflicts in Program.cs
```bash
git merge origin/master --no-ff
# CONFLICT in Program.cs
code Program.cs  # Manually merge
# Keep all signing workflow
# Accept new features that don't conflict
git add Program.cs
git commit -m "Merge master - integrated new features"
```

### Scenario 4: Need to Review Before Committing
```bash
git merge origin/master --no-ff --no-commit
# Review all changes
git diff --cached
# If happy:
git commit -m "Merge master into digisign-prod"
# If not happy:
git merge --abort
```

---

## 📞 Support Commands

```bash
# Check merge status
git status

# See what's different between branches
git diff digisign-prod..origin/master

# List conflicted files
git diff --name-only --diff-filter=U

# View file from master branch
git show origin/master:Program.cs

# Compare with backup
git diff digisign-prod-backup-<date> HEAD
```

---

## ⏱️ Estimated Time

- **Clean merge**: 5-10 minutes
- **With conflicts**: 15-30 minutes
- **Complex conflicts**: 30-60 minutes

---

## 🎓 Best Practices

1. **Always create a backup branch** before merging
2. **Fetch latest changes** from remote first
3. **Resolve conflicts carefully** - when in doubt, keep digisign-prod version for signing files
4. **Test thoroughly** after merge
5. **Document any manual changes** you make
6. **Don't force push** after merge
7. **Keep backup branch** until verified in production

---

## 📊 Success Criteria

✅ Merge is successful when:
- All conflicts resolved
- Solution builds without errors
- All signing files unchanged (or changes documented)
- All package versions unchanged
- New features from master integrated
- Signing functionality works correctly
- No breaking changes introduced

---

## 🔗 Next Steps After Merge

1. Build solution in Visual Studio
2. Run tests (if available)
3. Test with sample PDFs
4. Verify all new features work
5. Update changelog/release notes
6. Push to remote: `git push origin digisign-prod`
7. Deploy to test environment
8. Verify in production

---

**For detailed instructions, see MERGE_PLAN.md**

**For automated merge, run: .\merge.ps1**
