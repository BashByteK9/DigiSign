# Interactive Signature Placement - Drag & Resize

## Feature: Drag-and-Drop Signature Positioning

### Overview
The Preview tab now supports **interactive signature placement** - you can drag the signature box to move it and resize it by dragging corners/edges. All changes automatically update the X, Y, Width, and Height parameters in the Signature tab.

## Features Implemented

### ? Drag to Move
- Click and drag anywhere inside the signature box to move it
- X and Y coordinates update in real-time
- Cursor changes to ? (move) when hovering over the box

### ? Resize by Dragging
- Drag **corners** to resize in both dimensions
- Drag **edges** to resize in one dimension
- Width and Height parameters update in real-time
- Cursor changes to indicate resize direction

### ? Visual Feedback
- **Resize handles** appear when dragging/resizing (8 white squares with blue borders)
- **Interactive cursors** show what action will happen
- **Real-time updates** in Signature tab numeric controls

### ? Smart Constraints
- Coordinates stay within valid ranges (0-10000)
- Minimum width/height enforced (10 pixels)
- Smooth dragging with proper coordinate conversion

## How to Use

### Moving the Signature Box

1. **Hover** over the signature box
   - Cursor changes to ? (move cursor)

2. **Click and hold** left mouse button inside the box

3. **Drag** to desired position

4. **Release** mouse button
   - New position is applied
   - X and Y values update in Signature tab

### Resizing the Signature Box

#### Using Corners (Diagonal Resize)
1. **Hover** over a corner handle
   - Cursor changes to ? or ? (diagonal resize)

2. **Click and drag** the corner
   - Box resizes from that corner
   - Width and Height update in Signature tab

3. **Release** to apply

#### Using Edges (Single Direction)
1. **Hover** over an edge (top, bottom, left, right)
   - Cursor changes to ? (vertical) or ? (horizontal)

2. **Click and drag** the edge
   - Box resizes in that direction only

3. **Release** to apply

## Visual Indicators

### Cursors

| Cursor | Meaning | Action |
|--------|---------|--------|
| ? Hand | Default hover | Can interact with signature |
| ? Move All | Inside box | Click to drag and move |
| ? Size WE | Left/Right edges | Resize width |
| ? Size NS | Top/Bottom edges | Resize height |
| ?? Size NWSE | Top-Left/Bottom-Right | Diagonal resize |
| ?? Size NESW | Top-Right/Bottom-Left | Diagonal resize |

### Resize Handles

When dragging or resizing, **8 handles** appear:
- **4 Corner handles**: For diagonal resizing
- **4 Edge handles**: For single-direction resizing

**Visual**: White squares (8x8 pixels) with blue borders

```
?????????????????????????
? ?                   ? ?  ? Corner handles
?                       ?
? ?                   ? ?  ? Edge handles
?                       ?
? ?                   ? ?
?                       ?
? ?                   ? ?
?????????????????????????
```

## Coordinate System

### PDF Coordinates (Internal)
- Origin: **Bottom-left** corner
- X increases: Left ? Right
- Y increases: Bottom ? Top

### Screen Coordinates (Preview)
- Origin: **Top-left** corner
- X increases: Left ? Right
- Y increases: Top ? Bottom

**Conversion**: Automatically handled by the system
- Dragging up in preview ? Y increases in PDF coordinates
- Dragging down in preview ? Y decreases in PDF coordinates

## Real-Time Updates

### What Updates Automatically

**During Drag (Move)**:
- ? `numXCoord.Value` - X coordinate
- ? `numYCoord.Value` - Y coordinate
- ? Visual preview position

**During Resize**:
- ? `numWidth.Value` - Signature width
- ? `numHeight.Value` - Signature height
- ? `numXCoord.Value` - X coordinate (when resizing from left)
- ? `numYCoord.Value` - Y coordinate (when resizing from top)
- ? Visual preview size

**On Release**:
- ? Full preview refresh
- ? All settings synchronized
- ? Changes persist

## Examples

### Example 1: Moving Signature to Top-Right

**Before:**
```
X: 400, Y: 75
Position: Bottom-center of page
```

**Action:**
1. Click inside signature box
2. Drag to top-right corner
3. Release

**After:**
```
X: 450, Y: 750
Position: Top-right of page
Signature tab automatically updated
```

### Example 2: Making Signature Wider

**Before:**
```
Width: 150, Height: 50
Size: Standard
```

**Action:**
1. Hover over right edge
2. Cursor changes to ?
3. Drag right to expand
4. Release

**After:**
```
Width: 200, Height: 50
Size: Wider signature
Signature tab automatically updated
```

### Example 3: Resizing from Top-Left Corner

**Before:**
```
X: 400, Y: 75
Width: 150, Height: 50
```

**Action:**
1. Hover over top-left corner
2. Cursor changes to ??
3. Drag up and left
4. Release

**After:**
```
X: 380, Y: 95
Width: 170, Height: 70
Position and size both changed
All values updated in Signature tab
```

## Keyboard Shortcuts

While the feature is primarily mouse-based, you can still use:

- **Arrow keys** in Signature tab numeric controls for fine adjustments
- **Mouse wheel** for zooming in/out
- **Ctrl+Z** (if implemented) for undo

## Tips & Best Practices

### ?? Tip 1: Use Zoom for Precision
```
1. Zoom in to 200-300%
2. Drag/resize with precision
3. Zoom out to see overall placement
```

### ?? Tip 2: Corner Resize for Proportional Changes
```
Use corner handles when you want to:
- Maintain aspect ratio (manually)
- Resize in both dimensions
- Adjust position and size together
```

### ?? Tip 3: Edge Resize for Single Dimension
```
Use edge handles when you want to:
- Make signature wider/narrower only
- Make signature taller/shorter only
- Keep one dimension fixed
```

### ?? Tip 4: Combine with Numeric Controls
```
1. Drag to approximate position
2. Fine-tune with numeric controls in Signature tab
3. Or vice versa - set rough values, then drag to perfect
```

## Technical Details

### Mouse Events
- **MouseDown**: Detects which handle/area was clicked
- **MouseMove**: Updates position/size while dragging
- **MouseUp**: Finalizes changes and refreshes preview
- **Paint**: Draws resize handles during interaction

### Coordinate Conversion
```csharp
// Screen to PDF (for dragging)
float pdfDeltaX = screenDeltaX / zoomLevel;
float pdfDeltaY = -screenDeltaY / zoomLevel; // Invert Y

// PDF to Screen (for display)
float screenX = pdfX * zoomLevel;
float screenY = (pdfHeight - pdfY - height) * zoomLevel;
```

### Handle Detection
- **Hit testing**: 8-pixel tolerance around handles
- **Priority**: Corners > Edges > Interior
- **Smart cursor**: Updates based on hover position

## Limitations & Constraints

### ? What Works
- Dragging within page bounds
- Resizing to minimum dimensions (10px)
- Real-time coordinate updates
- Works with all zoom levels
- Multi-page PDFs (signature stays on selected page)

### ? Constraints
- Cannot drag outside page boundaries
- Minimum width: 10 pixels
- Minimum height: 10 pixels
- Maximum: 10000 pixels (numeric control limit)

### ? Notes
- Dragging is per-page (signature settings apply to page mode - First/Each/Last)
- Changes are immediate (no undo within preview - use numeric controls to revert)
- Resize handles only visible during drag/resize operation

## Troubleshooting

### Issue: Can't Drag Signature
**Check:**
- Is cursor inside the signature box?
- Is left mouse button pressed?
- Is signature box visible on screen?

**Solution:**
- Ensure signature is within visible area
- Try clicking directly in the center of the box
- Check zoom level (too small might make clicking difficult)

### Issue: Resize Handles Not Visible
**Normal Behavior:**
- Handles only appear when actively dragging/resizing
- Handles disappear on mouse release

**To See Handles:**
- Click and hold on signature box or edge
- Handles appear while mouse button is down

### Issue: Signature Jumps When Dragging
**Possible Cause:**
- Very high zoom levels with small mouse movements

**Solution:**
- Reduce zoom level for smoother dragging
- Use numeric controls for fine adjustments

### Issue: Can't Resize to Desired Size
**Possible Cause:**
- Hitting minimum size constraint (10px)
- Reaching coordinate boundaries

**Solution:**
- Move signature to give more room
- Check Signature tab for exact values
- Adjust manually if needed

## Integration with Existing Features

### ? Works With
- **Zoom**: Drag/resize at any zoom level
- **Multi-page**: Changes apply to current page
- **Auto-update**: Preview refreshes on release
- **Signature tab**: All controls sync automatically
- **Save settings**: Dragged position saves to IP.xml

### ? Combines With
- **Numeric controls**: Set exact values
- **Preview refresh**: Manual refresh button
- **Page selector**: Position per page
- **Grid reference**: Visual alignment aid

## Accessibility

### Mouse Required
- Feature is primarily mouse-driven
- Alternative: Use numeric controls in Signature tab
- Keyboard navigation still available for controls

### Visual Indicators
- Clear cursor changes
- Visible handles during operation
- Real-time coordinate updates
- Instruction label at bottom of preview

## Performance

### Optimized For
- ? Smooth dragging (Paint event optimization)
- ? Real-time updates (Invalidate only when needed)
- ? Responsive cursor changes
- ? Efficient coordinate conversion

### Resource Usage
- Minimal CPU during drag/resize
- No additional memory overhead
- Same preview rendering as before
- Handle drawing is lightweight

## Build Status
? **Build Successful**
- No errors
- No warnings (except existing BouncyCastle)
- Ready to test

## Summary

**New Capabilities:**
1. ? **Drag** signature box to reposition (updates X, Y)
2. ? **Resize** from corners/edges (updates Width, Height)
3. ? **Visual feedback** with handles and cursors
4. ? **Real-time sync** with Signature tab controls
5. ? **Smart constraints** prevent invalid values
6. ? **Works at all zoom levels**

**User Experience:**
- Intuitive drag-and-drop interface
- Visual positioning without guessing coordinates
- Immediate feedback
- Professional resize handles
- Smooth, responsive interaction

**Perfect For:**
- Quick signature positioning
- Visual adjustment
- Precise placement verification
- Iterative design workflow

Try it now: Open the Preview tab, hover over the signature box, and start dragging! ??
