using System.Collections.Generic;
using AGS.Types;

namespace AGS.Plugin.CursorAgent
{
    public class CursorAgentComponent : IEditorComponent
    {
        private const string ComponentId = "CursorAgentComponent";
        private const string RootNodeControlId = "CursorAgentRootNode";
        private const string MainMenuId = "CursorAgentMenu";
        private const string OpenPanelControlId = "CursorAgentOpenPanel";
        private const string PreviewPatchControlId = "CursorAgentPreviewPatch";

        private readonly IAGSEditor _editor;
        private readonly ContentDocument _panel;
        private readonly CursorAgentPane _paneControl;
        private readonly MenuCommands _mainMenuItems;

        public CursorAgentComponent(IAGSEditor editor)
        {
            _editor = editor;
            _paneControl = new CursorAgentPane(_editor);
            _panel = new ContentDocument(_paneControl, "Cursor Agent", this);

            _editor.GUIController.ProjectTree.AddTreeRoot(this, RootNodeControlId, "Cursor Agent", null);

            _mainMenuItems = new MenuCommands(MainMenuId);
            _mainMenuItems.Commands.Add(new MenuCommand(OpenPanelControlId, "Open Cursor Agent Panel"));
            _mainMenuItems.Commands.Add(new MenuCommand(PreviewPatchControlId, "Load Mock Patch Preview"));

            _editor.GUIController.AddMenu(this, MainMenuId, "Cursor Agent", _editor.GUIController.FileMenuID);
            _editor.GUIController.AddMenuItems(this, _mainMenuItems);
        }

        public string ComponentID
        {
            get { return ComponentId; }
        }

        public IList<MenuCommand> GetContextMenu(string controlID)
        {
            return new List<MenuCommand>();
        }

        public void CommandClick(string controlID)
        {
            if (controlID == RootNodeControlId || controlID == OpenPanelControlId)
            {
                _editor.GUIController.AddOrShowPane(_panel);
                return;
            }

            if (controlID == PreviewPatchControlId)
            {
                _editor.GUIController.AddOrShowPane(_panel);
                _paneControl.LoadMockDiff();
            }
        }

        public void PropertyChanged(string propertyName, object oldValue)
        {
        }

        public void BeforeSaveGame()
        {
        }

        public void RefreshDataFromGame()
        {
            _editor.GUIController.RemovePaneIfExists(_panel);
        }

        public void GameSettingsChanged()
        {
        }

        public void ToXml(System.Xml.XmlTextWriter writer)
        {
            writer.WriteElementString("OriginalText", _paneControl.OriginalText);
            writer.WriteElementString("ProposedText", _paneControl.ProposedText);
        }

        public void FromXml(System.Xml.XmlNode node)
        {
            if (node == null)
            {
                _paneControl.OriginalText = string.Empty;
                _paneControl.ProposedText = string.Empty;
                return;
            }

            var originalNode = node.SelectSingleNode("OriginalText");
            var proposedNode = node.SelectSingleNode("ProposedText");

            _paneControl.OriginalText = originalNode == null ? string.Empty : originalNode.InnerText;
            _paneControl.ProposedText = proposedNode == null ? string.Empty : proposedNode.InnerText;
        }

        public void EditorShutdown()
        {
        }
    }
}
