using System;
using System.Collections.Generic;
using Kalmia.ReversiTextProtocol;

namespace Kalmia.Engines.MCTS
{
    public class MCTSEngine : IEngine
    {
        const string NAME = "MCTS Engine";
        const string VERSION = "0.0";

        const int EXPANSION_THRESHOLD = 40;
        const int NODE_POOL_SIZE = 1000000;
        const int MAX_SIM_COUNT = 32000;
        const int TIME_LIMIT = 3000;

        Board Board;
        Stack<Board> BoardHistory = new Stack<Board>();
        UCT Tree;

        public MCTSEngine()
        {
            this.Board = new Board();
            this.Tree = new UCT(EXPANSION_THRESHOLD, NODE_POOL_SIZE);
            this.Tree.SetRoot(this.Board);
        }

        public void Quit()
        {

        }

        public string GetName()
        {
            return NAME;
        }

        public string GetVersion()
        {
            return VERSION;
        }

        public void ClearBoard(InitialPosition initPos)
        {
            this.Board = new Board(Color.Black, initPos);
            this.BoardHistory.Clear();
            this.Tree.SetRoot(this.Board);
        }

        public void Play(Color color, int posX, int posY)
        {
            if (color != this.Board.Turn)
            {
                this.Board.ChangeCurrentTurn(color);
                this.Tree.SetRoot(this.Board);
            }

            try
            {
                if (!this.Board.Move(color, posX, posY))
                    throw new RVTPException("illegal move", false);
                if (!this.Tree.UpdateRoot(this.Board))
                    this.Tree.SetRoot(this.Board);
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
                this.Board.PutDisc(color, posX, posY);
                if (!this.Tree.UpdateRoot(this.Board))
                    this.Tree.SetRoot(this.Board);
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

        public List<(int x, int y)> SetHandicap(int num)
        {
            try
            {
                var pos = this.Board.SetHandicap(num);
                this.Tree.SetRoot(this.Board);
                return pos;
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

        public string LoadSGF(string path)
        {
            throw new RVTPException("not supported", false);
        }

        public string LoadSGF(string path, int posX, int posY)
        {
            throw new RVTPException("not supported", false);
        }

        public string LoadSGF(string path, int moveNum)
        {
            throw new RVTPException("not supported", false);
        }

        public string GenerateMove(Color color)
        {
            if (color != this.Board.Turn)
            {
                this.Board.ChangeCurrentTurn(color);
                this.Tree.SetRoot(this.Board);
            }

            var move = SelectBestMove(this.Tree.Search(MAX_SIM_COUNT, TIME_LIMIT));
            this.Board.Update(move);
            this.Tree.UpdateRoot(this.Board);
            return Board.MoveToString(move);
        }

        public string RegGenerateMove(Color color)
        {
            return Board.MoveToString(SelectBestMove(this.Tree.Search(MAX_SIM_COUNT, TIME_LIMIT)));
        }

        public void Undo()
        {
            if (this.BoardHistory.Count == 0)
                throw new RVTPException("cannot undo", false);
            this.Board = this.BoardHistory.Pop();
            if (!this.Tree.UpdateRoot(this.Board))
                this.Tree.SetRoot(this.Board);
        }

        public void SetTime(int mainTime, int countdownTime, int countdownNum)
        {
            return;
        }

        public void SendTimeLeft(int timeLeft, int countdownNumLeft)
        {
            return;
        }

        public string GetFinalScore()
        {
            switch (this.Board.GetResult(Color.Black))
            {
                case GameResult.Win:
                    return $"B+{this.Board.GetDiscCount(Color.Black) - this.Board.GetDiscCount(Color.White)}";

                case GameResult.Lose:
                    return $"W+{this.Board.GetDiscCount(Color.White) - this.Board.GetDiscCount(Color.Black)}";

                case GameResult.Draw:
                    return "0";

                default:
                    return string.Empty;
            }
        }

        public Color GetColor(int posX, int posY)
        {
            return this.Board.GetColor(posX, posY);
        }

        public string ExecuteOriginalCommand(string command, string[] args)
        {
            throw new RVTPException("invalid command.", false);
        }

        public string[] GetOriginalCommands()
        {
            return new string[0];
        }

        static Move SelectBestMove(SearchResult result)
        {
            if (result.ChildNodesInfo.Length == 1)
                return result.ChildNodesInfo[0].Move;

            var bestNode = result.ChildNodesInfo[0];
            for (var i = 1; i < result.ChildNodesInfo.Length; i++)
                if (result.ChildNodesInfo[i].SimulationCount > bestNode.SimulationCount)
                    bestNode = result.ChildNodesInfo[i];
            return bestNode.Move;
        }
    }
}
