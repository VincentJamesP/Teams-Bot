namespace KTI.PAL.Teams.Core.Models.Merlot
{
    public abstract class EmployeeBase
    {
        public int id { get; set; }
        public int changeAction { get; set; }
    }

    public abstract class PairingBase
    {
        public int id { get; set; }
    }
}