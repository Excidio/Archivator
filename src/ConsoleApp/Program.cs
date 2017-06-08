using System;
using System.IO;
using Archivator.GzipArchivator;
using CommandLine;

namespace Archivator.ConsoleApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var options = new CommandLineOptions();
            var parser = new Parser(s =>
            {
                s.CaseSensitive = false;
                s.IgnoreUnknownArguments = false;
                s.MutuallyExclusive = true;
            });

            if (parser.ParseArguments(args, options))
            {
                FileStream sourceStream = null, targetStream = null;

                try
                {
                    OpenFiles(options.SourceFileName, options.ResultFileName, out sourceStream, out targetStream);

                    if (options.IsCompress)
                    {
                        new Compressor().Compress(sourceStream, targetStream);
                    }
                    else if (options.IsDecompress)
                    {
                        new Decompressor().Decompress(sourceStream, targetStream);
                    }
                    else
                    {
                        Console.WriteLine("You must select action: -c(compress) or -d(decompress)!");
                    }
                }
                catch (DirectoryNotFoundException)
                {
                    Console.WriteLine("Directory not found.");
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine("File not found.");
                }
                catch (OutOfMemoryException)
                {
                    Console.WriteLine("Memory is over, please, clear memory(close several applications) and try again.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unexcpected error: {0}. Please contact with developer!", ex);
                }
                finally
                {
                    sourceStream?.Close();
                    targetStream?.Close();
                }
            }
            else
            {
                Console.WriteLine(options.GetUsage());
                Console.ReadKey();
            }
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
