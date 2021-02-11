using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Kalmia.ReversiTextProtocol
{
    public class RVTPException : Exception
    {
        public bool IsFatal { get; private set; }
        public RVTPException(string message, bool isFatal) : base(message) { this.IsFatal = isFatal; }
    }

    public class RVTP
    {
        const string VERSION = "Beta";
        const int BOARD_SIZE = 8;
        const char BLACK_CHAR = 'X';
        const char WHITE_CHAR = 'O';
        const char BLANK_CHAR = '.';

        readonly IEngine ENGINE;
        readonly ReadOnlyDictionary<string, Action<string[]>> COMMANDS;
        Color[,] Board = new Color[BOARD_SIZE, BOARD_SIZE];
        bool Quit = false;

        public RVTP(IEngine engine)
        {
            this.ENGINE = engine;
            this.COMMANDS = new ReadOnlyDictionary<string, Action<string[]>>(InitCommands());
        }

        Dictionary<string, Action<string[]>> InitCommands()
        {
            var commands = new Dictionary<string, Action<string[]>>();
            commands.Add("protocol_version", ExecuteProtocolVersionCommand);
            commands.Add("quit", ExecuteQuitCommand);
            commands.Add("name", ExecuteNameCommand);
            commands.Add("version", ExecuteVersionCommand);
            commands.Add("clear_board", ExecuteClearBoardCommand);
            commands.Add("play", ExecutePlayCommand);
            commands.Add("put", ExecutePutCommand);
            commands.Add("handicap", ExecuteHandicapCommand);
            commands.Add("loadsgf", ExecuteLoadSGFCommand);
            commands.Add("genmove", ExecuteGenMoveCommand);
            commands.Add("reg_genmove", ExecuteRegGenMoveCommand);
            commands.Add("undo", ExecuteUndoCommand);
            commands.Add("time_settings", ExecuteTimeSettingsCommand);
            commands.Add("time_left", ExecuteTimeLeftCommand);
            commands.Add("final_score", ExecuteFinalScoreCommand);
            commands.Add("showboard", ExecuteShowBoardCommand);
            commands.Add("help", ExecuteHelpCommand);
            commands.Add("know_command", ExecuteKnowCommandCommand);

            foreach (var cmd in this.ENGINE.GetOriginalCommands())
                commands.Add(cmd, ExecuteOriginalCommand);
            return commands;
        }

        public void MainLoop()
        {
            while (!this.Quit)
            {
                var args = Console.ReadLine().ToLower().Split(' '); // argsには0番目に識別番号、1番目にコマンド名、2番目以降は引数が格納される
                int id = -1;

                try
                {
                    id = int.Parse(args[0]);
                    this.COMMANDS[args[1]](args);
                }
                catch (Exception ex) when (ex is KeyNotFoundException || ex is FormatException || ex is OverflowException)
                {
                    RvtpFailure(id, "unknown command", false);
                }
            }
            this.Quit = false;
        }

        void ExecuteProtocolVersionCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), () => VERSION);
        }

        void ExecuteQuitCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), () => 
            {
                this.ENGINE.Quit();
                this.Quit = true;
                return string.Empty;
            });
        }

        void ExecuteNameCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), this.ENGINE.GetName);
        }

        void ExecuteVersionCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), this.ENGINE.GetVersion);
        }

        void ExecuteClearBoardCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), () =>
            {
                if (args.Length != 3)
                    throw new RVTPException("invalid option", false);

                if (args[2] == "cross")
                    this.ENGINE.ClearBoard(InitialPosition.Cross);
                else if (args[2] == "parallel")
                    this.ENGINE.ClearBoard(InitialPosition.Parallel);
                else if (args[2] == "original")
                    this.ENGINE.ClearBoard(InitialPosition.Original);
                else
                    throw new RVTPException("invalid option", false);

                return string.Empty;
            });
        }

        void ExecutePlayCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), () =>
            {
                if (args[3] == "pass")
                {
                    this.ENGINE.Play(StringToColor(args[2]), -1, -1);
                    return string.Empty;
                }

                if (args.Length != 4)
                    throw new RVTPException("invalid option", false);
                var color = StringToColor(args[2]);
                var (x, y) = StringToPosition(args[3]);
                this.ENGINE.Play(color, x, y);
                return string.Empty;
            });
        }

        void ExecutePutCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), () =>
            {
                if (args.Length != 4)
                    throw new RVTPException("invalid option", false);
                var color = StringToColor(args[2]);
                var (x, y) = StringToPosition(args[3]);
                this.ENGINE.Put(color, x, y);
                return string.Empty;
            });
        }

        void ExecuteHandicapCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), () =>
            {
                if (args.Length != 3)
                    throw new RVTPException("invalid option", false);
                if (int.TryParse(args[2], out int num))
                {
                    var positions = this.ENGINE.SetHandicap(num);
                    var ret = string.Empty;
                    foreach (var pos in positions)
                        ret += PositionToString(pos) + ' ';
                    return ret;
                }
                else
                    throw new RVTPException("handicap not an integer", false);
            });
        }

        void ExecuteLoadSGFCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), () =>
            {
                if (args.Length == 2)
                    throw new RVTPException("cannnot open or parse \'\'", false);

                if (args.Length == 3)
                    return this.ENGINE.LoadSGF(args[2]);

                if (int.TryParse(args[3], out int moveNum))
                    return this.ENGINE.LoadSGF(args[2], moveNum);
                else
                {
                    var (x, y) = StringToPosition(args[3]);
                    return this.ENGINE.LoadSGF(args[2], x, y);
                }
            });
        }

        void ExecuteGenMoveCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), () =>
            {
                if (args.Length != 3)
                    throw new RVTPException("invalid option", false);
                var color = StringToColor(args[2]);
                return this.ENGINE.GenerateMove(color);
            });
        }

        void ExecuteRegGenMoveCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), () =>
            {
                if (args.Length != 3)
                    throw new RVTPException("invalid option", false);
                var color = StringToColor(args[2]);
                return this.ENGINE.RegGenerateMove(color);
            });
        }

        void ExecuteUndoCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), () =>
            {
                this.ENGINE.Undo();
                return string.Empty;
            });
        }

        void ExecuteTimeSettingsCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), () =>
            {
                if (int.TryParse(args[2], out int time) && int.TryParse(args[3], out int countDownTime) && int.TryParse(args[4], out int countDownNum))
                {
                    this.ENGINE.SetTime(time, countDownTime, countDownNum);
                    return string.Empty;
                }
                else
                    throw new RVTPException("not there intergers", false);
            });
        }

        void ExecuteTimeLeftCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), () =>
            {
                if (int.TryParse(args[2], out int time) && int.TryParse(args[3], out int countDownNumLeft))
                {
                    this.ENGINE.SendTimeLeft(time, countDownNumLeft);
                    return string.Empty;
                }
                else
                    throw new RVTPException("not there intergers", false);
            });
        }

        void ExecuteFinalScoreCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), this.ENGINE.GetFinalScore);
        }

        void ExecuteShowBoardCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), () => 
            {
                for (var x = 0; x < BOARD_SIZE; x++)
                    for (var y = 0; y < BOARD_SIZE; y++)
                        this.Board[x,y] = this.ENGINE.GetColor(x, y);
                return BoardToString(this.Board);
            });
        }

        void ExecuteHelpCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), () => 
            {
                var ret = string.Empty;
                foreach (var cmd in this.COMMANDS.Keys)
                    ret += cmd + '\n';
                return ret;
            });
        }

        void ExecuteKnowCommandCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), () => 
            {
                if (args.Length != 3)
                    throw new RVTPException("invalid option", false);
                return this.COMMANDS.ContainsKey(args[2]).ToString(); 
            });
        }

        void ExecuteOriginalCommand(string[] args)
        {
            ExecuteCommand(int.Parse(args[0]), () => this.ENGINE.ExecuteOriginalCommand(args[1], args));
        }

        void ExecuteCommand(int id, Func<string> command)
        {
            try
            {
                RvtpSuccess(id, command());
            }
            catch (Exception ex)
            {
                CatchException(id, ex);
            }
        }

        void CatchException(int id, Exception ex)
        {
            if (ex is RVTPException rvtpErr)
                RvtpFailure(id, rvtpErr.Message, rvtpErr.IsFatal);
            else
                RvtpFailure(id, $"{ex.Message}\n{ex.StackTrace}", true);
        }

        void RvtpSuccess(int id, string msg)
        {
            Console.WriteLine($"={id}\n {msg}\n\n");
        }

        void RvtpFailure(int id, string msg,bool isFatal)
        {
            if (isFatal)
            {
                Console.WriteLine($"?{id}\n FATAL_ERROR : {msg}\n");
                this.Quit = true;
            }
            else
                Console.WriteLine($"?{id}\n {msg}\n\n");
        }

        static string BoardToString(Color[,] board)
        {
            var boardStr = " ";
            for (var i = 0; i < BOARD_SIZE; i++)
                boardStr += (char)('A' + i)+" ";
            boardStr += " ";
            
            for(var y = 0; y < BOARD_SIZE; y++)
            {
                boardStr += $"\n{y + 1} ";
                for (var x = 0; x < BOARD_SIZE; x++)
                    if (board[x, y] == Color.Black)
                        boardStr += BLACK_CHAR + " ";
                    else if (board[x, y] == Color.White)
                        boardStr += WHITE_CHAR + " ";
                    else
                        boardStr += BLANK_CHAR + " ";
                boardStr += y + 1;
            }

            boardStr += "\n  ";
            for (var i = 0; i < BOARD_SIZE; i++)
                boardStr += (char)('A' + i)+" ";

            return boardStr;
        }

        static Color StringToColor(string str)
        {
            str = str.Trim().ToLower();
            if (str == "black")
                return Color.Black;
            else if (str == "white")
                return Color.White;
            else
                throw new RVTPException("invalid color", false);
        }

        static (int x,int y) StringToPosition(string str)
        {
            (int x, int y) pos;
            try
            {
                pos = (char.ToLower(str[0]) - 'a', int.Parse(str[1].ToString()) - 1);
            }
            catch
            {
                throw new RVTPException("invalid coordinate", false);
            }

            if (pos.x < 0 || pos.x > BOARD_SIZE - 1 || pos.y < 0 || pos.y > BOARD_SIZE)
                throw new RVTPException("invalid coordinate", false);
            return pos;
        }

        static string PositionToString((int x,int y) pos)
        {
            return ((char)('A' + pos.x) + (pos.y + 1)).ToString();
        }
    }
}
