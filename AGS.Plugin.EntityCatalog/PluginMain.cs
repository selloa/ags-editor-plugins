using AGS.Types;

namespace AGS.Plugin.EntityCatalog
{
    [RequiredAGSVersion("3.6.0.0")]
    public class PluginMain : IAGSEditorPlugin
    {
        public PluginMain(IAGSEditor editor)
        {
            editor.AddComponent(new EntityCatalogComponent(editor));
        }

        public void Dispose()
        {
        }
    }
}
