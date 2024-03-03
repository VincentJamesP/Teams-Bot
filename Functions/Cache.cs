using System;
using System.Collections.Generic;
using System.Linq;

namespace KTI.PAL.Teams.Functions
{
    /// <summary>
    /// A static class of values intended to be held in memory.
    /// </summary>
    public static class Cache
    {
        public static DateTime pairingStart { get; set; }
        public static DateTime pairingEnd { get; private set; }
        public static List<Core.Models.Merlot.Pairing> pairings { get; private set; }

        public static DateTime flightStart { get; set; }
        public static DateTime flightEnd { get; private set; }
        public static List<Core.Models.Merlot.Flight> flights { get; private set; }


        public static void SetPairings(List<Core.Models.Merlot.Pairing> p)
        {
            pairings = p;
            pairingStart = p.Select(p => p.startDate).Min();
            pairingEnd = p.Select(p => p.endDate).Max();
        }

        public static void SetFlights(List<Core.Models.Merlot.Flight> f)
        {
            flights = f;
            flightStart = f.Select(f => f.scheduledArrival).Min();
            flightEnd = f.Select(f => f.scheduledDeparture).Max();
        }
    }
}
