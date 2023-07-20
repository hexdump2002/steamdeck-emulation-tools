using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace SteamDeckEmuTools {
    class CommandLineVerbs {
        [Verb("cd2chd", HelpText = "Given a folder or a file name (iso, cue, img,ccd) it will be convertd to chd format")]
        public class Cd2ChdParser {
            [Option("source-folder",SetName ="batchSrcFiles",  Required = true, HelpText = "Folder where the cd imgs are to be processed in batch")]
            public string sourceFiles { get; set; } = null!;

            [Option("cd-image",SetName ="cdSrcFile", Required = true, HelpText = "Folder where the cd imgs are to be processed in batch")]
            public string cdImage { get; set; } = null!;

            [Option("output-folder", Required = true, HelpText = "Can be a path to a folder or a file")]
            public string outputFolder { get; set; } = null!;

            [Option("delete-original", Default =false , Required= false, HelpText = "Delete original cd images to save space")]
            public bool deleteOriginal { get; set; }

        }

        [Verb("verify-cd-layouts", HelpText = "Checks all cd layout files like cue, ccd, etc. for errors")]
        public class CdLayoutVerifierParser {
            [Option("source-folder", SetName = "batchSrcFiles", Required = true, HelpText = "Folder where the cd imgs are to be verified in batch")]
            public string sourceFiles { get; set; } = null!;

            [Option("cd-image", SetName = "cdSrcFile", Required = true, HelpText = "Single cd image to verify")]
            public string cdImage { get; set; } = null!;
            
            [Option("fix", Default = false, Required = false, HelpText = "If error found fix them")]
            public bool fix { get; set; }

        }
    }
}
