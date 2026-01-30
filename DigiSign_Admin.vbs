' DigiSign Admin Mode Launcher (VBScript)
' This bypasses all stdin redirection issues by launching a fresh cmd.exe process

Set objShell = CreateObject("WScript.Shell")
Set objFSO = CreateObject("Scripting.FileSystemObject")

' Get the directory where this script is located
scriptDir = objFSO.GetParentFolderName(WScript.ScriptFullName)

' Change to the application directory
objShell.CurrentDirectory = scriptDir

' Launch a NEW command prompt window with DigiSign in admin mode
' The 1 parameter makes the window visible
' The True parameter waits for the command to complete
objShell.Run "cmd.exe /k ""cd /d """ & scriptDir & """ && DigiSign.exe /admin && pause && exit""", 1, True
