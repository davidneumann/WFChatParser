﻿using Application.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    public interface IChatParserFactory
    {
        IChatParser CreateChatParser(ClientLanguage clientLanguage);
    }
}
