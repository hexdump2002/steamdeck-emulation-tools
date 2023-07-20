using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Serilog;

namespace SteamDeckEmuTools {
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
            var dataTrack = tracks.OrderBy(o=>o);

            return dataTrack.Count() > 0 ? dataTrack.First() : null;
        }

        static public GroupBestPick PickBestFormatsForConversionInGroup(List<string> group) {
            Debug.Assert(group.Count() > 0);
            
            var dataImages = GetDataImagesInGroup(group);

            string? imgLayoutFile = GetBestFileLayoutForConversionInGroup(group);
            string? imgDataFile = GetDataTrackInGroup(group);
            
            GroupBestPick? pick = null;
            
            if (dataImages.Count == 0) {
                if (imgLayoutFile == null)
                    Log.Logger.Error("\t# There are no data img or layout in the group");
                else
                    Log.Logger.Error($"\t# The layout file  do not have any data image file associated");
            }
            else if (dataImages.Count > 1) {
                if (imgLayoutFile == null) {
                    Log.Logger.Error("\t# There is more than one img data file in the group. This is not valid if no cue file exists.");
                }
                else {
                    Log.Logger.Information($"\t# There is more than one Track associated with the layoutfile");
                    string? dataTrack = CdService.GetDataTrackInGroup(group);
                    if(dataTrack==null) {
                        Log.Logger.Information("\t# It was not possible to find the data track for this cd image");
                     }
                    else {
                        Log.Logger.Information($"\t# We have selected the data track file -> {Path.GetFileName(dataTrack!)}");
                        pick = new GroupBestPick(imgLayoutFile, dataTrack);
                    }
                }
            }
            else
                pick = new GroupBestPick(imgLayoutFile,imgDataFile);

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


        static public List<List<string>> GetImageFiles(string searchFolder, string? partialName=null) {

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
            
            /*foreach (List<string> group in gameGroups) {
                Log.Logger.Information("===========================");
                foreach (string file in group) {
                    Log.Logger.Information(file);
                }
            }*/

            /*
            while(validFiles.Count > 0) {
                var group = new List<string>();
                if(validFiles.Count == 1) {
                    group.Add(validFiles[0]);
                    gameGroups.Add(group);
                    break;
                }

                string file = validFiles[0];
                string fileName = Path.GetFileNameWithoutExtension(file);

                group.Add(file);

                for(int i =1; i < validFiles.Count;++i) {
                    string choice = Path.GetFileNameWithoutExtension(validFiles[i]);

                    if (choice == fileName) group.Add(validFiles[i]);
                }

                gameGroups.Add(group);

                foreach(string fileInGroup in group) {
                    validFiles.Remove(fileInGroup);
                }

            }*/

            return gameGroups;
        }


        static public bool VerifyCueFileDataImgPath(string cueFilePath) {
            CueFile cf = new CueFile(cueFilePath);
            cf.Read();
            
            string? folder = Path.GetDirectoryName(cueFilePath);

            if (folder != null)
                return File.Exists(Path.Join(folder, cf.BinFile));

            return false;
        }

    }
}
