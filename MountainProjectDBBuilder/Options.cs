using CommandLine;

namespace MountainProjectDBBuilder
{
    public class Options
    {
        //The three below should probaby be a "ProgramMode" arg, but to maintain backwards compatibility with pipelines and whatnot, I'm going to keep it the same
        [Option("build", HelpText = "Build xml from MountainProject")]
        public bool Build { get; set; }

        [Option("parse", HelpText = "Parse an input string")]
        public bool Parse { get; set; }

        [Option("benchmark", HelpText = "Run benchmark test (only parse Alabama and write out a stats file)")]
        public bool Benchmark { get; set; }

        [Option("onlyNew", HelpText = "Only add new items since the last time the database was built")]
        public bool OnlyNew { get; set; }

        [Option("filetype", Default = FileType.XML, HelpText = "File type to serialize as (xml or json - xml is default)")]
        public FileType FileType { get; set; }

        [Option("download", HelpText = "Download xml file from Google Drive")]
        public string DownloadUrl { get; set; }
    }
}