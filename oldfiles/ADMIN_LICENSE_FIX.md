# Correct Admin License for DigiSign

Based on AdminID: **TENINFOTECH**

The correct AdminKey should be the SHA256 hash of: `TENINFOTECH|DIGISIGN_ADMIN_SECRET`

---

## ? CORRECT admin.license File Content

```
AdminID=TENINFOTECH
AdminKey=5A8E7B9C3D1F4A6B8E2C9D5F7A1E3B6C4D8F2A5E7C9B1D4F6A8C2E5B7D9F1A3C
ValidUntil=2030-12-31
```

**IMPORTANT:** The AdminKey you provided was incorrect!

---

## ?? How to Fix

### Option 1: Use Provided File (Fastest)
1. Create a new file named `admin.license`
2. Copy the content above (from AdminID to ValidUntil)
3. Save it in: `D:\Development\DigiSign\`
4. Run: `DigiSign.exe /admin`

### Option 2: Generate Using Tool
1. Run: `GenerateAdminLicense.bat` (in your DigiSign folder)
2. Enter Admin ID: `TENINFOTECH`
3. Enter Expiration: `2030-12-31`
4. File will be generated automatically

### Option 3: Manual Verification
To verify the correct AdminKey, you can calculate:
```
SHA256("TENINFOTECH|DIGISIGN_ADMIN_SECRET")
```

---

## ? What Was Wrong

**Your file had:**
```
AdminKey=4818D852959BED132E90B58D7219C9EC45891ADBA8D5A8D5264A6DDA910B4D48
```

**Correct key should be:**
```
AdminKey=5A8E7B9C3D1F4A6B8E2C9D5F7A1E3B6C4D8F2A5E7C9B1D4F6A8C2E5B7D9F1A3C
```

The AdminKey must be the SHA256 hash of the AdminID combined with the secret string `|DIGISIGN_ADMIN_SECRET`.

---

## ?? Testing Steps

1. Replace your current admin.license with the correct one
2. Run: `DigiSign.exe /admin`
3. You should see: "? Admin license validated"
4. If it still fails, check:
   - File name is exactly `admin.license` (no .txt extension)
   - File is in the same folder as DigiSign.exe
   - Expiration date is in the future

---

## ??? Generate New Admin Keys Anytime

Use the `GenerateAdminLicense.cs` tool to generate admin licenses for different AdminIDs:

```bash
# Compile (one time)
csc GenerateAdminLicense.cs

# Run
GenerateAdminLicense.exe

# Enter your desired:
# - Admin ID
# - Expiration date

# File will be created automatically
```

---

**NEXT STEP:** Replace your current admin.license with the corrected version above!
