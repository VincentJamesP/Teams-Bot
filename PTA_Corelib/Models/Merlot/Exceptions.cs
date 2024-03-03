using System;

namespace KTI.PAL.Teams.Core.Models.Merlot
{
    public class MerlotException : Exception
    {
        public MerlotException(string message) : base($"{message}")
        { }
    }

    public class MerlotInvalidInputException : MerlotException
    {
        public MerlotInvalidInputException(string message) : base($"{message}")
        { }
    }

}