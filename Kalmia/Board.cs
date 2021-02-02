using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using Kalmia.ReversiTextProtocol;

namespace Kalmia
{
    public enum Color
    {
        Black = 1,
        White = -1,
        Empty = 0
    }

    public enum InitialPosition
    {
        Cross,
        Parallel,
        Original
    }

    public enum GameResult
    {
        Win,
        Draw,
        Lose,
        NotEnd
    }

    public struct Move
    {
        public Color Turn;
        public ulong Position;
    }

    public class Board
    {
        public const int BOARD_SIZE = 8;

        static ReadOnlyCollection<(int x, int y)> HANDICAP_POSITIONS;

        ulong BlackBoard;
        ulong WhiteBoard;
        public Color Turn { get { return this._Turn; } }
        Color _Turn;
        public int MoveCount { get { return this._MoveCount; } }
        int _MoveCount;

        bool SolvedLegalPat = false;
        ulong LegalPat;

        int PassCount = 0;

        enum Direction
        {
            Top = 0,
            TopRight = 1,
            Right = 2,
            RightBottom = 3,
            Bottom = 4,
            BottomLeft = 5,
            Left = 6,
            TopLeft = 7
        }

        static Board()
        {
            var pos = new (int, int)[4];
            pos[0] = (0, 0);
            pos[1] = (7, 7);
            pos[2] = (7, 0);
            pos[3] = (0, 7);
            HANDICAP_POSITIONS = new ReadOnlyCollection<(int x, int y)>(pos);
        }

        public Board() : this(Color.Black, InitialPosition.Cross) { }

        public Board(Color turn, InitialPosition initPos)
        {
            this._Turn = Color.Black;

            switch (initPos)
            {
                case InitialPosition.Cross:
                    PutDisc(Color.Black, 4, 3);
                    PutDisc(Color.Black, 3, 4);
                    PutDisc(Color.White, 3, 3);
                    PutDisc(Color.White, 4, 4);
                    break;

                case InitialPosition.Parallel:
                    PutDisc(Color.Black, 3, 4);
                    PutDisc(Color.Black, 4, 4);
                    PutDisc(Color.White, 3, 3);
                    PutDisc(Color.White, 4, 3);
                    break;
            }
        }

        public Color GetColor(int posX,int posY)
        {
            var pos = 1UL << posX + posY * BOARD_SIZE;
            if ((this.BlackBoard & pos) != 0)
                return Color.Black;
            if ((this.WhiteBoard & pos) != 0)
                return Color.White;
            return Color.Empty;
        }

        public void ChangeCurrentTurn(Color turn)
        {
            this._Turn = turn;
            this.SolvedLegalPat = false;
        }

        public List<(int x, int y)> SetHandicap(int num)
        {
            if (num > HANDICAP_POSITIONS.Count || num < 0)
                throw new IndexOutOfRangeException();

            for(var i = 0; i < num; i++)
            {
                var pos = HANDICAP_POSITIONS[i];
                if (GetColor(pos.x, pos.y) != Color.Empty)
                    throw new ArgumentException("Board is not empty.");
            }

            for (var i = 0; i < num; i++)
            {
                var pos = HANDICAP_POSITIONS[i];
                PutDisc(Color.Black, pos.x, pos.y);
            }
            return HANDICAP_POSITIONS.ToList().GetRange(0, num);
        }

        public bool Move(Color turn,int posX,int posY)
        {
            if(posX == -1 && posY == -1)
            {
                Move pass;
                pass.Turn = turn;
                pass.Position = 0;
                return Move(pass);
            }

            if (posX < 0 || posX > 7)
                throw new ArgumentOutOfRangeException(nameof(posX));

            if (posY < 0 || posY > 7)
                throw new ArgumentOutOfRangeException(nameof(posY));

            Move move;
            move.Turn = turn;
            move.Position = 1UL << posX + posY * BOARD_SIZE;
            return Move(move);
        }

        public bool Move(Move move)
        {
            if (move.Turn != this.Turn)
                return false;

            if (move.Position != 0)
            {
                if ((move.Position & GetLegalPat()) == 0)
                    return false;

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

            this.SolvedLegalPat = false;
            this._Turn = (Color)(-(int)this._Turn);
            this._MoveCount++;
            return true;
        }

        public int GetNextMoves(Move[] moves)
        {
            if (this.PassCount == 2)
                return 0;

            var legalPat = GetLegalPat();
            if (legalPat == 0UL)
            {
                moves[0].Turn = this.Turn;
                moves[0].Position = 0UL;
                return 1;
            }

            var mask = 1UL;
            var count = 0;
            for (var i = 0; i < BOARD_SIZE * BOARD_SIZE; i++)
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

        public int GetBlankNum()
        {
            return BOARD_SIZE * BOARD_SIZE - (GetDiscCount(Color.Black) + GetDiscCount(Color.White));
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

        public bool IsSameAs(Board board)
        {
            return board != null && board.Turn == this.Turn && board.BlackBoard == this.BlackBoard && board.WhiteBoard == this.WhiteBoard;
        }

        public void CopyTo(Board board)
        {
            board._Turn = this.Turn;
            board.BlackBoard = this.BlackBoard;
            board.WhiteBoard = this.WhiteBoard;
            board.PassCount = this.PassCount;
            board._MoveCount = this._MoveCount;
        }

        public object Clone()
        {
            var board = new Board();
            CopyTo(board);
            return board;
        }

        public FastBoard ToFastBoard()
        {
            return new FastBoard(this.BlackBoard, this.WhiteBoard, this._Turn);
        }

        public int GetDiscCount(Color turn)
        {
            return (int)((turn == Color.Black) ? Popcnt.X64.PopCount(this.BlackBoard) : Popcnt.X64.PopCount(this.WhiteBoard));
        }

        public void PutDisc(Color turn, int posX, int posY)
        {
            if (posX < 0 || posX > 7)
                throw new ArgumentOutOfRangeException(nameof(posX));

            if (posY < 0 || posY > 7)
                throw new ArgumentOutOfRangeException(nameof(posY));

            this.SolvedLegalPat = false;
            var putPat = 1UL << posX + posY * BOARD_SIZE;

            if ((putPat & (this.BlackBoard | this.WhiteBoard)) != 0UL)
                throw new ArgumentException("Not empty.");

            if (turn == Color.Black)
                this.BlackBoard |= putPat;
            else
                this.WhiteBoard |= putPat;
        }

        public override string ToString()
        {
            var boardStr = "　ＡＢＣＤＥＦＧＨ\n";
            var mask = 1UL;
            for (var i = 0; i < BOARD_SIZE; i++)
            {
                boardStr += (char)('１' + i);
                for (var j = 0; j < BOARD_SIZE; j++)
                {
                    if ((mask & this.BlackBoard) != 0)
                        boardStr += "ｏ";
                    else if ((mask & this.WhiteBoard) != 0)
                        boardStr += "ｘ";
                    else
                        boardStr += "・";
                    mask <<= 1;
                }
                boardStr += "\n";
            }
            return boardStr;
        }

        public static Move StringToMove(Color turn, string move)
        {
            if(move == "pass")
            {
                Move pass;
                pass.Position = 0UL;
                pass.Turn = turn;
            }

            if (move.Length != 2)
                throw new ArgumentException("Invalid Move");

            var posX = char.ToLower(move[0]) - 'a';
            var posY = int.Parse(move[1].ToString()) - 1;

            if (posX < 0 || posX > BOARD_SIZE - 1 || posY < 0 || posY > BOARD_SIZE)
                throw new ArgumentException("Invalid Move.");

            Move ret;
            ret.Position = 1UL << (posX + posY * BOARD_SIZE);
            ret.Turn = turn;
            return ret;
        }

        public static string MoveToString(Move move)
        {
            if (move.Position == 0)
                return "pass";

            var mask = 1UL;
            var loc = 0;
            for (; (move.Position & mask) == 0; loc++)
                mask <<= 1;
            return (char)((loc % BOARD_SIZE) + 'A') + ((loc / BOARD_SIZE) + 1).ToString();
        }

        ulong GetLegalPat()
        {
            if (this.SolvedLegalPat)
                return this.LegalPat;
            if (this.Turn == Color.Black)
                this.LegalPat = CalcLegalPat(this.BlackBoard, this.WhiteBoard);
            else
                this.LegalPat = CalcLegalPat(this.WhiteBoard, this.BlackBoard);
            this.SolvedLegalPat = true;
            return this.LegalPat;
        }

        static ulong CalcRevPat(ulong putPat, ulong playerBoard, ulong opponentBoard)
        {
            var revPat = 0UL;
            for (var i = Direction.Top; i <= Direction.TopLeft; i++)
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
            for (var i = 0; i < 5; i++)
                tmp |= lrSideMaskedBoard & (tmp << 1);
            legalPat = blank & (tmp << 1);

            tmp = lrSideMaskedBoard & (playerBoard >> 1);
            for (var i = 0; i < 5; i++)
                tmp |= lrSideMaskedBoard & (tmp >> 1);
            legalPat |= blank & (tmp >> 1);

            tmp = tbSideMaskedBoard & (playerBoard << 8);
            for (var i = 0; i < 5; i++)
                tmp |= tbSideMaskedBoard & (tmp << 8);
            legalPat |= blank & (tmp << 8);

            tmp = tbSideMaskedBoard & (playerBoard >> 8);
            for (var i = 0; i < 5; i++)
                tmp |= tbSideMaskedBoard & (tmp >> 8);
            legalPat |= blank & (tmp >> 8);

            tmp = allSideMaskedBoard & (playerBoard << 7);
            for (var i = 0; i < 5; i++)
                tmp |= allSideMaskedBoard & (tmp << 7);
            legalPat |= blank & (tmp << 7);

            tmp = allSideMaskedBoard & (playerBoard >> 7);
            for (var i = 0; i < 5; i++)
                tmp |= allSideMaskedBoard & (tmp >> 7);
            legalPat |= blank & (tmp >> 7);

            tmp = allSideMaskedBoard & (playerBoard << 9);
            for (var i = 0; i < 5; i++)
                tmp |= allSideMaskedBoard & (tmp << 9);
            legalPat |= blank & (tmp << 9);

            tmp = allSideMaskedBoard & (playerBoard >> 9);
            for (var i = 0; i < 5; i++)
                tmp |= allSideMaskedBoard & (tmp >> 9);
            legalPat |= blank & (tmp >> 9);

            return legalPat;
        }

        static ulong ShiftPat(ulong putPat, Direction direction)
        {
            switch (direction)
            {
                case Direction.Top:
                    return (putPat << 8) & 0xffffffffffffff00;

                case Direction.TopRight:
                    return (putPat << 7) & 0x7f7f7f7f7f7f7f00;

                case Direction.Right:
                    return (putPat >> 1) & 0x7f7f7f7f7f7f7f7f;

                case Direction.RightBottom:
                    return (putPat >> 9) & 0x007f7f7f7f7f7f7f;

                case Direction.Bottom:
                    return (putPat >> 8) & 0x00ffffffffffffff;

                case Direction.BottomLeft:
                    return (putPat >> 7) & 0x00fefefefefefefe;

                case Direction.Left:
                    return (putPat << 1) & 0xfefefefefefefefe;

                case Direction.TopLeft:
                    return (putPat << 9) & 0xfefefefefefefe00;

                default:
                    return 0UL;
            }
        }
    }
}
