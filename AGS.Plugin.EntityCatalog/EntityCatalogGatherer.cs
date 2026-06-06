using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AGS.Types;

namespace AGS.Plugin.EntityCatalog
{
    internal static class EntityCatalogGatherer
    {
        private static readonly PropertyInfo[] CharacterViewProperties = typeof(Character)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.Name.EndsWith("View", StringComparison.Ordinal) && p.PropertyType == typeof(int))
            .ToArray();

        public static CatalogDocument Gather(IGame gameInterface)
        {
            var game = gameInterface as Game;
            if (game == null)
            {
                return EmptyDocument("(no game loaded)");
            }

            var sourceLabel = string.IsNullOrEmpty(game.DirectoryPath)
                ? "Game.agf"
                : Path.Combine(game.DirectoryPath, "Game.agf");

            var characterData = CollectCharacters(game);
            var viewToCharacters = BuildViewToCharacters(characterData);

            var sections = new List<CatalogSection>
            {
                CollectRooms(game),
                CollectInventory(game),
                CollectDialogs(game),
                BuildCharactersSection(characterData),
                CollectViews(game, viewToCharacters),
                CollectAudio(game),
                CollectGuis(game),
                CollectFonts(game),
                CollectCursors(game)
            };

            return new CatalogDocument
            {
                GameTitle = game.Settings != null ? (game.Settings.GameName ?? string.Empty) : string.Empty,
                SourceLabel = sourceLabel,
                Sections = sections
            };
        }

        private static CatalogDocument EmptyDocument(string sourceLabel)
        {
            return new CatalogDocument
            {
                GameTitle = string.Empty,
                SourceLabel = sourceLabel,
                Sections = new List<CatalogSection>()
            };
        }

        private sealed class CharacterData
        {
            public int Id { get; set; }
            public string ScriptName { get; set; }
            public string RealName { get; set; }
            public int[] UsedViews { get; set; }
            public string Label { get; set; }
        }

        private static List<CharacterData> CollectCharacters(Game game)
        {
            var result = new List<CharacterData>();
            if (game.RootCharacterFolder == null)
            {
                return result;
            }

            foreach (Character character in game.RootCharacterFolder.AllItemsFlat)
            {
                var script = character.ScriptName ?? string.Empty;
                var real = character.RealName ?? string.Empty;
                var usedViews = CollectCharacterUsedViews(character);
                var label = string.IsNullOrEmpty(real) ? script : script + " (" + real + ")";

                result.Add(new CharacterData
                {
                    Id = character.ID,
                    ScriptName = script,
                    RealName = real,
                    UsedViews = usedViews,
                    Label = label
                });
            }

            result.Sort((a, b) => a.Id.CompareTo(b.Id));
            return result;
        }

        private static int[] CollectCharacterUsedViews(Character character)
        {
            var used = new HashSet<int>();
            foreach (var property in CharacterViewProperties)
            {
                var value = (int)property.GetValue(character, null);
                if (value > 0)
                {
                    used.Add(value);
                }
            }

            var sorted = used.ToList();
            sorted.Sort();
            return sorted.ToArray();
        }

        private static Dictionary<int, List<string>> BuildViewToCharacters(List<CharacterData> characters)
        {
            var map = new Dictionary<int, List<string>>();
            foreach (var character in characters)
            {
                foreach (var viewId in character.UsedViews)
                {
                    List<string> labels;
                    if (!map.TryGetValue(viewId, out labels))
                    {
                        labels = new List<string>();
                        map[viewId] = labels;
                    }

                    labels.Add(character.Label);
                }
            }

            return map;
        }

        private static CatalogSection CollectRooms(Game game)
        {
            var rows = new List<CatalogRow>();
            if (game.RootRoomFolder != null)
            {
                foreach (UnloadedRoom room in game.RootRoomFolder.AllItemsFlat)
                {
                    rows.Add(new CatalogRow
                    {
                        Cells = new[]
                        {
                            room.Number.ToString(),
                            room.Description ?? string.Empty
                        }
                    });
                }
            }

            rows.Sort((a, b) => int.Parse(a.Cells[0]).CompareTo(int.Parse(b.Cells[0])));
            return new CatalogSection
            {
                Id = "rooms",
                Title = "Rooms",
                Note = "Room numbers and descriptions as stored under Game/Rooms (UnloadedRoom).",
                ColumnHeaders = new[] { "Number", "Description" },
                Rows = rows
            };
        }

        private static CatalogSection CollectInventory(Game game)
        {
            var rows = new List<CatalogRow>();
            if (game.RootInventoryItemFolder != null)
            {
                foreach (InventoryItem item in game.RootInventoryItemFolder.AllItemsFlat)
                {
                    rows.Add(new CatalogRow
                    {
                        Cells = new[]
                        {
                            item.ID.ToString(),
                            item.Name ?? string.Empty,
                            item.Description ?? string.Empty
                        }
                    });
                }
            }

            rows.Sort((a, b) => int.Parse(a.Cells[0]).CompareTo(int.Parse(b.Cells[0])));
            return new CatalogSection
            {
                Id = "inventory",
                Title = "Inventory items",
                Note = string.Empty,
                ColumnHeaders = new[] { "ID", "Name", "Description" },
                Rows = rows
            };
        }

        private static CatalogSection CollectDialogs(Game game)
        {
            var rows = new List<CatalogRow>();
            if (game.RootDialogFolder != null)
            {
                foreach (Dialog dialog in game.RootDialogFolder.AllItemsFlat)
                {
                    rows.Add(new CatalogRow
                    {
                        Cells = new[]
                        {
                            dialog.ID.ToString(),
                            dialog.Name ?? string.Empty
                        }
                    });
                }
            }

            rows.Sort((a, b) => int.Parse(a.Cells[0]).CompareTo(int.Parse(b.Cells[0])));
            return new CatalogSection
            {
                Id = "dialogs",
                Title = "Dialogs",
                Note = string.Empty,
                ColumnHeaders = new[] { "ID", "Name" },
                Rows = rows
            };
        }

        private static CatalogSection BuildCharactersSection(List<CharacterData> characters)
        {
            var rows = new List<CatalogRow>();
            foreach (var character in characters)
            {
                rows.Add(new CatalogRow
                {
                    Cells = new[]
                    {
                        character.Id.ToString(),
                        character.ScriptName,
                        character.RealName,
                        FormatUsedViews(character.UsedViews)
                    }
                });
            }

            return new CatalogSection
            {
                Id = "characters",
                Title = "Characters",
                Note = string.Empty,
                ColumnHeaders = new[] { "ID", "Script name", "Real name", "Used view IDs" },
                Rows = rows
            };
        }

        private static CatalogSection CollectViews(Game game, Dictionary<int, List<string>> viewToCharacters)
        {
            var rows = new List<CatalogRow>();
            if (game.ViewFlatList != null)
            {
                foreach (View view in game.ViewFlatList)
                {
                    rows.Add(new CatalogRow
                    {
                        Cells = new[]
                        {
                            view.ID.ToString(),
                            view.Name ?? string.Empty,
                            FormatAssociatedCharacters(view.ID, viewToCharacters)
                        }
                    });
                }
            }

            rows.Sort((a, b) => int.Parse(a.Cells[0]).CompareTo(int.Parse(b.Cells[0])));
            return new CatalogSection
            {
                Id = "views",
                Title = "Views",
                Note = string.Empty,
                ColumnHeaders = new[] { "ID", "Name", "Associated characters" },
                Rows = rows
            };
        }

        private static CatalogSection CollectAudio(Game game)
        {
            var rows = new List<CatalogRow>();
            if (game.AudioClipFlatList != null)
            {
                foreach (AudioClip clip in game.AudioClipFlatList)
                {
                    rows.Add(new CatalogRow
                    {
                        Cells = new[]
                        {
                            clip.ID.ToString(),
                            clip.ScriptName ?? string.Empty,
                            clip.FileType.ToString(),
                            BasenameOnly(clip.SourceFileName)
                        }
                    });
                }
            }

            rows.Sort((a, b) => int.Parse(a.Cells[0]).CompareTo(int.Parse(b.Cells[0])));
            return new CatalogSection
            {
                Id = "audio",
                Title = "Audio clips",
                Note = "From Game/AudioClips. Source file column is basename only.",
                ColumnHeaders = new[] { "ID", "Script name", "File type", "Source file" },
                Rows = rows
            };
        }

        private static CatalogSection CollectGuis(Game game)
        {
            var rows = new List<CatalogRow>();
            if (game.GUIFlatList != null)
            {
                foreach (GUI gui in game.GUIFlatList)
                {
                    var normalGui = gui as NormalGUI;
                    if (normalGui == null)
                    {
                        continue;
                    }

                    rows.Add(new CatalogRow
                    {
                        Cells = new[]
                        {
                            normalGui.ID.ToString(),
                            normalGui.Name ?? string.Empty
                        }
                    });
                }
            }

            rows.Sort((a, b) => int.Parse(a.Cells[0]).CompareTo(int.Parse(b.Cells[0])));
            return new CatalogSection
            {
                Id = "guis",
                Title = "GUIs",
                Note = "GUIMain / NormalGUI entries under Game/GUIs.",
                ColumnHeaders = new[] { "ID", "Name" },
                Rows = rows
            };
        }

        private static CatalogSection CollectFonts(Game game)
        {
            var rows = new List<CatalogRow>();
            if (game.Fonts != null)
            {
                foreach (Font font in game.Fonts)
                {
                    rows.Add(new CatalogRow
                    {
                        Cells = new[]
                        {
                            font.ID.ToString(),
                            font.Name ?? string.Empty,
                            font.SourceFilename ?? string.Empty
                        }
                    });
                }
            }

            rows.Sort((a, b) => int.Parse(a.Cells[0]).CompareTo(int.Parse(b.Cells[0])));
            return new CatalogSection
            {
                Id = "fonts",
                Title = "Fonts",
                Note = string.Empty,
                ColumnHeaders = new[] { "ID", "Name", "Source file" },
                Rows = rows
            };
        }

        private static CatalogSection CollectCursors(Game game)
        {
            var rows = new List<CatalogRow>();
            if (game.Cursors != null)
            {
                foreach (MouseCursor cursor in game.Cursors)
                {
                    rows.Add(new CatalogRow
                    {
                        Cells = new[]
                        {
                            cursor.ID.ToString(),
                            cursor.Name ?? string.Empty,
                            cursor.View.ToString()
                        }
                    });
                }
            }

            rows.Sort((a, b) => int.Parse(a.Cells[0]).CompareTo(int.Parse(b.Cells[0])));
            return new CatalogSection
            {
                Id = "cursors",
                Title = "Mouse cursors",
                Note = string.Empty,
                ColumnHeaders = new[] { "ID", "Name", "View" },
                Rows = rows
            };
        }

        private static string FormatUsedViews(int[] usedViews)
        {
            if (usedViews == null || usedViews.Length == 0)
            {
                return string.Empty;
            }

            return string.Join(", ", usedViews.Select(v => v.ToString()).ToArray());
        }

        private static string FormatAssociatedCharacters(int viewId, Dictionary<int, List<string>> viewToCharacters)
        {
            List<string> labels;
            if (!viewToCharacters.TryGetValue(viewId, out labels) || labels.Count == 0)
            {
                return string.Empty;
            }

            var unique = labels.Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            return string.Join(", ", unique);
        }

        private static string BasenameOnly(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Path.GetFileName(path.Replace('\\', '/'));
        }
    }
}
