namespace AGS.Plugin.GameStates.Core
{
    public sealed class AottStateCatalogProfile : IStateCatalogProfile
    {
        public static readonly AottStateCatalogProfile Instance = new AottStateCatalogProfile();

        private readonly DefaultStateCatalogProfile _base = DefaultStateCatalogProfile.Instance;

        private AottStateCatalogProfile()
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
                    new InitScanTarget("episode_irs_audit.asc", "EpisodeIRS_GameStartInit"),
                };
            }
        }

        public string[] InitFunctionsExcludedFromQa
        {
            get { return new[] { "game_start", "EpisodeIRS_GameStartInit" }; }
        }

        public string ResetNoteHtml
        {
            get
            {
                return "Values set in <code>game_start</code> or <code>EpisodeIRS_GameStartInit</code>.";
            }
        }

        public string DefaultExportFileName
        {
            get { return "Game_state_catalog.html"; }
        }

        public string RoomFromFile(string fileName)
        {
            var stem = System.IO.Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
            if (stem == "episode_irs_audit")
            {
                return "episode";
            }

            return _base.RoomFromFile(fileName);
        }
    }
}
