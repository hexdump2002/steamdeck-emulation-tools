using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Routing.Constraints;
using Serilog;

namespace SteamDeckEmuTools {
    class Track {
        public int Order { get; set; }
        public string Mode { get; set; }
    }
    
    
    class CueFile {
        List<Track> _tracks = new List<Track>();
        public string BinFile { get; set; } = "";

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

        Track _ReadTrackEntry(string line) {

            var match = Regex.Match(line, "TRACK\\s+(\\d+)\\s([\\w\\d\\/]+)");
            if (match.Success) {
                Track track = new Track();
                track.Order = int.Parse(match.Groups[1].Value);
                track.Mode = match.Groups[2].Value;
                return track;
            }

            return null;

        }

        public bool Read() {
            foreach (var line in File.ReadAllLines(_filePath)) {
                
                if(_IsFileEntry(line)) {
                    string fileName = string.Empty;
                    bool isOk = _ReadFileEntry(line, out fileName);
                    if(!isOk) {
                        Log.Logger.Error($"There was a problem parsing file {_filePath} (FILE ENTRY)");
                        return false;
                    }
                    BinFile = fileName;

                }
                else if (_IsTrackEntry(line)) {
                    Track track = _ReadTrackEntry(line);
                    if(track==null) {
                        Log.Logger.Error($"There was a problem parsing file {_filePath} (TRACK ENTRY)");
                        return false;
                    }
                    _tracks.Add(track);
                }
            }

            return true;
        }

        internal void WriteNewNameToFile(string newName) {
            string pattern = "FILE\\s+\\\"(.*?)\\\"\\s+BINARY[\\S\\s]*?\\n";
            string contents = File.ReadAllText(_filePath);
            string finalContents = contents.Replace(BinFile,newName);
            File.WriteAllText(_filePath, finalContents);
        }
    }
}
