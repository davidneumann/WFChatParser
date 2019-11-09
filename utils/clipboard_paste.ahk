#NoEnv  ; Recommended for performance and compatibility with future AutoHotkey releases.
; #Warn  ; Enable warnings to assist with detecting common errors.
SendMode Input  ; Recommended for new scripts due to its superior speed and reliability.
SetWorkingDir %A_ScriptDir%  ; Ensures a consistent starting directory.

#^k::
{
    originalClipboard := clipboard
    lines := StrSplit(originalClipboard, "`r`n")
    Send {ctrl down}
    sleep, 66
    Send {ctrl up}
    sleep, 66
    for index, line in lines
    {
        if(StrLen(line) <= 0)
            continue
        clipboard := line
        sleep, 66
        Send {ctrl down}
        sleep, 66
        Send {v}
        ; sleep, 66
        ; Send {v up}
        sleep, 66
        Send {ctrl up}
        sleep, 66
        SendRaw `n
        sleep, 66
    }  
    clipboard := originalClipboard
    return
}