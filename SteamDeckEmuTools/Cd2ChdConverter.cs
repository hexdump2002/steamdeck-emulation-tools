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
using static System.Net.Mime.MediaTypeNames;

namespace SteamDeckEmuTools {

    enum ConvertFileReturn { ConversionError = 0, UnknownFileFormat = 1,Success=20 }

    public record GroupBestPick {
        public string? LayoutFile { get; set; }
        public string ImgFile { get; set; }

        public GroupBestPick(string? layoutFile, string imgFile) {
            LayoutFile = layoutFile;
            ImgFile = imgFile;
        }

        public string GetBestFileForConversion() {
            return LayoutFile ?? ImgFile;
        }
    }

    static class Cd2ChdConverter {

        static public int Convert(CommandLineVerbs.Cd2ChdParser options) {
            string sourceFiles = options.sourceFiles;
            string cdImageFile = options.cdImage;
            string outputFolder = options.outputFolder;

            bool deleteOriginal = options.deleteOriginal;


            if (!string.IsNullOrEmpty(cdImageFile)) {
                if(File.Exists(cdImageFile)) _ProcessSingleImage(cdImageFile, outputFolder, deleteOriginal);
            }
            else if (Directory.Exists(sourceFiles)) {
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
            process.WaitForExit();

            if (process == null || (process != null && process.ExitCode != 0)) // failed to start
{
                return ConvertFileReturn.ConversionError;
            }
            return ConvertFileReturn.Success;

        }

        static private ConvertFileReturn _ConvertCcd2Cue(string ccdLayoutFile, string outputDir) {
            string currentFolder = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            
            string fileNameNoExt = Path.GetFileNameWithoutExtension(ccdLayoutFile);
            string outputFile = Path.Join(outputDir, fileNameNoExt + ".cue");
            string imageFile = fileNameNoExt + ".img";
            string command = null;
            
            command = $"\"{currentFolder}/bintools/ccd2cue.exe\" --input \"{ccdLayoutFile}\" --output \"{outputFile}\" --image \"{imageFile}\"";

            Log.Logger.Information(command);
            var process = Process.Start(command);
            if (process == null) // failed to start
            {
                return ConvertFileReturn.ConversionError;
            }

            process.WaitForExit();
            return ConvertFileReturn.Success;

        }

        private static ConvertFileReturn _ConvertCcdImg(string filePath, string outputPath) {
            string ext = Path.GetExtension(filePath).ToLower();
            if (ext == ".ccd") {
                Log.Logger.Information("Converting .ccd to .cue...");
                ConvertFileReturn ret = _ConvertCcd2Cue(filePath, Path.GetDirectoryName(outputPath));
                if (ret == ConvertFileReturn.ConversionError) {
                    Log.Logger.Information($"Ccd {filePath} couldn't be converter to .cue");
                }
                else {
                    string cueFile = Path.ChangeExtension(filePath, ".cue");
                    _convertBinCue(cueFile, outputPath);
                }
            }
            string fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
            string outputFilePath = Path.Join(outputPath, fileNameNoExt + ".chd");
            ConvertFileReturn result = _ConvertToChd(filePath, outputFilePath);
            return result;
        }

        static private ConvertFileReturn _convertBinCue(string filePath, string outputPath) {
            string ext = Path.GetExtension(filePath);
            if (ext == ".cue") {
                Log.Logger.Information("Verifying cue file...");
                bool ok = CdService.VerifyCueFileDataImgPath(filePath);
                if (!ok) {
                    Log.Logger.Information("Cd data file in cue file is not correct. Fixing it...");
                    //FixCueFileDataImgDataPath(filePath);
                }
            }
            string fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
            string outputFilePath = Path.Join(outputPath, fileNameNoExt + ".chd");
            ConvertFileReturn result = _ConvertToChd(filePath, outputFilePath);
            return result;
        }

        static private ConvertFileReturn _ConvertIso(string filePath, string outputPath) {
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
                    case ".bin":
                        _convertBinCue(pick.LayoutFile, outputFilePath);
                        break;
                    case ".ccd":
                    case ".img":
                        _ConvertCcdImg(pick.LayoutFile, outputFilePath);
                        break;
                    case ".iso":
                        _ConvertIso(pick.LayoutFile, outputFilePath);
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
                        _ConvertIso(pick.ImgFile, outputFilePath);
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


            foreach (GroupBestPick bestVer in bestVers) {
                string fileName = bestVer.GetBestFileForConversion();
                fileName = Path.GetRelativePath(outputFolder, fileName);
                if (!StringService.IsASCII(fileName))
                    Log.Logger.Information($"File {Path.GetRelativePath(outputFolder, fileName)} contains non ASCII chars not supported by chdman. Please rename it. Skipping this one!:..");
                else {
                    Log.Logger.Information($"Converting file {fileName}");
                    _ConvertFile(bestVer, outputFolder);
                }
                
            }
        }

        private static void _ProcessSingleImage(string cdImageFile, string outputFolder, bool deleteOriginal) {
            if (!File.Exists(cdImageFile)) {
                Log.Logger.Error($"Cd Image {cdImageFile} does not exist...");
                return;
            }

            string loggingName = Path.GetFileName(cdImageFile);

            Log.Logger.Information("Generating image file group...");

            string folder = Path.GetDirectoryName(cdImageFile)!;
            string nameNoExt = Path.GetFileNameWithoutExtension(cdImageFile);

            List<List<string>> groups = CdService.GetImageFiles(folder!, nameNoExt);

            if (groups.Count > 1) throw new Exception("More than one group was created from the cd image {cdImageFile}. This is not possible.");
            if (groups.Count == 0) throw new Exception($"No groups were created from the cd image {loggingName}. This is not possible.");

            List<string> group = groups[0];

            Log.Logger.Information($"Calculating best file version for {loggingName}");

            GroupBestPick pick = CdService.PickBestFormatsForConversionInGroup(group);
            if (pick == null)
                Log.Logger.Warning("Skiping game...");
            else {
                Log.Logger.Information($"Converting file...");
                string fileName = pick.GetBestFileForConversion();
                fileName = Path.GetRelativePath(Path.GetDirectoryName(cdImageFile), fileName);
                if (!StringService.IsASCII(fileName))
                    Log.Logger.Information($"File {fileName} contains non ASCII chars not supported by chdman. Please rename it. Skipping this one!:..");
                else {
                    Log.Logger.Information($"Converting file {fileName}");
                    _ConvertFile(pick, outputFolder);
                }
            }
            
            
        }
    }
}
