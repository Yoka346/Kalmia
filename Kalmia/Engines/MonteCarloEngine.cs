using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kalmia.ReversiTextProtocol;

namespace Kalmia.Engines
{
    public class MonteCarloEngine : IEngine
    {
        const string NAME = "MonteCarlo";
        const string VERSION = "Version 0.0";

        readonly int PLAYOUT_NUM;
        readonly int THREAD_NUM;
        Board board;
        Stack<Board> BoardHistory = new Stack<Board>();
        readonly Xorshift[] RAND;

        readonly Board[] BOARD_CACHE;
        readonly Move[][] MOVES_CACHE;

        public MonteCarloEngine(int playoutNum,int threadNum)
        {
            this.PLAYOUT_NUM = playoutNum;
            this.THREAD_NUM = threadNum;
            this.RAND = new Xorshift[this.THREAD_NUM];
            this.BOARD_CACHE = new Board[this.THREAD_NUM];
            this.MOVES_CACHE = new Move[this.THREAD_NUM][];
            for(var threadID = 0; threadID < this.THREAD_NUM; threadID++)
            {
                this.RAND[threadID] = new Xorshift();
                this.BOARD_CACHE[threadID] = new Board();
                this.MOVES_CACHE[threadID] = new Move[Board.BOARD_SIZE * Board.BOARD_SIZE];
            }
            this.board = new Board();
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

        public void ClearBoard(InitialPosition initPosition)
        {
            this.board = new Board(Color.Black, initPosition);
            this.BoardHistory.Clear();
        }

        public void Play(Color color,int posX,int posY)
        {
            if (color != this.board.Turn)
                this.board.ChangeCurrentTurn(color);
            try
            {
                if (!this.board.Update(color, posX, posY))
                    throw new RVTPException("illegal move", false);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new RVTPException("illegal coordinate", false);
            }
        }

        public void Put(Color color,int posX,int posY)
        {
            try
            {
                this.board.PutDisc(color, posX, posY);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new RVTPException("invalid coordinate", false);
            }catch(ArgumentException ex)
            {
                if (ex.Message == "Not empty.")
                    throw new RVTPException("board not empty.", false);
            }
        }

        public List<(int x, int y)> SetHandicap(int num)
        {
            try
            {
                return this.board.SetHandicap(num);
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

        public string LoadSGF(string path,int posX,int posY)
        {
            throw new RVTPException("not supported", false);
        }

        public string LoadSGF(string path,int moveNum)
        {
            throw new RVTPException("not supported", false);
        }

        public string GenerateMove(Color color)
        {
            if (color != this.board.Turn)
                this.board.ChangeCurrentTurn(color);
            var move = GetNextMove(color);
            this.BoardHistory.Push((Board)this.board.Clone());
            this.board.Update(move);
            return Board.MoveToString(move);
        }

        public string RegGenerateMove(Color color)
        {
            if (color != this.board.Turn)
                this.board.ChangeCurrentTurn(color);
            return Board.MoveToString(GetNextMove(color));
        }

        public void Undo()
        {
            if (this.BoardHistory.Count == 0)
                throw new RVTPException("cannot undo", false);
            this.board = this.BoardHistory.Pop();
        }

        public void SetTime(int mainTime,int countdownTime,int countdownNum)
        {
            return;     //MonteCarloEngine ignore time left, and simulation count have priority.
        }

        public void SendTimeLeft(int timeLeft,int countdownNumLeft)
        {
            return;     //MonteCarloEngine ignore time left, and simulation count have priority.
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

        public Color GetColor(int posX,int posY)
        {
            return this.board.GetColor(posX, posY);
        }

        public string[] GetOriginalCommands()
        {
            return new string[0];
        }

        public string ExecuteOriginalCommand(string command,string[] args)
        {
            throw new RVTPException("invalid command.", false);
        }

        Move GetNextMove(Color color)
        {
            var moves = new Move[Board.MAX_MOVE_NUM];
            var movesNum = this.board.GetNextMoves(moves);
            var values = new float[movesNum];

            for (var i = 0; i < movesNum; i++)
            {
                var board = new Board();
                this.board.CopyTo(board);
                board.Update(moves[i]);
                values[i] = Playout(color, board);
            }

            var idx = 0;
            for (var i = 0; i < movesNum; i++)
                if (values[i] > values[idx])
                    idx = i;

            return moves[idx];
        }

        float Playout(Color turn, Board board)
        {
            var scoreSum = new float[this.THREAD_NUM];
            Parallel.For(0, this.THREAD_NUM, (threadID) => scoreSum[threadID] = Simulate(board, turn, threadID));
            for (var i = 0; i < this.PLAYOUT_NUM % this.THREAD_NUM; i++)
                Simulate(board, turn, 0);
            return scoreSum.Average();
        }

        float Simulate(Board board, Color turn, int threadID)
        {
            var boardCache = this.BOARD_CACHE[threadID];
            var movesCache = this.MOVES_CACHE[threadID];
            var scoreSum = 0.0f;
            for (var i = 0; i < this.PLAYOUT_NUM / this.THREAD_NUM; i++)
            {
                board.CopyTo(boardCache);
                GameResult result;
                while ((result = boardCache.GetResult(turn)) == GameResult.NotEnd)
                {
                    var num = boardCache.GetNextMoves(movesCache);
                    boardCache.Update(movesCache[this.RAND[threadID].Next((uint)num)]);
                }
                scoreSum += GetScore(result);
            }
            return scoreSum;
        }

        static float GetScore(GameResult result)
        {
            switch (result)
            {
                case GameResult.Win:
                    return 1.0f;

                case GameResult.Lose:
                    return 0.0f;

                default:
                    return 0.5f;
            }
        }
    }
}
