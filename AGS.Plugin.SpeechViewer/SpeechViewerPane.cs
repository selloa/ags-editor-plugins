using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AGS.Types;

namespace AGS.Plugin.SpeechViewer
{
    public partial class SpeechViewerPane : EditorContentPanel
    {
        private readonly IAGSEditor _editor;
        private SpeechDocument _document;
        private IList<SpeechRow> _allRowsForBinding = new List<SpeechRow>();
        private bool _optionsOnly;

        public SpeechViewerPane(IAGSEditor editor)
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

            _document = SpeechGatherer.Gather(_editor.CurrentGame);
            lblStats.Text = _document.StatsSummary;
            RebuildTree(_document);
        }

        public void Clear()
        {
            _document = null;
            _allRowsForBinding = new List<SpeechRow>();
            treeNavigation.Nodes.Clear();
            gridSpeech.Rows.Clear();
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
                dialog.FileName = "VoiceSpeech.html";
                dialog.Title = "Export speech viewer HTML";
                if (_editor.CurrentGame.DirectoryPath != null)
                {
                    var voiceDir = Path.Combine(_editor.CurrentGame.DirectoryPath, "voice-work");
                    try
                    {
                        Directory.CreateDirectory(voiceDir);
                        dialog.InitialDirectory = voiceDir;
                    }
                    catch
                    {
                        dialog.InitialDirectory = _editor.CurrentGame.DirectoryPath;
                    }
                }

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    var html = SpeechHtmlExporter.FormatHtml(_document);
                    File.WriteAllText(dialog.FileName, html, Encoding.UTF8);
                    _editor.GUIController.ShowMessage(
                        "Exported speech data to:\n" + dialog.FileName,
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

        private void RebuildTree(SpeechDocument document)
        {
            treeNavigation.BeginUpdate();
            treeNavigation.Nodes.Clear();
            gridSpeech.Rows.Clear();
            _allRowsForBinding = new List<SpeechRow>();

            var part1 = treeNavigation.Nodes.Add("part1", "Part 1 — Dialog trees");
            if (document.DialogsMissing)
            {
                part1.Nodes.Add("missing", "(No dialogs found)");
            }
            else
            {
                foreach (var dialog in document.Dialogs)
                {
                    var dialogNode = part1.Nodes.Add(
                        "dlg-" + dialog.Id,
                        FormatTreeLabel(string.Format("dlg-{0} — {1}", dialog.Id, dialog.Name), GetDialogRows(dialog)));
                    dialogNode.Tag = dialog;
                    dialogNode.ToolTipText = string.Format("dlg-{0} — {1}", dialog.Id, dialog.Name);

                    if (dialog.Options != null && dialog.Options.Count > 0)
                    {
                        var optionsNode = dialogNode.Nodes.Add("options", FormatTreeLabel("Player options", dialog.Options));
                        optionsNode.Tag = dialog.Options;
                        optionsNode.ToolTipText = "Player options";
                    }

                    if (dialog.Branches != null)
                    {
                        foreach (var branch in dialog.Branches)
                        {
                            var rows = branch.Rows ?? new List<SpeechRow>();
                            var branchNode = dialogNode.Nodes.Add(
                                branch.Anchor,
                                FormatTreeLabel(branch.Title, rows));
                            branchNode.Tag = rows;
                            branchNode.ToolTipText = branch.Title;
                        }
                    }

                    if (!dialog.HasSpeech && (dialog.Options == null || dialog.Options.Count == 0))
                    {
                        dialogNode.Nodes.Add("empty", "(No speech lines)");
                    }
                }
            }

            var part2 = treeNavigation.Nodes.Add("part2", "Part 2 — Scripted speech");
            if (document.AscSections == null || document.AscSections.Count == 0)
            {
                part2.Nodes.Add("empty", "(No root *.asc speech lines)");
            }
            else
            {
                foreach (var section in document.AscSections)
                {
                    var label = section.Filename;
                    if (!string.IsNullOrEmpty(section.Error))
                    {
                        label += " (error)";
                    }

                    var rows = section.Rows ?? new List<SpeechRow>();
                    var sectionNode = part2.Nodes.Add(section.Anchor, FormatTreeLabel(label, rows));
                    sectionNode.Tag = rows;
                    sectionNode.ToolTipText = label;
                }
            }

            part1.Expand();
            part2.Expand();
            treeNavigation.EndUpdate();

            SelectFirstRowNode();
        }

        private void SelectFirstRowNode()
        {
            var first = FindFirstRowNode(treeNavigation.Nodes);
            if (first != null)
            {
                treeNavigation.SelectedNode = first;
            }
        }

        private static TreeNode FindFirstRowNode(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is IList<SpeechRow>)
                {
                    return node;
                }

                if (node.Tag is DialogSection)
                {
                    return node;
                }

                var child = FindFirstRowNode(node.Nodes);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private void BindRows(IList<SpeechRow> rows, bool optionsOnly)
        {
            _allRowsForBinding = rows ?? new List<SpeechRow>();
            _optionsOnly = optionsOnly;
            colSpeaker.Visible = !optionsOnly;
            colNotes.Visible = !optionsOnly;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            gridSpeech.Rows.Clear();
            var tokens = NormalizeTokens(txtFilter.Text);

            foreach (var row in _allRowsForBinding)
            {
                if (!RowMatches(row, tokens))
                {
                    continue;
                }

                gridSpeech.Rows.Add(
                    row.LineId ?? string.Empty,
                    _optionsOnly ? string.Empty : (row.Speaker ?? string.Empty),
                    row.Text ?? string.Empty,
                    _optionsOnly ? string.Empty : (row.Notes ?? string.Empty));
            }

            UpdateTreeFilterLabels();
        }

        private void UpdateTreeFilterLabels()
        {
            if (_document == null)
            {
                return;
            }

            var tokens = NormalizeTokens(txtFilter.Text);
            UpdateTreeNodeLabels(treeNavigation.Nodes, tokens);
        }

        private void UpdateTreeNodeLabels(TreeNodeCollection nodes, string[] tokens)
        {
            foreach (TreeNode node in nodes)
            {
                var rows = node.Tag as IList<SpeechRow>;
                if (rows != null)
                {
                    node.Text = FormatTreeLabel(GetBaseTreeLabel(node), rows, tokens);
                }
                else
                {
                    var dialog = node.Tag as DialogSection;
                    if (dialog != null)
                    {
                        node.Text = FormatTreeLabel(
                            GetBaseTreeLabel(node),
                            GetDialogRows(dialog),
                            tokens);
                    }
                }

                if (node.Nodes.Count > 0)
                {
                    UpdateTreeNodeLabels(node.Nodes, tokens);
                }
            }
        }

        private static string GetBaseTreeLabel(TreeNode node)
        {
            if (!string.IsNullOrEmpty(node.ToolTipText))
            {
                return node.ToolTipText;
            }

            var dialog = node.Tag as DialogSection;
            if (dialog != null)
            {
                return string.Format("dlg-{0} — {1}", dialog.Id, dialog.Name);
            }

            return node.Text;
        }

        private string FormatTreeLabel(string baseLabel, IList<SpeechRow> rows)
        {
            return FormatTreeLabel(baseLabel, rows, NormalizeTokens(txtFilter.Text));
        }

        private static string FormatTreeLabel(string baseLabel, IList<SpeechRow> rows, string[] tokens)
        {
            var total = rows == null ? 0 : rows.Count;
            if (tokens.Length == 0 || rows == null)
            {
                return total > 0 ? baseLabel + " (" + total + ")" : baseLabel;
            }

            var matches = rows.Count(row => RowMatches(row, tokens));
            return baseLabel + " (" + matches + "/" + total + ")";
        }

        private static IList<SpeechRow> GetDialogRows(DialogSection dialog)
        {
            var combined = new List<SpeechRow>();
            if (dialog.Options != null)
            {
                combined.AddRange(dialog.Options);
            }

            if (dialog.Branches != null)
            {
                foreach (var branch in dialog.Branches)
                {
                    if (branch.Rows != null)
                    {
                        combined.AddRange(branch.Rows);
                    }
                }
            }

            return combined;
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

        private static bool RowMatches(SpeechRow row, string[] tokens)
        {
            if (tokens.Length == 0)
            {
                return true;
            }

            var haystack = string.Join(
                " ",
                row.LineId ?? string.Empty,
                row.Speaker ?? string.Empty,
                row.Text ?? string.Empty,
                row.Notes ?? string.Empty).ToLowerInvariant();

            return tokens.All(token => haystack.Contains(token));
        }

        private void treeNavigation_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node == null)
            {
                return;
            }

            var rows = e.Node.Tag as IList<SpeechRow>;
            if (rows != null)
            {
                BindRows(rows, e.Node.Name == "options");
                return;
            }

            var dialog = e.Node.Tag as DialogSection;
            if (dialog != null)
            {
                BindRows(GetDialogRows(dialog), false);
            }
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
