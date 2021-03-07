using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var vec = Vector128.Create(2UL, 1UL);
            Console.WriteLine(Avx2.BroadcastScalarToVector256(vec));
        }
    }
}
