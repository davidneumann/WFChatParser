using System;
using System.Collections.Generic;
using System.Text;

namespace ConfigHelper
{
    internal class Appsettings
    {
        public Credentials Credentials { get; set; } = new Credentials();
        public List<Launcher> Launchers { get; set; } = new List<Launcher>();
    }
    public class Credentials
    {
        public string Key { get; set; }
        public string Salt { get; set; }
    }
    public class Launcher
    {
        public string WarframeCredentialsTarget { get; set; }
        public string LauncherPath { get; set; } = String.Empty;
        public string Username { get; set; }
        public string Password { get; set; }
        public string Region { get; set; }
    }
}
