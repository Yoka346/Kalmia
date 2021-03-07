using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Kalmia
{
    public struct UInt64_4     // Vector256<ulong>を内包する構造体. ボードの計算に使う. AVX2に対応したCPUでなければ動かない.
    {
        static readonly Vector256<byte> FLIP_VERTICAL_SHUFFLE_TABLE_256;

        static UInt64_4()
        {
            FLIP_VERTICAL_SHUFFLE_TABLE_256 = Vector256.AsByte(Vector256.Create(24, 25, 26, 27, 28, 29, 30, 31,
                                                                                16, 17, 18, 19, 20, 21, 22, 23,
                                                                                 8, 9, 10, 11, 12, 13, 14, 15,
                                                                                 0, 1, 2, 3, 4, 5, 6, 7));
        }

        public Vector256<ulong> Data;

        public UInt64_4(ulong value)
        {
            this.Data = Vector256.Create(value);
        }

        public UInt64_4(ulong x, ulong y, ulong z, ulong w)
        {
            this.Data = Vector256.Create(x, y, z, w);
        }

        public static UInt64_4 operator >>(UInt64_4 a, int count)
        {
            UInt64_4 ret;
            ret.Data = Avx2.ShiftRightLogical(a.Data, (byte)count);
            return ret;
        }

        public static UInt64_4 operator <<(UInt64_4 a, int count)
        {
            UInt64_4 ret;
            ret.Data = Avx2.ShiftLeftLogical(a.Data, (byte)count);
            return ret;
        }

        public static UInt64_4 operator &(UInt64_4 left, UInt64_4 right)
        {
            UInt64_4 ret;
            ret.Data = Avx2.And(left.Data, right.Data);
            return ret;
        }

        public static UInt64_4 operator |(UInt64_4 left, UInt64_4 right)
        {
            UInt64_4 ret;
            ret.Data = Avx2.Or(left.Data, right.Data);
            return ret;
        }

        public static UInt64_4 operator +(UInt64_4 left, UInt64_4 right)
        {
            UInt64_4 ret;
            ret.Data = Avx2.Add(left.Data, right.Data);
            return ret;
        }

        public static UInt64_4 operator +(UInt64_4 left, ulong right)
        {
            UInt64_4 ret;
            var rightVec = Vector256.Create(right);
            ret.Data = Avx2.Add(left.Data, rightVec);
            return ret;
        }

        public static UInt64_4 operator -(UInt64_4 left, UInt64_4 right)
        {
            UInt64_4 ret;
            ret.Data = Avx2.Subtract(left.Data, right.Data);
            return ret;
        }

        public static UInt64_4 operator -(UInt64_4 a)
        {
            UInt64_4 ret;
            ret.Data = Avx2.Subtract(Vector256.Create(0UL), a.Data);
            return ret;
        }

        public static UInt64_4 AndNot(UInt64_4 left, UInt64_4 right)        // ~left & right の演算を行う. NANDではない.
        {
            UInt64_4 ret;
            ret.Data = Avx2.AndNot(left.Data, right.Data);
            return ret;
        }

        public static UInt64_4 operator ~(UInt64_4 a)
        {
            UInt64_4 ret;
            ret.Data = Avx2.AndNot(a.Data, Vector256.Create(0xffffffffffffffff));
            return ret;
        }

        public static UInt64_4 NotZero(UInt64_4 a)
        {
            UInt64_4 ret;
            ret.Data = Avx2.Add(Avx2.CompareEqual(a.Data, Vector256.Create(0UL)), Vector256.Create(1UL));
            return ret;
        }

        public static Vector128<ulong> Hor(UInt64_4 a)      // Vector256<ulong>の全要素の論理和をとる
        {
            var xOrZ_yOrW = Sse2.Or(a.Data.GetLower(), a.Data.GetUpper());
            return Sse2.Or(xOrZ_yOrW, Ssse3.AlignRight(xOrZ_yOrW, xOrZ_yOrW, 8));
        }

        public static UInt64_4 UpperBit(UInt64_4 a)     // 最も左側で立っているビットだけを残す. 例えば、a = 14(0b00001110)のとき、戻り値は8(0b00001000).
        {
            a |= a >> 1;
            a |= a >> 2;
            a |= a >> 4;
            a = AndNot(a >> 1, a);
            a = FlipVertical(a);
            a &= -a;
            return FlipVertical(a);
        }

        static UInt64_4 FlipVertical(UInt64_4 a)
        {
            UInt64_4 ret;
            ret.Data = Vector256.AsUInt64(Avx2.Shuffle(Vector256.AsByte(a.Data), FLIP_VERTICAL_SHUFFLE_TABLE_256));
            return ret;
        }
    }

    public struct BitBoard
    {
        const int BOARD_SIZE = 8;
        static readonly Vector128<byte>[] ROTATE_RIGHT8_SHUFFLE_TABLE = new Vector128<byte>[8];
        static readonly Vector256<byte>[] ROTATE_RIGHT8_SHUFFLE_TABLE_256 = new Vector256<byte>[8];

        static BitBoard()
        {
            for (var i = 0; i < 8; i++)
            {
                ROTATE_RIGHT8_SHUFFLE_TABLE[i] = Vector128.Create(
                (byte)((7 + i) % 8 + 8),
                (byte)((6 + i) % 8 + 8),
                (byte)((5 + i) % 8 + 8),
                (byte)((4 + i) % 8 + 8),
                (byte)((3 + i) % 8 + 8),
                (byte)((2 + i) % 8 + 8),
                (byte)((1 + i) % 8 + 8),
                (byte)((0 + i) % 8 + 8),
                (byte)((7 + i) % 8),
                (byte)((6 + i) % 8),
                (byte)((5 + i) % 8),
                (byte)((4 + i) % 8),
                (byte)((3 + i) % 8),
                (byte)((2 + i) % 8),
                (byte)((1 + i) % 8),
                (byte)((0 + i) % 8));

                ROTATE_RIGHT8_SHUFFLE_TABLE_256[i] = Vector256.Create(
                    (byte)((7 + i) % 8 + 24),
                    (byte)((6 + i) % 8 + 24),
                    (byte)((5 + i) % 8 + 24),
                    (byte)((4 + i) % 8 + 24),
                    (byte)((3 + i) % 8 + 24),
                    (byte)((2 + i) % 8 + 24),
                    (byte)((1 + i) % 8 + 24),
                    (byte)((0 + i) % 8 + 24),
                    (byte)((7 + i) % 8 + 16),
                    (byte)((6 + i) % 8 + 16),
                    (byte)((5 + i) % 8 + 16),
                    (byte)((4 + i) % 8 + 16),
                    (byte)((3 + i) % 8 + 16),
                    (byte)((2 + i) % 8 + 16),
                    (byte)((1 + i) % 8 + 16),
                    (byte)((0 + i) % 8 + 16),
                    (byte)((7 + i) % 8 + 8),
                    (byte)((6 + i) % 8 + 8),
                    (byte)((5 + i) % 8 + 8),
                    (byte)((4 + i) % 8 + 8),
                    (byte)((3 + i) % 8 + 8),
                    (byte)((2 + i) % 8 + 8),
                    (byte)((1 + i) % 8 + 8),
                    (byte)((0 + i) % 8 + 8),
                    (byte)((7 + i) % 8),
                    (byte)((6 + i) % 8),
                    (byte)((5 + i) % 8),
                    (byte)((4 + i) % 8),
                    (byte)((3 + i) % 8),
                    (byte)((2 + i) % 8),
                    (byte)((1 + i) % 8),
                    (byte)((0 + i) % 8));
            }
        }

        public Vector128<ulong> Data;

        public BitBoard(Vector128<ulong> data)
        {
            this.Data = data;
        }

        public static explicit operator Vector128<ulong>(BitBoard bitBoard)
        {
            return bitBoard.Data;
        }

        public static explicit operator BitBoard(Vector128<ulong> data)
        {
            return new BitBoard(data);
        }

        public BitBoard Update(ref BitBoard bitBoard, Move move)
        {
            if (move.Position == Move.PASS)
                return GetSwappedBoard(ref bitBoard);
            var flipPat = GetFlipPat(ref bitBoard, move.Position);
            return (BitBoard)Sse2.Or(Sse2.Xor(GetSwappedBoard(ref bitBoard).Data, flipPat), Vector128.Create(1UL << move.Position, 0));
        }

        public ulong GetLegalMovesPat(ref BitBoard bitBoard)
        {
            return (GetLegalMovesPatHorizontal(ref bitBoard) | GetLegalMovesPatDiagonal(ref bitBoard) & ~GetDiscsPat(ref bitBoard));
        }

        public BitBoard Update(ref BitBoard bitBoard, int posX, int posY)
        {
            if (posX == -1 && posY == -1)
                return GetSwappedBoard(ref bitBoard);
            return Update(ref bitBoard, new Move(posX + posY * BOARD_SIZE));
        }

        static BitBoard GetSwappedBoard(ref BitBoard bitBoard)
        {
            return new BitBoard(Ssse3.AlignRight(bitBoard.Data, bitBoard.Data, 8));
        }

        static UInt64_4 BroadcastCurrentPlayer(ref BitBoard bitBoard)
        {
            UInt64_4 ret;
            ret.Data = Avx2.BroadcastScalarToVector256(bitBoard.Data);
            return ret;
        }

        static UInt64_4 BroadcastOpponentPlayer(ref BitBoard bitBoard)
        {
            UInt64_4 ret;
            ret.Data = Avx2.Permute4x64(Vector256.Create(bitBoard.Data, bitBoard.Data), 0x55);
            return ret;
        }

        static ulong GetDiscsPat(ref BitBoard bitBoard)
        {
            return Sse2.X64.ConvertToUInt64(Sse2.Or(Ssse3.AlignRight(bitBoard.Data, bitBoard.Data, 8), bitBoard.Data));
        }

        static Vector128<ulong> GetFlipPat(ref BitBoard bitBoard, int pos)
        {
            var current = BroadcastCurrentPlayer(ref bitBoard);
            var opponent = BroadcastOpponentPlayer(ref bitBoard);
            var yzw = new UInt64_4(0xFFFFFFFFFFFFFFFF, 0x7E7E7E7E7E7E7E7E, 0x7E7E7E7E7E7E7E7E, 0x7E7E7E7E7E7E7E7E);
            var om = opponent & yzw;
            var mask = new UInt64_4(0x0080808080808080, 0x7F00000000000000, 0x0102040810204000, 0x0040201008040201);
            mask >>= 63 - pos;
            var outflank = UInt64_4.UpperBit(UInt64_4.AndNot(om, mask)) & current;
            var flipped = (-outflank << 1) & mask;
            mask.Data = Vector256.Create(0x0101010101010100, 0x00000000000000FE, 0x0002040810204080, 0x8040201008040200);
            mask <<= pos;
            outflank = mask & ((om | ~mask) + 1) & current;
            flipped |= (outflank - UInt64_4.NotZero(outflank)) & mask;
            return UInt64_4.Hor(flipped);
        }

        static Vector128<ulong> GetLegalMovesPat128(ref BitBoard bitBoard)
        {
            var doubleBitBoard = new DoubleBitBoard(bitBoard);
            var tmp = Sse2.Or(Sse2.Or(GetLegalMovesPatHorizontal(ref doubleBitBoard), GetLegalMovesPatVertical(ref doubleBitBoard)), GetLegalMovesPatDiagonal(ref doubleBitBoard));
            return Sse2.AndNot(GetDiscsPat(ref doubleBitBoard), tmp);
        }

        static ulong GetLegalMovesPatHorizontal(ref BitBoard bitBoard)
        {

        }

        static Vector128<ulong> GetLegalMovesPatHorizontal(ref DoubleBitBoard doubleBitBoard)
        {
            var tmp0 = GetMirrorHorizontalBoard(ref doubleBitBoard);
            var tmp1 = GetLegalPatBackwardP4(ref doubleBitBoard, ref tmp0);
            tmp1.Data = Avx2.Permute4x64(tmp1.Data, 0xd8);
            var tmp2 = tmp1.Board_1;
            return Sse2.Or(doubleBitBoard.Board_0.Data, GetMirrorHorizontalBoard(ref tmp2).Data);
        }

        static Vector128<ulong> GetLegalMovesPatVertical(ref DoubleBitBoard doubleBitBoard)
        {
            var tmp0 = FlipDiagonalA1H8(ref doubleBitBoard);
            var tmp1 = (BitBoard)GetLegalMovesPatHorizontal(ref tmp0);
            return FlipDiagonalA1H8(ref tmp1).Data;
        }

        static ulong GetLegalMovesPatDiagonal(ref BitBoard bitBoard)
        {

        }

        static Vector128<ulong> GetLegalMovesPatDiagonal(ref DoubleBitBoard doubleBitBoard)
        {
            return RotateRight8(
                Sse2.Or(GetLegalMovesDiagonalA8H1(ref doubleBitBoard),
                GetLegalMovesDiagonalA8H1(ref doubleBitBoard)), 1);
        }

        static Vector128<ulong> GetLegalMovesDiagonalA8H1(ref DoubleBitBoard doubleBitBoard)
        {
            var prot45DoubleBitBoard = PseudoRotate45Clockwise(ref doubleBitBoard);
            var mask64 = 0x80C0E0F0F8FCFEFFUL;
            var mask128 = Vector128.Create(mask64);
            var mask256 = Vector256.Create(mask64);
            var tmp0 = (DoubleBitBoard)Avx2.And(mask256, prot45DoubleBitBoard.Data);
            var tmp1 = (DoubleBitBoard)Avx2.AndNot(mask256, prot45DoubleBitBoard.Data);
            var res = (BitBoard)Sse2.Or(Sse2.And(mask128, GetLegalMovesPatHorizontal(ref tmp0)),
                Sse2.AndNot(mask128, GetLegalMovesPatHorizontal(ref tmp1)));
            return PseudoRotate45AntiClockwise(ref res).Data;
        }

        static BitBoard GetLegalPatBackwardP4(BitBoard bitBoard0, BitBoard bitBoard1)
        {

        }

        static DoubleBitBoard GetLegalPatBackwardP4(ref DoubleBitBoard doubleBitBoard_0, ref DoubleBitBoard doubleBitBoard_1)
        {
            var b_0 = Avx2.UnpackLow(doubleBitBoard_0.Data, doubleBitBoard_1.Data);
            var b_1 = Avx2.Add(b_0, b_0);
            var w = Avx2.UnpackHigh(doubleBitBoard_0.Data, doubleBitBoard_1.Data);
            return (DoubleBitBoard)Avx2.AndNot(Avx2.Or(b_1, w), Avx2.Add(b_1, w));
        }

        static BitBoard GetMirrorHorizontalBoard(ref BitBoard bitBoard)
        {

        }

        static DoubleBitBoard GetMirrorHorizontalBoard(ref DoubleBitBoard doubleBitBoard)
        {
            var mask_0 = Vector256.Create((byte)0x55).AsUInt64();
            var mask_1 = Vector256.Create((byte)0x33).AsUInt64();
            var mask_2 = Vector256.Create((byte)0x0f).AsUInt64();
            doubleBitBoard.Data = Avx2.Or(Avx2.And(Avx2.ShiftRightLogical(doubleBitBoard.Data, 1), mask_0), Avx2.ShiftLeftLogical(Avx2.And(doubleBitBoard.Data, mask_0), 1));
            doubleBitBoard.Data = Avx2.Or(Avx2.And(Avx2.ShiftRightLogical(doubleBitBoard.Data, 2), mask_1), Avx2.ShiftLeftLogical(Avx2.And(doubleBitBoard.Data, mask_1), 2));
            doubleBitBoard.Data = Avx2.Or(Avx2.And(Avx2.ShiftRightLogical(doubleBitBoard.Data, 4), mask_2), Avx2.ShiftLeftLogical(Avx2.And(doubleBitBoard.Data, mask_2), 4));
            return doubleBitBoard;
        }

        static ulong FlipDiagonalA1H8(ulong bits)
        {

        }

        static BitBoard FlipDiagonalA1H8(ref BitBoard bitBoard)
        {
            var mask_0 = Vector128.Create(0x5500).AsUInt64();
            var mask_1 = Vector128.Create(0x33330000).AsUInt64();
            var mask_2 = Vector128.Create(0x0f0f0f0f00000000UL);
            var data = DeltaSwap(bitBoard.Data, mask_2, 28);
            data = DeltaSwap(data, mask_1, 14);
            return (BitBoard)DeltaSwap(data, mask_0, 7);
        }

        static DoubleBitBoard FlipDiagonalA1H8(ref DoubleBitBoard doubleBitBoard)
        {
            var mask_0 = Vector256.Create((ushort)0xaa00).AsUInt64();
            var mask_1 = Vector256.Create(0xcccc0000U).AsUInt64();
            var mask_2 = Vector256.Create(0xf0f0f0f000000000UL);
            var data = DeltaSwap(doubleBitBoard.Data, mask_2, 36);
            data = DeltaSwap(data, mask_1, 18);
            return (DoubleBitBoard)DeltaSwap(data, mask_0, 9);
        }

        static BitBoard GetMirrorHorizontalBoard(ref BitBoard bitBoard)
        {
            var mask_0 = Vector128.Create(0x55).AsUInt64();
            var mask_1 = Vector128.Create(0x33).AsUInt64();
            var mask_2 = Vector128.Create(0x0f).AsUInt64();
            bitBoard.Data = Sse2.Or(Sse2.And(Sse2.ShiftRightLogical(bitBoard.Data, 1), mask_0), Sse2.ShiftLeftLogical(Sse2.And(bitBoard.Data, mask_0), 1));
            bitBoard.Data = Sse2.Or(Sse2.And(Sse2.ShiftRightLogical(bitBoard.Data, 2), mask_1), Sse2.ShiftLeftLogical(Sse2.And(bitBoard.Data, mask_1), 2));
            bitBoard.Data = Sse2.Or(Sse2.And(Sse2.ShiftRightLogical(bitBoard.Data, 4), mask_2), Sse2.ShiftLeftLogical(Sse2.And(bitBoard.Data, mask_2), 4));
            return bitBoard;
        }

        static BitBoard PseudoRotate45Clockwise(ref BitBoard bitBoard)
        {
            var mask_0 = Vector128.Create(0x55).AsUInt64();
            var mask_1 = Vector128.Create(0x33).AsUInt64();
            var mask_2 = Vector128.Create(0x0f).AsUInt64();
            var data = Sse2.Xor(bitBoard.Data, Sse2.And(mask_0, Sse2.Xor(bitBoard.Data, RotateRight8(bitBoard.Data, 1))));
            data = Sse2.Xor(data, Sse2.And(mask_1, Sse2.Xor(data, RotateRight8(data, 2))));
            return (BitBoard)Sse2.Xor(data, Sse2.And(mask_2, Sse2.Xor(data, RotateRight8(data, 4))));
        }

        static DoubleBitBoard PseudoRotate45Clockwise(ref DoubleBitBoard doubleBitBoard)
        {
            var mask0 = Vector256.Create(0x55).AsUInt64();
            var mask1 = Vector256.Create(0x33).AsUInt64();
            var mask2 = Vector256.Create(0x0f).AsUInt64();
            var data = Avx2.Xor(doubleBitBoard.Data, Avx2.And(mask0, Avx2.Xor(doubleBitBoard.Data, RotateRight8(doubleBitBoard.Data, 1))));
            data = Avx2.Xor(data, Avx2.And(mask1, Avx2.Xor(data, RotateRight8(data, 2))));
            return (DoubleBitBoard)Avx2.Xor(data, Avx2.And(mask2, Avx2.Xor(data, RotateRight8(data, 4))));
        }

        static BitBoard PseudoRotate45AntiClockwise(ref BitBoard bitBoard)
        {
            var mask_0 = Vector128.Create(0xaa).AsUInt64();
            var mask_1 = Vector128.Create(0xcc).AsUInt64();
            var mask_2 = Vector128.Create(0xf0).AsUInt64();
            var data = Sse2.Xor(bitBoard.Data, Sse2.And(mask_0, Sse2.Xor(bitBoard.Data, RotateRight8(bitBoard.Data, 1))));
            data = Sse2.Xor(data, Sse2.And(mask_1, Sse2.Xor(data, RotateRight8(data, 2))));
            return (BitBoard)Sse2.Xor(data, Sse2.And(mask_2, Sse2.Xor(data, RotateRight8(data, 4))));
        }

        static DoubleBitBoard PseudoRotate45AntiClockwise(ref DoubleBitBoard doubleBitBoard)
        {
            var mask0 = Vector256.Create(0xaa).AsUInt64();
            var mask1 = Vector256.Create(0xcc).AsUInt64();
            var mask2 = Vector256.Create(0xf0).AsUInt64();
            var data = Avx2.Xor(doubleBitBoard.Data, Avx2.And(mask0, Avx2.Xor(doubleBitBoard.Data, RotateRight8(doubleBitBoard.Data, 1))));
            data = Avx2.Xor(data, Avx2.And(mask1, Avx2.Xor(data, RotateRight8(data, 2))));
            return (DoubleBitBoard)Avx2.Xor(data, Avx2.And(mask2, Avx2.Xor(data, RotateRight8(data, 4))));
        }

        static Vector128<ulong> GetDiscsPat(ref DoubleBitBoard doubleBitBoard)
        {
            var tmp = Avx2.Or(Avx2.AlignRight(doubleBitBoard.Data, doubleBitBoard.Data, 8), doubleBitBoard.Data);
            return Avx2.Permute4x64(tmp, 0x08).GetLower();
        }

        static Vector128<ulong> DeltaSwap(Vector128<ulong> bits, Vector128<ulong> mask, int delta)
        {
            var tmp = Sse2.And(mask, Sse2.Xor(bits, Sse2.ShiftLeftLogical(bits, (byte)delta)));
            return Sse2.Xor(Sse2.Xor(bits, tmp), Sse2.ShiftRightLogical(tmp, (byte)delta));
        }

        static Vector256<ulong> DeltaSwap(Vector256<ulong> bits, Vector256<ulong> mask, int delta)
        {
            var tmp = Avx2.And(mask, Avx2.Xor(bits, Avx2.ShiftLeftLogical(bits, (byte)delta)));
            return Avx2.Xor(Avx2.Xor(bits, tmp), Avx2.ShiftRightLogical(tmp, (byte)delta));
        }

        static Vector128<ulong> RotateRight8(Vector128<ulong> bits, int idx)
        {
            return Ssse3.Shuffle(bits.AsByte(), ROTATE_RIGHT8_SHUFFLE_TABLE[idx]).AsUInt64();
        }

        static Vector256<ulong> RotateRight8(Vector256<ulong> bits, int idx)
        {
            return Avx2.Shuffle(bits.AsByte(), ROTATE_RIGHT8_SHUFFLE_TABLE_256[idx]).AsUInt64();
        }

        static ulong RotateRight(ulong a, int shift)    // _rotr64 のソフトウェア実装
        {
            var ret = a;
            var count = shift & 63;
            for (var i = 0; i < count; i++)
                ret = (ret >> 1) | ((ret & 1UL) << 63);
            return ret;
        }

        static int MovePatToPos(ulong movePat)
        {
            var pos = 1;
            var mask = 1UL;
            while ((movePat & mask) == 0)
                pos++;
            return pos;
        }
    }

    public struct DoubleBitBoard    // BitBoardを2つ並べたもの. 合法手生成の途中計算で用いる.
    {
        public Vector256<ulong> Data;
        public BitBoard Board_0{ get{ return new BitBoard(this.Data.GetLower()); } }
        public BitBoard Board_1 { get { return new BitBoard(this.Data.GetUpper()); } }

        public ulong this[int i]
        {
            get
            {
                return this.Data.GetElement(i);
            }
        }

        public DoubleBitBoard(BitBoard bitBoard) : this(bitBoard, bitBoard) { }

        public DoubleBitBoard(BitBoard bitBoard_0, BitBoard bitBoard_1) : this(bitBoard_0.Data, bitBoard_1.Data) { }

        public DoubleBitBoard(ulong currentPlayerBoard_0, ulong opponentPlayerBoard_0, ulong currentPlayerBoard_1, ulong opponentPlayerBoard_1)
        {
            this.Data = Vector256.Create(currentPlayerBoard_0, opponentPlayerBoard_0, currentPlayerBoard_1, opponentPlayerBoard_1);
        }

        public DoubleBitBoard(Vector256<ulong> data)
        {
            this.Data = data;
        }

        public DoubleBitBoard(Vector128<ulong> data)
        {
            this.Data = Vector256.Create(data, data);
        }

        public DoubleBitBoard(Vector128<ulong> data_0, Vector128<ulong> data_1)
        {
            this.Data = Vector256.Create(data_0, data_1);
        }

        public static explicit operator Vector256<ulong> (DoubleBitBoard doubleBitBoard)
        {
            return doubleBitBoard.Data;
        }

        public static explicit operator DoubleBitBoard(Vector256<ulong> data)
        {
            return new DoubleBitBoard(data);
        }
    }

    public class FastBoard : ICloneable      // 探索に用いる盤面. Boardクラスに比べて機能が省かれている.
    {
        public const int BOARD_SIZE = 8;
        public const int GRID_NUM = BOARD_SIZE * BOARD_SIZE;
        public const int MAX_MOVE_NUM = 46;     // 1つの局面に現れる合法手数は多くても46.

        BitBoard bitboard;
        public Color Turn { get; }

        bool isLegalPatSolved = false;
        ulong legalPat;     // 合法手のビットボード

        int passCount = 0;

        public FastBoard() : this(0UL, 0UL, Color.Black) { }

        public FastBoard(ulong blackBoard, ulong whiteBoard, Color turn)
        {
            this.Turn = turn;
            if (this.Turn == Color.Black)
                this.bitboard = new BitBoard(Vector128.Create(blackBoard, whiteBoard));
            else
                this.bitboard = new BitBoard(Vector128.Create(whiteBoard, blackBoard));
        }

        public bool IsLegalMove(Move move)
        {
            return move.Turn == this.Turn && (move.Position > 1 && move.Position < BOARD_SIZE * BOARD_SIZE) && ((move.Position & BitBoard.GetLegalMovesPat(ref this.bitboard)) != 0);
        }

        public void Update(Move move)
        {
            if (move.Position != 0UL)
            {
                if (this.Turn == Color.Black)
                {
                    var revPat = CalcRevPat(move.Position, this.blackBoard, this.whiteBoard);
                    this.blackBoard ^= move.Position | revPat;
                    this.whiteBoard ^= revPat;
                }
                else
                {
                    var revPat = CalcRevPat(move.Position, this.whiteBoard, this.blackBoard);
                    this.whiteBoard ^= move.Position | revPat;
                    this.blackBoard ^= revPat;
                }
                this.passCount = 0;
            }
            else
                this.passCount++;

            this.isLegalPatSolved = false;
            this.Turn = (Color)(-(int)this.Turn);
        }

        public int GetNextMoves(Move[] moves)
        {
            var legalPat = GetLegalPat();
            if (legalPat == 0UL)
            {
                moves[0].Turn = this.Turn;
                moves[0].Position = 0UL;
                return 1;
            }

            var mask = 1UL;
            var count = 0;
            for (var i = 0; i < GRID_NUM; i++)
            {
                if ((mask & legalPat) != 0)
                {
                    moves[count].Turn = this.Turn;
                    moves[count].Position = mask;
                    count++;
                }
                mask <<= 1;
            }
            return count;
        }

        public int GetDiscCount(Color color)
        {
            return (int)((color == Color.Black) ? PopCount(this.blackBoard) : PopCount(this.whiteBoard));
        }

        public int GetBlankCount()
        {
            return (int)PopCount(~(this.blackBoard | this.whiteBoard));
        }

        public GameResult GetResult(Color turn)
        {
            if (this.passCount == 2)
            {
                var firstCount = GetDiscCount(Color.Black);
                var secondCount = GetDiscCount(Color.White);
                if (firstCount > secondCount)
                    return (turn == Color.Black) ? GameResult.Win : GameResult.Lose;
                if (firstCount < secondCount)
                    return (turn == Color.Black) ? GameResult.Lose : GameResult.Win;
                return GameResult.Draw;
            }
            return GameResult.NotEnd;
        }

        public void PutDisc(Color turn, int posX, int posY)
        {
            this.isLegalPatSolved = false;
            var putPat = 1UL << posX + posY * BOARD_SIZE;
            if (turn == Color.Black)
                this.blackBoard |= putPat;
            else
                this.whiteBoard |= putPat;
        }

        public bool EqualTo(FastBoard board)
        {
            return board.blackBoard == this.blackBoard && board.whiteBoard == this.whiteBoard && board.Turn == this.Turn;
        }

        public void CopyTo(FastBoard board)
        {
            board.Turn = this.Turn;
            board.blackBoard = this.blackBoard;
            board.whiteBoard = this.whiteBoard;
            board.passCount = this.passCount;
        }

        public object Clone()
        {
            var board = new FastBoard();
            CopyTo(board);
            return board;
        }

        ulong GetLegalPat()
        {
            if (this.isLegalPatSolved)
                return this.legalPat;
            if (this.Turn == Color.Black)
                this.legalPat = CalcLegalPat(this.blackBoard, this.whiteBoard);
            else
                this.legalPat = CalcLegalPat(this.whiteBoard, this.blackBoard);
            this.isLegalPatSolved = true;
            return this.legalPat;
        }

        static ulong CalcRevPat(ulong putPat, ulong playerBoard, ulong opponentBoard)
        {
            var revPat = 0UL;
            for (var i = 0; i < 8; i++)
            {
                var pat = 0UL;
                var mask = ShiftPat(putPat, i);
                while ((mask != 0UL) && ((mask & opponentBoard) != 0UL))
                {
                    pat |= mask;
                    mask = ShiftPat(mask, i);
                }

                if ((mask & playerBoard) != 0)
                    revPat |= pat;
            }
            return revPat;
        }

        static ulong CalcLegalPat(ulong playerBoard, ulong opponentBoard)
        {
            var lrSideMaskedBoard = opponentBoard & 0x7e7e7e7e7e7e7e7e;
            var tbSideMaskedBoard = opponentBoard & 0x00ffffffffffff00;
            var allSideMaskedBoard = opponentBoard & 0x007e7e7e7e7e7e00;
            var blank = ~(playerBoard | opponentBoard);
            ulong legalPat;
            ulong tmp;

            tmp = lrSideMaskedBoard & (playerBoard << 1);
            tmp |= lrSideMaskedBoard & (tmp << 1);
            tmp |= lrSideMaskedBoard & (tmp << 1);
            tmp |= lrSideMaskedBoard & (tmp << 1);
            tmp |= lrSideMaskedBoard & (tmp << 1);
            legalPat = blank & (tmp << 1);

            tmp = lrSideMaskedBoard & (playerBoard >> 1);
            tmp |= lrSideMaskedBoard & (tmp >> 1);
            tmp |= lrSideMaskedBoard & (tmp >> 1);
            tmp |= lrSideMaskedBoard & (tmp >> 1);
            tmp |= lrSideMaskedBoard & (tmp >> 1);
            legalPat |= blank & (tmp >> 1);

            tmp = tbSideMaskedBoard & (playerBoard << 8);
            tmp |= tbSideMaskedBoard & (tmp << 8);
            tmp |= tbSideMaskedBoard & (tmp << 8);
            tmp |= tbSideMaskedBoard & (tmp << 8);
            tmp |= tbSideMaskedBoard & (tmp << 8);
            legalPat |= blank & (tmp << 8);

            tmp = tbSideMaskedBoard & (playerBoard >> 8);
            tmp |= tbSideMaskedBoard & (tmp >> 8);
            tmp |= tbSideMaskedBoard & (tmp >> 8);
            tmp |= tbSideMaskedBoard & (tmp >> 8);
            tmp |= tbSideMaskedBoard & (tmp >> 8);
            legalPat |= blank & (tmp >> 8);

            tmp = allSideMaskedBoard & (playerBoard << 7);
            tmp |= allSideMaskedBoard & (tmp << 7);
            tmp |= allSideMaskedBoard & (tmp << 7);
            tmp |= allSideMaskedBoard & (tmp << 7);
            tmp |= allSideMaskedBoard & (tmp << 7);
            legalPat |= blank & (tmp << 7);

            tmp = allSideMaskedBoard & (playerBoard >> 7);
            tmp |= allSideMaskedBoard & (tmp >> 7);
            tmp |= allSideMaskedBoard & (tmp >> 7);
            tmp |= allSideMaskedBoard & (tmp >> 7);
            tmp |= allSideMaskedBoard & (tmp >> 7);
            legalPat |= blank & (tmp >> 7);

            tmp = allSideMaskedBoard & (playerBoard << 9);
            tmp |= allSideMaskedBoard & (tmp << 9);
            tmp |= allSideMaskedBoard & (tmp << 9);
            tmp |= allSideMaskedBoard & (tmp << 9);
            tmp |= allSideMaskedBoard & (tmp << 9);
            legalPat |= blank & (tmp << 9);

            tmp = allSideMaskedBoard & (playerBoard >> 9);
            tmp |= allSideMaskedBoard & (tmp >> 9);
            tmp |= allSideMaskedBoard & (tmp >> 9);
            tmp |= allSideMaskedBoard & (tmp >> 9);
            tmp |= allSideMaskedBoard & (tmp >> 9);
            legalPat |= blank & (tmp >> 9);

            return legalPat;
        }

        static ulong ShiftPat(ulong putPat, int direction)
        {
            switch (direction)
            {
                case 0:     // 上方向
                    return (putPat << 8) & 0xffffffffffffff00;

                case 1:    // 右上方向
                    return (putPat << 7) & 0x7f7f7f7f7f7f7f00;

                case 2:   // 右方向
                    return (putPat >> 1) & 0x7f7f7f7f7f7f7f7f;

                case 3:     // 右下方向
                    return (putPat >> 9) & 0x007f7f7f7f7f7f7f;

                case 4:      // 下方向
                    return (putPat >> 8) & 0x00ffffffffffffff;

                case 5:      // 左下方向
                    return (putPat >> 7) & 0x00fefefefefefefe;

                case 6:        // 左方向
                    return (putPat << 1) & 0xfefefefefefefefe;

                case 7:     // 左上方向
                    return (putPat << 9) & 0xfefefefefefefe00;

                default:
                    return 0UL;
            }
        }

        static ulong PopCount(ulong n)
        {
            ulong num;
            if (Popcnt.X64.IsSupported)
                num = Popcnt.X64.PopCount(n);
            else
            {
                num = n;
                num = (num & 0x5555555555555555) + ((num >> 1) & 0x5555555555555555);
                num = (num & 0x3333333333333333) + ((num >> 2) & 0x3333333333333333);
                num = (num & 0x0f0f0f0f0f0f0f0f) + ((num >> 4) & 0x0f0f0f0f0f0f0f0f);
                num = (num & 0x00ff00ff00ff00ff) + ((num >> 8) & 0x00ff00ff00ff00ff);
                num = (num & 0x0000ffff0000ffff) + ((num >> 16) & 0x0000ffff0000ffff);
                num = (num & 0x00000000ffffffff) + (num >> 32);
            }
            return num;
        }
    }
}
