namespace AGS.Plugin.GameStates.Core
{
    public sealed class InitScanTarget
    {
        public InitScanTarget(string scriptFileName, string functionName)
        {
            ScriptFileName = scriptFileName;
            FunctionName = functionName;
        }

        public string ScriptFileName { get; private set; }
        public string FunctionName { get; private set; }
    }

    public interface IStateCatalogProfile
    {
        InitScanTarget[] InitScanTargets { get; }
        string[] InitFunctionsExcludedFromQa { get; }
        string ResetNoteHtml { get; }
        string DefaultExportFileName { get; }
        string RoomFromFile(string fileName);
    }
}
