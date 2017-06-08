using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommandLine;
using CommandLine.Text;

namespace Archivator.ConsoleApp
{
    /// <summary>
    /// A class to receive parsed command line parameters
    /// </summary>
    public class CommandLineOptions
    {
        [Option('s', "source", HelpText = "Original file name.", Required = true)]
        public string SourceFileName { get; set; }

        [Option('r', "result", HelpText = "Result file name.", Required = true)]
        public string ResultFileName { get; set; }

        [Option('c', "compress", HelpText = "Compress file.", MutuallyExclusiveSet = "action")]
        public bool IsCompress { get; set; }

        [Option('d', "decompress", HelpText = "Decompress file.", MutuallyExclusiveSet = "action")]
        public bool IsDecompress { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
