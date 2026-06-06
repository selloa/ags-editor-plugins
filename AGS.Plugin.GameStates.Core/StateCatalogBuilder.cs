using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AGS.Plugin.GameStates.Core
{
    internal static class StateCatalogBuilder
    {
        private const int DoorSlotMin = 24;
        private const int DoorSlotMax = 33;
        private static readonly HashSet<int> EngineSlots = new HashSet<int> { 2, 4, 12 };
        private static readonly Regex IntegerPattern = new Regex(@"^-?\d+$", RegexOptions.Compiled);

        private static readonly string[] FlagColumns =
        {
            "Title", "Symbol", "Slot", "Description", "Initial", "Sets", "Gets", "Rooms",
        };

        private static readonly string[] DoorColumns =
        {
            "Slot", "Description", "Initial", "Sets", "Gets",
        };

        private static readonly string[] ResetColumns =
        {
            "Title", "Symbol", "Slot", "Initial", "Source",
        };

        private static readonly string[] WarningColumns =
        {
            "Kind", "Symbol / arg", "Slot / line", "Detail",
        };

        private static readonly string[] RoomFlagColumns =
        {
            "Title", "Symbol", "Slot",
        };

        public static StateCatalogDocument Build(
            Dictionary<string, DefineInfo> defines,
            Dictionary<int, string> slotRegistry,
            List<RefSite> sites,
            Dictionary<int, KeyValuePair<string, string>> inits,
            List<CatalogWarning> magicWarnings,
            int filesScanned,
            Dictionary<string, string> roomTitles,
            IStateCatalogProfile profile,
            string gameTitle,
            string sourceLabel)
        {
            var slotToSymbol = new Dictionary<int, string>();
            foreach (var pair in defines)
            {
                if (pair.Value.Prefix != "gi" && !pair.Key.StartsWith("GI_", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!slotToSymbol.ContainsKey(pair.Value.Slot))
                {
                    slotToSymbol[pair.Value.Slot] = pair.Key;
                }
            }

            var referencedSlots = new HashSet<int>();
            foreach (var site in sites)
            {
                if (IntegerPattern.IsMatch(site.RawArg))
                {
                    referencedSlots.Add(int.Parse(site.RawArg));
                    continue;
                }

                DefineInfo defineInfo;
                if (defines.TryGetValue(site.RawArg, out defineInfo))
                {
                    referencedSlots.Add(defineInfo.Slot);
                }
            }

            var allSlots = new HashSet<int>(slotRegistry.Keys);
            foreach (var slot in slotToSymbol.Keys)
            {
                allSlots.Add(slot);
            }

            foreach (var slot in referencedSlots)
            {
                allSlots.Add(slot);
            }

            var flags = new Dictionary<int, FlagEntry>();
            foreach (var slot in allSlots.OrderBy(s => s))
            {
                string symbol;
                slotToSymbol.TryGetValue(slot, out symbol);
                string registryNote;
                slotRegistry.TryGetValue(slot, out registryNote);
                registryNote = registryNote ?? string.Empty;

                var comment = string.Empty;
                if (!string.IsNullOrEmpty(symbol))
                {
                    DefineInfo defineInfo;
                    if (defines.TryGetValue(symbol, out defineInfo))
                    {
                        comment = defineInfo.Comment ?? string.Empty;
                    }
                }

                flags[slot] = new FlagEntry
                {
                    Slot = slot,
                    Symbol = symbol,
                    Category = InferCategory(symbol, slot, registryNote),
                    Comment = comment,
                    FriendlyTitle = FriendlyTitle(symbol, slot, registryNote),
                    RegistryNote = registryNote,
                };
            }

            foreach (var site in sites)
            {
                int? slot = ResolveSiteSlot(site, defines);
                if (!slot.HasValue)
                {
                    continue;
                }

                FlagEntry entry;
                if (!flags.TryGetValue(slot.Value, out entry))
                {
                    string registryNote;
                    slotRegistry.TryGetValue(slot.Value, out registryNote);
                    entry = new FlagEntry
                    {
                        Slot = slot.Value,
                        Symbol = null,
                        Category = "other",
                        Comment = string.Empty,
                        FriendlyTitle = FriendlyTitle(null, slot.Value, registryNote ?? string.Empty),
                        RegistryNote = registryNote ?? string.Empty,
                    };
                    flags[slot.Value] = entry;
                }

                if (site.Op == "set")
                {
                    entry.Sets.Add(site);
                }
                else
                {
                    entry.Gets.Add(site);
                }
            }

            foreach (var pair in inits)
            {
                FlagEntry entry;
                if (!flags.TryGetValue(pair.Key, out entry))
                {
                    string symbol;
                    slotToSymbol.TryGetValue(pair.Key, out symbol);
                    string registryNote;
                    slotRegistry.TryGetValue(pair.Key, out registryNote);
                    entry = new FlagEntry
                    {
                        Slot = pair.Key,
                        Symbol = symbol,
                        Category = InferCategory(symbol, pair.Key, registryNote ?? string.Empty),
                        Comment = GetDefineComment(defines, symbol),
                        FriendlyTitle = FriendlyTitle(symbol, pair.Key, registryNote ?? string.Empty),
                        RegistryNote = registryNote ?? string.Empty,
                    };
                    flags[pair.Key] = entry;
                }

                entry.InitialValue = pair.Value.Key;
                entry.InitialSource = pair.Value.Value;
            }

            var giDefines = defines
                .Where(pair => pair.Key.StartsWith("GI_IRS_", StringComparison.Ordinal) ||
                               pair.Key.StartsWith("GI_", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            var referencedSymbols = new HashSet<string>(StringComparer.Ordinal);
            foreach (var site in sites)
            {
                if (giDefines.ContainsKey(site.RawArg))
                {
                    referencedSymbols.Add(site.RawArg);
                }
            }

            var catalogWarnings = new List<CatalogWarning>();
            foreach (var name in giDefines.Keys.OrderBy(n => n, StringComparer.Ordinal))
            {
                if (!referencedSymbols.Contains(name))
                {
                    catalogWarnings.Add(new CatalogWarning
                    {
                        Kind = "orphan_define",
                        Symbol = name,
                        Slot = giDefines[name].Slot,
                        Detail = "Defined in .ash but never referenced",
                    });
                }
            }

            catalogWarnings.AddRange(magicWarnings);

            var excluded = new HashSet<string>(
                profile.InitFunctionsExcludedFromQa ?? new string[0],
                StringComparer.Ordinal);
            foreach (var entry in flags.Values)
            {
                var nonInitSets = entry.Sets.Where(s => !excluded.Contains(s.Function)).ToList();
                if (entry.GetCount == 0 && nonInitSets.Count > 0)
                {
                    entry.Warnings.Add("set_never_read");
                }

                if (entry.SetCount == 0 && entry.GetCount > 0 && entry.InitialValue == null)
                {
                    entry.Warnings.Add("read_never_set");
                }
            }

            var story = flags.Values.Where(f => f.Category == "story").OrderBy(f => f.Symbol ?? f.Slot.ToString()).ToList();
            var doors = flags.Values.Where(f => f.Category == "door").OrderBy(f => f.Slot).ToList();
            var engine = flags.Values.Where(f => f.Category == "engine").OrderBy(f => f.Slot).ToList();
            var legacy = flags.Values.Where(f => f.Category == "legacy").OrderBy(f => f.Slot).ToList();
            var other = flags.Values.Where(f => f.Category == "other").OrderBy(f => f.Slot).ToList();
            var resetList = flags.Values
                .Where(f => f.InitialValue != null)
                .OrderBy(f => f.Symbol ?? ("slot_" + f.Slot))
                .ThenBy(f => f.Slot)
                .ToList();

            var byRoom = new Dictionary<string, List<FlagEntry>>(StringComparer.Ordinal);
            foreach (var entry in flags.Values)
            {
                foreach (var room in entry.Rooms)
                {
                    List<FlagEntry> list;
                    if (!byRoom.TryGetValue(room, out list))
                    {
                        list = new List<FlagEntry>();
                        byRoom[room] = list;
                    }

                    list.Add(entry);
                }
            }

            foreach (var room in byRoom.Keys.ToList())
            {
                byRoom[room] = byRoom[room]
                    .OrderBy(f => f.Symbol ?? ("slot_" + f.Slot))
                    .ThenBy(f => f.Slot)
                    .ToList();
            }

            foreach (var entry in flags.Values)
            {
                foreach (var warning in entry.Warnings)
                {
                    catalogWarnings.Add(new CatalogWarning
                    {
                        Kind = warning,
                        Symbol = entry.Symbol ?? ("slot_" + entry.Slot),
                        Slot = entry.Slot,
                        Detail = "Heuristic QA hint",
                    });
                }
            }

            var relatedDefines = defines
                .Where(pair => pair.Value.Prefix == "inv" ||
                               pair.Value.Prefix == "dialog" ||
                               pair.Value.Prefix == "room")
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            var document = new StateCatalogDocument
            {
                GameTitle = gameTitle ?? string.Empty,
                SourceLabel = sourceLabel ?? string.Empty,
                GeneratedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC",
                FilesScanned = filesScanned,
                Summary = new StateCatalogSummary
                {
                    StoryFlags = story.Count,
                    DoorSlots = doors.Count,
                    EngineSlots = engine.Count,
                    LegacySlots = legacy.Count,
                    Warnings = catalogWarnings.Count,
                },
                RoomTitles = roomTitles ?? new Dictionary<string, string>(),
                Flags = flags,
                Story = story,
                Doors = doors,
                Engine = engine,
                Legacy = legacy,
                Other = other,
                ResetList = resetList,
                ByRoom = byRoom,
                CatalogWarnings = catalogWarnings,
                RelatedDefines = relatedDefines,
                Profile = profile,
            };

            document.Sections = BuildSections(document, roomTitles);
            return document;
        }

        private static List<StateCatalogSection> BuildSections(
            StateCatalogDocument document,
            Dictionary<string, string> roomTitles)
        {
            var sections = new List<StateCatalogSection>
            {
                CreateFlagSection("story", "Story flags", document.Story),
                CreateDoorSection(document.Doors),
                CreateFlagSection("engine", "Engine slots", document.Engine),
                CreateFlagSection("legacy", "Legacy slots", document.Legacy),
                CreateFlagSection("other", "Other", document.Other),
                CreateResetSection(document.ResetList),
                CreateWarningSection(document.CatalogWarnings),
            };

            foreach (var roomPair in document.ByRoom.OrderBy(p => RoomSortKey(p.Key)))
            {
                sections.Add(new StateCatalogSection
                {
                    Id = "room-" + roomPair.Key,
                    Title = FormatRoomTitle(roomPair.Key, roomTitles),
                    ParentTreeId = "by_room",
                    ColumnHeaders = RoomFlagColumns,
                    Rows = roomPair.Value.Select(f => new StateCatalogRow
                    {
                        Cells = new[]
                        {
                            f.FriendlyTitle,
                            f.Symbol ?? "—",
                            f.Slot.ToString(),
                        },
                    }).ToList(),
                });
            }

            return sections;
        }

        private static StateCatalogSection CreateFlagSection(string id, string title, List<FlagEntry> flags)
        {
            return new StateCatalogSection
            {
                Id = id,
                Title = title,
                ColumnHeaders = FlagColumns,
                Rows = flags.Select(FlagRow).ToList(),
            };
        }

        private static StateCatalogSection CreateDoorSection(List<FlagEntry> flags)
        {
            return new StateCatalogSection
            {
                Id = "doors",
                Title = "Door slots",
                ColumnHeaders = DoorColumns,
                Rows = flags.Select(f => new StateCatalogRow
                {
                    Cells = new[]
                    {
                        f.Slot.ToString(),
                        f.RegistryNote ?? f.Comment ?? string.Empty,
                        f.InitialValue ?? "—",
                        f.SetCount.ToString(),
                        f.GetCount.ToString(),
                    },
                }).ToList(),
            };
        }

        private static StateCatalogSection CreateResetSection(List<FlagEntry> flags)
        {
            return new StateCatalogSection
            {
                Id = "reset",
                Title = "New game reset",
                ColumnHeaders = ResetColumns,
                Rows = flags.Select(f => new StateCatalogRow
                {
                    Cells = new[]
                    {
                        f.FriendlyTitle,
                        f.Symbol ?? "—",
                        f.Slot.ToString(),
                        f.InitialValue ?? string.Empty,
                        f.InitialSource ?? string.Empty,
                    },
                }).ToList(),
            };
        }

        private static StateCatalogSection CreateWarningSection(List<CatalogWarning> warnings)
        {
            return new StateCatalogSection
            {
                Id = "warnings",
                Title = "Warnings",
                ColumnHeaders = WarningColumns,
                Rows = warnings.Select(w => new StateCatalogRow
                {
                    Cells = new[]
                    {
                        w.Kind ?? string.Empty,
                        w.Kind == "magic_number" ? (w.RawArg ?? string.Empty) : (w.Symbol ?? string.Empty),
                        w.Kind == "magic_number" ? w.Line.ToString() : w.Slot.ToString(),
                        w.Detail ?? w.Context ?? string.Empty,
                    },
                }).ToList(),
            };
        }

        private static StateCatalogRow FlagRow(FlagEntry f)
        {
            return new StateCatalogRow
            {
                Cells = new[]
                {
                    f.FriendlyTitle,
                    f.Symbol ?? "—",
                    f.Slot.ToString(),
                    !string.IsNullOrEmpty(f.Comment) ? f.Comment : (f.RegistryNote ?? string.Empty),
                    f.InitialValue ?? "—",
                    f.SetCount.ToString(),
                    f.GetCount.ToString(),
                    string.Join(", ", f.Rooms.ToArray()),
                },
            };
        }

        private static int? ResolveSiteSlot(RefSite site, Dictionary<string, DefineInfo> defines)
        {
            if (IntegerPattern.IsMatch(site.RawArg))
            {
                return int.Parse(site.RawArg);
            }

            DefineInfo defineInfo;
            if (defines.TryGetValue(site.RawArg, out defineInfo))
            {
                return defineInfo.Slot;
            }

            return null;
        }

        private static string GetDefineComment(Dictionary<string, DefineInfo> defines, string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                return string.Empty;
            }

            DefineInfo defineInfo;
            return defines.TryGetValue(symbol, out defineInfo) ? (defineInfo.Comment ?? string.Empty) : string.Empty;
        }

        private static string InferCategory(string symbol, int slot, string registryNote)
        {
            if (!string.IsNullOrEmpty(symbol) && symbol.StartsWith("GI_IRS_", StringComparison.Ordinal))
            {
                return "story";
            }

            if (slot >= DoorSlotMin && slot <= DoorSlotMax)
            {
                var noteLower = (registryNote ?? string.Empty).ToLowerInvariant();
                if (noteLower.Contains("door") ||
                    (noteLower.Contains("room") && registryNote.Contains("<->")))
                {
                    return "door";
                }
            }

            if (EngineSlots.Contains(slot))
            {
                return "engine";
            }

            var lower = (registryNote ?? string.Empty).ToLowerInvariant();
            if (lower.Contains("(free)") || lower.Contains("legacy"))
            {
                return "legacy";
            }

            if (!string.IsNullOrEmpty(symbol) && symbol.StartsWith("GI_", StringComparison.Ordinal))
            {
                return "story";
            }

            return "other";
        }

        private static string FriendlyTitle(string symbol, int slot, string registryNote)
        {
            if (!string.IsNullOrEmpty(symbol))
            {
                var s = symbol;
                if (s.StartsWith("GI_IRS_", StringComparison.Ordinal))
                {
                    s = s.Substring("GI_IRS_".Length);
                }
                else if (s.StartsWith("GI_", StringComparison.Ordinal))
                {
                    s = s.Substring("GI_".Length);
                }

                var parts = new List<string>();
                foreach (var word in s.Split('_'))
                {
                    if (string.IsNullOrEmpty(word))
                    {
                        continue;
                    }

                    if (word.ToUpperInvariant() == word && word.Length > 1)
                    {
                        parts.Add(word);
                    }
                    else
                    {
                        parts.Add(char.ToUpperInvariant(word[0]) + word.Substring(1));
                    }
                }

                if (parts.Count > 0)
                {
                    return string.Join(" ", parts.ToArray());
                }
            }

            if (!string.IsNullOrEmpty(registryNote))
            {
                var semi = registryNote.IndexOf(';');
                return semi >= 0 ? registryNote.Substring(0, semi).Trim() : registryNote.Trim();
            }

            return "GlobalInt slot " + slot;
        }

        private static string FormatRoomTitle(string room, Dictionary<string, string> roomTitles)
        {
            if (IsAllDigits(room))
            {
                string title;
                if (roomTitles != null && roomTitles.TryGetValue(room, out title) && !string.IsNullOrEmpty(title))
                {
                    return room + " — " + title;
                }
            }

            return room;
        }

        private static Tuple<int, string> RoomSortKey(string room)
        {
            int number;
            if (int.TryParse(room, out number))
            {
                return Tuple.Create(0, number.ToString("D8"));
            }

            return Tuple.Create(1, room);
        }

        private static bool IsAllDigits(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (var ch in value)
            {
                if (ch < '0' || ch > '9')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
