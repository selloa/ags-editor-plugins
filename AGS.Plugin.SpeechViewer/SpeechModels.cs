using System.Collections.Generic;

namespace AGS.Plugin.SpeechViewer
{
    public sealed class SpeechRow
    {
        public string LineId { get; set; }
        public string Speaker { get; set; }
        public string Text { get; set; }
        public string Notes { get; set; }
    }

    public sealed class DialogBranch
    {
        public string Anchor { get; set; }
        public string Title { get; set; }
        public List<SpeechRow> Rows { get; set; }
    }

    public sealed class DialogSection
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Anchor { get; set; }
        public List<SpeechRow> Options { get; set; }
        public List<DialogBranch> Branches { get; set; }
        public bool HasSpeech { get; set; }
    }

    public sealed class ScriptSection
    {
        public string Filename { get; set; }
        public string Anchor { get; set; }
        public string Error { get; set; }
        public List<SpeechRow> Rows { get; set; }
    }

    public sealed class SpeechDocument
    {
        public string AgfName { get; set; }
        public string RepoName { get; set; }
        public bool DialogsMissing { get; set; }
        public List<DialogSection> Dialogs { get; set; }
        public List<ScriptSection> AscSections { get; set; }
        public int DialogCount { get; set; }
        public int DialogSpeechRows { get; set; }
        public int ScriptDisplaySpeechRows { get; set; }
        public int ScriptSayRows { get; set; }

        public string StatsSummary
        {
            get
            {
                return string.Format(
                    "Dialogs: {0} · Dialog script lines: {1} · DisplaySpeech rows: {2} · player.Say rows: {3}",
                    DialogCount,
                    DialogSpeechRows,
                    ScriptDisplaySpeechRows,
                    ScriptSayRows);
            }
        }
    }
}
