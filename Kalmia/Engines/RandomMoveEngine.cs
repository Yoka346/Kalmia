using System;
using System.Collections.Generic;
using System.Text;
using Kalmia.ReversiTextProtocol;

namespace Kalmia.Engines
{
    public class RandomMoveEngine : IEngine
    {
        const string NAME = "Random Move Engine";
        const string VERSION = "0.0";

        Board board = new Board();
        readonly Random RAND = new Random();
        Stack<Board> boardHistory = new Stack<Board>();

        public void ClearBoard(InitialPosition initPos)
        {
            this.board = new Board(Color.Black, initPos);
            this.boardHistory.Clear();
            this.boardHistory.Push((Board)this.board.Clone());
        }

        public string ExecuteOriginalCommand(string command, string[] args)
        {
            throw new RVTPException("invalid command.", false);
        }

        public string GenerateMove(Color color)
        {
            if (board.Turn != color)
                board.ChangeCurrentTurn(color);

            var moves = new Move[Board.MAX_MOVE_NUM];
            var moveNum = board.GetNextMoves(moves);
            var move = moves[RAND.Next(moveNum)];
            this.board.Update(move);
            this.boardHistory.Push((Board)this.board.Clone());
            return Board.MoveToString(move);
        }

        public Color GetColor(int posX, int posY)
        {
            return this.board.GetColor(posX, posY);
        }

        public string GetFinalScore()
        {
            switch (this.board.GetResult(Color.Black))
            {
                case GameResult.Win:
                    return $"B+{this.board.GetDiscCount(Color.Black) - this.board.GetDiscCount(Color.White)}";

                case GameResult.Lose:
                    return $"W+{this.board.GetDiscCount(Color.White) - this.board.GetDiscCount(Color.Black)}";

                case GameResult.Draw:
                    return "0";

                default:
                    return string.Empty;
            }
        }

        public string GetName()
        {
            return NAME;
        }

        public string[] GetOriginalCommands()
        {
            return new string[0];
        }

        public string GetVersion()
        {
            return VERSION;
        }

        public string LoadSGF(string path)
        {
            throw new NotImplementedException();
        }

        public string LoadSGF(string path, int posX, int posY)
        {
            throw new NotImplementedException();
        }

        public string LoadSGF(string path, int moveNum)
        {
            throw new NotImplementedException();
        }

        public void Play(Color color, int posX, int posY)
        {
            if (color != this.board.Turn)
                this.board.ChangeCurrentTurn(color);
            try
            {
                if (!this.board.Update(color, posX, posY))
                    throw new RVTPException("illegal move", false);
                this.boardHistory.Push((Board)this.board.Clone());
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new RVTPException("illegal coordinate", false);
            }
        }

        public void Put(Color color, int posX, int posY)
        {
            try
            {
                this.board.PutDisc(color, posX, posY);
                this.boardHistory.Push((Board)this.board.Clone());
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new RVTPException("invalid coordinate", false);
            }
            catch (ArgumentException ex)
            {
                if (ex.Message == "Not empty.")
                    throw new RVTPException("board not empty.", false);
            }
        }

        public void Quit()
        {
            return;
        }

        public string RegGenerateMove(Color color)
        {
            if (board.Turn != color)
                board.ChangeCurrentTurn(color);

            var moves = new Move[Board.MAX_MOVE_NUM];
            var moveNum = board.GetNextMoves(moves);
            return Board.MoveToString(moves[RAND.Next(moveNum)]);
        }

        public void SendTimeLeft(int timeLeft, int countdownNumLeft)
        {
            throw new NotImplementedException();
        }

        public List<(int x, int y)> SetHandicap(int num)
        {
            try
            {
                var posList = this.board.SetHandicap(num);
                this.boardHistory.Push((Board)this.board.Clone());
                return posList;
            }
            catch (IndexOutOfRangeException)
            {
                throw new RVTPException("too many handicap", false);
            }
            catch (ArgumentException)
            {
                throw new RVTPException("board not empty", false);
            }
        }

        public void SetTime(int mainTime, int countdownTime, int countdownNum)
        {
            return;
        }

        public void Undo()
        {
            this.board = this.boardHistory.Pop();
        }
    }
}
