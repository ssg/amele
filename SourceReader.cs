using System;
using System.IO;

namespace amele
{
    class SourceReader : IDisposable
    {
        private TextReader reader;

        public int CurrentLine = 0;

        public SourceReader(string fileName)
        {
            this.reader = File.OpenText(fileName);
            CurrentLine = 0;
        }

        public bool TryReadLine(out string line)
        {
            line = reader.ReadLine()?.Trim();
            CurrentLine++;
            return line != null;
        }

        public void Dispose()
        {
            reader.Dispose();
            reader = null;
        }
    }
}
