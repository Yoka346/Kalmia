using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Intrinsics.X86;

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
        public const int PASS = -1;
        public Color Turn;
        public int Position;

        public Move(Color turn, int pos)
        {
            this.Turn = turn;
            this.Position = pos;
        }

        public Move(int pos) : this(Color.Black, pos) { }
    }

    public class Board
    {
        public const int BOARD_SIZE = 8;
        public const int MAX_MOVE_NUM = 46;

        static readonly ReadOnlyCollection<(int x, int y)> HANDICAP_POSITIONS;

        ulong blackBoard;
        ulong whiteBoard;
        public Color Turn { get { return this.turn; } }
        Color turn;
        public int MoveCount { get { return this.moveCount; } }
        int moveCount;

        bool solvedLegalPat = false;
        ulong legalPat;

        int passCount = 0;

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
            this.turn = turn;

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
            if ((this.blackBoard & pos) != 0)
                return Color.Black;
            if ((this.whiteBoard & pos) != 0)
                return Color.White;
            return Color.Empty;
        }

        public void ChangeCurrentTurn(Color turn)
        {
            this.turn = turn;
            this.solvedLegalPat = false;
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

        public bool Update(Color turn,int posX,int posY)
        {
            if(posX == -1 && posY == -1)
            {
                Move pass;
                pass.Turn = turn;
                pass.Position = Move.PASS;
                return Update(pass);
            }

            if (posX < 0 || posX > 7)
                throw new ArgumentOutOfRangeException(nameof(posX));

            if (posY < 0 || posY > 7)
                throw new ArgumentOutOfRangeException(nameof(posY));

            Move move;
            move.Turn = turn;
            move.Position = posX + posY * BOARD_SIZE;
            return Update(move);
        }

        public bool Update(Move move)
        {
            if (move.Turn != this.Turn)
                return false;

            if (move.Position != Move.PASS)
            {
                var movePat = 1UL << move.Position;
                if ((movePat & GetLegalPat()) == 0)
                    return false;

                if (this.Turn == Color.Black)
                {
                    var revPat = CalcRevPat(movePat, this.blackBoard, this.whiteBoard);
                    this.blackBoard ^= movePat | revPat;
                    this.whiteBoard ^= revPat;
                }
                else
                {
                    var revPat = CalcRevPat(movePat, this.whiteBoard, this.blackBoard);
                    this.whiteBoard ^= movePat | revPat;
                    this.blackBoard ^= revPat;
                }
                this.passCount = 0;
            }
            else
                this.passCount++;

            this.solvedLegalPat = false;
            this.turn = (Color)(-(int)this.turn);
            this.moveCount++;
            return true;
        }

        public int GetNextMoves(Move[] moves)
        {
            if (this.passCount == 2)
                return 0;

            var legalPat = GetLegalPat();
            if (legalPat == 0UL)
            {
                moves[0].Turn = this.Turn;
                moves[0].Position = Move.PASS;
                return 1;
            }

            var mask = 1UL;
            var count = 0;
            for (var i = 0; i < BOARD_SIZE * BOARD_SIZE; i++)
            {
                if ((mask & legalPat) != 0)
                {
                    moves[count].Turn = this.Turn;
                    moves[count].Position = i;
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

        public bool IsSameAs(Board board)
        {
            return board != null && board.Turn == this.Turn && board.blackBoard == this.blackBoard && board.whiteBoard == this.whiteBoard;
        }

        public void CopyTo(Board board)
        {
            board.turn = this.Turn;
            board.blackBoard = this.blackBoard;
            board.whiteBoard = this.whiteBoard;
            board.passCount = this.passCount;
            board.moveCount = this.moveCount;
        }

        public object Clone()
        {
            var board = new Board();
            CopyTo(board);
            return board;
        }

        public FastBoard ToFastBoard()
        {
            return new FastBoard(this.blackBoard, this.whiteBoard, this.turn);
        }

        public int GetDiscCount(Color turn)
        {
            return (int)((turn == Color.Black) ? Popcnt.X64.PopCount(this.blackBoard) : Popcnt.X64.PopCount(this.whiteBoard));
        }

        public void PutDisc(Color turn, int posX, int posY)
        {
            if (posX < 0 || posX > 7)
                throw new ArgumentOutOfRangeException(nameof(posX));

            if (posY < 0 || posY > 7)
                throw new ArgumentOutOfRangeException(nameof(posY));

            this.solvedLegalPat = false;
            var putPat = 1UL << posX + posY * BOARD_SIZE;

            if ((putPat & (this.blackBoard | this.whiteBoard)) != 0UL)
                throw new ArgumentException("Not empty.");

            if (turn == Color.Black)
                this.blackBoard |= putPat;
            else
                this.whiteBoard |= putPat;
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
                    if ((mask & this.blackBoard) != 0)
                        boardStr += "ｏ";
                    else if ((mask & this.whiteBoard) != 0)
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
                pass.Position = Move.PASS;
                pass.Turn = turn;
            }

            if (move.Length != 2)
                throw new ArgumentException("Invalid Move");

            var posX = char.ToLower(move[0]) - 'a';
            var posY = int.Parse(move[1].ToString()) - 1;

            if (posX < 0 || posX > BOARD_SIZE - 1 || posY < 0 || posY > BOARD_SIZE)
                throw new ArgumentException("Invalid Move.");

            Move ret;
            ret.Position = posX + posY * BOARD_SIZE;
            ret.Turn = turn;
            return ret;
        }

        public static string MoveToString(Move move)
        {
            if (move.Position == 0)
                return "pass";

            var movePat = 1UL << move.Position;
            var mask = 1UL;
            var loc = 0;
            for (; (movePat & mask) == 0; loc++)
                mask <<= 1;
            return (char)((loc % BOARD_SIZE) + 'A') + ((loc / BOARD_SIZE) + 1).ToString();
        }

        ulong GetLegalPat()
        {
            if (this.solvedLegalPat)
                return this.legalPat;
            if (this.Turn == Color.Black)
                this.legalPat = CalcLegalPat(this.blackBoard, this.whiteBoard);
            else
                this.legalPat = CalcLegalPat(this.whiteBoard, this.blackBoard);
            this.solvedLegalPat = true;
            return this.legalPat;
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
