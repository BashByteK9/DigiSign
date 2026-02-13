# DigiSign License Quick Reference

## For Users

### Initial Setup (No License)
1. Run `DigiSign.exe`
2. **license.key** is automatically created in the application folder
3. Application runs in **Demo Mode** (watermark on signatures)
4. Share **license.key** with your administrator

### After Receiving License
1. Place **license.txt** (received from admin) in the application folder
2. Run `DigiSign.exe`
3. Application runs in **Full Mode** (no watermark)

---

## For Administrators

### Setup
1. Create **admin.license** file in application folder
2. Use provided template or example file

### Generate User License
1. Receive **license.key** from user
2. Run: `DigiSign.exe /admin`
3. Choose **Y** when prompted
4. Enter path to user's **license.key** file
5. Enter:
   - Customer ID
   - License Number  
   - Expiration Date (YYYY-MM-DD)
6. **license.txt** is generated in same folder as **license.key**
7. Send **license.txt** back to user

### Use Application Normally
- Just run `DigiSign.exe` (without /admin flag)
- Works like regular user for signing PDFs

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Demo mode watermark appears | No valid **license.txt** in app folder |
| "Device mismatch" error | License was generated for different computer |
| Cannot generate licenses | Missing **admin.license** or it's expired |
| Application waits for input | Run without `/admin` flag for normal use |

---

## File Locations

All files should be in the **same folder** as `DigiSign.exe`:
- ? `license.key` - Auto-generated on first run (share with admin)
- ? `license.txt` - Place here after receiving from admin
- ? `admin.license` - Admin only (enables license generation)
- ? `IP.xml` - Configuration file for PDF signing

---

## Command Line Options

```bash
DigiSign.exe          # Normal mode - sign PDFs
DigiSign.exe /admin   # Admin mode - generate licenses
```
