using Application.interfaces;
using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace WarframeDriver
{
    public class MouseHelper: IMouseMover
    {
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, IntPtr dwExtraInfo);
        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const int MOUSEEVENTF_RIGHTUP = 0x10;
        private const int MOUSEEVENTF_WHEEL = 0x0800;

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        public void ScrollUp()
        {
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, 120, (IntPtr)0);
        }
        public void ScrollDown()
        {
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, -120, (IntPtr)0);
        }

        public void MoveTo(int x, int y)
        {
            SetCursorPos(x, y);
        }
        public void Click(int x, int y)
        {
            POINT p;
            GetCursorPos(out p);
            if (p.X != x || p.Y != y)
            {
                MoveTo((int)x, (int)y);
                System.Threading.Thread.Sleep(33);
            }
            mouse_event(MOUSEEVENTF_LEFTDOWN, x, y, 0, (IntPtr)0);
            System.Threading.Thread.Sleep(33);
            mouse_event(MOUSEEVENTF_LEFTUP, x, y, 0, (IntPtr)0);
        }
    }

    /// <summary>
    /// Struct representing a point.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;

        public static implicit operator Point(POINT point)
        {
            return new Point(point.X, point.Y);
        }
    }
}
