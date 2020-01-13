SendHotkey(key, modMask)
{
    static HotkeyMsg := DllCall("RegisterWindowMessage", Str, "KolonnadeHotKey")
    SendMessage, % HotkeyMsg, Ord(key), % modMask,, ahk_id 0xFFFF
    SendInput {F13}
    return
}

ShiftMask := 0x4


#1::SendHotkey("1", 0)
#2::SendHotkey("2", 0)
#3::SendHotkey("3", 0)
#4::SendHotkey("4", 0)
#5::SendHotkey("5", 0)
#6::SendHotkey("6", 0)
#7::SendHotkey("7", 0)
#8::SendHotkey("8", 0)
#9::SendHotkey("9", 0)

#+1::SendHotkey("1", ShiftMask)
#+2::SendHotkey("2", ShiftMask)
#+3::SendHotkey("3", ShiftMask)
#+4::SendHotkey("4", ShiftMask)
#+5::SendHotkey("5", ShiftMask)
#+6::SendHotkey("6", ShiftMask)
#+7::SendHotkey("7", ShiftMask)
#+8::SendHotkey("8", ShiftMask)
#+9::SendHotkey("9", ShiftMask)

#f::SendHotkey("f", 0)

; Swap workspaces of first two displays
#s::SendHotkey("s", 0)

; Change focus
#j::SendHotkey("j", 0)
#k::SendHotkey("k", 0)
#m::SendHotkey("m", 0)

; Move window in stack
#+j::SendHotkey("j", ShiftMask)
#+k::SendHotkey("k", ShiftMask)

; Shrink and expand
#l::SendHotkey("l", 0)
#h::SendHotkey("h", 0) 

; Focus display 1, 2, 3
#w::SendHotkey("w", 0)
#e::SendHotkey("e", 0)
#r::SendHotkey("r", 0)

#Enter::SendHotkey("`r", 0)
#+Enter::SendHotkey("`r", ShiftMask)

; Cycle layouts
#Space::SendHotkey(" ", 0)
#+Space::SendHotKey(" ", ShiftMask)
