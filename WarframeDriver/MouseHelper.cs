using Application.Interfaces;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

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
            System.Threading.Thread.Sleep(60);
            mouse_event(MOUSEEVENTF_LEFTUP, x, y, 0, (IntPtr)0);
        }

        public void ClickAndDrag(Point startPoint, Point finalPoint, int timeInMS)
        {
            int lerp(int a, int b, float t) => a + (int)((b - a) * t);
            SetCursorPos(startPoint.X, startPoint.Y);
            Thread.Sleep(17);
            mouse_event(MOUSEEVENTF_LEFTDOWN, startPoint.X, startPoint.Y, 0, (IntPtr)0);
            Thread.Sleep(17);
            for (int x = 0; x < timeInMS; x+=17)
            {
                var t = (float)(x) / timeInMS;
                SetCursorPos(lerp(startPoint.X, finalPoint.X, t), lerp(startPoint.Y, finalPoint.Y, t));
                Thread.Sleep(17);
            }
            mouse_event(MOUSEEVENTF_LEFTUP, finalPoint.X, finalPoint.Y, 0, (IntPtr)0);
            Thread.Sleep(17);
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
