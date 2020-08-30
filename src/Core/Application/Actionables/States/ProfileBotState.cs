using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Actionables.States
{
    public enum ProfileBotState
    {
        WaitingForBaseBot,
        OpenProfile,
        WaitingForProfile,
        ParsingProfile
    }
}
