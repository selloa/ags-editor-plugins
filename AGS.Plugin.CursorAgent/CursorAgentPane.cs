using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AGS.Types;

namespace AGS.Plugin.CursorAgent
{
    public class CursorAgentPane : EditorContentPanel
    {
        private readonly IAGSEditor _editor;
        private readonly TextBox _originalText;
        private readonly TextBox _proposedText;
        private readonly Label _captureStatusLabel;
        private EditorCaptureContext _captureContext;
        private string _lastApplyBefore;
        private string _lastApplyAfter;
        private IScriptEditorControl _lastApplyScriptEditor;
        private string _lastApplyScriptBefore;
        private string _lastApplyScriptAfter;
        private ContentDocument _ownPanel;
        private string _ownComponentId;

        public CursorAgentPane(IAGSEditor editor)
        {
            _editor = editor;

            var title = new Label
            {
                Text = "Cursor Agent patch preview (Phase 1 scaffold)",
                AutoSize = true,
                Location = new Point(12, 12)
            };

            var originalLabel = new Label
            {
                Text = "Original selection",
                AutoSize = true,
                Location = new Point(12, 40)
            };

            _originalText = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Location = new Point(12, 58),
                Size = new Size(360, 130)
            };

            var proposedLabel = new Label
            {
                Text = "Proposed output",
                AutoSize = true,
                Location = new Point(12, 197)
            };

            _proposedText = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Location = new Point(12, 215),
                Size = new Size(360, 130)
            };

            _captureStatusLabel = new Label
            {
                Text = "Target: none (capture first)",
                AutoSize = true,
                Location = new Point(12, 347)
            };

            var loadMockButton = new Button
            {
                Text = "Test mock",
                Location = new Point(12, 369),
                Size = new Size(80, 28)
            };
            loadMockButton.Click += (sender, args) => LoadMockDiff();

            var captureContextButton = new Button
            {
                Text = "Capture context",
                Location = new Point(98, 369),
                Size = new Size(90, 28)
            };
            captureContextButton.Click += (sender, args) => CaptureContextFromEditor();

            var selectionTemplateButton = new Button
            {
                Text = "Template sel",
                Location = new Point(194, 369),
                Size = new Size(80, 28)
            };
            selectionTemplateButton.Click += SelectionTemplateButtonClick;

            var previewButton = new Button
            {
                Text = "Preview",
                Location = new Point(234, 403),
                Size = new Size(60, 28)
            };
            previewButton.Click += PreviewButtonClick;

            var applyButton = new Button
            {
                Text = "Apply manually",
                Location = new Point(12, 403),
                Size = new Size(110, 28)
            };
            applyButton.Click += ApplyButtonClick;

            var undoButton = new Button
            {
                Text = "Undo",
                Location = new Point(128, 403),
                Size = new Size(80, 28)
            };
            undoButton.Click += UndoButtonClick;

            Controls.Add(title);
            Controls.Add(originalLabel);
            Controls.Add(_originalText);
            Controls.Add(proposedLabel);
            Controls.Add(_proposedText);
            Controls.Add(_captureStatusLabel);
            Controls.Add(loadMockButton);
            Controls.Add(captureContextButton);
            Controls.Add(selectionTemplateButton);
            Controls.Add(previewButton);
            Controls.Add(applyButton);
            Controls.Add(undoButton);

            Size = new Size(390, 440);
        }

        public void SetCaptureContext(ContentDocument ownPanel, string ownComponentId)
        {
            _ownPanel = ownPanel;
            _ownComponentId = ownComponentId;
        }

        public string OriginalText
        {
            get { return _originalText.Text; }
            set { _originalText.Text = value; }
        }

        public string ProposedText
        {
            get { return _proposedText.Text; }
            set { _proposedText.Text = value; }
        }

        public void LoadMockDiff()
        {
            _originalText.Text = "function StartGame() {\r\n    // TODO\r\n}";
            _proposedText.Text =
                "REPLACE\r\n" +
                "OLD:\r\n" +
                "    // TODO\r\n" +
                "END\r\n" +
                "NEW:\r\n" +
                "    player.Say(\"Hello from Cursor Agent\");\r\n" +
                "END\r\n";
        }

        public void CaptureContextFromEditor()
        {
            EditorCaptureContext context;
            string contextText;
            string statusMessage;
            var hasContext = EditorContextCapture.TryCapture(
                _editor,
                _ownComponentId ?? string.Empty,
                _ownPanel,
                out context,
                out contextText,
                out statusMessage);

            if (!hasContext)
            {
                _editor.GUIController.ShowMessage(statusMessage, MessageBoxIconType.Warning);
                return;
            }

            _captureContext = context;
            _originalText.Text = contextText;
            UpdateCaptureStatus();
            _editor.GUIController.ShowMessage(statusMessage, MessageBoxIconType.Information);
        }

        private void ApplyButtonClick(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_proposedText.Text))
            {
                _editor.GUIController.ShowMessage("There is no proposed patch to apply.", MessageBoxIconType.Warning);
                return;
            }

            List<PatchOperation> operations;
            string parseError;
            if (!TryParseProposedOperations(_proposedText.Text, out operations, out parseError))
            {
                _editor.GUIController.ShowMessage("Patch parse failed: " + parseError, MessageBoxIconType.Warning);
                return;
            }

            var applied = PatchEngine.Apply(_originalText.Text, operations);
            if (!applied.Success)
            {
                _editor.GUIController.ShowMessage("Patch apply failed: " + applied.ErrorMessage, MessageBoxIconType.Warning);
                return;
            }

            string scriptBefore = null;
            string scriptAfter = null;
            if (!TryApplyToScript(applied.UpdatedText, out scriptBefore, out scriptAfter, out var scriptApplyError))
            {
                _editor.GUIController.ShowMessage("Patch apply failed: " + scriptApplyError, MessageBoxIconType.Warning);
                return;
            }

            _lastApplyBefore = _originalText.Text;
            _lastApplyAfter = applied.UpdatedText;
            _originalText.Text = applied.UpdatedText;
            _lastApplyScriptBefore = scriptBefore;
            _lastApplyScriptAfter = scriptAfter;
            _lastApplyScriptEditor = _captureContext == null ? null : _captureContext.ScriptEditor;

            _editor.GUIController.ShowMessage(
                "Patch applied safely. " + PatchEngine.BuildSummary(operations),
                MessageBoxIconType.Information);
        }

        private void PreviewButtonClick(object sender, EventArgs e)
        {
            List<PatchOperation> operations;
            string parseError;
            if (!TryParseProposedOperations(_proposedText.Text, out operations, out parseError))
            {
                _editor.GUIController.ShowMessage("Patch parse failed: " + parseError, MessageBoxIconType.Warning);
                return;
            }

            _editor.GUIController.ShowMessage(PatchEngine.BuildSummary(operations), MessageBoxIconType.Information);
        }

        private void SelectionTemplateButtonClick(object sender, EventArgs e)
        {
            if (_captureContext == null || !_captureContext.HasSelection)
            {
                _editor.GUIController.ShowMessage("Selection template needs a captured selection. Select code, then capture context.", MessageBoxIconType.Warning);
                return;
            }

            _proposedText.Text =
                "REPLACE\r\n" +
                "OLD:\r\n" +
                _captureContext.SelectedText + "\r\n" +
                "END\r\n" +
                "NEW:\r\n" +
                "\r\n" +
                "END\r\n";
            _editor.GUIController.ShowMessage("Selection-based replace template generated.", MessageBoxIconType.Information);
        }

        private void UndoButtonClick(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_lastApplyBefore))
            {
                _editor.GUIController.ShowMessage("There is no applied patch to undo.", MessageBoxIconType.Warning);
                return;
            }

            if (_originalText.Text != _lastApplyAfter)
            {
                _editor.GUIController.ShowMessage("Undo unavailable because text changed after apply.", MessageBoxIconType.Warning);
                return;
            }

            if (_lastApplyScriptEditor != null && _lastApplyScriptAfter != null)
            {
                var currentScriptText = _lastApplyScriptEditor.Text ?? string.Empty;
                if (!string.Equals(currentScriptText, _lastApplyScriptAfter, StringComparison.Ordinal))
                {
                    _editor.GUIController.ShowMessage("Undo unavailable because script content changed after apply.", MessageBoxIconType.Warning);
                    return;
                }
            }

            _originalText.Text = _lastApplyBefore;
            if (_lastApplyScriptEditor != null && _lastApplyScriptBefore != null)
            {
                _lastApplyScriptEditor.Text = _lastApplyScriptBefore;
            }
            _lastApplyBefore = null;
            _lastApplyAfter = null;
            _lastApplyScriptBefore = null;
            _lastApplyScriptAfter = null;
            _lastApplyScriptEditor = null;
            _editor.GUIController.ShowMessage("Last plugin-applied patch was rolled back.", MessageBoxIconType.Information);
        }

        private bool TryApplyToScript(string updatedPanelText, out string scriptBefore, out string scriptAfter, out string errorMessage)
        {
            scriptBefore = null;
            scriptAfter = null;
            errorMessage = string.Empty;

            if (_captureContext == null || _captureContext.ScriptEditor == null)
            {
                return true;
            }

            var scriptEditor = _captureContext.ScriptEditor;
            var currentScript = scriptEditor.Text ?? string.Empty;
            scriptBefore = currentScript;

            if (_captureContext.HasSelection)
            {
                var start = _captureContext.SelectionStart;
                var end = _captureContext.SelectionEnd;
                if (start < 0 || end < start || end > currentScript.Length)
                {
                    errorMessage = "Captured selection is no longer valid in the current script.";
                    return false;
                }

                var selectedNow = currentScript.Substring(start, end - start);
                if (!string.Equals(selectedNow, _originalText.Text, StringComparison.Ordinal))
                {
                    errorMessage = "Selection conflict: script selection changed since capture.";
                    return false;
                }

                scriptAfter = currentScript.Substring(0, start) + updatedPanelText + currentScript.Substring(end);
                scriptEditor.Text = scriptAfter;
                return true;
            }

            if (!string.Equals(currentScript, _originalText.Text, StringComparison.Ordinal))
            {
                errorMessage = "Script conflict: full script changed since capture.";
                return false;
            }

            scriptAfter = updatedPanelText;
            scriptEditor.Text = scriptAfter;
            return true;
        }

        private void UpdateCaptureStatus()
        {
            if (_captureContext == null)
            {
                _captureStatusLabel.Text = "Target: none (capture first)";
                return;
            }

            var mode = _captureContext.HasSelection ? "selection" : "full script";
            var name = string.IsNullOrWhiteSpace(_captureContext.ScriptPaneName) ? "<unknown>" : _captureContext.ScriptPaneName;
            _captureStatusLabel.Text = "Target: " + name + " (" + mode + ")";
        }

        private bool TryParseProposedOperations(string proposedText, out List<PatchOperation> operations, out string errorMessage)
        {
            operations = new List<PatchOperation>();
            errorMessage = string.Empty;

            if (CursorResponseContractParser.LooksLikeJsonContract(proposedText))
            {
                var parsedContract = CursorResponseContractParser.Parse(proposedText);
                if (!parsedContract.Success)
                {
                    errorMessage = parsedContract.ErrorMessage;
                    return false;
                }

                var adapted = CursorContractPatchAdapter.ToPatchOperations(parsedContract.Operations);
                if (!adapted.Success)
                {
                    errorMessage = adapted.ErrorMessage;
                    return false;
                }

                operations = adapted.Operations;
                return true;
            }

            var parsedPatch = PatchEngine.Parse(proposedText);
            if (!parsedPatch.Success)
            {
                errorMessage = parsedPatch.ErrorMessage;
                return false;
            }

            operations = parsedPatch.Operations;
            return true;
        }

    }
}
