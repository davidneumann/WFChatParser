using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Logger
{
    public interface ILogger
    {
        void Log(string message, bool writeToConsole = true, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "", bool sendToSocket = true);
    }
}
