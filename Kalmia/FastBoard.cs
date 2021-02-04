using System;
using System.Runtime.Intrinsics.X86;

namespace Kalmia
{
    public class FastBoard : ICloneable      // 探索に用いる盤面. Boardクラスに比べて機能が省かれている.
    {
        public const int BOARD_SIZE = 8;
        public const int GRID_NUM = BOARD_SIZE * BOARD_SIZE;
        public const int MAX_MOVE_NUM = 46;     // 1つの局面に現れる合法手数は少なくとも46手以下.

        ulong BlackBoard;
        ulong WhiteBoard;
        public Color Turn { get; private set; }

        bool IsLegalPatSolved = false;
        ulong LegalPat;     // 合法手のビットボード

        int PassCount = 0;

        public FastBoard() : this(0UL, 0UL, Color.Black) { }

        public FastBoard(ulong blackBoard, ulong whiteBoard, Color turn)
        {
            this.BlackBoard = blackBoard;
            this.WhiteBoard = whiteBoard;
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
                    var revPat = CalcRevPat(move.Position, this.BlackBoard, this.WhiteBoard);
                    this.BlackBoard ^= move.Position | revPat;
                    this.WhiteBoard ^= revPat;
                }
                else
                {
                    var revPat = CalcRevPat(move.Position, this.WhiteBoard, this.BlackBoard);
                    this.WhiteBoard ^= move.Position | revPat;
                    this.BlackBoard ^= revPat;
                }
                this.PassCount = 0;
            }
            else
                this.PassCount++;

            this.IsLegalPatSolved = false;
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
            return (int)((color == Color.Black) ? PopCount(this.BlackBoard) : PopCount(this.WhiteBoard));
        }

        public int GetBlankCount()
        {
            return (int)PopCount(~(this.BlackBoard | this.WhiteBoard));
        }

        public GameResult GetResult(Color turn)
        {
            if (this.PassCount == 2)
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
            this.IsLegalPatSolved = false;
            var putPat = 1UL << posX + posY * BOARD_SIZE;
            if (turn == Color.Black)
                this.BlackBoard |= putPat;
            else
                this.WhiteBoard |= putPat;
        }

        public bool EqualTo(FastBoard board)
        {
            return board.BlackBoard == this.BlackBoard && board.WhiteBoard == this.WhiteBoard && board.Turn == this.Turn;
        }

        public void CopyTo(FastBoard board)
        {
            board.Turn = this.Turn;
            board.BlackBoard = this.BlackBoard;
            board.WhiteBoard = this.WhiteBoard;
            board.PassCount = this.PassCount;
        }

        public object Clone()
        {
            var board = new FastBoard();
            CopyTo(board);
            return board;
        }

        ulong GetLegalPat()
        {
            if (this.IsLegalPatSolved)
                return this.LegalPat;
            if (this.Turn == Color.Black)
                this.LegalPat = CalcLegalPat(this.BlackBoard, this.WhiteBoard);
            else
                this.LegalPat = CalcLegalPat(this.WhiteBoard, this.BlackBoard);
            this.IsLegalPatSolved = true;
            return this.LegalPat;
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
