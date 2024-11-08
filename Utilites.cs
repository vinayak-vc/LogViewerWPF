using System.Linq;
using System.Text.RegularExpressions;

namespace LogViewerApp {

    internal static class Utilites {

        public static (string, string) ExtractClassNamesAndLineNumbers(string stackTrace) {
            var regex = new Regex(@"(?<fileName>\w+\.cs):(?<lineNumber>\d+)");
            var results = regex.Matches(stackTrace)
                               .Cast<Match>()
                               .Select(match => $"{match.Groups["fileName"].Value}:{match.Groups["lineNumber"].Value}")
                               .Reverse()
                               .ToList();
            if (results.Count > 0) {
                return (string.Join(" ->\n", results), results[results.Count - 1]);
            } else {
                return ("", "");
            }
        }

        public static string GetTruncatedStackTrace(string stackTrace) {
            return stackTrace.Substring(stackTrace.IndexOf('\n') + 1, stackTrace.Length - (stackTrace.IndexOf('\n') + 1));
        }
    }
}