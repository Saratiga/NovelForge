using System;

namespace NovelForge.Runtime
{
    public class ParseException : Exception
    {
        public int LineNumber { get; }

        public ParseException(int lineNumber, string message) : base($"Line {lineNumber}: {message}")
        {
            LineNumber = lineNumber;
        }
    }
}
