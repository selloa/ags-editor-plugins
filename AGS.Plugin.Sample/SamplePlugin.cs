using AGS.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace AGS.Plugin.Sample
{
	[RequiredAGSVersion("3.0.0.0")]
	public class SamplePlugin : IAGSEditorPlugin
	{
		public SamplePlugin(IAGSEditor editor)
		{
			editor.AddComponent(new SampleComponent(editor));
		}

		public void Dispose()
		{
			// We don't need any cleanup code
		}

	}
}
