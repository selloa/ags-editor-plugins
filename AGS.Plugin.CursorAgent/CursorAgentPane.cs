using System;
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

            var loadMockButton = new Button
            {
                Text = "Load mock diff",
                Location = new Point(12, 355),
                Size = new Size(110, 28)
            };
            loadMockButton.Click += (sender, args) => LoadMockDiff();

            var captureContextButton = new Button
            {
                Text = "Capture context",
                Location = new Point(130, 355),
                Size = new Size(110, 28)
            };
            captureContextButton.Click += (sender, args) => CaptureContextFromEditor();

            var applyButton = new Button
            {
                Text = "Apply manually",
                Location = new Point(248, 355),
                Size = new Size(110, 28)
            };
            applyButton.Click += ApplyButtonClick;

            Controls.Add(title);
            Controls.Add(originalLabel);
            Controls.Add(_originalText);
            Controls.Add(proposedLabel);
            Controls.Add(_proposedText);
            Controls.Add(loadMockButton);
            Controls.Add(captureContextButton);
            Controls.Add(applyButton);

            Size = new Size(390, 400);
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
            _proposedText.Text = "function StartGame() {\r\n    player.Say(\"Hello from Cursor Agent\");\r\n}";
        }

        public void CaptureContextFromEditor()
        {
            string contextText;
            string statusMessage;
            var hasContext = EditorContextCapture.TryCapture(
                _editor,
                _ownComponentId ?? string.Empty,
                _ownPanel,
                out contextText,
                out statusMessage);

            if (!hasContext)
            {
                _editor.GUIController.ShowMessage(statusMessage, MessageBoxIconType.Warning);
                return;
            }

            _originalText.Text = contextText;
            _editor.GUIController.ShowMessage(statusMessage, MessageBoxIconType.Information);
        }

        private void ApplyButtonClick(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_proposedText.Text))
            {
                _editor.GUIController.ShowMessage("There is no proposed patch to apply.", MessageBoxIconType.Warning);
                return;
            }

            _editor.GUIController.ShowMessage(
                "Phase 1 scaffold: patch apply remains manual. Next step is a safe line-based apply engine.",
                MessageBoxIconType.Information);
        }

    }
}
