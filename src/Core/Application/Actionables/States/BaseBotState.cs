using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Actionables.States
{
    public enum BaseBotState
    {
        StartWarframe,
        WaitForLoadScreen,
        LogIn,
        ClaimReward,
        CloseWarframe,
        Running
    }
}
