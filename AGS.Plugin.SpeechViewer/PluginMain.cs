using AGS.Types;

namespace AGS.Plugin.SpeechViewer
{
    [RequiredAGSVersion("3.6.0.0")]
    public class PluginMain : IAGSEditorPlugin
    {
        public PluginMain(IAGSEditor editor)
        {
            editor.AddComponent(new SpeechViewerComponent(editor));
        }

        public void Dispose()
        {
        }
    }
}
