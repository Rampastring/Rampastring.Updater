using System;

namespace SecondStageUpdater
{
    public class LogEventArgs : EventArgs
    {
        public LogEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; private set; }
    }
}
