using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AGS.Plugin.SpeechViewer
{
    internal static class DialogSpeechParser
    {
        private static readonly string[] SkipScriptPrefixes =
        {
            "goto-dialog",
            "goto-previous",
            "run-script",
            "option-off",
            "set-speech-view",
            "return",
            "stop",
            "activate"
        };

        private static readonly Regex SpeakerRe = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private static readonly Regex BranchRe = new Regex(@"^@(\d+)\b", RegexOptions.Compiled);

        internal sealed class DialogSpeechLine
        {
            public string BranchTag { get; set; }
            public string LineId { get; set; }
            public string Speaker { get; set; }
            public string Message { get; set; }
        }

        public static List<DialogSpeechLine> ParseDialogScriptBody(string scriptBody, int dialogId)
        {
            var rows = new List<DialogSpeechLine>();
            var currentBranch = "root";
            var seqByBranch = new Dictionary<string, int>(StringComparer.Ordinal);

            if (string.IsNullOrEmpty(scriptBody))
            {
                return rows;
            }

            var lines = scriptBody.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                var branchMatch = BranchRe.Match(line);
                if (branchMatch.Success)
                {
                    currentBranch = "@" + branchMatch.Groups[1].Value;
                    if (!seqByBranch.ContainsKey(currentBranch))
                    {
                        seqByBranch[currentBranch] = 0;
                    }
                    continue;
                }

                if (line.StartsWith("@", StringComparison.Ordinal))
                {
                    continue;
                }

                var lower = line.ToLowerInvariant();
                var skip = false;
                foreach (var prefix in SkipScriptPrefixes)
                {
                    if (lower.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        skip = true;
                        break;
                    }
                }

                if (skip)
                {
                    continue;
                }

                var colonIndex = line.IndexOf(':');
                if (colonIndex < 0)
                {
                    continue;
                }

                var speaker = line.Substring(0, colonIndex).Trim();
                var message = line.Substring(colonIndex + 1).Trim();
                if (message.Length == 0 || speaker.Length == 0 || !SpeakerRe.IsMatch(speaker))
                {
                    continue;
                }

                if (!seqByBranch.ContainsKey("root"))
                {
                    seqByBranch["root"] = 0;
                }

                if (!seqByBranch.ContainsKey(currentBranch))
                {
                    seqByBranch[currentBranch] = 0;
                }

                seqByBranch[currentBranch]++;
                var branchTag = currentBranch;
                var lineId = string.Format("dlg-{0}-{1}-L{2}", dialogId, branchTag, seqByBranch[currentBranch]);

                rows.Add(new DialogSpeechLine
                {
                    BranchTag = branchTag,
                    LineId = lineId,
                    Speaker = speaker,
                    Message = message
                });
            }

            return rows;
        }

        public static int CompareBranchTags(string a, string b)
        {
            var keyA = BranchSortKey(a);
            var keyB = BranchSortKey(b);
            var cmp = keyA.Item1.CompareTo(keyB.Item1);
            return cmp != 0 ? cmp : keyA.Item2.CompareTo(keyB.Item2);
        }

        public static string BranchTitle(string branchTag)
        {
            return branchTag == "root" ? "Root (before first `@N`)" : "Branch " + branchTag;
        }

        private static Tuple<int, int> BranchSortKey(string branchTag)
        {
            if (branchTag != null && branchTag.StartsWith("@", StringComparison.Ordinal))
            {
                int n;
                if (int.TryParse(branchTag.Substring(1), out n))
                {
                    return Tuple.Create(0, n);
                }
            }

            return Tuple.Create(-1, 0);
        }
    }
}
