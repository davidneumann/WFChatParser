#SingleInstance Force
^+v::   ;;; Start on CTRL+SHIFT+V
; Made by Semlar the great


; Play a startup sound
SoundBeep, 750, 250
SoundBeep, 500, 250
SoundBeep, 1000, 500 

KeyWait, CONTROL
KeyWait, SHIFT
KeyWait, v

; Read and paste 12 "overlap_slice" files
Loop, 12
{
	Send, CLEAR
	Send, {enter}
	Sleep, 33
	i := A_Index - 1
	Loop, read, overlap_slice_%i%.txt
	{
		clipboard = /c %A_LoopReadLine%
		Send, {CONTROL down}
		Sleep, 33
		Send, {v down}
		Sleep, 33
		Send, {v up}
		Sleep, 33
		Send, {CONTROL up}
		Sleep, 33
		Send, {enter}
		Sleep, 1000
	}
	Send, CLEAR
	Send, {enter}
	; Pause for screenshot to be taken
	; Play noise and wait until ctrl key is pressed before continuing
	SoundBeep, 1200, 50
	SoundBeep, 1200, 50
	KeyWait, CONTROL, D
	KeyWait, CONTROL
}

; Read and paste 16 "space_slice" files
Loop, 16
{
	Send, CLEAR
	Send, {enter}
	Sleep, 33
	i := A_Index - 1
	Loop, read, space_slice_%i%.txt
	{
		clipboard = /c %A_LoopReadLine%
		Send, {CONTROL down}
		Sleep, 33
		Send, {v down}
		Sleep, 33
		Send, {v up}
		Sleep, 33
		Send, {CONTROL up}
		Sleep, 33
		Send, {enter}
		Sleep, 1000
	}
	Send, CLEAR
	Send, {enter}
	; Pause for screenshot to be taken
	; Play noise and wait until ctrl key is pressed before continuing
	SoundBeep, 1200, 50
	SoundBeep, 1200, 50
	KeyWait, CONTROL, D
	KeyWait, CONTROL
}


; Play a finish sound
SoundBeep, 1000, 250 
SoundBeep, 500, 250
SoundBeep, 750, 500
return ;===================================
Esc::ExitApp   ; Escape key will exit
^Esc::ExitApp  ; Ctrl+Escape will also exit