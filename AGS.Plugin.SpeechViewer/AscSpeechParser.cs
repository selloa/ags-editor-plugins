using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AGS.Plugin.SpeechViewer
{
    internal static class AscSpeechParser
    {
        private static readonly Regex DisplaySpeechRe = new Regex(@"DisplaySpeech\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PlayerSayRe = new Regex(@"\bplayer\.Say\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex IntArgRe = new Regex(@"^\s*(\d+)\s*$", RegexOptions.Compiled);

        internal sealed class DisplaySpeechCall
        {
            public string LineId { get; set; }
            public string CharacterExpression { get; set; }
            public string Text { get; set; }
            public string Note { get; set; }
        }

        internal sealed class PlayerSayCall
        {
            public string LineId { get; set; }
            public string Text { get; set; }
            public string Note { get; set; }
        }

        public static List<DisplaySpeechCall> ExtractDisplaySpeechCalls(string line, int lineNo, string basename)
        {
            var results = new List<DisplaySpeechCall>();
            foreach (Match match in DisplaySpeechRe.Matches(line))
            {
                var lp = line.IndexOf('(', match.Index);
                if (lp < 0)
                {
                    continue;
                }

                int endCall;
                if (!TryFindMatchingParen(line, lp, out endCall))
                {
                    continue;
                }

                var inner = line.Substring(lp + 1, endCall - lp - 2);
                int comma;
                if (!TryFirstTopLevelComma(inner, out comma))
                {
                    continue;
                }

                var first = inner.Substring(0, comma).Trim();
                var rest = inner.Substring(comma + 1);
                var j = SkipWs(rest, 0);
                string text;
                int afterQuote;
                if (!TryParseQuotedString(rest, j, out text, out afterQuote))
                {
                    continue;
                }

                var note = text.IndexOf('%') >= 0 ? "(contains % — verify if dynamic)" : string.Empty;
                results.Add(new DisplaySpeechCall
                {
                    LineId = string.Format("asc-{0}-L{1}", basename, lineNo),
                    CharacterExpression = first,
                    Text = text,
                    Note = note
                });
            }

            return results;
        }

        public static List<PlayerSayCall> ExtractPlayerSayCalls(string line, int lineNo, string basename)
        {
            var results = new List<PlayerSayCall>();
            foreach (Match match in PlayerSayRe.Matches(line))
            {
                var lp = line.IndexOf('(', match.Index);
                if (lp < 0)
                {
                    continue;
                }

                int endCall;
                if (!TryFindMatchingParen(line, lp, out endCall))
                {
                    continue;
                }

                var inner = line.Substring(lp + 1, endCall - lp - 2).Trim();
                var j = SkipWs(inner, 0);
                string text;
                int afterQuote;
                if (!TryParseQuotedString(inner, j, out text, out afterQuote))
                {
                    continue;
                }

                var note = text.IndexOf('%') >= 0 ? "dynamic / verify — format string" : string.Empty;
                results.Add(new PlayerSayCall
                {
                    LineId = string.Format("asc-{0}-L{1}-say", basename, lineNo),
                    Text = text,
                    Note = note
                });
            }

            return results;
        }

        public static string ResolveSpeakerExpression(
            string expression,
            int? playerId,
            IDictionary<int, Tuple<string, string>> characters)
        {
            var expr = (expression ?? string.Empty).Trim();
            if (expr == "player.ID")
            {
                return "player → " + CharacterLabel(characters, playerId);
            }

            var match = IntArgRe.Match(expr);
            if (match.Success)
            {
                return CharacterLabel(characters, int.Parse(match.Groups[1].Value));
            }

            return expr;
        }

        public static string CharacterLabel(IDictionary<int, Tuple<string, string>> characters, int? characterId)
        {
            if (!characterId.HasValue)
            {
                return "(unknown)";
            }

            Tuple<string, string> entry;
            if (characters == null || !characters.TryGetValue(characterId.Value, out entry))
            {
                return string.Format("ID {0} (not in Game.agf)", characterId.Value);
            }

            var script = entry.Item1 ?? string.Empty;
            var real = entry.Item2 ?? string.Empty;
            if (real.Length > 0 && real != script)
            {
                return script + " — " + real;
            }

            return script.Length > 0 ? script : string.Format("ID {0}", characterId.Value);
        }

        private static bool TryParseQuotedString(string src, int start, out string value, out int indexAfter)
        {
            value = null;
            indexAfter = start;
            if (start >= src.Length)
            {
                return false;
            }

            var quote = src[start];
            if (quote != '"' && quote != '\'')
            {
                return false;
            }

            var i = start + 1;
            var builder = new System.Text.StringBuilder();
            while (i < src.Length)
            {
                var c = src[i];
                if (c == '\\' && i + 1 < src.Length)
                {
                    builder.Append(src[i + 1]);
                    i += 2;
                    continue;
                }

                if (c == quote)
                {
                    value = builder.ToString();
                    indexAfter = i + 1;
                    return true;
                }

                builder.Append(c);
                i++;
            }

            return false;
        }

        private static int SkipWs(string s, int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t'))
            {
                i++;
            }

            return i;
        }

        private static bool TryFindMatchingParen(string s, int openIdx, out int indexAfterClose)
        {
            indexAfterClose = openIdx;
            var depth = 0;
            var i = openIdx;
            while (i < s.Length)
            {
                var c = s[i];
                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        indexAfterClose = i + 1;
                        return true;
                    }
                }
                else if (c == '"' || c == '\'')
                {
                    string ignored;
                    int j;
                    if (!TryParseQuotedString(s, i, out ignored, out j))
                    {
                        return false;
                    }

                    i = j;
                    continue;
                }

                i++;
            }

            return false;
        }

        private static bool TryFirstTopLevelComma(string s, out int commaIndex)
        {
            commaIndex = -1;
            var depth = 0;
            var i = 0;
            while (i < s.Length)
            {
                var c = s[i];
                if (c == '"' || c == '\'')
                {
                    string ignored;
                    int j;
                    if (!TryParseQuotedString(s, i, out ignored, out j))
                    {
                        return false;
                    }

                    i = j;
                    continue;
                }

                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    commaIndex = i;
                    return true;
                }

                i++;
            }

            return false;
        }
    }
}
