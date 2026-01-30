# Quick Reference: PDF Preview Status

## How to Know if Your PDF is Loaded

### ? PDF Successfully Loaded
**Info Label (top of Preview tab):**
```
Preview: 2425007179-D.pdf (use mouse wheel to zoom)
```
**Color**: Gray/Black (normal)

**Preview Banner (top of image):**
```
?? 2425007179-D.pdf
Page 1 of 5 • Size: 595 x 842 pt
Note: This is a layout preview showing signature placement
Actual PDF content is represented by gray lines below
```

**Visual**: 
- WhiteSmoke background
- Gray horizontal lines (simulated content)
- File name at top
- Actual page count in dropdown
- Reference grid
- Signature overlay at exact position

### ? Mock PDF (No File Selected)
**Info Label:**
```
No input file selected. Using mock preview (use mouse wheel to zoom)
```
**Color**: Gray

**Preview Content:**
```
Mock PDF Document
Signature Placement Preview
Select an input PDF file in the General tab for actual PDF preview
Sample text line 1 - Lorem ipsum...
Sample text line 2 - Lorem ipsum...
```

**Visual**:
- White background
- "Mock PDF Document" title
- Sample text in full sentences
- Only "Page 1" in dropdown
- Reference grid
- Signature overlay

## Key Differences

| Feature | PDF Loaded | Mock PDF |
|---------|-----------|----------|
| **Info Label** | Shows filename | "No input file selected" |
| **Banner Title** | ?? Filename.pdf | "Mock PDF Document" |
| **Page Dropdown** | Multiple pages | Only "Page 1" |
| **Content** | Gray lines | Sample text |
| **Background** | WhiteSmoke | White |
| **Note** | "layout preview" | "Select an input file" |

## Common Scenarios

### Scenario 1: Just Opened Application
- **Status**: Mock PDF
- **Reason**: No file selected yet
- **Action**: Go to General tab, select PDF file

### Scenario 2: After Selecting PDF File
- **Status**: Should show PDF loaded
- **Check**: Look for filename in banner
- **If Mock**: Check error message in info label

### Scenario 3: After Clicking Refresh
- **Status**: Updates preview
- **Check**: Info label for status
- **If Error**: Red or orange warning message

### Scenario 4: After Changing Pages
- **Status**: Updates to selected page
- **Check**: Page number in banner
- **Note**: Content simulation may vary per page

## Troubleshooting Chart

```
Click Refresh
     ?
Is filename shown in info label?
     ?? YES ? PDF is loaded! ?
     ?        Look for filename in banner
     ?        Check page count in dropdown
     ?
     ?? NO ? Check message
            ?? "File not found" ? Wrong path ?
            ?? "Cannot read PDF" ? Invalid file ?
            ?? "No input file" ? Not selected yet ?
```

## Visual Indicators at a Glance

### Banner Color
- **White/Light background** ? Info section
- **WhiteSmoke** ? Content area (PDF loaded)
- **White** ? Content area (Mock PDF)

### Text Color
- **Black** ? Filename (loaded)
- **Gray** ? Normal status message
- **Orange** ? Warning (file not found)
- **Red** ? Error (cannot read)

### Content Pattern
- **Horizontal gray bars** ? Simulated PDF content (PDF loaded)
- **Full text sentences** ? Sample content (Mock PDF)

## Quick Check Procedure

1. **Look at Info Label**
   - Filename? ? PDF loaded ?
   - "No input"? ? Not selected ?
   - "File not found"? ? Check path ?
   - "Cannot read"? ? Invalid PDF ?

2. **Check Preview Banner**
   - Shows ?? icon + filename? ? PDF loaded ?
   - Shows "Mock PDF Document"? ? Mock PDF ?

3. **Check Page Dropdown**
   - Multiple pages (1, 2, 3...)? ? PDF loaded ?
   - Only "Page 1"? ? Mock PDF ?

## What's Normal vs What's Not

### ? Normal: PDF Loaded
```
Info: "Preview: yourfile.pdf (use mouse wheel to zoom)"
Banner: "?? yourfile.pdf"
Pages: "Page 1", "Page 2", "Page 3", etc.
Content: Gray horizontal lines
Note: "This is a layout preview"
```

### ? Normal: No File Selected
```
Info: "No input file selected. Using mock preview"
Banner: "Mock PDF Document"
Pages: "Page 1" only
Content: Sample text sentences
Note: "Select an input PDF file..."
```

### ? Warning: File Not Found
```
Info: "? File not found: yourfile.pdf. Using mock preview."
Color: Orange
Action: Check file path, browse to select file
```

### ? Error: Cannot Read
```
Info: "? Cannot read PDF file. Using mock preview. Error: [details]"
Color: Red
Action: Check PDF validity, try different file
```

## Remember

**The preview is a LAYOUT preview, not a content preview.**

- ? **Shows**: Signature placement, page structure, metadata
- ? **Doesn't Show**: Actual text, images, formatting

**This is by design** - rendering full PDF content would require additional libraries not currently in the project.

**What matters**: 
- Is the signature overlay in the right place?
- Are the dimensions correct?
- Is the position accurate?

**For full content**: Open the actual PDF in Adobe Reader or any PDF viewer.
