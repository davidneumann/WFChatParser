using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Application
{
    public class ChatRivenBot
    {
        public ChatRivenBot()
        {
        }

        public async Task AsyncRun(CancellationToken cancellationToken)
        {
            //Check if WF is running
            ////If not start launcher, click play until WF starts
            //Check if on login screen
            ////If so paste in password and click login
            //Check if on daily reward screen
            ////IF so cilck what ever the middle most item is
            //start an infinite loop
            ////Check if is in Warframe controller mode / not in UI interaction mode
            //////If so open menu 
            //////      -> profile 
            //////      -> glyphs 
            //////      -> Check if chat icon is in default location or already moved location
            ////////         If already moved open chat
            ////////         If in deafult location open chat and move it
            ////////         If somewhere else, crash
            //////      -> check if chat is in the default location and if so move it 
            ////Tell chat parser to parse and send the next page of results
        }
    }
}
