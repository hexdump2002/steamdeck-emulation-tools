using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;

namespace SteamDeckEmuTools {

    enum ConvertFileReturn { ConversionError = 0, UnknownFileFormat = 1,Success=20 }

    public record GroupBestPick {
        public string? LayoutFile { get; set; }
        public string ImgFile { get; set; }

        public GroupBestPick(string? layoutFile, string imgFile) {
            LayoutFile = layoutFile;
            ImgFile = imgFile;
        }
    }
    
    static class Cd2ChdConverter {

        static public int Convert(CommandLineVerbs.Cd2ChdParser options) {
            string sourceFiles = options.sourceFiles;
            string cdImageFile = options.cdImage;
            string outputFolder = options.outputFolder;
            
            bool deleteOriginal = options.deleteOriginal;

            var bestVers = new List<string>();

            bool sourceFilesExist = false;

            if (!string.IsNullOrEmpty(cdImageFile) && File.Exists(cdImageFile))
                _ProcessSingleImage(cdImageFile, outputFolder, deleteOriginal);
            else if(Directory.Exists(sourceFiles)) {
                _ProcessBatchFolder(sourceFiles, outputFolder, deleteOriginal);
            }


            return 0;
        }

        //Img file is used when we use a lauout file as filepath because sometimes the data imaged linked is not valid (was renamed) so
        //Better to point to it authomatically
        static private ConvertFileReturn _ConvertToChd(string inputFile, string outputDir) {
            string currentFolder = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

            string command = null;

           
           
            command = $"\"{currentFolder}/bintools/chdman.exe\" createcd -i \"{inputFile}\" -o \"{outputDir}\"";

            Log.Logger.Information(command);
            var process = Process.Start(command);
            if (process == null) // failed to start
            {
                return ConvertFileReturn.ConversionError;
            }
         
            process.WaitForExit();
            return ConvertFileReturn.Success;
            
        }

        private static void _ConvertCcdImg(string filePath, string outputFilePath) {
            throw new NotImplementedException();
        }

        static private ConvertFileReturn _convertBinCue(string filePath, string outputPath) {
            string ext = Path.GetExtension(filePath);
            if (ext == ".cue") {
                Log.Logger.Information("Verifying cue file...");
                bool ok = CdService.VerifyCueFileDataImgPath(filePath);
                if(!ok) {
                    Log.Logger.Information("Cd data file in cue file is not correct. Fixing it...");
                    //FixCueFileDataImgDataPath(filePath);
                }
            }
            string fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
            string outputFilePath = Path.Join(outputPath, fileNameNoExt + ".chd");
            ConvertFileReturn result = _ConvertToChd(filePath, outputFilePath);
            return result;
        }

        static private ConvertFileReturn _convertIso(string filePath, string outputPath) {
            string fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
            string outputFilePath = Path.Join(outputPath, fileNameNoExt + ".chd");
            ConvertFileReturn result = _ConvertToChd(filePath, outputFilePath);
            return result;
        }


        static private ConvertFileReturn _ConvertFile(GroupBestPick pick, string outputFilePath) {
            
            if (!Directory.Exists(outputFilePath)) {
                Directory.CreateDirectory(outputFilePath);
            }

            if (pick.LayoutFile != null) {
                string ext = Path.GetExtension(pick.LayoutFile).ToLower();
                if (string.IsNullOrEmpty(ext) || !CdService.IsCdLayout(pick.LayoutFile))
                    return ConvertFileReturn.UnknownFileFormat;

                switch (ext) {
                    case ".cue":
                        _convertBinCue(pick.LayoutFile, outputFilePath);
                        break;
                    case ".ccd":
                        //_convertCcdImg(pick, outputFilePath);
                        break;
                }
            }
            else {
                string ext = Path.GetExtension(pick.ImgFile).ToLower();
                if (string.IsNullOrEmpty(ext) || !CdService.IsCdImageData(pick.ImgFile))
                    return ConvertFileReturn.UnknownFileFormat;

                switch (ext) {
                    case ".img":
                        //_convertCcdImg(filePath, outputFilePath);
                        break;
                    case ".bin":
                        _convertBinCue(pick.ImgFile, outputFilePath);
                        break;
                    case ".iso":
                        _convertIso(pick.ImgFile, outputFilePath);
                        break;
                    default:
                        return ConvertFileReturn.UnknownFileFormat;

                }
            }

            return ConvertFileReturn.Success;

        }


        

        private static void _ProcessBatchFolder(string sourceFiles, string outputFolder, bool deleteOriginal) {
            var bestVers = new List<GroupBestPick>();

            Log.Logger.Information("Generating image file groups...");
            List<List<string>> groups = CdService.GetImageFiles(sourceFiles);

            
            Log.Logger.Information("Calculating best file versions...");
            foreach (List<string> group in groups) {
                GroupBestPick pick = CdService.PickBestFormatsForConversionInGroup(group);
                if (pick == null) 
                    Log.Logger.Warning("Skiping game...");
                else
                    bestVers.Add(pick);
            }

            //Debug.Assert(groups.Count == bestVers.Count);

            /*Log.Logger.Information($"We are going to convert {bestVers.Count} cd images");
            Log.Logger.Information("===================================");
            foreach (string bestVer in bestVers) {
                Log.Logger.Information(bestVer);
            }*/

            
            foreach(GroupBestPick bestVer in bestVers) {
                Log.Logger.Information($"Converting file {bestVer}");
                _ConvertFile(bestVer, outputFolder);
            }
        }

        private static void _ProcessSingleImage(string cdImageFile, string outputFolder, bool deleteOriginal) {
            throw new NotImplementedException();
        }
    }
}
