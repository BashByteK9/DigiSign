# Demo Mode - Quick Reference Card

## ?? Quick Test (30 seconds)

```bash
# 1. Remove license
del license.txt

# 2. Run application
DigiSign.exe

# 3. Check console - Should see:
```
```
?????????????????????????????????????????????????????????????
?               RUNNING IN DEMO MODE                        ?
?   All signed PDFs will include '*** DEMO MODE ***'        ?
?   watermark in RED on the signature.                      ?
?????????????????????????????????????????????????????????????
```

```bash
# 4. Sign a PDF and check signature
# Expected: "*** DEMO MODE ***" in ?? RED color
```

---

## ?? Visual Indicators

### Console Output
| Mode | Console Message |
|------|-----------------|
| DEMO | ?? Yellow box: "RUNNING IN DEMO MODE" |
| FULL | ? Green check: "License valid — Full Mode enabled" |

### PDF Signature
| Mode | Watermark | Color |
|------|-----------|-------|
| DEMO | "*** DEMO MODE ***" | ?? RED |
| FULL | (none) | ? N/A |

### Log File
| Mode | Log Entry |
|------|-----------|
| DEMO | `Application mode: DEMO` |
| FULL | `Application mode: FULL` |

---

## ?? Troubleshooting Matrix

| Symptom | Check | Fix |
|---------|-------|-----|
| No watermark visible | Signature box size | Increase height in IP.xml |
| Watermark is black | Log for "RED" | Update application |
| Demo mode not active | License.txt exists | Delete license.txt |
| Text cut off | Box dimensions | Height ? 100px |

---

## ?? Signature Appearance

### DEMO Mode
```
????????????????????????????????
? John Doe                     ?
? Digitally signed by John Doe ?
? Date: 20.01.2025 14:30:00   ?
? *** DEMO MODE ***  ?? RED    ?
????????????????????????????????
```

### FULL Mode
```
????????????????????????????????
? John Doe                     ?
? Digitally signed by John Doe ?
? Date: 20.01.2025 14:30:00   ?
????????????????????????????????
```

---

## ?? Log Search Terms

| To Find | Search For |
|---------|-----------|
| Current mode | `Application mode:` |
| Demo watermark | `DEMO MODE watermark` |
| Signature text | `Signature text:` |
| Drawing status | `Drawing.*RED` |

---

## ?? Configuration Check

**IP.xml - Signature Box Minimum Size:**
```xml
<FILENAME>200</FILENAME> <!-- Width: ?200 -->
<FILENAME>100</FILENAME> <!-- Height: ?100 -->
```

**Formula:**
```
Height = (Lines × LineHeight) + Padding
       = (4 × 20) + 20
       = 100 pixels minimum
```

---

## ?? Quick Support

**Send These Files:**
1. `application_log.txt` - Full execution trace
2. Screenshot of signed PDF signature
3. Screenshot of console output

**Critical Log Lines:**
```
INFO     | Application mode: DEMO/FULL
INFO     | Starting PDF signing - Demo Mode: True/False
DEBUG    | Drawing DEMO MODE watermark in RED
```

---

## ? Success Criteria

Demo mode is working correctly if ALL are true:

- [ ] Console shows yellow warning box
- [ ] Log shows "Application mode: DEMO"
- [ ] Log shows "Demo Mode: True" during signing
- [ ] Log shows "Drawing DEMO MODE watermark in RED"
- [ ] PDF signature contains "*** DEMO MODE ***"
- [ ] Watermark text is RED in color
- [ ] All 4 lines are visible (not cut off)

---

**Quick Help:** See DEMO_MODE_VERIFICATION.md for detailed guide
