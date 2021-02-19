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
        public Vector128<ulong> Data;
        public Color Turn;

        public static UInt64_4 BroadcastBlack(BitBoard bitBoard)
        {
            UInt64_4 ret;
            ret.Data = Avx2.BroadcastScalarToVector256(bitBoard.Data);
            return ret;
        }

        public static UInt64_4 BroadCastWhite(BitBoard bitBoard)
        {
            UInt64_4 ret;
            ret.Data = Avx2.Permute4x64(Vector256.Create(bitBoard.Data, bitBoard.Data), 0x55);
            return ret;
        }

        public static UInt64_4 BroadcastCurrentColor(BitBoard bitBoard)
        {
            return (bitBoard.Turn == Color.Black) ? BroadcastBlack(bitBoard) : BroadCastWhite(bitBoard);
        }

        public static UInt64_4 BroadcastNotCurrentColor(BitBoard bitBoard)
        {
            return (bitBoard.Turn != Color.Black) ? BroadcastBlack(bitBoard) : BroadCastWhite(bitBoard);
        }

        public static ulong GetDiscsPat(BitBoard bitBoard)
        {
            return Sse2.X64.ConvertToUInt64(Sse2.Or(Ssse3.AlignRight(bitBoard.Data, bitBoard.Data, 8), bitBoard.Data));
        }

        public static Vector128<ulong> GetFlipPat(BitBoard bitBoard, int pos)
        {
            var current = BroadcastBlack(bitBoard);
            var opponent = BroadCastWhite(bitBoard);
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

        public static Vector128<ulong> GetLegalMovePat(BitBoard bitBoard)
        {
            var doubleBitBoard = Vector256.Create(bitBoard.Data, bitBoard.Data);
            var tmp = (GetLegalMovePatHorizontal(doubleBitBoard) | GetLegalMovePatVertical(doubleBitBoard) | GetLegalMovePatDiagonal(doubleBitBoard));
            return Avx.AndNot(GetDiscsPat(doubleBitBoard), tmp);
        }

        static Vector128<ulong> GetLegalMovePatHorizontal(Vector256<ulong> doubleBitBoard)
        {

        }

        static Vector256<ulong> GetLegalPatBackwardP4(Vector256<ulong> doubleBitBoard_0, Vector256<ulong> doubleBitBoard_1)
        {
            var b_0 = Avx2.UnpackLow(doubleBitBoard_0, doubleBitBoard_1);
            var b_1 = Avx2.Add(b_0, b_0);
            var w = Avx2.UnpackHigh(doubleBitBoard_0, doubleBitBoard_1);
            return Avx2.AndNot(Avx2.Or(b_1, w), Avx2.Add(b_1, w));
        }

        static Vector128<ulong> GetDiscsPat(Vector256<ulong> doubleBitBoard)
        {
            var tmp = Avx2.Or(Avx2.AlignRight(doubleBitBoard, doubleBitBoard, 8), doubleBitBoard);
            return Avx2.Permute4x64(tmp, 0x08).GetLower();
        }

        Vector256<ulong> GetMirrorHorizontalBoard(Vector256<ulong> doubleBitBoard)
        {
            var mask_0 = Vector256.Create((byte)0x55).AsUInt64();
            var mask_1 = Vector256.Create((byte)0x33).AsUInt64();
            var mask_2 = Vector256.Create((byte)0x0f).AsUInt64();
            doubleBitBoard = Avx2.Or(Avx2.And(Avx2.ShiftRightLogical(doubleBitBoard, 1), mask_0), Avx2.ShiftLeftLogical(Avx2.And(doubleBitBoard, mask_0), 1));
            doubleBitBoard = Avx2.Or(Avx2.And(Avx2.ShiftRightLogical(doubleBitBoard, 2), mask_1), Avx2.ShiftLeftLogical(Avx2.And(doubleBitBoard, mask_1), 2));
            doubleBitBoard = Avx2.Or(Avx2.And(Avx2.ShiftRightLogical(doubleBitBoard, 4), mask_2), Avx2.ShiftLeftLogical(Avx2.And(doubleBitBoard, mask_2), 4));
            return doubleBitBoard;
        }

    public class FastBoard : ICloneable      // 探索に用いる盤面. Boardクラスに比べて機能が省かれている.
    {
        public const int BOARD_SIZE = 8;
        public const int GRID_NUM = BOARD_SIZE * BOARD_SIZE;
        public const int MAX_MOVE_NUM = 46;     // 1つの局面に現れる合法手数は少なくとも46手以下.

        internal Vector128<ulong> bitboard;
        public Color Turn { get; private set; }

        bool isLegalPatSolved = false;
        ulong legalPat;     // 合法手のビットボード

        int passCount = 0;

        public FastBoard() : this(0UL, 0UL, Color.Black) { }

        public FastBoard(ulong blackBoard, ulong whiteBoard, Color turn)
        {
            this.blackBoard = blackBoard;
            this.whiteBoard = whiteBoard;
            this.Turn = turn;
        }

        public bool IsLegalMove(Move move)
        {
            return move.Turn == this.Turn && PopCount(move.Position) == 1 && (move.Position & GetLegalPat()) != 0;
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
            if(legalPat == 0UL)
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
