using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
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

        static private bool _FixCueLayoutBinFile(string layoutFilePath, string dataTrack) {
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

        static private bool GenerateCueFileForDataImage(string cueFilePath, string dataTrackFileName) {
            string contents = $"FILE \"{Path.GetFileName(dataTrackFileName)}\" BINARY\r\n   TRACK 1 MODE2/2352\r\n   INDEX 1 00:00:00";
            File.WriteAllText(cueFilePath, contents);            
            return true;
        }

        /*static private bool FixGroupNames(List<string> group) {
            foreach (string filePath in group) {
                string folder = Path.GetDirectoryName(filePath);
                string fileName = Path.GetFileName(filePath);

                fileName = StringService.ConvertToASCIIStr(fileName);
                File.Move(filePath, Path.Join(folder,fileName));
            }
            
            //Fix Cue file now
            CueFile cue = new CueFile()

            return true;
        }*/

        private static void _FixGroup(List<string> group, GroupStateType groupState) {
            List<string> layoutFiles = CdService.GetLayoutFilesInGroup(group);

            if (groupState == GroupStateType.Empty) return;

            string logingFileName = Path.GetFileName(group[0]);


            if (groupState == GroupStateType.TooManyImgFormats ||
                groupState == GroupStateType.NoDataTrack ||
                groupState == GroupStateType.NoASCIICodes) {
                Log.Logger.Warning(StringService.Indent($"Game {logingFileName} SKIPPED because it has uncoverable errors (see report)", 1));
                return;
            }
            else if (groupState == GroupStateType.InvalidBinFile) {
                string layoutFile = CdService.GetBestFileLayoutForConversionInGroup(group)!;
                string dataTrack = CdService.GetDataTrackInGroup(group)!;

                Log.Logger.Information(StringService.Indent($"Fixing Cue Bin file for game {logingFileName}", 1));
                _FixCueLayoutBinFile(layoutFile, dataTrack);
                Log.Logger.Information(StringService.Indent($"Fixed!", 1));
            }
            else if(groupState == GroupStateType.NoLayoutTrack) {
                Log.Logger.Information(StringService.Indent($"Generating Cue file for game {logingFileName}", 1));
                string layoutFile = Path.Join(Path.GetDirectoryName(group[0]), Path.GetFileNameWithoutExtension(group[0])+".cue");
                string dataTrack = CdService.GetDataTrackInGroup(group)!;
                GenerateCueFileForDataImage(layoutFile, dataTrack);
            }

        }


        private static void _ProcessBatchFolder(string sourceFiles, bool fix) {
            var bestVers = new List<GroupBestPick>();

            Log.Logger.Information("Generating image file groups...");
            List<List<string>> groups = CdService.GetImageFileGroups(sourceFiles);

            Log.Logger.Information("Verifying cd images...");
            List<GroupStateType> groupsState = CdService.GetGroupStates(groups);

            Log.Logger.Information("Groups report...");
            CdService.LogGroupStates(groups, groupsState);

            int gamesWithProbs = groupsState.Where(o=>o!=GroupStateType.Ok).Count();

            if(gamesWithProbs > 0 && fix) {
                Log.Logger.Information("Fixing problems...");
                for (int i = 0; i < groupsState.Count; ++i) {
                    List<string> group = groups[i];
                    GroupStateType groupState = groupsState[i];

                    if (groupState != GroupStateType.Ok) _FixGroup(group, groupState);
                }
            }
            
        }

        private static void _ProcessSingleImage(string cdImageFile,  bool fix) {
            Log.Logger.Information("Generating image file group...");

            string folder = Path.GetDirectoryName(cdImageFile)!;
            string nameNoExt= Path.GetFileNameWithoutExtension(cdImageFile);
            List<List<string>> groups = CdService.GetImageFileGroups(folder!, nameNoExt);

            if (groups.Count > 1) throw new Exception("More than one group was created from the layout image. This is not possible.");
            if (groups.Count == 0) throw new Exception("No groups were created from the layout image. This is not possible.");
            
            Log.Logger.Information("Verifying cd images...");
            List<GroupStateType> groupsState = CdService.GetGroupStates(groups);

            List<string> group = groups[0];
            GroupStateType groupState = CdService.GetGroupStates(groups)[0];

            Log.Logger.Information("Groups report...");
            CdService.LogGroupStates(groups, groupsState);

            if (groupState!=GroupStateType.Ok && fix) {
                Log.Logger.Information("Fixing problems...");
                if(groupState != GroupStateType.Ok) _FixGroup(group, groupState);
            }

        }
    }
}
