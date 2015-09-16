using System.Text.RegularExpressions;

namespace PluginTopProcesses
{
    public class Utils
    {
        public static bool WildcardMatch(string input, string pattern)
        {
            return Regex.IsMatch(input, WildcardToRegex(pattern));
        }

        public static string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern.Replace("%", "*")).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        }
    }
}
