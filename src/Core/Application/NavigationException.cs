using System;
using System.Runtime.Serialization;
using Application.Enums;

namespace Application
{
    [Serializable]
    internal class NavigationException : Exception
    {
        private ScreenState _targetScreen;

        public NavigationException()
        {
        }

        public NavigationException(ScreenState targetScreen)
        {
            this._targetScreen = targetScreen;
        }

        public NavigationException(string message) : base(message)
        {
        }

        public NavigationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected NavigationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public override string ToString()
        {
            return "Failed to navigate to: " + _targetScreen.ToString() + "\n" + base.ToString();
        }
    }
}