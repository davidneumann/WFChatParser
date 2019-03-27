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
    }
}
