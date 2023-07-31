using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Routing.Constraints;
using Serilog;

namespace SteamDeckEmuTools {
    class CueBinFileTrack {
        public int Order { get; set; }
        public string Mode { get; set; }
    }

     class CueBinFile {
        public CueBinFile(string file) { FileName = file; }
        public string FileName { get; set; } = "";
        public List<CueBinFileTrack> Tracks { get; set; } = new List<CueBinFileTrack>();

        public void AddTrack(CueBinFileTrack track) { Tracks.Add(track); }
    }
    
    class CueFile {
        List<CueBinFile> _files = new List<CueBinFile>();

        private string _filePath = "";

        public CueFile(string cueFilePath) {
            _filePath = cueFilePath;
        }

        bool _IsFileEntry(string line) {
            return line.StartsWith("FILE");
        }
        
        bool _ReadFileEntry(string line, out string fileName) {

            fileName = "";
            
            var match = Regex.Match(line, "^FILE\\s\"(.*?)\"");
            if (match.Success) {
                fileName = match.Groups[1].Value;
                return true;
            }

            return false;
            
        }

        bool _IsTrackEntry(string line) {
            return line.Trim().StartsWith("TRACK");
        }

        CueBinFileTrack _ReadTrackEntry(string line) {

            var match = Regex.Match(line, "TRACK\\s+(\\d+)\\s([\\w\\d\\/]+)");
            if (match.Success) {
                CueBinFileTrack track = new CueBinFileTrack();
                track.Order = int.Parse(match.Groups[1].Value);
                track.Mode = match.Groups[2].Value;
                return track;
            }

            return null;

        }

        public bool Read() {
            CueBinFile current = null;
            foreach (var line in File.ReadAllLines(_filePath)) {
                if(_IsFileEntry(line)) {
                    string fileName = string.Empty;
                    bool isOk = _ReadFileEntry(line, out fileName);
                    if(!isOk) {
                        Log.Logger.Error($"There was a problem parsing file {_filePath} (FILE ENTRY)");
                        return false;
                    }
                    current = new CueBinFile(fileName);
                    _files.Add(current);
                }
                else if (_IsTrackEntry(line)) {
                    CueBinFileTrack track = _ReadTrackEntry(line);
                    if(track==null) {
                        Log.Logger.Error($"There was a problem parsing file {_filePath} (TRACK ENTRY)");
                        return false;
                    }
                    current.AddTrack(track);
                }
            }

            return true;
        }

        public CueBinFile? GetFirstFileWithDataTrack() {
            return _files.Where(o => o.Tracks.Any(o => o.Mode != "AUDIO"))?.First();
        }

        public bool DoesDataFileExistInFolder(string folder) {
            CueBinFile file = GetFirstFileWithDataTrack();
            return File.Exists(Path.Join(folder, file.FileName));
        }

        internal void WriteNewNameToFile(string newName) {
            string pattern = "FILE\\s+\\\"(.*?)\\\"\\s+BINARY[\\S\\s]*?\\n";
            string contents = File.ReadAllText(_filePath);
            CueBinFile file = GetFirstFileWithDataTrack();
            string finalContents = contents.Replace(file.FileName,newName);
            File.WriteAllText(_filePath, finalContents);
            file.FileName = newName;
        }
    }
}
