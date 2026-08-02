Option Explicit

Dim fileSystem, shell, projectRoot, executable
Set fileSystem = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")

projectRoot = fileSystem.GetParentFolderName(WScript.ScriptFullName)
executable = fileSystem.BuildPath(projectRoot, "dist\GameTranslator.exe")

If Not fileSystem.FileExists(executable) Then
    MsgBox "找不到 dist\GameTranslator.exe，请确认项目文件完整。", _
        vbExclamation, "游戏翻译器"
    WScript.Quit 1
End If

shell.CurrentDirectory = fileSystem.GetParentFolderName(executable)
shell.Run Chr(34) & executable & Chr(34), 1, False
