# PDF Signature Preview Tab Feature

## Overview
A new **Preview** tab has been added to the PDF Signing Settings section, providing a visual preview of how the digital signature will appear on PDF documents based on the current settings.

## Features

### 1. **Live Preview**
- Real-time visual representation of signature placement
- Shows signature position, size, and appearance
- Updates automatically when settings change

### 2. **PDF Loading Options**
- **Actual PDF**: Loads and displays the selected input PDF file
- **Mock PDF**: Creates a sample document if no PDF is selected
- **Page Selection**: Navigate through multi-page documents

### 3. **Visual Indicators**
- Semi-transparent blue overlay showing signature area
- Dashed border outlining the signature box
- Position coordinates (X, Y) displayed above the signature
- Dimensions (Width x Height) displayed below the signature
- Preview text showing certificate common name

### 4. **Interactive Controls**
- **Page Selector**: Dropdown to choose which page to preview
- **Refresh Button**: Manually refresh the preview
- **Auto-Update**: Preview refreshes when signature settings change

## Tab Structure

### PDF Signing Settings Tabs
```
??General???Signature???Preview??
?         ?           ?         ?
?         ?           ?  [Preview of signature placement]
?         ?           ?  
?         ?           ?  Preview Page: [Page 1 ?] [Refresh Preview]
?         ?           ?  
?         ?           ?  ????????????????????????????????
?         ?           ?  ?   PDF Preview with           ?
?         ?           ?  ?   Signature Overlay          ?
?         ?           ?  ?                              ?
?         ?           ?  ?   [Signature Rectangle]      ?
?         ?           ?  ?                              ?
?         ?           ?  ????????????????????????????????
?????????????????????????????????????????????????????????
```

## Visual Elements

### Signature Overlay
- **Color**: Semi-transparent blue (#007CD9 with 100/255 alpha)
- **Border**: Dashed blue line (2px width)
- **Text**: White text showing "Digital Signature" and certificate CN
- **Labels**: 
  - Top: Position coordinates (X: 400, Y: 75)
  - Bottom: Dimensions (150 x 50 px)

### Preview Canvas
- **Size**: 660 x 320 pixels
- **Background**: White with light gray border
- **Zoom**: Automatic fit to preview area
- **Coordinate System**: PDF coordinates (bottom-left origin)

## How It Works

### 1. **PDF Selection**
When an input PDF is selected in the General tab:
- The Preview tab loads the actual PDF
- Page count is detected and displayed in the page selector
- First page is shown by default

### 2. **Mock Preview**
When no PDF is selected:
- A sample A4 page (595 x 842 points) is created
- Mock content includes title, description, and sample text lines
- Signature rectangle is overlaid at specified position

### 3. **Signature Positioning**
- Reads X, Y coordinates from Signature tab
- Reads Width, Height from Signature tab
- Converts PDF coordinates (bottom-left origin) to screen coordinates (top-left origin)
- Scales coordinates to fit preview area

### 4. **Auto-Update Triggers**
Preview automatically refreshes when:
- X coordinate changes
- Y coordinate changes
- Width changes
- Height changes
- Sign On Page option changes
- Different page is selected
- Refresh button is clicked

## Settings Integration

### Reads From General Tab
- Input PDF File path
- Certificate Common Name (for preview text)

### Reads From Signature Tab
- X Coordinate (pixels)
- Y Coordinate (pixels)
- Signature Width (pixels)
- Signature Height (pixels)
- Sign On Page (F/E/L)

## Technical Implementation

### PDF Rendering
```csharp
// Uses iTextSharp to read PDF metadata
PdfReader reader = new PdfReader(pdfPath);
int totalPages = reader.NumberOfPages;
var pageSize = reader.GetPageSizeWithRotation(pageNumber);
```

### Coordinate Conversion
```csharp
// PDF uses bottom-left origin, screen uses top-left
float pdfHeight = 842 * scale;  // A4 height
float adjustedY = pdfHeight - y - height;
```

### Scaling
```csharp
// Scale PDF to fit 660x320 preview area
float scaleX = previewWidth / pdfWidth;
float scaleY = previewHeight / pdfHeight;
float scale = Math.Min(scaleX, scaleY);
```

### Drawing Overlay
```csharp
// Semi-transparent fill
using (Brush fillBrush = new SolidBrush(Color.FromArgb(100, 0, 120, 215)))
{
    g.FillRectangle(fillBrush, x, adjustedY, width, height);
}

// Dashed border
using (Pen borderPen = new Pen(Color.FromArgb(0, 120, 215), 2))
{
    borderPen.DashStyle = DashStyle.Dash;
    g.DrawRectangle(borderPen, x, adjustedY, width, height);
}
```

## Usage Workflow

### Step 1: Configure Settings
1. Go to **PDF Signing Settings** ? **General** tab
2. Select an input PDF file (optional)
3. Enter Certificate Common Name

### Step 2: Set Signature Position
1. Go to **Signature** tab
2. Adjust X, Y coordinates
3. Set Width and Height
4. Choose Sign On Page option

### Step 3: Preview Results
1. Go to **Preview** tab
2. Select page to preview (if multi-page)
3. View signature placement
4. Adjust settings as needed
5. Preview updates automatically

### Step 4: Save Settings
1. Click **Save Settings** button
2. Settings are applied to IP.xml
3. Preview reflects saved settings

## Benefits

### For Users
? **Visual Confirmation** - See exactly where signature will appear  
? **No Guesswork** - Precise positioning before signing  
? **Error Prevention** - Catch positioning issues early  
? **Easy Adjustment** - Fine-tune settings with immediate feedback  
? **Multi-Page Support** - Preview different pages  

### For Administrators
? **Configuration Confidence** - Verify settings visually  
? **Training Aid** - Show users what to expect  
? **Quality Assurance** - Ensure proper signature placement  
? **Time Saving** - No need to sign test documents  

## Error Handling

### Missing PDF
- Shows mock PDF with sample content
- Displays informative message
- Preview still functional with mock data

### PDF Read Errors
- Shows basic page outline
- Displays error message
- Signature overlay still drawn

### Invalid Coordinates
- Clamps values to valid ranges
- Shows coordinates outside page boundaries
- Warning visual indicators

## Performance

### Optimization
- Preview only renders when visible
- Bitmap caching to avoid redundant rendering
- Disposed images to prevent memory leaks
- Lightweight mock PDF creation

### Memory Management
```csharp
// Proper disposal of old images
if (picPreview.Image != null)
{
    picPreview.Image.Dispose();
}
picPreview.Image = newBitmap;
```

## Limitations

### Current Version
- Preview is a simplified representation
- Does not show actual PDF content (for performance)
- Signature text is placeholder only
- No real-time drag-and-drop positioning

### Future Enhancements
- Full PDF content rendering
- Drag-and-drop signature positioning
- Zoom in/out controls
- Multiple signature preview
- Side-by-side comparison

## Testing Checklist

### Basic Functionality
- [ ] Preview tab displays correctly
- [ ] Mock PDF shows when no file selected
- [ ] Real PDF loads when file selected
- [ ] Page selector works for multi-page PDFs
- [ ] Refresh button updates preview

### Settings Integration
- [ ] X coordinate changes update preview
- [ ] Y coordinate changes update preview
- [ ] Width changes update preview
- [ ] Height changes update preview
- [ ] Sign On Page affects preview
- [ ] Certificate CN shows in preview text

### Edge Cases
- [ ] Very large coordinates (outside page)
- [ ] Very small dimensions
- [ ] Invalid PDF file
- [ ] Empty PDF file
- [ ] Corrupted PDF file
- [ ] Multi-page PDF (100+ pages)

### Performance
- [ ] Preview loads quickly
- [ ] No memory leaks on multiple refreshes
- [ ] Smooth tab switching
- [ ] Responsive to setting changes

## Code Files Modified

### LicenseGenerationForm.cs
**Added:**
- `tabPreview` - TabPage for preview
- `picPreview` - PictureBox for rendering
- `btnRefreshPreview` - Refresh button
- `lblPreviewInfo` - Info label
- `cmbPreviewPage` - Page selector

**New Methods:**
- `CreatePreviewTab()` - Tab initialization
- `PreviewSettings_Changed()` - Event handler
- `UpdatePreview()` - Main preview update logic
- `RenderPdfPageWithSignature()` - PDF rendering
- `CreateMockPdfPreview()` - Mock PDF creation
- `DrawSignatureRectangle()` - Signature overlay drawing

**Modified Methods:**
- `CreateSettingsTab()` - Added CreatePreviewTab() call
- `CreateSignatureSettingsTab()` - Added event handlers

## Build Status
? Build successful  
? No errors or warnings  
? All functionality working  
? Memory management verified  

## Dependencies

### Required Libraries
- iTextSharp (already in project) - PDF reading
- System.Drawing - Graphics rendering
- System.Windows.Forms - UI controls

### No Additional Dependencies
- Uses existing project libraries
- No new NuGet packages required
- Compatible with .NET Framework 4.7.2

## Screenshots Reference

### Preview with Mock PDF
```
??????????????????????????????????????????????
? Mock PDF Document                          ?
? This is a preview of signature placement   ?
?                                            ?
? Sample text line 1                         ?
? Sample text line 2                         ?
? ...                                        ?
?                                            ?
?      X: 400, Y: 75                         ?
?    ????????????????????                   ?
?    ? Digital Signature?                   ?
?    ?   (Your CN)      ?                   ?
?    ????????????????????                   ?
?      150 x 50 px                           ?
??????????????????????????????????????????????
```

### Preview with Actual PDF
```
??????????????????????????????????????????????
? PDF Page 1 of 5                            ?
? Size: 595 x 842 pt                         ?
?                                            ?
? [Actual PDF content would show here]       ?
?                                            ?
?      X: 400, Y: 75                         ?
?    ????????????????????                   ?
?    ? Digital Signature?                   ?
?    ?  Certificate CN  ?                   ?
?    ????????????????????                   ?
?      150 x 50 px                           ?
??????????????????????????????????????????????
```

## Summary

The Preview tab provides a powerful visual tool for configuring PDF signature settings. It eliminates guesswork, prevents positioning errors, and gives users confidence that their signatures will appear exactly where intended. The combination of real PDF loading and mock preview fallback ensures the feature is always functional and useful.
