using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Models
{
    public class ProfileRequest
    {
        public string Username { get; set; }
        public string Target { get; set; }
        public string Command { get; set; }

        public ProfileRequest(string username, string target, string command)
        {
            Username = username;
            Target = target;
            Command = command;
        }
    }
}
