using System.IO;
using System.Text.RegularExpressions;

namespace AGS.Plugin.GameStates.Core
{
    public sealed class DefaultStateCatalogProfile : IStateCatalogProfile
    {
        public static readonly DefaultStateCatalogProfile Instance = new DefaultStateCatalogProfile();

        private static readonly Regex RoomFilePattern = new Regex(
            @"^room(\d+)\.asc$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private DefaultStateCatalogProfile()
        {
        }

        public InitScanTarget[] InitScanTargets
        {
            get
            {
                return new[]
                {
                    new InitScanTarget("GlobalScript.asc", "game_start"),
                    new InitScanTarget("GlobalScript.ash", "game_start"),
                };
            }
        }

        public string[] InitFunctionsExcludedFromQa
        {
            get { return new[] { "game_start" }; }
        }

        public string ResetNoteHtml
        {
            get { return "Values set in <code>game_start</code>."; }
        }

        public string DefaultExportFileName
        {
            get { return "Game_state_catalog.html"; }
        }

        public string RoomFromFile(string fileName)
        {
            var match = RoomFilePattern.Match(fileName ?? string.Empty);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            var stem = Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
            if (stem == "GlobalScript")
            {
                return "global";
            }

            if (stem == "core_doors")
            {
                return "doors";
            }

            if (stem.StartsWith("core_"))
            {
                return stem.Substring("core_".Length);
            }

            return stem;
        }
    }
}
