using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AGS.Types;

namespace AGS.Plugin.EntityCatalog
{
    public partial class EntityCatalogPane : EditorContentPanel
    {
        private readonly IAGSEditor _editor;
        private CatalogDocument _document;
        private CatalogSection _selectedSection;
        private List<CatalogRow> _allRowsForSection = new List<CatalogRow>();

        public EntityCatalogPane(IAGSEditor editor)
        {
            _editor = editor;
            InitializeComponent();
        }

        public new void Refresh()
        {
            if (_editor.CurrentGame == null)
            {
                Clear();
                lblStats.Text = "No game loaded.";
                return;
            }

            _document = EntityCatalogGatherer.Gather(_editor.CurrentGame);
            lblStats.Text = _document.StatsSummary;
            RebuildTree(_document);
        }

        public void Clear()
        {
            _document = null;
            _selectedSection = null;
            _allRowsForSection.Clear();
            treeSections.Nodes.Clear();
            gridCatalog.Columns.Clear();
            gridCatalog.Rows.Clear();
            txtFilter.Text = string.Empty;
            lblStats.Text = string.Empty;
        }

        public void ExportHtml()
        {
            if (_document == null)
            {
                Refresh();
            }

            if (_document == null || _editor.CurrentGame == null)
            {
                _editor.GUIController.ShowMessage("Open a game project before exporting.", MessageBoxIconType.Information);
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "HTML files (*.html)|*.html|All files (*.*)|*.*";
                dialog.FileName = "Game_entity_catalog.html";
                dialog.Title = "Export entity catalog HTML";
                if (_editor.CurrentGame.DirectoryPath != null)
                {
                    dialog.InitialDirectory = _editor.CurrentGame.DirectoryPath;
                }

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    var html = EntityCatalogHtmlExporter.FormatHtml(_document);
                    File.WriteAllText(dialog.FileName, html, Encoding.UTF8);
                    _editor.GUIController.ShowMessage(
                        "Exported entity catalog to:\n" + dialog.FileName,
                        MessageBoxIconType.Information);
                }
                catch (Exception ex)
                {
                    _editor.GUIController.ShowMessage(
                        "Export failed:\n" + ex.Message,
                        MessageBoxIconType.Warning);
                }
            }
        }

        private void RebuildTree(CatalogDocument document)
        {
            treeSections.BeginUpdate();
            treeSections.Nodes.Clear();
            gridCatalog.Columns.Clear();
            gridCatalog.Rows.Clear();
            _selectedSection = null;
            _allRowsForSection.Clear();

            if (document.Sections != null)
            {
                foreach (var section in document.Sections)
                {
                    var label = FormatSectionTreeLabel(section);
                    var node = treeSections.Nodes.Add(section.Id, label);
                    node.Tag = section;
                }
            }

            if (treeSections.Nodes.Count > 0)
            {
                treeSections.SelectedNode = treeSections.Nodes[0];
            }

            treeSections.EndUpdate();
            UpdateTreeFilterLabels();
        }

        private string FormatSectionTreeLabel(CatalogSection section)
        {
            var tokens = NormalizeTokens(txtFilter.Text);
            if (tokens.Length == 0)
            {
                return string.Format("{0} ({1})", section.Title, section.Count);
            }

            var matches = CountMatchingRows(section, tokens);
            return string.Format("{0} ({1}/{2})", section.Title, matches, section.Count);
        }

        private void BindSection(CatalogSection section)
        {
            _selectedSection = section;
            _allRowsForSection = section.Rows ?? new List<CatalogRow>();

            gridCatalog.Columns.Clear();
            gridCatalog.Rows.Clear();

            if (section.ColumnHeaders != null)
            {
                foreach (var header in section.ColumnHeaders)
                {
                    gridCatalog.Columns.Add(header, header);
                }
            }

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            gridCatalog.Rows.Clear();
            if (_selectedSection == null)
            {
                UpdateTreeFilterLabels();
                return;
            }

            var tokens = NormalizeTokens(txtFilter.Text);
            foreach (var row in _allRowsForSection)
            {
                if (!RowMatches(row, tokens))
                {
                    continue;
                }

                var values = row.Cells ?? new string[0];
                var rowValues = new object[_selectedSection.ColumnHeaders.Length];
                for (var i = 0; i < rowValues.Length; i++)
                {
                    rowValues[i] = i < values.Length ? values[i] : string.Empty;
                }

                gridCatalog.Rows.Add(rowValues);
            }

            UpdateTreeFilterLabels();
        }

        private void UpdateTreeFilterLabels()
        {
            if (_document == null || _document.Sections == null)
            {
                return;
            }

            var tokens = NormalizeTokens(txtFilter.Text);
            foreach (TreeNode node in treeSections.Nodes)
            {
                var section = node.Tag as CatalogSection;
                if (section == null)
                {
                    continue;
                }

                if (tokens.Length == 0)
                {
                    node.Text = string.Format("{0} ({1})", section.Title, section.Count);
                }
                else
                {
                    var matches = CountMatchingRows(section, tokens);
                    node.Text = string.Format("{0} ({1}/{2})", section.Title, matches, section.Count);
                }
            }
        }

        private static int CountMatchingRows(CatalogSection section, string[] tokens)
        {
            if (section.Rows == null || tokens.Length == 0)
            {
                return section.Count;
            }

            var count = 0;
            foreach (var row in section.Rows)
            {
                if (RowMatches(row, tokens))
                {
                    count++;
                }
            }

            return count;
        }

        private static string[] NormalizeTokens(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new string[0];
            }

            return text.Trim()
                .ToLowerInvariant()
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool RowMatches(CatalogRow row, string[] tokens)
        {
            if (tokens.Length == 0)
            {
                return true;
            }

            var haystack = string.Join(" ", row.Cells ?? new string[0]).ToLowerInvariant();
            return tokens.All(token => haystack.Contains(token));
        }

        private void treeSections_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var section = e.Node == null ? null : e.Node.Tag as CatalogSection;
            if (section == null)
            {
                return;
            }

            BindSection(section);
        }

        private void txtFilter_TextChanged(object sender, EventArgs e)
        {
            ApplyFilter();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            Refresh();
        }

        private void btnExportHtml_Click(object sender, EventArgs e)
        {
            ExportHtml();
        }
    }
}
