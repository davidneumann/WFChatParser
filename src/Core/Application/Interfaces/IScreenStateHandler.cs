using Application.Enums;
using Application.Window;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Application.Interfaces
{
    public interface IScreenStateHandler
    {
        ScreenState GetScreenState(Bitmap bitmap);
        bool IsExitable(Bitmap b);
        bool IsChatCollapsed(Bitmap screen);
        bool GlyphFiltersPresent(Bitmap screen);
        bool IsChatOpen(Bitmap screen);
        bool IsPromptOpen(Bitmap screen);

        /// <summary>
        /// Gives a specified hwnd focused, restoring from minimize if required, and returns if the window has switched from not-active to active.
        /// </summary>
        /// <param name="hwnd"></param>
        /// <returns></returns>
        bool GiveWindowFocus(IntPtr hwnd);
        Rect GetWindowRectangle(IntPtr hwnd);
    }
}
