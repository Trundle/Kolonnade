using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace KolonnadeApp
{
    public class MessagePump
    {
        public delegate void ShellEventHandler(Kolonnade.ShellEvent e);

        public event ShellEventHandler ShellEvent;
        
        private static IntPtr HWND_MESSAGE = new IntPtr(-3);
        private const int HSHELL_WINDOWACTIVATED = 4;
        private const int HSHELL_RUDEWAPPACTIVATED = 32772; // HSHELL_WINDOWACTIVATED + high bit
        private const int HSHELL_REDRAW = 6;
        private const int HSHELL_WINDOWREPLACED = 13;

        private readonly int _shellMessageId;
        // Message-only HWND (i.e. not visible)
        private readonly HwndSource _messageHwnd = new HwndSource(0, 0, 0, 0, 0, 0, 0, null, HWND_MESSAGE);

        public MessagePump()
        {
            _shellMessageId = User32.RegisterWindowMessage("SHELLHOOK");
            _messageHwnd.AddHook(OnWindowMessage);
            User32.RegisterShellHookWindow(_messageHwnd.Handle);
        }

        private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == _shellMessageId)
            {
                switch (wParam.ToInt32())
                {
                    case HSHELL_RUDEWAPPACTIVATED:
                    case HSHELL_WINDOWACTIVATED:
                        ShellEvent(Kolonnade.ShellEvent.NewActivated(lParam));
                        break;
                    case HSHELL_WINDOWREPLACED:
                        Console.WriteLine("Window replaced");
                        break;
                    case HSHELL_REDRAW:
                        ShellEvent(Kolonnade.ShellEvent.NewTitleChanged(lParam));
                        break;
                    default:
                        ShellEvent(Kolonnade.ShellEvent.NewUnknown(msg, lParam));
                        break;
                }
            }

            return IntPtr.Zero;
        }
    }

    static class User32
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterShellHookWindow(IntPtr hWnd);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int RegisterWindowMessage(string lpString);
    }
}