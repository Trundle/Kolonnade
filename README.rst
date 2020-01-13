=========
Kolonnade
=========

What started as a nice way to switch windows became a window manager.

Kolonnade is a tiling window manager for Windows 10. It integrates seamless
into Windows 10's virtual desktops and supports multiple layouts and
per-display workspaces.


Requirements
============

* `AutoHotKey`_
* .NET Core 3.1 SDK (v3.1.100)
* Windows 10 Build 16299 or newer (Fall Creators Update)


How to run
==========

Kolonnade consists of two different components: a hotkey handler and a
window manager. The hotkey handler is an `AutoHotKey`_ script. It can be
run by double-clicking on ``Kolonnade.ahk``.

The window manager itself can be run with::

   dotnet run /c Release --project .\src\KolonnadeApp\KolonnadeApp.csproj 


Why the split into two components?
----------------------------------

Kolonnade's hotkey handler works around two challenges:

* It's not possible to register a hotkey that involves the Windows key
* Applications are not allowed to raise arbitrary windows to the foreground via `SetForegroundWindow`_

The first challenge can be solved by using a `low-level keyboard hook <https://docs.microsoft.com/en-us/previous-versions/windows/desktop/legacy/ms644985(v%3Dvs.85)>`_
that gets called on all keypresses. The second challenge can be worked around
by abusing the fact that the foreground is not locked for some time after a
hotkey has been activated. Once the low-level keyboard hook detecs a 
Kolonnade hotkey, a virtual keypress of the F13 key is injected into Window's
input system. Kolonnade has a regular hotkey registration (via
`RegisterHotKey`_) on the F13 key, gets activated and therefore is
allowed to bring arbitrary windows to the foreground.

While it's possible to do all of that in .NET and only one process, AutoHotKey
already exists and does the job.


Prior Art
=========

Kolonnade was obviously heavily influenced by XMonad_. Kolonnade's core
data structures are more or less a direct port from XMonad's Haskell
source to F#. See `Roll your own window manager`_ for an introduction.


Why another window manager instead of using one of the many existing ones?
--------------------------------------------------------------------------

It was the perfect opportunity to learn a bit of C# and F# and to learn
even more about a few Windows internals.


License
=======

Kolonnade is released under the Apache License, Version 2.0. See ``LICENSE``
or http://www.apache.org/licenses/LICENSE-2.0.html for details.

Part of Kolonnade is influenced by XMonad_, which is released under a
BSD license.


.. _AutoHotKey: https://www.autohotkey.com/
.. _Roll your own window manager: https://web.archive.org/web/20091201114414/https://donsbot.wordpress.com/2007/05/17/roll-your-own-window-manager-tracking-focus-with-a-zipper/
.. _RegisterHotKey: https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey
.. _SetForegroundWindow: https://docs.microsoft.com/de-de/windows/win32/api/winuser/nf-winuser-setforegroundwindow
.. _Xmonad: https://xmonad.org/