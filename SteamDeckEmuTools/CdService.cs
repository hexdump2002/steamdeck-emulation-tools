using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Serilog;
using SteamDeckEmuTools;

namespace SteamDeckEmuTools {
    enum GroupStateType { Ok, TooManyImgFormats, NoDataTrack, NoLayoutTrack, Empty,
        InvalidBinFile,
        NoASCIICodes
    }

    class CdService {
        static readonly string[] validExtensions = { ".iso", ".bin", ".cue", ".img", ".ccd", ".sub" };
        static readonly string[] imgExts = { ".iso", ".img", ".bin" };
        static readonly string[] imgLayoutExts = { ".cue", ".ccd" };

        static private int _getFilePoints(string filePath) {
            string ext = Path.GetExtension(filePath).ToLower();

            switch (ext) {
                case ".cue":
                    return 100;
                case ".bin":
                case ".iso":
                    return 70;
                    break;
                case ".ccd":
                    return 40;
                    break;
                case ".img":
                    return 30;
                    break;
                case ".sub":
                    return 0;
                    break;
                default:
                    throw new Exception($"Unknow extension {ext}");

            }
        }

        static public List<string> GetLayoutFilesInGroup(List<string> group) {
            return group.Where(o => IsCdLayout(o)).ToList();
        }


        internal static List<string> GetDataImagesInGroup(List<string> group) {
            return group.Where(o => IsCdImageData(o)).ToList();
        }


        static public string? GetBestFileLayoutForConversionInGroup(List<string> group) {

            var bestVers = new List<string>();
            string bestVer = null;

            int bestVerPoints = 0;

            foreach (string file in group) {
                if (CdService.IsCdLayout(file)) {
                    int points = _getFilePoints(file);
                    if (points > bestVerPoints) {
                        bestVer = file;
                        bestVerPoints = points;
                    }
                }
            }

            return bestVer;
        }

        //Ussually first track is data.
        static public string? GetDataTrackInGroup(List<string> group) {
            List<string> tracks = CdService.GetDataImagesInGroup(group);
            var dataTrack = tracks.OrderBy(o => o);

            return dataTrack.Count() > 0 ? dataTrack.First() : null;
        }

        static public GroupBestPick PickBestFormatsForConversionInGroup(List<string> group) {
            Debug.Assert(group.Count() > 0);

            var dataImages = GetDataImagesInGroup(group);

            string? imgLayoutFile = GetBestFileLayoutForConversionInGroup(group);
            string? imgDataFile = GetDataTrackInGroup(group);

            string loggingName = Path.GetFileNameWithoutExtension(group[0]);

            GroupBestPick? pick = null;

            if (dataImages.Count == 0) {
                if (imgLayoutFile == null)
                    Log.Logger.Error($"\t# There are no data img or layout in the group for game {loggingName}");
                else
                    Log.Logger.Error($"\t# The layout file  do not have any data image file associated for game {loggingName}");
            }
            else if (dataImages.Count > 1) {
                if (imgLayoutFile == null) {
                    Log.Logger.Error($"\t# There is more than one img data file in the group for game {loggingName}. This is not valid if no cue file exists.");
                }
                else {
                    Log.Logger.Information($"\t# There is more than one Track associated with the layoutfile for game {loggingName}");
                    string? dataTrack = CdService.GetDataTrackInGroup(group);
                    if (dataTrack == null) {
                        Log.Logger.Information($"\t# It was not possible to find the data track for cd image for game {loggingName}");
                    }
                    else {
                        Log.Logger.Information($"\t# We have selected the data track file -> {Path.GetFileName(dataTrack!)}");
                        pick = new GroupBestPick(imgLayoutFile, dataTrack);
                    }
                }
            }
            else
                pick = new GroupBestPick(imgLayoutFile, imgDataFile);

            return pick;
        }

        static public bool IsValidFile(string path) {
            return validExtensions.Contains(Path.GetExtension(path).ToLower());
        }

        static public bool IsCdImageData(string path) {
            return imgExts.Contains(Path.GetExtension(path).ToLower());
        }

        static public bool IsCdLayout(string path) {
            return imgLayoutExts.Contains(Path.GetExtension(path).ToLower());
        }

        static public bool IsValidFileExtension(string ext) {
            return validExtensions.Contains(ext.ToLower());
        }

        static public bool IsCdImageDataExtension(string ext) {
            return imgExts.Contains(ext.ToLower());
        }

        static public bool IsCdLayoutExtension(string ext) {
            return imgLayoutExts.Contains(ext.ToLower());
        }


        static public List<List<string>> GetImageFileGroups(string searchFolder, string? partialName = null) {

            DirectoryInfo d = new DirectoryInfo(searchFolder);

            //List<string> files = new List<string>();
            var imgDataFiles = new List<string>();
            var imgLayoutFiles = new List<string>();
            var nonUsableFiles = new List<string>();

            string nameSearch = partialName != null ? partialName + "*" : "*";

            foreach (var file in d.GetFiles(nameSearch)) {
                if (!IsValidFile(file.FullName)) {
                    nonUsableFiles.Add(file.FullName);
                }
                else {
                    if (IsCdImageData(file.FullName)) {
                        imgDataFiles.Add(file.FullName);
                    }
                    else if (IsCdLayout(file.FullName)) {
                        imgLayoutFiles.Add(file.FullName);
                    }
                }
            }


            //We need to do this because some games come with several layout files
            var layoutFileGroups = imgLayoutFiles.GroupBy(
                file => Path.GetFileNameWithoutExtension(file),
                file => file,
                (fileName, files) => files.ToList()
                ).ToList();


            var gameGroups = new List<List<string>>();

            foreach (var layoutFileGroup in layoutFileGroups) {
                //First one is enough, we just want to get base name
                string layoutPickNoExt = Path.GetFileNameWithoutExtension(layoutFileGroup[0]);
                List<string> associatedFiles = imgDataFiles.FindAll(o => Path.GetFileName(o).StartsWith(layoutPickNoExt));
                var group = new List<string>(associatedFiles);
                group.AddRange(layoutFileGroup);
                gameGroups.Add(group);
                imgDataFiles.RemoveAll(o => associatedFiles.Contains(o));
            }

            //Now add standalone bin/img cd images because they seem they are just a datacd

            foreach (var imgDataFile in imgDataFiles) {
                var group = new List<string>();
                group.Add(imgDataFile);
                gameGroups.Add(group);
            }

            return gameGroups;
        }


        static public bool VerifyCueFileDataImgPath(string cueFilePath) {
            CueFile cf = new CueFile(cueFilePath);
            cf.Read();

            string? folder = Path.GetDirectoryName(cueFilePath);

            if (folder != null)
                return cf.DoesDataFileExistInFolder(folder);

            return false;
        }


        static public void LogGroupStates(List<List<string>> groups, List<GroupStateType> groupsStates) {
            if (groups.Count != groupsStates.Count) throw new ArgumentException("Groups and groupStates must have same size");

            List<List<string>> okGroups = groups.Select((value, index) => new { value, index })
                                      .Where(o => groupsStates[o.index] == GroupStateType.Ok)
                                      .Select(o=>o.value)
                                      .ToList();

            List<int> errorGroupIndexes = groups.Select((value, index) => new { value, index })
                                      .Where(o => groupsStates[o.index] != GroupStateType.Ok)
                                      .Select(o => o.index)
                                      .ToList();

            Log.Logger.Information($"There are {okGroups.Count} Ok Games and {errorGroupIndexes.Count} Games with errors");

            foreach(var okGroup in okGroups) {
                string loggingName = Path.GetFileName(okGroup[0]);
                Log.Logger.Information(StringService.Indent($"[OK] {loggingName} is OK", 1));
            }
               

            foreach( var errGroupIndex in errorGroupIndexes) { 
                GroupStateType state = groupsStates[errGroupIndex];
                if (state == GroupStateType.Empty)
                    Log.Logger.Warning(StringService.Indent("An empty group has been generated. Check why, this must not happen.",1));
                else {
                    string loggingName = Path.GetFileName(groups[errGroupIndex][0]);
                    if (state == GroupStateType.NoLayoutTrack)
                        Log.Logger.Warning(StringService.Indent($"[ERROR] {loggingName} needs a layout file to be converted. Use verify command to detect and fix this problem.", 1));
                    else if (state == GroupStateType.NoDataTrack)
                        Log.Logger.Warning(StringService.Indent($"[ERROR] {loggingName} needs a data file to be converted. Did you forget to copy it?", 1));
                    else if (state == GroupStateType.TooManyImgFormats)
                        Log.Logger.Warning(StringService.Indent($"[ERROR] There can't be more than 1 track type for each game. {loggingName}", 1));
                    else if (state == GroupStateType.InvalidBinFile)
                        Log.Logger.Warning(StringService.Indent($"[ERROR] {loggingName} has a layout file but bin file it is pointing to is invalid. Use verify command to detect and fix this problem.", 1));
                    else if (state == GroupStateType.NoASCIICodes)
                        Log.Logger.Warning(StringService.Indent($"[ERROR] {loggingName} contains non ASCII chars that are not supported by chdman. Please rename it.", 1));
                    else
                        throw new Exception("Invalid GroupStateType");
                }

            }
        }

        static public List<GroupStateType> GetGroupStates(List<List<string>> groups) {

            List<GroupStateType> groupStates = new();

            foreach (List<string> group in groups) {
                GroupStateType groupState = GroupStateType.Ok;

                if (group.Count == 0) {
                    groupState = GroupStateType.Empty;
                }
                else {
                    string loggingName = Path.GetFileName(group[0]);

                    var extensionsInGroup = group.GroupBy(
                            file => Path.GetExtension(file),
                            file => file,
                            (ext, files) => ext).ToList();

                    if (extensionsInGroup.Where(o => IsCdImageDataExtension(o)).Count() == 0)
                        groupState = GroupStateType.NoDataTrack;
                    else if (extensionsInGroup.Where(o => IsCdLayoutExtension(o)).Count() == 0)
                        groupState = GroupStateType.NoLayoutTrack;
                    else if (extensionsInGroup.Where(o => IsCdImageDataExtension(o)).Count() > 1)
                        groupState = GroupStateType.TooManyImgFormats;
                    else if (!StringService.IsASCII(loggingName))
                        groupState = GroupStateType.NoASCIICodes;
                    else {
                        string layoutFile = CdService.GetBestFileLayoutForConversionInGroup(group);
                        string ext = Path.GetExtension(layoutFile).ToLower();
                        if (ext == ".cue") {
                            CueFile cueFile = new CueFile(layoutFile);
                            cueFile.Read();
                            if (!cueFile.DoesDataFileExistInFolder(Path.GetDirectoryName(layoutFile)))
                                groupState = GroupStateType.InvalidBinFile;
                        }
                    }
                }

                groupStates.Add(groupState);

            }
            return groupStates;
        }
    }
}
