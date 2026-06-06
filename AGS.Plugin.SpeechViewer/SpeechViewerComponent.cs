using System.Collections.Generic;
using AGS.Types;

namespace AGS.Plugin.SpeechViewer
{
    public sealed class SpeechViewerComponent : IEditorComponent
    {
        private const string ComponentId = "SpeechViewerComponent";
        private const string RootNodeControlId = "SpeechViewerRootNode";
        private const string MainMenuId = "SpeechViewerMenu";
        private const string OpenPanelControlId = "SpeechViewerOpenPanel";
        private const string RefreshControlId = "SpeechViewerRefresh";
        private const string ExportHtmlControlId = "SpeechViewerExportHtml";

        private readonly IAGSEditor _editor;
        private readonly ContentDocument _panel;
        private readonly SpeechViewerPane _paneControl;
        private readonly MenuCommands _mainMenuItems;

        public SpeechViewerComponent(IAGSEditor editor)
        {
            _editor = editor;
            _paneControl = new SpeechViewerPane(_editor);
            _panel = new ContentDocument(_paneControl, "Speech Viewer", this);

            _editor.GUIController.RegisterIcon("SpeechViewerIcon", GetIcon("PluginIcon.ico"));
            _editor.GUIController.ProjectTree.AddTreeRoot(this, RootNodeControlId, "Speech Viewer", "SpeechViewerIcon");

            _mainMenuItems = new MenuCommands(MainMenuId);
            _mainMenuItems.Commands.Add(new MenuCommand(OpenPanelControlId, "Open Speech Viewer Panel"));
            _mainMenuItems.Commands.Add(new MenuCommand(RefreshControlId, "Refresh Speech Data"));
            _mainMenuItems.Commands.Add(new MenuCommand(ExportHtmlControlId, "Export HTML..."));

            _editor.GUIController.AddMenu(this, MainMenuId, "Speech Viewer", _editor.GUIController.FileMenuID);
            _editor.GUIController.AddMenuItems(this, _mainMenuItems);
        }

        private System.Drawing.Icon GetIcon(string fileName)
        {
            return new System.Drawing.Icon(GetType(), fileName);
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

            if (controlID == RefreshControlId)
            {
                _editor.GUIController.AddOrShowPane(_panel);
                _paneControl.Refresh();
                return;
            }

            if (controlID == ExportHtmlControlId)
            {
                _editor.GUIController.AddOrShowPane(_panel);
                _paneControl.ExportHtml();
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
            _paneControl.Clear();
        }

        public void GameSettingsChanged()
        {
        }

        public void ToXml(System.Xml.XmlTextWriter writer)
        {
        }

        public void FromXml(System.Xml.XmlNode node)
        {
        }

        public void EditorShutdown()
        {
        }
    }
}
