using System.Collections.Generic;
using AGS.Plugin.GameStates.Core;
using AGS.Types;

namespace AGS.Plugin.GameStates
{
    public sealed class GameStatesComponent : IEditorComponent
    {
        private const string ComponentId = "GameStatesComponent";
        private const string RootNodeControlId = "GameStatesRootNode";
        private const string MainMenuId = "GameStatesMenu";
        private const string OpenPanelControlId = "GameStatesOpenPanel";
        private const string RefreshControlId = "GameStatesRefresh";
        private const string ExportHtmlControlId = "GameStatesExportHtml";

        private readonly IAGSEditor _editor;
        private readonly ContentDocument _panel;
        private readonly StateCatalogPane _paneControl;
        private readonly MenuCommands _mainMenuItems;

        public GameStatesComponent(IAGSEditor editor)
        {
            _editor = editor;
            _paneControl = new StateCatalogPane(
                _editor,
                DefaultStateCatalogProfile.Instance,
                "Export state catalog HTML");
            _panel = new ContentDocument(_paneControl, "Game States", this);

            _editor.GUIController.RegisterIcon("GameStatesIcon", GetIcon("PluginIcon.ico"));
            _editor.GUIController.ProjectTree.AddTreeRoot(this, RootNodeControlId, "Game States", "GameStatesIcon");

            _mainMenuItems = new MenuCommands(MainMenuId);
            _mainMenuItems.Commands.Add(new MenuCommand(OpenPanelControlId, "Open Game States Panel"));
            _mainMenuItems.Commands.Add(new MenuCommand(RefreshControlId, "Refresh Catalog"));
            _mainMenuItems.Commands.Add(new MenuCommand(ExportHtmlControlId, "Export HTML..."));

            _editor.GUIController.AddMenu(this, MainMenuId, "Game States", _editor.GUIController.FileMenuID);
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
