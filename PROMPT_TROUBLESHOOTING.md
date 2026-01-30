# Console Prompt Not Displaying - Troubleshooting Guide

## ?? Issue
The "Path: " prompt in admin mode is not displaying when running `DigiSign.exe /admin`

---

## ?? Diagnostic Steps

### Step 1: Run the Test Script
```batch
TestAdminPrompt.bat
```

This will help confirm if the issue is specific to your environment.

---

### Step 2: Check Application Log
```batch
# Open the log file
notepad application_log.txt

# Look for this line:
"Waiting for user input on license key path"
```

If this line appears in the log, the code is reaching the prompt section.

---

### Step 3: Test with Minimal Console App
```batch
# Compile the test app
csc ConsoleInputTest.cs

# Run it
ConsoleInputTest.exe

# Check if the prompt "Test Input: " displays and waits
```

If this works but DigiSign doesn't, the issue is specific to DigiSign.

---

## ?? Common Causes & Solutions

### Cause 1: Console Output Redirection
**Symptom:** Running from certain environments (Task Scheduler, services, etc.)

**Test:**
```batch
# Try running from different locations:

# 1. Command Prompt (Run as Administrator)
cd D:\Development\DigiSign
DigiSign.exe /admin

# 2. PowerShell
cd D:\Development\DigiSign
.\DigiSign.exe /admin

# 3. Direct double-click
# (Right-click DigiSign.exe ? Run as administrator)
```

---

### Cause 2: Input Buffer Not Fully Cleared
**Current fix in code:**
```csharp
// Double-clear with delay
while (Console.KeyAvailable) Console.ReadKey(true);
Thread.Sleep(100);
while (Console.KeyAvailable) Console.ReadKey(true);
```

**Test:** Check if delay helps:
- Current delay: 100ms
- Try manually: After seeing last "===", wait 1-2 seconds before typing

---

### Cause 3: Console Mode Issues
**Possible issue:** Console is in wrong mode

**Manual test:**
```batch
# Run and watch carefully:
DigiSign.exe /admin

# Do you see:
# 1. All the "===" lines?
# 2. The instructions (Full path, Press Enter, Type 'exit')?
# 3. But NOT "Path: " ?

# If yes, it's likely a console mode issue
```

---

### Cause 4: Output Buffering
**Test:** Check if output is buffered

**In Command Prompt:**
```batch
# Run with output redirection to check:
DigiSign.exe /admin > output.txt 2>&1

# Then check output.txt:
type output.txt
```

If "Path: " appears in output.txt but not on console, it's a buffering issue.

---

## ??? Solutions to Try

### Solution 1: Increase Delays
Edit Program.cs and change:
```csharp
// From:
Thread.Sleep(100);

// To:
Thread.Sleep(500);  // Half a second
```

### Solution 2: Add More Visible Indicator
Edit Program.cs and add:
```csharp
Console.WriteLine();
Console.WriteLine(">>> Waiting for your input below <<<");
Console.WriteLine();
Console.Write("Path: ");
```

### Solution 3: Use Different Input Method
Instead of `Console.ReadLine()`, try:
```csharp
// Build input character by character
Console.Write("Path: ");
Console.Out.Flush();

StringBuilder input = new StringBuilder();
while (true)
{
    ConsoleKeyInfo key = Console.ReadKey(intercept: false);
    if (key.Key == ConsoleKey.Enter)
    {
        Console.WriteLine();
        break;
    }
    input.Append(key.KeyChar);
}
string userLicenseKeyPath = input.ToString().Trim();
```

---

## ?? Information to Collect

If the issue persists, please provide:

### 1. Environment Details
```batch
# Run these commands and note the output:
ver
echo %PROCESSOR_ARCHITECTURE%
echo %COMSPEC%
```

### 2. Console Test Results
```batch
# Does this work?
echo Test > nul
set /p TEST="Enter something: "
echo You entered: %TEST%
```

### 3. Application Log Snippet
```batch
# Last 20 lines:
powershell -Command "Get-Content application_log.txt -Tail 20"
```

### 4. Exact Behavior
- [ ] The "Path: " text never appears
- [ ] The "Path: " text appears but cursor doesn't wait
- [ ] The "Path: " text appears after you start typing
- [ ] The entire instructions section doesn't appear
- [ ] Everything appears but input is immediately consumed

---

## ?? Quick Test Checklist

Try each of these and note which works:

### Test A: Basic Console
```batch
cmd
cd D:\Development\DigiSign
DigiSign.exe /admin
```
**Works?** ? Yes ? No

### Test B: PowerShell
```powershell
cd D:\Development\DigiSign
.\DigiSign.exe /admin
```
**Works?** ? Yes ? No

### Test C: With Pause Before
```batch
cd D:\Development\DigiSign
echo Press any key when ready...
pause > nul
DigiSign.exe /admin
```
**Works?** ? Yes ? No

### Test D: Run from Different Directory
```batch
cd C:\
D:\Development\DigiSign\DigiSign.exe /admin
```
**Works?** ? Yes ? No

---

## ?? Advanced Diagnostics

### Check Console Handle
Add this to Program.cs temporarily:
```csharp
Console.WriteLine($"Console.IsInputRedirected: {Console.IsInputRedirected}");
Console.WriteLine($"Console.IsOutputRedirected: {Console.IsOutputRedirected}");
Console.WriteLine($"Console.IsErrorRedirected: {Console.IsErrorRedirected}");
```

### Check KeyAvailable State
Add logging:
```csharp
Logger.Debug($"KeyAvailable before clear: {Console.KeyAvailable}");
while (Console.KeyAvailable) Console.ReadKey(true);
Logger.Debug($"KeyAvailable after clear: {Console.KeyAvailable}");
Thread.Sleep(100);
Logger.Debug($"KeyAvailable after sleep: {Console.KeyAvailable}");
```

---

## ?? If Nothing Works

### Alternative Input Method
We can change admin mode to use command-line arguments instead:

```batch
# Instead of interactive prompts:
DigiSign.exe /admin /key "C:\path\to\license.key"

# Or create a config file:
DigiSign.exe /admin /config "admin-config.xml"
```

Would you prefer this approach?

---

## ? Expected Working Behavior

When working correctly, you should see:

```
???????????????????????????????????????????????????????????
?? Admin License Generation Mode
???????????????????????????????????????????????????????????
? Admin license validated

This mode is ONLY for generating user licenses.
No PDF signing will be performed.

???????????????????????????????????????????????????????????

Enter the path to license.key file:
  - Full path (e.g., C:\Users\Admin\Desktop\license.key)
  - Press Enter to use current directory (license.key)
  - Type 'exit' to cancel

Path: _  <-- Cursor blinks here, waiting for input
```

---

**Status:** Diagnostic mode enabled  
**Next Step:** Run TestAdminPrompt.bat and report results
