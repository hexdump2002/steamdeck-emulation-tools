using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamDeckEmuTools {
    public class StringService {
        public static string ConvertToASCIIStr(string s) {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; ++i) {
                if (!char.IsAscii(s[i]))
                    sb.Append('_');
                else
                    sb.Append(s[i]);
            }
            return sb.ToString();
        
        }

        public static bool IsASCII(string s) { 
            foreach (char c in s) {
                if (!char.IsAscii(c))
                    return false;
            }
            return true;
        }

        public static string Indent(string s, int indentLevel) {
            StringBuilder sb = new StringBuilder();
            sb.Append(new string(' ', indentLevel * 4));
            sb.Append("# ");
            sb.Append(s);
            return sb.ToString();
        }
    }

}
