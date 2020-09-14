using Kalmia.Engines;
using Kalmia.ReversiTextProtocol;
using System;

namespace Kalmia
{
    class Program
    {
        static void Main(string[] args)
        {
            var mc = new MonteCarloEngine(1000, 8);
            var rvtp = new RVTP(mc);
            rvtp.MainLoop();
        }
    }
}
