using System;
using System.Text.RegularExpressions;

namespace PluginTopProcesses
{
    public class Utils
    {
        private static string[] FORMAT_BYTES = new string[] { "B", "KB", "MB", "GB" };

        public static bool WildcardMatch(string input, string pattern)
        {
            return Regex.IsMatch(input, WildcardToRegex(pattern));
        }

        public static string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern.Replace("%", "*")).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        }

        public static string ReplaceString(string format, string key, string measure)
        {
            int startIndex = format.IndexOf(key);
            if (format.IndexOf("s(", StringComparison.CurrentCultureIgnoreCase) == startIndex - 2 && startIndex >= 2)
            {
                startIndex = startIndex - 2;
                int endIndex = format.IndexOf(")", startIndex);
                string fullKey = format.Substring(startIndex, endIndex - startIndex + 1);
                string[] fullKeySplit = fullKey.Replace(" ", "").Split(new char[] { ',', ')', '(' });

                int subStrStart;
                int subStrEnd;
                if (!Int32.TryParse(fullKeySplit[2], out subStrStart) || !Int32.TryParse(fullKeySplit[3], out subStrEnd))
                {
                    subStrStart = 0;
                    subStrEnd = measure.Length;
                }
                else
                {
                    if (subStrStart < 0) subStrStart = 0;
                    if (subStrStart >= measure.Length) subStrStart = measure.Length - 1;
                    if (subStrEnd < 0) subStrEnd = 0;
                    if (subStrEnd >= measure.Length) subStrEnd = measure.Length - 1;
                }
                format = format.Replace(format.Substring(startIndex, endIndex - startIndex + 1), measure.Substring(subStrStart, subStrEnd - subStrStart + 1));
            }
            else
            {
                format = format.Replace(key, measure);
            }
            return format;
        }

        public static string ToByteString(Int64 byteNum)
        {
            for (int i = FORMAT_BYTES.Length; i > 0; i--)
            {
                if (byteNum > Math.Pow(1024.0, i))
                {
                    return String.Format("{0:0.00 " + FORMAT_BYTES[i] + "}", (double)byteNum / Math.Pow(1024, i));
                }
            }

            return byteNum.ToString() + " " + FORMAT_BYTES[0];

        }
    }
}
