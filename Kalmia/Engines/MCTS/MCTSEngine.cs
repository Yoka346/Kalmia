using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Kalmia.ReversiTextProtocol;

namespace Kalmia.Engines.MCTS
{
    public class MCTSEngine : IEngine
    {
        const string NAME = "MCTS Engine";
        const string VERSION = "0.0";

        const int EXPANSION_THRESHOLD = 40;
        const int NODE_POOL_SIZE = 1000000;
        const int ITERATION_COUNT = 32000;
        const int MAX_SIM_COUNT = 32000;
        const int TIME_LIMIT = 1000;

        Board board;
        Stack<Board> boardHistory = new Stack<Board>();
        UCT tree;

        readonly ReadOnlyDictionary<string, Func<string[], string>> COMMANDS;

        public MCTSEngine()
        {
            this.board = new Board();
            this.tree = new UCT(EXPANSION_THRESHOLD, NODE_POOL_SIZE);
            this.tree.SetRoot(this.board);
            this.COMMANDS = new ReadOnlyDictionary<string, Func<string[], string>>(InitCommands());
        }

        Dictionary<string, Func<string[], string>> InitCommands()
        {
            var commands = new Dictionary<string, Func<string[], string>>();
            commands.Add("ave_sim_count", ExecuteAveSimCountCommand);
            commands.Add("nodes_mem_use", ExecuteNodesMemUseCommand);
            return commands;
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
            this.board = new Board(Color.Black, initPos);
            this.boardHistory.Clear();
            this.tree.SetRoot(this.board);
        }

        public void Play(Color color, int posX, int posY)
        {
            if (color != this.board.Turn)
            {
                this.board.ChangeCurrentTurn(color);
                this.tree.SetRoot(this.board);
            }

            try
            {
                if (!this.board.Update(color, posX, posY))
                    throw new RVTPException("illegal move", false);
                if (!this.tree.UpdateRoot(this.board))
                    this.tree.SetRoot(this.board);
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
                if (!this.tree.UpdateRoot(this.board))
                    this.tree.SetRoot(this.board);
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
                var pos = this.board.SetHandicap(num);
                this.tree.SetRoot(this.board);
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
            if (color != this.board.Turn)
            {
                this.board.ChangeCurrentTurn(color);
                this.tree.SetRoot(this.board);
            }

            var result = this.tree.Search(ITERATION_COUNT, MAX_SIM_COUNT, TIME_LIMIT);
            var move = SelectBestMove(result);
            this.board.Update(move);        // 変な手を返すバグがある. NodePool周りにバグがあるのではないか
            this.tree.UpdateRoot(this.board);
            return Board.MoveToString(move);
        }

        public string RegGenerateMove(Color color)
        {
            return Board.MoveToString(SelectBestMove(this.tree.Search(ITERATION_COUNT, MAX_SIM_COUNT, TIME_LIMIT)));
        }

        public void Undo()
        {
            if (this.boardHistory.Count == 0)
                throw new RVTPException("cannot undo", false);
            this.board = this.boardHistory.Pop();
            if (!this.tree.UpdateRoot(this.board))
                this.tree.SetRoot(this.board);
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

        public Color GetColor(int posX, int posY)
        {
            return this.board.GetColor(posX, posY);
        }

        public string ExecuteOriginalCommand(string command, string[] args)
        {
            if (this.COMMANDS.ContainsKey(command))
                return this.COMMANDS[command](args);

            throw new RVTPException("Invalid command.", false);
        }

        public string[] GetOriginalCommands()
        {
            var commands = new string[this.COMMANDS.Keys.Count];
            int i = 0;
            foreach (var key in this.COMMANDS.Keys)
                commands[i++] = key;
            return commands;
        }

        string ExecuteAveSimCountCommand(string[] args)
        {
            var iterationCount = int.Parse(args[2]);
            var board = new Board(Color.Black, InitialPosition.Cross);
            var count = 0;

            var sw = new Stopwatch();
            sw.Start();
            while (board.GetResult(Color.Black) == GameResult.NotEnd)
            {
                this.tree.SetRoot(board);
                var result = this.tree.Search(iterationCount, int.MaxValue, int.MaxValue);
                board.Update(SelectBestMove(result));
                this.tree.UpdateRoot(board);
                count++;
            }
            sw.Stop();
            return ((iterationCount * count) / (sw.ElapsedMilliseconds / 1000.0f)).ToString();
        }

        string ExecuteNodesMemUseCommand(string[] args)
        {
            int nodeNum = int.Parse(args[2]);
            var init = GC.GetTotalMemory(false);
            Node[] nodes = new Node[nodeNum];
            for (var i = 0; i < nodes.Length; i++)
                nodes[i] = new Node();
            return (GC.GetTotalMemory(false) - init).ToString();
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
