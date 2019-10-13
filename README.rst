=========
Kolonnade
=========

What started as a nice way to switch windows became a window manager.

Kolonnade is a tiling window manager for Windows 10. It integrates seamless
into Windows 10's virtual desktops and supports multiple layouts and
per-display workspaces.


How to run
==========

::

   dotnet run /c Release --project .\src\KolonnadeApp\KolonnadeApp.csproj 

A word of warning
-----------------

Kolonnade was created to fulfill my needs. In its current shape, it only
works on my machine because it assumes that there is some external 
component that does the hotkey activation. The reason for that is that
Windows doesn't allow the registration of hotkeys that involve the
Windows key. On my machine, a low-level keyboard hook works around that
limitation.


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


.. _Roll your own window manager: https://web.archive.org/web/20091201114414/https://donsbot.wordpress.com/2007/05/17/roll-your-own-window-manager-tracking-focus-with-a-zipper/
.. _Xmonad: https://xmonad.org/