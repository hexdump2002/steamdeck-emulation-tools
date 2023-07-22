using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Forms;
using Serilog;
using static System.Net.Mime.MediaTypeNames;

namespace SteamDeckEmuTools {

    enum ConvertFileReturn { ConversionError = 0, UnknownFileFormat = 1, ExportedBefore= 2, Success = 20 }

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
                _ProcessSingleImage(cdImageFile, outputFolder, deleteOriginal);
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

            //Log.Logger.Information(command);
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

            Process process = new Process();
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.FileName = $"\"{currentFolder}/bintools/ccd2cue.exe\"";
            //process.OutputDataReceived += (s, e) => Test(e.Data);
            process.StartInfo.Arguments = $"--input \"{ccdLayoutFile}\" --output \"{outputFile}\" --image \"{imageFile}\"";
            process.Start();
            process.WaitForExit();

            if (process == null || (process != null && process.ExitCode != 0)) // failed to start
            {
                return ConvertFileReturn.ConversionError;
            }

            return ConvertFileReturn.Success;

        }

        private static ConvertFileReturn _ConvertCcdImg(string filePath, string outputPath) {
            Log.Logger.Information($"Converting {Path.GetFileName(filePath)}...");
            ConvertFileReturn result = ConvertFileReturn.Success;
            
            string ext = Path.GetExtension(filePath).ToLower();
            if (ext == ".ccd") {
                Log.Logger.Information("Converting .ccd to .cue...");
                result = _ConvertCcd2Cue(filePath, Path.GetDirectoryName(outputPath));
                if (result == ConvertFileReturn.ConversionError) {
                    Log.Logger.Information($"Ccd {filePath} couldn't be converter to .cue");
                }
                else {
                    string cueFile = Path.ChangeExtension(filePath, ".cue");
                    result = _convertBinCue(cueFile, outputPath);
                }
            }
            else { 
                string fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
                string outputFilePath = Path.Join(outputPath, fileNameNoExt + ".chd");
                result = _ConvertToChd(filePath, outputFilePath);
            }
            return result;
        }

        static private ConvertFileReturn _convertBinCue(string filePath, string outputPath) {
            Log.Logger.Information($"Converting {Path.GetFileName(filePath)}...");
            string ext = Path.GetExtension(filePath);
            if (ext == ".cue") {
                Log.Logger.Information("Verifying cue file...");
                bool ok = CdService.VerifyCueFileDataImgPath(filePath);
                if (!ok) {
                    Log.Logger.Information("Cd data file in cue file is not correct. Run verify command and fix it!");
                    //FixCueFileDataImgDataPath(filePath);
                    return ConvertFileReturn.ConversionError;
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

        static private bool _WasFileConverted(string pickFile, string outputPath) {
            string fileNamePath = Path.Join(outputPath,Path.GetFileName(Path.ChangeExtension(pickFile, "chd")));
            if (File.Exists(fileNamePath)) {
                long fileSize = new System.IO.FileInfo(fileNamePath).Length;
                if(fileSize > 1000000)
                    return true;
            }

            return false;
        }
        static private ConvertFileReturn _ConvertFile(GroupBestPick pick, string outputFilePath) {

            //Avoid re-converting files again
            if (_WasFileConverted(pick.GetBestFileForConversion(), outputFilePath)) return ConvertFileReturn.ExportedBefore;

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
        private static ConvertFileReturn _ConvertPick(GroupBestPick pick, string outputFolder) {
            string fileName = pick.GetBestFileForConversion();
            if (CdService.IsCdLayoutExtension(fileName))
                throw new Exception($"{fileName} must be a layout file");

            ConvertFileReturn retval = _ConvertFile(pick, outputFolder);
            if(retval == ConvertFileReturn.ExportedBefore)
                Log.Logger.Information(StringService.Indent($"{fileName} game found in output folder. It won't be exported again", 1));
            return retval;
        }


        private static void _ProcessBatchFolder(string sourceFiles, string outputFolder, bool deleteOriginal) {
            var bestVers = new List<GroupBestPick>();

            Log.Logger.Information("Generating image file groups...");
            List<List<string>> groups = CdService.GetImageFileGroups(sourceFiles);

            Log.Logger.Information("Verifying cd images...");
            List<GroupStateType> groupsState = CdService.GetGroupStates(groups);

            Log.Logger.Information("Groups report...");
            CdService.LogGroupStates(groups,groupsState);

            Log.Logger.Information("Converting cd images...");
            for(int i=0;i<groups.Count;++i) {
                var group = groups[i];
                string loggingName = Path.GetRelativePath(outputFolder, group[0]);

                if (groupsState[i] == GroupStateType.Ok) {
                    GroupBestPick pick = CdService.PickBestFormatsForConversionInGroup(group);
                    bestVers.Add(pick);
                }
                else
                    Log.Logger.Warning(StringService.Indent($"Skiping {loggingName} game...",1));
            }

            foreach (GroupBestPick bestVer in bestVers) {
                _ConvertPick(bestVer, outputFolder);
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

            List<List<string>> groups = CdService.GetImageFileGroups(folder!, nameNoExt);

            if (groups.Count > 1) throw new Exception("More than one group was created from the cd image {cdImageFile}. This is not possible.");
            if (groups.Count == 0) throw new Exception($"No groups were created from the cd image {loggingName}. This is not possible.");

            Log.Logger.Information("Verifying Cd Images...");
            List<GroupStateType> groupsStates = CdService.GetGroupStates(groups);

            Log.Logger.Information("Group report...");
            CdService.LogGroupStates(groups, groupsStates);

            List<string> group = groups[0];
            GroupStateType groupState = groupsStates[0];
            if (groupState == GroupStateType.Ok) {
                Log.Logger.Information(StringService.Indent($"Converting Image {loggingName}",1));
                GroupBestPick pick = CdService.PickBestFormatsForConversionInGroup(group);
                _ConvertPick(pick, outputFolder);
            }
            else
                Log.Logger.Warning(StringService.Indent($"Skiping {loggingName} game...", 1));
          
        }
    }
}
