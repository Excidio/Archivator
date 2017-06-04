using System.IO;

namespace Archivator.ConsoleApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            FileStream sourceStream, targetStream;
            OpenFiles(@"D:\Test\1.gz", @"D:\Test\1.mkv", true, out targetStream, out sourceStream);
            new Archivator().Compress(targetStream, sourceStream);

            sourceStream.Close();
            targetStream.Close();
        }

        private static void OpenFiles(string targetFileName, string sourceFileName, bool canOverWrite,
            out FileStream targetFileStream, out FileStream sourceFileStream)
        {
            sourceFileStream = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            if (canOverWrite)
            {
                try
                {
                    targetFileStream = new FileStream(targetFileName, FileMode.Truncate);
                }
                catch (FileNotFoundException)
                {
                    targetFileStream = new FileStream(targetFileName, FileMode.CreateNew);
                }
            }
            else
            {
                targetFileStream = new FileStream(targetFileName, FileMode.CreateNew);
            }
        }
    }
}
