using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

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
            commands.Add("fixed_handicap", ExecuteFixedHandicapCommand);
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
                var args = Console.ReadLine().ToLower().Split(' ');

                try
                {
                    this.COMMANDS[args[0]](args);
                }
                catch (KeyNotFoundException)
                {
                    RvtpFailure("unknown command", false);
                }
            }
            this.Quit = false;
        }

        void ExecuteProtocolVersionCommand(string[] args)
        {
            ExecuteCommand(() => VERSION);
        }

        void ExecuteQuitCommand(string[] args)
        {
            ExecuteCommand(()=> 
            {
                this.ENGINE.Quit();
                return string.Empty;
            });
        }

        void ExecuteNameCommand(string[] args)
        {
            ExecuteCommand(this.ENGINE.GetName);
        }

        void ExecuteVersionCommand(string[] args)
        {
            ExecuteCommand(this.ENGINE.GetVersion);
        }

        void ExecuteClearBoardCommand(string[] args)
        {
            ExecuteCommand(() =>
            {
                if (args.Length == 1)
                    throw new RVTPException("invalid option", false);

                if (args[1] == "cross")
                    this.ENGINE.ClearBoard(InitialPosition.Cross);
                else if (args[1] == "parallel")
                    this.ENGINE.ClearBoard(InitialPosition.Parallel);
                else if (args[1] == "original")
                    this.ENGINE.ClearBoard(InitialPosition.Original);
                else
                    throw new RVTPException("invalid option", false);

                return string.Empty;
            });
        }

        void ExecutePlayCommand(string[] args)
        {
            ExecuteCommand(() =>
            {
                var color = StringToColor(args[1]);
                var (x, y) = StringToPosition(args[2]);
                this.ENGINE.Play(color, x, y);
                return string.Empty;
            });
        }

        void ExecuteFixedHandicapCommand(string[] args)
        {
            ExecuteCommand(() =>
            {
                if (int.TryParse(args[1], out int num))
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
            ExecuteCommand(() =>
            {
                if (args.Length == 1)
                    throw new RVTPException("cannnot open or parse \'\'", false);

                if (args.Length == 2)
                    return this.ENGINE.LoadSGF(args[1]);

                if (int.TryParse(args[2], out int moveNum))
                    return this.ENGINE.LoadSGF(args[1], moveNum);
                else
                {
                    var (x, y) = StringToPosition(args[2]);
                    return this.ENGINE.LoadSGF(args[1], x, y);
                }
            });
        }

        void ExecuteGenMoveCommand(string[] args)
        {
            ExecuteCommand(() =>
            {
                if (args.Length < 2)
                    throw new RVTPException("invalid option", false);
                var color = StringToColor(args[1]);
                return this.ENGINE.GenerateMove(color);
            });
        }

        void ExecuteRegGenMoveCommand(string[] args)
        {
            ExecuteCommand(() =>
            {
                if (args.Length < 2)
                    throw new RVTPException("invalid option", false);
                var color = StringToColor(args[1]);
                return this.ENGINE.RegGenerateMove(color);
            });
        }

        void ExecuteUndoCommand(string[] args)
        {
            ExecuteCommand(() =>
            {
                this.ENGINE.Undo();
                return string.Empty;
            });
        }

        void ExecuteTimeSettingsCommand(string[] args)
        {
            ExecuteCommand(() =>
            {
                if (int.TryParse(args[1], out int time) && int.TryParse(args[2], out int countDownTime) && int.TryParse(args[3], out int countDownNum))
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
            ExecuteCommand(() =>
            {
                if (int.TryParse(args[1], out int time) && int.TryParse(args[2], out int countDownNumLeft))
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
            ExecuteCommand(this.ENGINE.GetFinalScore);
        }

        void ExecuteShowBoardCommand(string[] args)
        {
            ExecuteCommand(()=> 
            {
                for (var x = 0; x < BOARD_SIZE; x++)
                    for (var y = 0; y < BOARD_SIZE; y++)
                        this.Board[x,y] = this.ENGINE.GetColor(x, y);
                return BoardToString(this.Board);
            });
        }

        void ExecuteHelpCommand(string[] args)
        {
            ExecuteCommand(()=> 
            {
                var ret = string.Empty;
                foreach (var cmd in this.COMMANDS.Keys)
                    ret += cmd + '\n';
                return ret;
            });
        }

        void ExecuteKnowCommandCommand(string[] arg)
        {
            ExecuteCommand(() => 
            {
                if (arg.Length < 2)
                    throw new RVTPException("invalid option", false);
                return this.COMMANDS.ContainsKey(arg[1]).ToString(); 
            });
        }

        void ExecuteOriginalCommand(string[] args)
        {
            ExecuteCommand(() => this.ENGINE.ExecuteOriginalCommand(args[0], args));
        }

        void ExecuteCommand(Func<string> command)
        {
            try
            {
                RvtpSuccess(command());
            }
            catch (Exception ex)
            {
                CatchException(ex);
            }
        }

        void CatchException(Exception ex)
        {
            if (ex is RVTPException rvtpErr)
                RvtpFailure(rvtpErr.Message, rvtpErr.IsFatal);
            else
                RvtpFailure($"{ex.Message}\n{ex.StackTrace}", true);
        }

        void RvtpSuccess(string msg)
        {
            Console.WriteLine($"=\n {msg}");
        }

        void RvtpFailure(string msg,bool isFatal)
        {
            if (isFatal)
            {
                Console.WriteLine($"\nFATAL ERROR\n?{msg}");
                this.Quit = true;
            }
            else
                Console.WriteLine($"\n? {msg}");
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
