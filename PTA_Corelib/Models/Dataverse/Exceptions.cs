using System;

namespace KTI.PAL.Teams.Core.Models.Dataverse
{
    public class DataverseException : Exception
    {
        public DataverseException(string message) : base($"{message}")
        { }
    }
}
