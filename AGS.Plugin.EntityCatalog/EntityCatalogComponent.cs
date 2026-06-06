using System.Collections.Generic;
using AGS.Types;

namespace AGS.Plugin.EntityCatalog
{
    public sealed class EntityCatalogComponent : IEditorComponent
    {
        private const string ComponentId = "EntityCatalogComponent";
        private const string RootNodeControlId = "EntityCatalogRootNode";
        private const string MainMenuId = "EntityCatalogMenu";
        private const string OpenPanelControlId = "EntityCatalogOpenPanel";
        private const string RefreshControlId = "EntityCatalogRefresh";
        private const string ExportHtmlControlId = "EntityCatalogExportHtml";

        private readonly IAGSEditor _editor;
        private readonly ContentDocument _panel;
        private readonly EntityCatalogPane _paneControl;
        private readonly MenuCommands _mainMenuItems;

        public EntityCatalogComponent(IAGSEditor editor)
        {
            _editor = editor;
            _paneControl = new EntityCatalogPane(_editor);
            _panel = new ContentDocument(_paneControl, "Entity Catalog", this);

            _editor.GUIController.RegisterIcon("EntityCatalogIcon", GetIcon("PluginIcon.ico"));
            _editor.GUIController.ProjectTree.AddTreeRoot(this, RootNodeControlId, "Entity Catalog", "EntityCatalogIcon");

            _mainMenuItems = new MenuCommands(MainMenuId);
            _mainMenuItems.Commands.Add(new MenuCommand(OpenPanelControlId, "Open Entity Catalog Panel"));
            _mainMenuItems.Commands.Add(new MenuCommand(RefreshControlId, "Refresh Catalog"));
            _mainMenuItems.Commands.Add(new MenuCommand(ExportHtmlControlId, "Export HTML..."));

            _editor.GUIController.AddMenu(this, MainMenuId, "Entity Catalog", _editor.GUIController.FileMenuID);
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
