using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AGS.Types;

namespace AGS.Plugin.GameStates.Core
{
    public static class StateCatalogGatherer
    {
        public static StateCatalogDocument Gather(IGame gameInterface, IStateCatalogProfile profile)
        {
            var game = gameInterface as Game;
            if (game == null || profile == null)
            {
                return EmptyDocument("(no game loaded)", profile);
            }

            var directoryPath = game.DirectoryPath;
            if (string.IsNullOrEmpty(directoryPath))
            {
                return EmptyDocument("(game directory unknown)", profile);
            }

            var scriptTexts = BuildScriptTextLookup(game, directoryPath);
            var ashFiles = CollectFiles(scriptTexts, directoryPath, "*.ash");
            var ascFiles = CollectFiles(scriptTexts, directoryPath, "*.asc");
            var defines = StateCatalogParsers.ParseDefines(ashFiles);
            var globalScriptText = FindGlobalScriptText(scriptTexts);
            var slotRegistry = StateCatalogParsers.ParseSlotRegistry(globalScriptText);
            var scan = StateCatalogParsers.ScanGiUsage(ascFiles, defines, profile);
            var inits = StateCatalogParsers.ParseInitialValues(scriptTexts, defines, profile);
            var roomTitles = CollectRoomTitles(game);
            var gameTitle = game.Settings != null ? (game.Settings.GameName ?? string.Empty) : string.Empty;
            var sourceLabel = Path.Combine(directoryPath, "Game.agf");

            return StateCatalogBuilder.Build(
                defines,
                slotRegistry,
                scan.Sites,
                inits,
                scan.MagicWarnings,
                ascFiles.Count,
                roomTitles,
                profile,
                gameTitle,
                sourceLabel);
        }

        private static StateCatalogDocument EmptyDocument(string sourceLabel, IStateCatalogProfile profile)
        {
            return new StateCatalogDocument
            {
                GameTitle = string.Empty,
                SourceLabel = sourceLabel,
                GeneratedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC",
                Summary = new StateCatalogSummary(),
                RoomTitles = new Dictionary<string, string>(),
                Flags = new Dictionary<int, FlagEntry>(),
                Story = new List<FlagEntry>(),
                Doors = new List<FlagEntry>(),
                Engine = new List<FlagEntry>(),
                Legacy = new List<FlagEntry>(),
                Other = new List<FlagEntry>(),
                ResetList = new List<FlagEntry>(),
                ByRoom = new Dictionary<string, List<FlagEntry>>(),
                CatalogWarnings = new List<CatalogWarning>(),
                RelatedDefines = new Dictionary<string, DefineInfo>(),
                Sections = new List<StateCatalogSection>(),
                Profile = profile,
            };
        }

        private static Dictionary<string, string> BuildScriptTextLookup(Game game, string directoryPath)
        {
            var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (game.RootScriptFolder != null)
            {
                foreach (Script script in game.RootScriptFolder.AllScriptsFlat)
                {
                    if (script == null || script.IsHeader || string.IsNullOrEmpty(script.FileName))
                    {
                        continue;
                    }

                    lookup[script.FileName] = script.Text ?? string.Empty;
                }
            }

            if (Directory.Exists(directoryPath))
            {
                foreach (var pattern in new[] { "*.asc", "*.ash" })
                {
                    string[] files;
                    try
                    {
                        files = Directory.GetFiles(directoryPath, pattern);
                    }
                    catch (IOException)
                    {
                        continue;
                    }

                    foreach (var path in files)
                    {
                        var fileName = Path.GetFileName(path);
                        if (lookup.ContainsKey(fileName))
                        {
                            continue;
                        }

                        try
                        {
                            lookup[fileName] = File.ReadAllText(path);
                        }
                        catch (IOException)
                        {
                            lookup[fileName] = string.Empty;
                        }
                    }
                }
            }

            return lookup;
        }

        private static List<KeyValuePair<string, string>> CollectFiles(
            Dictionary<string, string> scriptTexts,
            string directoryPath,
            string pattern)
        {
            var result = new List<KeyValuePair<string, string>>();
            if (!Directory.Exists(directoryPath))
            {
                return result;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(directoryPath, pattern);
            }
            catch (IOException)
            {
                return result;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (var path in files)
            {
                var fileName = Path.GetFileName(path);
                string text;
                if (!scriptTexts.TryGetValue(fileName, out text))
                {
                    text = string.Empty;
                }

                result.Add(new KeyValuePair<string, string>(fileName, text ?? string.Empty));
            }

            return result;
        }

        private static string FindGlobalScriptText(Dictionary<string, string> scriptTexts)
        {
            string text;
            if (StateCatalogParsers.TryGetScriptText(scriptTexts, "GlobalScript.asc", out text))
            {
                return text;
            }

            StateCatalogParsers.TryGetScriptText(scriptTexts, "GlobalScript.ash", out text);
            return text ?? string.Empty;
        }

        private static Dictionary<string, string> CollectRoomTitles(Game game)
        {
            var titles = new Dictionary<string, string>();
            if (game.RootRoomFolder == null)
            {
                return titles;
            }

            foreach (UnloadedRoom room in game.RootRoomFolder.AllItemsFlat)
            {
                if (room == null)
                {
                    continue;
                }

                titles[room.Number.ToString()] = room.Description ?? string.Empty;
            }

            return titles;
        }
    }
}
