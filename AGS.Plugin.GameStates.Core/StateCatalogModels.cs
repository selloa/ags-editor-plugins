using System.Collections.Generic;

namespace AGS.Plugin.GameStates.Core
{
    public sealed class DefineInfo
    {
        public int Slot { get; set; }
        public string Comment { get; set; }
        public string Prefix { get; set; }
        public string File { get; set; }
    }

    public sealed class RefSite
    {
        public string File { get; set; }
        public int Line { get; set; }
        public string Op { get; set; }
        public string Function { get; set; }
        public string Room { get; set; }
        public string Value { get; set; }
        public string Context { get; set; }
        public string RawArg { get; set; }
    }

    public sealed class FlagEntry
    {
        public int Slot { get; set; }
        public string Symbol { get; set; }
        public string Category { get; set; }
        public string Comment { get; set; }
        public string FriendlyTitle { get; set; }
        public string RegistryNote { get; set; }
        public string InitialValue { get; set; }
        public string InitialSource { get; set; }
        public List<RefSite> Sets { get; set; }
        public List<RefSite> Gets { get; set; }
        public List<string> Warnings { get; set; }

        public FlagEntry()
        {
            Sets = new List<RefSite>();
            Gets = new List<RefSite>();
            Warnings = new List<string>();
        }

        public int SetCount
        {
            get { return Sets == null ? 0 : Sets.Count; }
        }

        public int GetCount
        {
            get { return Gets == null ? 0 : Gets.Count; }
        }

        public List<string> Rooms
        {
            get
            {
                var seen = new HashSet<string>();
                var result = new List<string>();
                AppendRooms(Sets, seen, result);
                AppendRooms(Gets, seen, result);
                result.Sort((a, b) =>
                {
                    int ai;
                    int bi;
                    if (int.TryParse(a, out ai) && int.TryParse(b, out bi))
                    {
                        return ai.CompareTo(bi);
                    }

                    return string.CompareOrdinal(a, b);
                });
                return result;
            }
        }

        public string Anchor
        {
            get
            {
                return !string.IsNullOrEmpty(Symbol)
                    ? "flag-" + Symbol
                    : "flag-slot-" + Slot;
            }
        }

        private static void AppendRooms(List<RefSite> sites, HashSet<string> seen, List<string> result)
        {
            if (sites == null)
            {
                return;
            }

            foreach (var site in sites)
            {
                if (site == null || string.IsNullOrEmpty(site.Room))
                {
                    continue;
                }

                if (!IsAllDigits(site.Room) || seen.Contains(site.Room))
                {
                    continue;
                }

                seen.Add(site.Room);
                result.Add(site.Room);
            }
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

    public sealed class CatalogWarning
    {
        public string Kind { get; set; }
        public string Symbol { get; set; }
        public int Slot { get; set; }
        public string RawArg { get; set; }
        public int Line { get; set; }
        public string File { get; set; }
        public string Context { get; set; }
        public string Detail { get; set; }
    }

    public sealed class StateCatalogSummary
    {
        public int StoryFlags { get; set; }
        public int DoorSlots { get; set; }
        public int EngineSlots { get; set; }
        public int LegacySlots { get; set; }
        public int Warnings { get; set; }
    }

    public sealed class StateCatalogRow
    {
        public string[] Cells { get; set; }
    }

    public sealed class StateCatalogSection
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string ParentTreeId { get; set; }
        public string[] ColumnHeaders { get; set; }
        public List<StateCatalogRow> Rows { get; set; }

        public int Count
        {
            get { return Rows == null ? 0 : Rows.Count; }
        }
    }

    public sealed class StateCatalogDocument
    {
        public string GameTitle { get; set; }
        public string SourceLabel { get; set; }
        public string GeneratedAt { get; set; }
        public int FilesScanned { get; set; }
        public StateCatalogSummary Summary { get; set; }
        public Dictionary<string, string> RoomTitles { get; set; }
        public Dictionary<int, FlagEntry> Flags { get; set; }
        public List<FlagEntry> Story { get; set; }
        public List<FlagEntry> Doors { get; set; }
        public List<FlagEntry> Engine { get; set; }
        public List<FlagEntry> Legacy { get; set; }
        public List<FlagEntry> Other { get; set; }
        public List<FlagEntry> ResetList { get; set; }
        public Dictionary<string, List<FlagEntry>> ByRoom { get; set; }
        public List<CatalogWarning> CatalogWarnings { get; set; }
        public Dictionary<string, DefineInfo> RelatedDefines { get; set; }
        public List<StateCatalogSection> Sections { get; set; }
        public IStateCatalogProfile Profile { get; set; }

        public string StatsSummary
        {
            get
            {
                if (Summary == null)
                {
                    return string.Empty;
                }

                return string.Format(
                    "Story: {0} · Doors: {1} · Engine: {2} · Warnings: {3}",
                    Summary.StoryFlags,
                    Summary.DoorSlots,
                    Summary.EngineSlots,
                    Summary.Warnings);
            }
        }
    }
}
