using System.Collections.Generic;

namespace AGS.Plugin.EntityCatalog
{
    public sealed class CatalogRow
    {
        public string[] Cells { get; set; }
    }

    public sealed class CatalogSection
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Note { get; set; }
        public string[] ColumnHeaders { get; set; }
        public List<CatalogRow> Rows { get; set; }

        public int Count
        {
            get { return Rows == null ? 0 : Rows.Count; }
        }
    }

    public sealed class CatalogDocument
    {
        public string GameTitle { get; set; }
        public string SourceLabel { get; set; }
        public List<CatalogSection> Sections { get; set; }

        public string StatsSummary
        {
            get
            {
                if (Sections == null || Sections.Count == 0)
                {
                    return string.Empty;
                }

                var parts = new List<string>();
                foreach (var section in Sections)
                {
                    parts.Add(section.Title + ": " + section.Count);
                }

                return string.Join(" · ", parts.ToArray());
            }
        }

        public CatalogSection FindSection(string sectionId)
        {
            if (Sections == null || sectionId == null)
            {
                return null;
            }

            foreach (var section in Sections)
            {
                if (section.Id == sectionId)
                {
                    return section;
                }
            }

            return null;
        }
    }
}
