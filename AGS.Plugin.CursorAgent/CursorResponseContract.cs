using System;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace AGS.Plugin.CursorAgent
{
    internal enum CursorContractOperationType
    {
        Replace,
        AddAfter,
        AddBefore,
        Remove
    }

    internal sealed class CursorContractOperation
    {
        public CursorContractOperationType Type { get; set; }
        public string MatchText { get; set; }
        public string NewText { get; set; }
    }

    internal sealed class CursorResponseParseResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<CursorContractOperation> Operations { get; set; }
    }

    internal static class CursorResponseContractParser
    {
        private const string ContractName = "ags.cursor.patch-ops";
        private const int ContractVersion = 1;

        public static bool LooksLikeJsonContract(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return text.TrimStart().StartsWith("{", StringComparison.Ordinal);
        }

        public static CursorResponseParseResult Parse(string contractText)
        {
            if (string.IsNullOrWhiteSpace(contractText))
            {
                return Failure("Contract text is empty.");
            }

            Dictionary<string, object> root;
            try
            {
                var serializer = new JavaScriptSerializer();
                root = serializer.Deserialize<Dictionary<string, object>>(contractText);
            }
            catch (Exception ex)
            {
                return Failure("Invalid JSON: " + ex.Message);
            }

            if (root == null)
            {
                return Failure("Contract root must be a JSON object.");
            }

            var allowedRootKeys = new HashSet<string>(StringComparer.Ordinal) { "contract", "version", "operations" };
            foreach (var key in root.Keys)
            {
                if (!allowedRootKeys.Contains(key))
                {
                    return Failure("Unsupported top-level key: '" + key + "'.");
                }
            }

            if (!TryGetString(root, "contract", out var contractName))
            {
                return Failure("Missing or invalid 'contract' value.");
            }

            if (!string.Equals(contractName, ContractName, StringComparison.Ordinal))
            {
                return Failure("Unsupported contract name: '" + contractName + "'.");
            }

            if (!TryGetInt(root, "version", out var version))
            {
                return Failure("Missing or invalid 'version' value.");
            }

            if (version != ContractVersion)
            {
                return Failure("Unsupported contract version: " + version + ".");
            }

            if (!root.TryGetValue("operations", out var operationsRaw) || !(operationsRaw is ArrayList operationArray))
            {
                return Failure("Missing or invalid 'operations' array.");
            }

            if (operationArray.Count == 0)
            {
                return Failure("Contract contains no operations.");
            }

            var operations = new List<CursorContractOperation>();
            for (var i = 0; i < operationArray.Count; i++)
            {
                if (!(operationArray[i] is Dictionary<string, object> operationObject))
                {
                    return Failure("Operation " + (i + 1) + " must be an object.");
                }

                if (!TryParseOperation(operationObject, i + 1, out var operation, out var operationError))
                {
                    return Failure(operationError);
                }

                operations.Add(operation);
            }

            return new CursorResponseParseResult
            {
                Success = true,
                ErrorMessage = string.Empty,
                Operations = operations
            };
        }

        private static bool TryParseOperation(
            Dictionary<string, object> operationObject,
            int operationNumber,
            out CursorContractOperation operation,
            out string errorMessage)
        {
            operation = null;
            errorMessage = string.Empty;

            if (!TryGetString(operationObject, "op", out var opCode))
            {
                errorMessage = "Operation " + operationNumber + " is missing required string field 'op'.";
                return false;
            }

            CursorContractOperationType type;
            bool needsText;
            if (string.Equals(opCode, "replace", StringComparison.Ordinal))
            {
                type = CursorContractOperationType.Replace;
                needsText = true;
            }
            else if (string.Equals(opCode, "add_after", StringComparison.Ordinal))
            {
                type = CursorContractOperationType.AddAfter;
                needsText = true;
            }
            else if (string.Equals(opCode, "add_before", StringComparison.Ordinal))
            {
                type = CursorContractOperationType.AddBefore;
                needsText = true;
            }
            else if (string.Equals(opCode, "remove", StringComparison.Ordinal))
            {
                type = CursorContractOperationType.Remove;
                needsText = false;
            }
            else
            {
                errorMessage = "Operation " + operationNumber + " has unsupported op '" + opCode + "'.";
                return false;
            }

            if (!TryGetString(operationObject, "match", out var matchText) || string.IsNullOrEmpty(matchText))
            {
                errorMessage = "Operation " + operationNumber + " must include non-empty string field 'match'.";
                return false;
            }

            string newText = string.Empty;
            if (needsText)
            {
                if (!TryGetStringAllowEmpty(operationObject, "text", out newText))
                {
                    errorMessage = "Operation " + operationNumber + " requires string field 'text'.";
                    return false;
                }
            }

            var allowedKeys = needsText
                ? new HashSet<string>(StringComparer.Ordinal) { "op", "match", "text" }
                : new HashSet<string>(StringComparer.Ordinal) { "op", "match" };

            foreach (var key in operationObject.Keys)
            {
                if (!allowedKeys.Contains(key))
                {
                    errorMessage = "Operation " + operationNumber + " contains unsupported key '" + key + "'.";
                    return false;
                }
            }

            operation = new CursorContractOperation
            {
                Type = type,
                MatchText = matchText,
                NewText = newText
            };

            return true;
        }

        private static bool TryGetString(Dictionary<string, object> values, string key, out string value)
        {
            value = null;
            if (!values.TryGetValue(key, out var raw))
            {
                return false;
            }

            value = raw as string;
            return value != null;
        }

        private static bool TryGetStringAllowEmpty(Dictionary<string, object> values, string key, out string value)
        {
            return TryGetString(values, key, out value);
        }

        private static bool TryGetInt(Dictionary<string, object> values, string key, out int value)
        {
            value = 0;
            if (!values.TryGetValue(key, out var raw))
            {
                return false;
            }

            if (raw is int intValue)
            {
                value = intValue;
                return true;
            }

            if (raw is long longValue && longValue <= int.MaxValue && longValue >= int.MinValue)
            {
                value = (int)longValue;
                return true;
            }

            if (raw is double doubleValue && Math.Abs(doubleValue % 1) < 0.00001)
            {
                value = (int)doubleValue;
                return true;
            }

            return false;
        }

        private static CursorResponseParseResult Failure(string errorMessage)
        {
            return new CursorResponseParseResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Operations = new List<CursorContractOperation>()
            };
        }
    }
}
