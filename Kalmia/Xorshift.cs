using System;

namespace Kalmia
{
    class Xorshift
    {
        static readonly Xorshift SEED_GENERATOR = new Xorshift((uint)Environment.TickCount);
        static readonly object LOCK_OBJ = new object();

        uint Y;

        public Xorshift()
        {
            lock (LOCK_OBJ)
                this.Y = SEED_GENERATOR.Next();
        }

        public Xorshift(uint seed)
        {
            this.Y = seed;
        }

        public uint Next()
        {
            this.Y ^= this.Y << 13;
            this.Y ^= this.Y >> 17;
            this.Y ^= this.Y << 15;
            return this.Y;
        }

        public uint Next(uint maxValue)
        {
            return Next() / (uint.MaxValue / maxValue);
        }

        public ulong Next(uint minValue, uint maxValue)
        {
            return Next(maxValue - minValue) + minValue;
        }
    }
}
