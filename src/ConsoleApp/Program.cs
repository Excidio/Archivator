using System;
using System.IO;
using Archivator.ConsoleApp.CommandLineOptions;
using Archivator.GzipArchivator;

namespace Archivator.ConsoleApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var parameters = GetParameters(args);
            if (parameters != null)
            {
                FileStream sourceStream = null, targetStream = null;
                try
                {
                    OpenFiles(parameters.SourceFileName, parameters.DestinationFileName, out sourceStream, out targetStream);

                    switch (parameters.ArchivatorAction)
                    {
                        case ArchivatorAction.compress:
                            new Compressor().Compress(sourceStream, targetStream);
                            break;
                        case ArchivatorAction.decompress:
                            new Decompressor().Decompress(sourceStream, targetStream);
                            break;
                        default:
                            Console.WriteLine("Wrong action selected!");
                            break;
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
                    Console.WriteLine("Unexpected error: {0}. Please contact with developer!", ex);
                }
                finally
                {
                    sourceStream?.Close();
                    targetStream?.Close();
                }
            }
        }

        private static CommandLineParameters GetParameters(string[] args)
        {
            CommandLineParameters parameters = null;
            try
            {
                parameters = new CommandLineParser().Parse(args);
            }
            catch (ApplicationException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(ex.Message);
            }

            return parameters;
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
