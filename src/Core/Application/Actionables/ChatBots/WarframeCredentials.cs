using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Application.Actionables.ChatBots
{
    public class WarframeClientInformation
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public ProcessStartInfo StartInfo { get; set; }
        public string Region { get; set; }
    }
}
