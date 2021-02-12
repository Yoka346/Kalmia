using Kalmia.Engines;
using Kalmia.Engines.MCTS;
using Kalmia.ReversiTextProtocol;

namespace Kalmia
{
    class Program
    {
        static void Main(string[] args)
        {
            var engine = new MCTSEngine();
            var rvtp = new RVTP(engine);
            rvtp.MainLoop();
        }
    }
}
