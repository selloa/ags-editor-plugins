using System.Collections.Generic;

namespace AGS.Plugin.CursorAgent
{
    internal sealed class CursorContractAdaptResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<PatchOperation> Operations { get; set; }
    }

    internal static class CursorContractPatchAdapter
    {
        public static CursorContractAdaptResult ToPatchOperations(List<CursorContractOperation> contractOperations)
        {
            if (contractOperations == null || contractOperations.Count == 0)
            {
                return Failure("No operations were provided by the Cursor contract.");
            }

            var patchOperations = new List<PatchOperation>();
            for (var i = 0; i < contractOperations.Count; i++)
            {
                var contractOperation = contractOperations[i];
                patchOperations.Add(new PatchOperation
                {
                    Type = MapType(contractOperation.Type),
                    MatchText = contractOperation.MatchText,
                    NewText = contractOperation.NewText ?? string.Empty
                });
            }

            return new CursorContractAdaptResult
            {
                Success = true,
                ErrorMessage = string.Empty,
                Operations = patchOperations
            };
        }

        private static PatchOperationType MapType(CursorContractOperationType type)
        {
            switch (type)
            {
                case CursorContractOperationType.Replace:
                    return PatchOperationType.Replace;
                case CursorContractOperationType.AddAfter:
                    return PatchOperationType.AddAfter;
                case CursorContractOperationType.AddBefore:
                    return PatchOperationType.AddBefore;
                default:
                    return PatchOperationType.Remove;
            }
        }

        private static CursorContractAdaptResult Failure(string errorMessage)
        {
            return new CursorContractAdaptResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Operations = new List<PatchOperation>()
            };
        }
    }
}
