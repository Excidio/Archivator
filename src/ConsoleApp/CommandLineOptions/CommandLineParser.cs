using System;

namespace Archivator.ConsoleApp.CommandLineOptions
{
    public class CommandLineParser
    {
        public CommandLineParameters Parse(string[] args)
        {
            if (args.Length != 3)
            {
                throw new ApplicationException(
                    "Wrong command args length! You should enter args like that: 'compress/decompress [source file name] [destination file name]'");
            }

            return new CommandLineParameters
            {
                ArchivatorAction = (ArchivatorAction)Enum.Parse(typeof(ArchivatorAction), args[0]),
                SourceFileName = args[1],
                DestinationFileName = args[2]
            };
        }
    }
}
