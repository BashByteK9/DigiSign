# Admin Mode - License Generation Only

## ?? IMPORTANT: Admin Mode Does NOT Sign PDFs

Admin mode (`/admin` flag) is **EXCLUSIVELY** for generating user licenses. It will **NEVER** process or sign PDFs.

---

## ?? Purpose

Admin mode allows administrators to:
- ? Generate `license.txt` files from user `license.key` files
- ? Validate admin.license before proceeding
- ? Provide detailed error messages for troubleshooting

Admin mode will **NOT**:
- ? Sign PDFs
- ? Process any documents
- ? Read IP.xml configuration
- ? Load certificates
- ? Perform any PDF operations

---

## ?? Usage

### Command
```bash
DigiSign.exe /admin
```

### Prerequisites
1. Valid `admin.license` file in application directory
2. User's `license.key` file (path will be requested)

---

## ?? Process Flow

### Step 1: Admin License Validation
```
DigiSign.exe /admin
?
Check if admin.license exists
?
?? NOT FOUND ? Show error and exit
?? INVALID/EXPIRED ? Show error and exit
?? VALID ? Continue to Step 2
```

### Step 2: License Key Path Input
```
Prompt for license.key file path
?
User enters path (or presses Enter to cancel)
?
?? EMPTY ? Show "cancelled" message and exit
?? FILE NOT FOUND ? Show detailed error and exit
?? FILE FOUND ? Continue to Step 3
```

### Step 3: License Generation
```
Generate license.txt from license.key
?
?? SUCCESS ? Show success message and exit
?? FAILURE ? Show error message and exit
```

### Step 4: Exit
```
Application exits
?
NO PDF processing occurs
NO document signing occurs
```

---

## ?? Console Output Examples

### Success Case
```
???????????????????????????????????????????????????????????
?? Admin License Generation Mode
???????????????????????????????????????????????????????????
? Admin license validated

This mode is ONLY for generating user licenses.
No PDF signing will be performed.

???????????????????????????????????????????????????????????

Enter the path to license.key file (or press Enter to exit): C:\Users\JohnDoe\Desktop\license.key

? License key file found: license.key

Device ID: CPUID123_DISKID456
Machine Name: WORKSTATION01

Enter Customer ID: CUST-2025-001
Enter License Number: LIC-2025-001
Enter Expiration Date (YYYY-MM-DD): 2026-12-31

???????????????????????????????????????????????????????????
? LICENSE GENERATED SUCCESSFULLY!
???????????????????????????????????????????????????????????

The license.txt file has been created in the same folder
as the license.key file. Please send this file to the user.

Press any key to exit...
```

### Error Case 1: No Admin License
```
???????????????????????????????????????????????????????????
?? Admin License Generation Mode
???????????????????????????????????????????????????????????
? ERROR: admin.license file not found!

To use admin mode, you need a valid admin.license file
in the application directory: D:\Development\DigiSign\

Press any key to exit...
```

### Error Case 2: Invalid Admin License
```
???????????????????????????????????????????????????????????
?? Admin License Generation Mode
???????????????????????????????????????????????????????????
? ERROR: Invalid or expired admin.license!

Your admin license is either invalid or has expired.
Please contact the administrator for a valid license.

Press any key to exit...
```

### Error Case 3: License Key Not Found
```
???????????????????????????????????????????????????????????
?? Admin License Generation Mode
???????????????????????????????????????????????????????????
? Admin license validated

This mode is ONLY for generating user licenses.
No PDF signing will be performed.

???????????????????????????????????????????????????????????

Enter the path to license.key file (or press Enter to exit): C:\Wrong\Path\license.key

? ERROR: License key file not found!

Path provided: C:\Wrong\Path\license.key

Please verify:
  1. The file path is correct
  2. The file exists at that location
  3. You have permission to read the file

Example valid paths:
  C:\Users\Admin\Desktop\license.key
  D:\Licenses\user123\license.key
  .\license.key (current directory)

Press any key to exit...
```

### Error Case 4: User Cancels
```
???????????????????????????????????????????????????????????
?? Admin License Generation Mode
???????????????????????????????????????????????????????????
? Admin license validated

This mode is ONLY for generating user licenses.
No PDF signing will be performed.

???????????????????????????????????????????????????????????

Enter the path to license.key file (or press Enter to exit): 

??  License generation cancelled.

Press any key to exit...
```

---

## ?? Error Messages Guide

| Error Message | Cause | Solution |
|--------------|-------|----------|
| `admin.license file not found` | No admin.license in app folder | Place valid admin.license file |
| `Invalid or expired admin.license` | Admin license is invalid/expired | Get new admin.license from issuer |
| `License key file not found` | Incorrect path or file missing | Verify file path and existence |
| `License generation cancelled` | User pressed Enter without path | Normal - no action needed |
| `Failed to generate license file` | Invalid license.key or no write permission | Check file validity and permissions |

---

## ? Verification Checklist

### Before Running Admin Mode
- [ ] `admin.license` exists in application directory
- [ ] `admin.license` is valid and not expired
- [ ] You have the user's `license.key` file
- [ ] You know the full path to `license.key`
- [ ] You have write permissions to the output folder

### After License Generation
- [ ] `license.txt` created in same folder as `license.key`
- [ ] `license.txt` contains all required fields
- [ ] Application exited without processing PDFs
- [ ] No PDF files were modified or created

---

## ?? Common Scenarios

### Scenario 1: Generate License for New User
```bash
# User sends you their license.key file
# You save it to: C:\Licenses\NewUser\license.key

# Run admin mode
DigiSign.exe /admin

# Enter path when prompted
C:\Licenses\NewUser\license.key

# Enter license details
Customer ID: CUST-2025-100
License Number: LIC-2025-100
Expiration Date: 2026-12-31

# Result: license.txt created at C:\Licenses\NewUser\license.txt
# Send license.txt back to the user
```

### Scenario 2: Batch License Generation
```bash
# For each user:
# 1. Run: DigiSign.exe /admin
# 2. Enter path to their license.key
# 3. Enter their license details
# 4. Send generated license.txt back

# Process repeats for each user
# No PDF processing occurs at any point
```

### Scenario 3: License Regeneration
```bash
# User lost their license.txt
# They send you their license.key again

# Run admin mode
DigiSign.exe /admin

# Enter path to their license.key
C:\Users\Support\Downloads\license.key

# Enter NEW license details (can be same or different)
Customer ID: CUST-2025-100
License Number: LIC-2025-100-RENEWED
Expiration Date: 2027-12-31

# New license.txt generated
# Send to user
```

---

## ?? Security Notes

1. **Admin License Protection**
   - Keep `admin.license` secure
   - Don't share admin.license with end users
   - Only authorized admins should have this file

2. **License Key Privacy**
   - Each license.key is unique to a device
   - Don't use same license.key for multiple devices
   - Verify device info matches user's machine

3. **License Generation Logging**
   - All license generation attempts are logged
   - Check `application_log.txt` for audit trail
   - Review logs periodically

---

## ?? Comparison: Admin Mode vs Normal Mode

| Aspect | Admin Mode (`/admin`) | Normal Mode |
|--------|----------------------|-------------|
| **Purpose** | Generate licenses | Sign PDFs |
| **Admin License** | Required | Optional (shows hint) |
| **User License** | Not checked | Checked (demo/full) |
| **PDF Processing** | ? Never | ? Always |
| **Certificate Loading** | ? Never | ? Always |
| **IP.xml Reading** | ? Never | ? Always |
| **Output** | license.txt file | Signed PDFs |
| **Exit Behavior** | Exits immediately | Exits after PDF processing |

---

## ?? Troubleshooting

### Problem: "admin.license file not found"
**Solution:**
1. Verify admin.license exists in app folder
2. Check file name is exactly `admin.license` (not `admin.license.txt`)
3. Ensure file is in same folder as DigiSign.exe

### Problem: "Invalid or expired admin.license"
**Solution:**
1. Check expiration date in admin.license
2. Verify AdminKey matches AdminID
3. Contact license issuer for new admin.license

### Problem: Can't find license.key file
**Solution:**
1. Use full path (e.g., `C:\Users\...`)
2. Use quotes for paths with spaces: `"C:\My Documents\license.key"`
3. Or use relative path from app directory: `.\license.key`

### Problem: License generation fails
**Solution:**
1. Check `application_log.txt` for detailed error
2. Verify license.key format is correct
3. Ensure write permissions to output folder
4. Check disk space availability

---

## ?? Best Practices

1. **Organize License Files**
   ```
   D:\Licenses\
   ??? User001\
   ?   ??? license.key (received)
   ?   ??? license.txt (generated)
   ??? User002\
   ?   ??? license.key
   ?   ??? license.txt
   ??? ...
   ```

2. **Track License Generation**
   - Keep record of all licenses generated
   - Note customer ID, license number, and expiry
   - Save application_log.txt for each generation

3. **Verify Before Sending**
   - Open generated license.txt
   - Verify all fields are correct
   - Confirm expiration date is future

4. **Communication with Users**
   - Send license.txt via secure channel
   - Include instructions for installation
   - Provide expiration date notice

---

## ? Summary

| ? Admin Mode DOES | ? Admin Mode DOES NOT |
|-------------------|----------------------|
| Generate license.txt | Sign PDFs |
| Validate admin.license | Process documents |
| Prompt for license.key path | Read IP.xml |
| Show detailed error messages | Load certificates |
| Log all operations | Create signed PDFs |
| Exit after generation | Run in background |

**Remember:** Admin mode is license generation only. For PDF signing, run without `/admin` flag.

---

**Last Updated:** 2025-01-20  
**Version:** 2.2  
**Mode:** Admin License Generation (No PDF Processing)
