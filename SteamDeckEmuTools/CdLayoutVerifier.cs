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

    static class CdLayoutVerifier {

        static public int Verify(CommandLineVerbs.CdLayoutVerifierParser options) {
            string sourceFiles = options.sourceFiles;
            string cdImageFile = options.cdImage;
            
            bool fix = options.fix;


            if (!string.IsNullOrEmpty(cdImageFile) && File.Exists(cdImageFile))
                _ProcessSingleImage(cdImageFile, fix);
            else if(Directory.Exists(sourceFiles)) {
                _ProcessBatchFolder(sourceFiles, fix);
            }


            return 0;
        }


        static private void FixCueFileDataImgDataPath(string cueFilePath) {
            bool isOk = CdService.VerifyCueFileDataImgPath(cueFilePath);
            
            if(!isOk) {
                //string cdImage = CdService.GetBestDataImageForLayoutFile(cueFilePath);
                string contents = File.ReadAllText(cueFilePath);
                
            }
        }

        static private bool _VerifyLayoutFile(string layoutFilePath) {
            string ext = Path.GetExtension(layoutFilePath).ToLower();
            if(CdService.IsCdLayout(layoutFilePath)) {
                switch(ext) {
                    case ".cue":
                        return CdService.VerifyCueFileDataImgPath(layoutFilePath);
                        break;
                    case ".ccd":
                        return true;
                        break;
                }
            }

            throw new FormatException("Unkown image format");
        }

        static private bool _FixLayoutFile(string layoutFilePath, string dataTrack) {
            string ext = Path.GetExtension(layoutFilePath);
            if (ext != ".cue") {
                Log.Logger.Information($"Only .cue layout files can be fixed. So {layoutFilePath} will stay the same");
                return false;
            }

            CueFile cf = new CueFile(layoutFilePath);
            cf.Read();
            
            cf.WriteNewNameToFile(Path.GetFileName(dataTrack));

            return true;
        }

        private static void _ProcessBatchFolder(string sourceFiles, bool fix) {
            var bestVers = new List<GroupBestPick>();

            Log.Logger.Information("Generating image file groups...");
            List<List<string>> groups = CdService.GetImageFiles(sourceFiles);

            Log.Logger.Information("Checking cd images state...");
            foreach (var group in groups) {
                List<string> layoutFiles = CdService.GetLayoutFilesInGroup(group);

                foreach (string layoutFile in layoutFiles) {
                    _ProcessSingleImage(layoutFile, fix);
                }

            }
        }

        private static void _ProcessSingleImage(string cdImageFile,  bool fix) {
            Log.Logger.Information("Generating image file group...");

            string folder = Path.GetDirectoryName(cdImageFile)!;
            string nameNoExt= Path.GetFileNameWithoutExtension(cdImageFile);
            List<List<string>> groups = CdService.GetImageFiles(folder!, nameNoExt);

            if (groups.Count > 1) throw new Exception("More than one group was created from the layout image. This is not possible.");
            if (groups.Count == 0) throw new Exception("No groups were created from the layout image. This is not possible.");
            
            List<string> group = groups[0];

            string? layoutFile = CdService.GetBestFileLayoutForConversionInGroup(groups[0]);
            if (layoutFile != null) {
                Log.Logger.Information("Checking cd image state...");
                bool isOk = _VerifyLayoutFile(layoutFile);
                if (!isOk) {
                    Log.Logger.Information($"[State: BAD] {layoutFile}");
                    if (fix) {
                        Log.Logger.Information($"Fixing layout file {layoutFile}");
                        string? dataTrack = CdService.GetDataTrackInGroup(group);
                        if (dataTrack == null) throw new Exception("A layout file must always have an associated data image file");
                        _FixLayoutFile(layoutFile, dataTrack);
                    }
                }
                else
                    Log.Logger.Information($"[State: GOOD] {layoutFile}");
            }

        }
    }
}
