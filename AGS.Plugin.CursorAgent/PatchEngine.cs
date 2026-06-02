using System;
using System.Collections.Generic;
using System.Text;

namespace AGS.Plugin.CursorAgent
{
    internal enum PatchOperationType
    {
        Replace,
        AddAfter,
        AddBefore,
        Remove
    }

    internal sealed class PatchOperation
    {
        public PatchOperationType Type { get; set; }
        public string MatchText { get; set; }
        public string NewText { get; set; }
    }

    internal sealed class PatchParseResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<PatchOperation> Operations { get; set; }
    }

    internal sealed class PatchApplyResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string UpdatedText { get; set; }
    }

    internal static class PatchEngine
    {
        public static PatchParseResult Parse(string patchText)
        {
            if (string.IsNullOrWhiteSpace(patchText))
            {
                return Failure("Patch text is empty.");
            }

            var lines = Normalize(patchText).Split('\n');
            var operations = new List<PatchOperation>();
            var index = 0;

            while (index < lines.Length)
            {
                if (string.IsNullOrWhiteSpace(lines[index]))
                {
                    index++;
                    continue;
                }

                var header = lines[index].Trim();
                index++;
                PatchOperationType type;
                if (!TryParseHeader(header, out type))
                {
                    return Failure("Unknown operation header: " + header);
                }

                string matchLabel;
                string newLabel;
                switch (type)
                {
                    case PatchOperationType.Replace:
                        matchLabel = "OLD:";
                        newLabel = "NEW:";
                        break;
                    case PatchOperationType.AddAfter:
                    case PatchOperationType.AddBefore:
                        matchLabel = "MATCH:";
                        newLabel = "TEXT:";
                        break;
                    default:
                        matchLabel = "TEXT:";
                        newLabel = null;
                        break;
                }

                var matchText = ReadField(lines, ref index, matchLabel);
                if (matchText == null)
                {
                    return Failure("Missing field '" + matchLabel + "' for operation " + header + ".");
                }

                string newText = string.Empty;
                if (newLabel != null)
                {
                    newText = ReadField(lines, ref index, newLabel);
                    if (newText == null)
                    {
                        return Failure("Missing field '" + newLabel + "' for operation " + header + ".");
                    }
                }

                operations.Add(new PatchOperation
                {
                    Type = type,
                    MatchText = matchText,
                    NewText = newText
                });
            }

            if (operations.Count == 0)
            {
                return Failure("No operations found.");
            }

            return new PatchParseResult
            {
                Success = true,
                Operations = operations,
                ErrorMessage = string.Empty
            };
        }

        public static PatchApplyResult Apply(string sourceText, List<PatchOperation> operations)
        {
            var current = sourceText ?? string.Empty;
            for (var i = 0; i < operations.Count; i++)
            {
                var op = operations[i];
                var position = current.IndexOf(op.MatchText, StringComparison.Ordinal);
                if (position < 0)
                {
                    return new PatchApplyResult
                    {
                        Success = false,
                        ErrorMessage = "Operation " + (i + 1) + " failed conflict check: expected text not found.",
                        UpdatedText = current
                    };
                }

                switch (op.Type)
                {
                    case PatchOperationType.Replace:
                        current = current.Substring(0, position) + op.NewText + current.Substring(position + op.MatchText.Length);
                        break;
                    case PatchOperationType.AddAfter:
                        current = current.Substring(0, position + op.MatchText.Length) + op.NewText + current.Substring(position + op.MatchText.Length);
                        break;
                    case PatchOperationType.AddBefore:
                        current = current.Substring(0, position) + op.NewText + current.Substring(position);
                        break;
                    case PatchOperationType.Remove:
                        current = current.Substring(0, position) + current.Substring(position + op.MatchText.Length);
                        break;
                }
            }

            return new PatchApplyResult
            {
                Success = true,
                ErrorMessage = string.Empty,
                UpdatedText = current
            };
        }

        public static string BuildSummary(List<PatchOperation> operations)
        {
            var replace = 0;
            var add = 0;
            var remove = 0;
            foreach (var op in operations)
            {
                if (op.Type == PatchOperationType.Replace) replace++;
                else if (op.Type == PatchOperationType.Remove) remove++;
                else add++;
            }

            return "Parsed operations: replace=" + replace + ", add=" + add + ", remove=" + remove + ".";
        }

        private static bool TryParseHeader(string value, out PatchOperationType type)
        {
            if (value == "REPLACE") { type = PatchOperationType.Replace; return true; }
            if (value == "ADD_AFTER") { type = PatchOperationType.AddAfter; return true; }
            if (value == "ADD_BEFORE") { type = PatchOperationType.AddBefore; return true; }
            if (value == "REMOVE") { type = PatchOperationType.Remove; return true; }
            type = PatchOperationType.Replace;
            return false;
        }

        private static string ReadField(string[] lines, ref int index, string expectedLabel)
        {
            if (index >= lines.Length || lines[index].Trim() != expectedLabel)
            {
                return null;
            }

            index++;
            var sb = new StringBuilder();
            var foundEnd = false;
            while (index < lines.Length)
            {
                var line = lines[index];
                if (line.Trim() == "END")
                {
                    index++;
                    foundEnd = true;
                    break;
                }

                sb.AppendLine(line);
                index++;
            }

            if (!foundEnd)
            {
                return null;
            }

            return sb.ToString().TrimEnd('\r', '\n');
        }

        private static PatchParseResult Failure(string message)
        {
            return new PatchParseResult
            {
                Success = false,
                ErrorMessage = message,
                Operations = new List<PatchOperation>()
            };
        }

        private static string Normalize(string text)
        {
            return text.Replace("\r\n", "\n");
        }
    }
}
