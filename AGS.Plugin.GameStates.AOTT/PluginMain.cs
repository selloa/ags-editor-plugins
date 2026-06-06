using AGS.Types;

namespace AGS.Plugin.GameStates.AOTT
{
    [RequiredAGSVersion("3.6.0.0")]
    public class PluginMain : IAGSEditorPlugin
    {
        public PluginMain(IAGSEditor editor)
        {
            editor.AddComponent(new GameStatesAottComponent(editor));
        }

        public void Dispose()
        {
        }
    }
}
