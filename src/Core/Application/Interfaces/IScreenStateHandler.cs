using Application.Enums;
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
    }
}
