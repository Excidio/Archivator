using System.IO;
using Archivator.GzipArchivator;

namespace Archivator.ConsoleApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            FileStream sourceStream, targetStream;
            //OpenFiles(@"D:\Test\1.gz", @"D:\Test\1.mkv", out targetStream, out sourceStream);
            //new Compressor().Compress(targetStream, sourceStream);

            OpenFiles(@"D:\Test\res.mkv", @"D:\Test\1.gz", out targetStream, out sourceStream);
            new Decompressor().Decompress(targetStream, sourceStream);

            sourceStream.Close();
            targetStream.Close();
        }
        private static void OpenFiles(string sourceFileName, string targetFileName,
            out FileStream sourceFileStream, out FileStream targetFileStream)
        {
            sourceFileStream = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            try
            {
                targetFileStream = new FileStream(targetFileName, FileMode.Truncate);
            }
            catch (FileNotFoundException)
            {
                targetFileStream = new FileStream(targetFileName, FileMode.CreateNew);
            }
        }
    }
}
