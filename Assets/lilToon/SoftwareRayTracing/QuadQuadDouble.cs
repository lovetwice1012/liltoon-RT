using System;
using System.Numerics;

namespace lilToon.RayTracing
{
    /// <summary>
    /// Represents a 512-bit floating point number using a BigInteger mantissa and base-2 exponent.
    /// This is a minimal high-precision type intended for experimentation and is not a full IEEE implementation.
    /// </summary>
    [Serializable]
    public struct QuadQuadDouble : IComparable<QuadQuadDouble>, IEquatable<QuadQuadDouble>
    {
        const int Precision = 512;
        BigInteger mantissa;
        int exponent;

        public QuadQuadDouble(BigInteger mantissa, int exponent)
        {
            this.mantissa = mantissa;
            this.exponent = exponent;
            Normalize();
        }

        public static QuadQuadDouble Zero => new QuadQuadDouble(BigInteger.Zero, 0);
        public static QuadQuadDouble One => new QuadQuadDouble(BigInteger.One, 0);

        public static QuadQuadDouble FromDouble(double value)
        {
            if (value == 0.0)
                return Zero;

            long bits = BitConverter.DoubleToInt64Bits(value);
            int sign = (bits >> 63) == 0 ? 1 : -1;
            int exp = (int)((bits >> 52) & 0x7ff);
            long frac = bits & 0xfffffffffffffL;
            if (exp == 0)
            {
                exp = 1 - 1023;
            }
            else
            {
                frac |= 1L << 52;
                exp -= 1023;
            }
            BigInteger m = new BigInteger(frac * sign);
            return new QuadQuadDouble(m, exp - 52);
        }

        public static QuadQuadDouble FromInt(int value)
        {
            return new QuadQuadDouble(new BigInteger(value), 0);
        }

        public double ToDouble()
        {
            if (mantissa.IsZero)
                return 0.0;

            BigInteger m = mantissa;
            int exp = exponent;
            bool negative = m.Sign < 0;
            m = BigInteger.Abs(m);

            int bits = GetBitLength(m);
            if (bits > 53)
            {
                int shift = bits - 53;
                m >>= shift;
                exp += shift;
            }
            else if (bits < 53)
            {
                int shift = 53 - bits;
                m <<= shift;
                exp -= shift;
            }

            long frac = (long)(m & ((1L << 52) - 1));
            long e = exp + 1023;
            if (e <= 0)
                return 0.0; // underflow
            long sign = negative ? (1L << 63) : 0L;
            long bits64 = sign | (e << 52) | frac;
            return BitConverter.Int64BitsToDouble(bits64);
        }

        static int GetBitLength(BigInteger x)
        {
            byte[] bytes = BigInteger.Abs(x).ToByteArray();
            int len = bytes.Length;
            if (len == 0)
                return 0;
            int msb = bytes[len - 1];
            int bits = (len - 1) * 8;
            while (msb != 0)
            {
                msb >>= 1;
                bits++;
            }
            return bits;
        }

        void Normalize()
        {
            if (mantissa.IsZero)
            {
                exponent = 0;
                return;
            }
            int bits = GetBitLength(mantissa);
            int shift = bits - Precision;
            if (shift > 0)
            {
                mantissa >>= shift;
                exponent += shift;
            }
        }

        public static QuadQuadDouble operator +(QuadQuadDouble a, QuadQuadDouble b)
        {
            if (a.mantissa.IsZero) return b;
            if (b.mantissa.IsZero) return a;

            int exp = Math.Max(a.exponent, b.exponent);
            BigInteger ma = a.mantissa << (exp - a.exponent);
            BigInteger mb = b.mantissa << (exp - b.exponent);
            return new QuadQuadDouble(ma + mb, exp);
        }

        public static QuadQuadDouble operator -(QuadQuadDouble a, QuadQuadDouble b)
        {
            return a + new QuadQuadDouble(-b.mantissa, b.exponent);
        }

        public static QuadQuadDouble operator -(QuadQuadDouble value)
        {
            return new QuadQuadDouble(-value.mantissa, value.exponent);
        }

        public static QuadQuadDouble operator *(QuadQuadDouble a, QuadQuadDouble b)
        {
            BigInteger m = a.mantissa * b.mantissa;
            int e = a.exponent + b.exponent;
            return new QuadQuadDouble(m, e);
        }

        public static QuadQuadDouble operator /(QuadQuadDouble a, QuadQuadDouble b)
        {
            if (b.mantissa.IsZero)
                throw new DivideByZeroException();
            BigInteger numerator = a.mantissa << Precision;
            BigInteger m = numerator / b.mantissa;
            int e = a.exponent - b.exponent - Precision;
            return new QuadQuadDouble(m, e);
        }

        public static QuadQuadDouble Abs(QuadQuadDouble value)
        {
            return new QuadQuadDouble(BigInteger.Abs(value.mantissa), value.exponent);
        }

        public static implicit operator QuadQuadDouble(double value) => FromDouble(value);
        public static explicit operator double(QuadQuadDouble value) => value.ToDouble();

        public int CompareTo(QuadQuadDouble other)
        {
            if (mantissa.IsZero && other.mantissa.IsZero) return 0;
            int exp = Math.Max(exponent, other.exponent);
            BigInteger ma = mantissa << (exp - exponent);
            BigInteger mb = other.mantissa << (exp - other.exponent);
            return ma.CompareTo(mb);
        }

        public bool Equals(QuadQuadDouble other) => CompareTo(other) == 0;

        public override bool Equals(object obj) => obj is QuadQuadDouble other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(mantissa, exponent);

        public static bool operator ==(QuadQuadDouble a, QuadQuadDouble b) => a.Equals(b);
        public static bool operator !=(QuadQuadDouble a, QuadQuadDouble b) => !a.Equals(b);
        public static bool operator <(QuadQuadDouble a, QuadQuadDouble b) => a.CompareTo(b) < 0;
        public static bool operator >(QuadQuadDouble a, QuadQuadDouble b) => a.CompareTo(b) > 0;
        public static bool operator <=(QuadQuadDouble a, QuadQuadDouble b) => a.CompareTo(b) <= 0;
        public static bool operator >=(QuadQuadDouble a, QuadQuadDouble b) => a.CompareTo(b) >= 0;

        public override string ToString() => ToDouble().ToString();
    }
}
