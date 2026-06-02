using System.Text;
using AGS.Types;

namespace AGS.Plugin.CursorAgent
{
    internal sealed class EditorCaptureContext
    {
        public IScriptEditorControl ScriptEditor { get; set; }
        public string ScriptPaneName { get; set; }
        public string SelectedText { get; set; }
        public string FullScriptText { get; set; }
        public int SelectionStart { get; set; }
        public int SelectionEnd { get; set; }
        public bool HasSelection
        {
            get { return !string.IsNullOrEmpty(SelectedText); }
        }
    }

    internal static class EditorContextCapture
    {
        public static bool TryCapture(
            IAGSEditor editor,
            string ownComponentId,
            ContentDocument ownPanel,
            out EditorCaptureContext captureContext,
            out string contextText,
            out string statusMessage)
        {
            captureContext = null;
            var details = new StringBuilder();

            var game = editor.CurrentGame;
            if (game != null && game.Settings != null && !string.IsNullOrWhiteSpace(game.Settings.GameName))
            {
                details.AppendLine("Game: " + game.Settings.GameName);
            }

            IScriptEditorControl scriptEditor = null;
            string scriptPaneName = null;
            var foundSelection = false;

            var activePane = editor.GUIController.ActivePane;
            if (TryGetScriptEditor(activePane, ownComponentId, ownPanel, out scriptEditor, out scriptPaneName))
            {
                foundSelection = HasSelection(scriptEditor);
            }

            if (!foundSelection)
            {
                foreach (var pane in editor.GUIController.Panes)
                {
                    if (pane == ownPanel || pane == activePane)
                    {
                        continue;
                    }

                    if (!TryGetScriptEditor(pane, ownComponentId, ownPanel, out var candidate, out var candidateName))
                    {
                        continue;
                    }

                    if (HasSelection(candidate))
                    {
                        scriptEditor = candidate;
                        scriptPaneName = candidateName;
                        foundSelection = true;
                        break;
                    }

                    if (scriptEditor == null)
                    {
                        scriptEditor = candidate;
                        scriptPaneName = candidateName;
                    }
                }
            }

            if (scriptEditor != null)
            {
                captureContext = new EditorCaptureContext
                {
                    ScriptEditor = scriptEditor,
                    ScriptPaneName = scriptPaneName,
                    SelectedText = scriptEditor.SelectedText ?? string.Empty,
                    FullScriptText = scriptEditor.Text ?? string.Empty,
                    SelectionStart = scriptEditor.SelectionStart,
                    SelectionEnd = scriptEditor.SelectionEnd
                };

                details.AppendLine("Script: " + scriptPaneName);

                if (!string.IsNullOrEmpty(captureContext.SelectedText))
                {
                    contextText = captureContext.SelectedText;
                    statusMessage = "Captured script selection.";
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(captureContext.FullScriptText))
                {
                    contextText = captureContext.FullScriptText;
                    statusMessage = "Captured open script text (no selection). Select text first for a smaller capture.";
                    return true;
                }
            }

            if (details.Length > 0)
            {
                details.AppendLine();
                details.AppendLine("No open script editor found. Open a script tab and try again.");
                contextText = details.ToString().Trim();
                statusMessage = "Captured game metadata only. Open a script tab to capture code.";
                return true;
            }

            contextText = string.Empty;
            statusMessage = "Open a script tab (with optional selection), then capture again.";
            return false;
        }

        private static bool TryGetScriptEditor(
            ContentDocument pane,
            string ownComponentId,
            ContentDocument ownPanel,
            out IScriptEditorControl scriptEditor,
            out string paneName)
        {
            scriptEditor = null;
            paneName = null;

            if (pane == null || pane == ownPanel)
            {
                return false;
            }

            if (pane.Owner != null && pane.Owner.ComponentID == ownComponentId)
            {
                return false;
            }

            var scriptPane = pane.Control as IScriptEditor;
            if (scriptPane == null)
            {
                return false;
            }

            scriptEditor = scriptPane.ScriptEditorControl;
            if (scriptEditor == null)
            {
                return false;
            }

            paneName = pane.Name;
            return true;
        }

        private static bool HasSelection(IScriptEditorControl scriptEditor)
        {
            return scriptEditor != null && !string.IsNullOrEmpty(scriptEditor.SelectedText);
        }
    }
}
