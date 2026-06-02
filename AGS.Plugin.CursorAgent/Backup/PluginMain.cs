using AGS.Types;

namespace AGS.Plugin.CursorAgent
{
    [RequiredAGSVersion("3.6.0.0")]
    public class PluginMain : IAGSEditorPlugin
    {
        public PluginMain(IAGSEditor editor)
        {
            editor.AddComponent(new CursorAgentComponent(editor));
        }

        public void Dispose()
        {
        }
    }
}
