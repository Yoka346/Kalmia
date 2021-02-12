using System;

namespace Kalmia
{
    public static class FastMath    // 高速な数学関数を提供するクラス(Leela Chess Zeroのコードを参考)
    {
        public static unsafe float Log2(float a)
        {
            var tmp = *((uint*)&a);
            var expb = tmp >> 23;
            tmp = (tmp & 0x7fffff) | (0x7f << 23);
            var output = *((float*)&tmp);
            output -= 1.0f;
            return output * (1.3465552f - 0.34655523f * output) - 127.0f + expb;
        }

        public static unsafe float Pow2(float a)
        {
            if (a < -126.0f)
                return 0.0f;
            var exp = (int)MathF.Floor(a);
            var output = a - exp;
            output = 1.0f + output * (0.6602339f + 0.33976686f * output);
            var tmp = *((int*)&output);
            tmp += exp << 23;
            output = *((float*)&tmp);
            return output;
        }

        public static unsafe float Log(float a)
        {
            return 0.6931471805599453f * Log2(a);
        }

        public static unsafe float Exp(float a)
        {
            return Pow2(1.442695040f * a);
        }

        public static unsafe float Logit(float a)
        {
            return 0.5f * Log((1.0f + a) / (1.0f - a));
        }
    }
}
