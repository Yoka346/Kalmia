using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Kalmia
{
    public class TextGameRecord
    {
        readonly string TEXT;

        public TextGameRecord(string path)
        {
            this.TEXT = string.Empty;
            using(var sr=new StreamReader(path))
            {
                while (sr.Peek() != -1)
                    this.TEXT += sr.ReadLine().Replace(" ", string.Empty).Replace("\n", string.Empty);
            }
        }

        public string[] GetMoves()
        {
            var moves = new List<string>();
            for(var i = 0; i < this.TEXT.Length; i += 2)
                moves.Add((this.TEXT[i].ToString() + this.TEXT[i + 1].ToString()).ToLower());
            return moves.ToArray();
        }
    }
}
