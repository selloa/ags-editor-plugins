using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AGS.Types;

namespace AGS.Plugin.SpeechViewer
{
    internal static class SpeechGatherer
    {
        public static SpeechDocument Gather(IGame gameInterface)
        {
            var game = gameInterface as Game;
            if (game == null)
            {
                return EmptyDocument("(no game loaded)");
            }

            var directoryPath = game.DirectoryPath;
            if (string.IsNullOrEmpty(directoryPath))
            {
                return EmptyDocument("(game directory unknown)");
            }

            var characters = CollectCharacters(game);
            int? playerId = game.PlayerCharacter != null ? (int?)game.PlayerCharacter.ID : null;
            var scriptByFileName = BuildScriptLookup(game);

            var document = new SpeechDocument
            {
                AgfName = "Game.agf",
                RepoName = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                DialogsMissing = game.RootDialogFolder == null,
                Dialogs = new List<DialogSection>(),
                AscSections = new List<ScriptSection>(),
                DialogCount = 0,
                DialogSpeechRows = 0,
                ScriptDisplaySpeechRows = 0,
                ScriptSayRows = 0
            };

            GatherDialogs(game, document);
            GatherAscScripts(directoryPath, scriptByFileName, characters, playerId, document);

            return document;
        }

        private static SpeechDocument EmptyDocument(string repoName)
        {
            return new SpeechDocument
            {
                AgfName = "Game.agf",
                RepoName = repoName,
                DialogsMissing = true,
                Dialogs = new List<DialogSection>(),
                AscSections = new List<ScriptSection>()
            };
        }

        private static Dictionary<int, Tuple<string, string>> CollectCharacters(Game game)
        {
            var characters = new Dictionary<int, Tuple<string, string>>();
            if (game.RootCharacterFolder == null)
            {
                return characters;
            }

            foreach (Character character in game.RootCharacterFolder.AllItemsFlat)
            {
                characters[character.ID] = Tuple.Create(character.ScriptName ?? string.Empty, character.RealName ?? string.Empty);
            }

            return characters;
        }

        private static Dictionary<string, Script> BuildScriptLookup(Game game)
        {
            var lookup = new Dictionary<string, Script>(StringComparer.OrdinalIgnoreCase);
            if (game.RootScriptFolder == null)
            {
                return lookup;
            }

            foreach (Script script in game.RootScriptFolder.AllScriptsFlat)
            {
                if (script == null || script.IsHeader || string.IsNullOrEmpty(script.FileName))
                {
                    continue;
                }

                lookup[script.FileName] = script;
            }

            return lookup;
        }

        private static void GatherDialogs(Game game, SpeechDocument document)
        {
            if (game.RootDialogFolder == null)
            {
                document.DialogsMissing = true;
                return;
            }

            var dialogs = game.RootDialogFolder.AllItemsFlat
                .Cast<Dialog>()
                .OrderBy(d => d.ID)
                .ToList();

            document.DialogsMissing = dialogs.Count == 0;

            foreach (var dialog in dialogs)
            {
                document.DialogCount++;
                var section = new DialogSection
                {
                    Id = dialog.ID,
                    Name = string.IsNullOrEmpty(dialog.Name) ? "dialog_" + dialog.ID : dialog.Name,
                    Anchor = HtmlId("dialog", dialog.ID.ToString()),
                    Options = new List<SpeechRow>(),
                    Branches = new List<DialogBranch>()
                };

                foreach (DialogOption option in dialog.Options)
                {
                    var text = option.Text ?? string.Empty;
                    if (text.Trim().Length == 0)
                    {
                        continue;
                    }

                    section.Options.Add(new SpeechRow
                    {
                        LineId = string.Format("dlg-{0}-opt-{1}", dialog.ID, option.ID),
                        Speaker = string.Empty,
                        Text = text.Trim(),
                        Notes = string.Empty
                    });
                }

                var speechLines = DialogSpeechParser.ParseDialogScriptBody(dialog.Script ?? string.Empty, dialog.ID);
                if (speechLines.Count > 0)
                {
                    var byBranch = new Dictionary<string, List<SpeechRow>>(StringComparer.Ordinal);
                    foreach (var line in speechLines)
                    {
                        List<SpeechRow> branchRows;
                        if (!byBranch.TryGetValue(line.BranchTag, out branchRows))
                        {
                            branchRows = new List<SpeechRow>();
                            byBranch[line.BranchTag] = branchRows;
                        }

                        branchRows.Add(new SpeechRow
                        {
                            LineId = line.LineId,
                            Speaker = line.Speaker,
                            Text = line.Message,
                            Notes = string.Empty
                        });
                    }

                    var branchTags = byBranch.Keys.ToList();
                    branchTags.Sort(DialogSpeechParser.CompareBranchTags);

                    foreach (var branchTag in branchTags)
                    {
                        var rows = byBranch[branchTag];
                        document.DialogSpeechRows += rows.Count;
                        var slug = branchTag == "root" ? "root" : branchTag.TrimStart('@');
                        section.Branches.Add(new DialogBranch
                        {
                            Anchor = HtmlId("dialog", dialog.ID.ToString(), "b", slug),
                            Title = DialogSpeechParser.BranchTitle(branchTag),
                            Rows = rows
                        });
                    }
                }

                section.HasSpeech = section.Branches.Count > 0;
                document.Dialogs.Add(section);
            }
        }

        private static void GatherAscScripts(
            string directoryPath,
            Dictionary<string, Script> scriptByFileName,
            Dictionary<int, Tuple<string, string>> characters,
            int? playerId,
            SpeechDocument document)
        {
            string[] ascFiles;
            try
            {
                ascFiles = Directory.GetFiles(directoryPath, "*.asc");
                Array.Sort(ascFiles, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return;
            }

            foreach (var ascPath in ascFiles)
            {
                var fileName = Path.GetFileName(ascPath);
                var stem = Path.GetFileNameWithoutExtension(fileName);
                var section = new ScriptSection
                {
                    Filename = fileName,
                    Anchor = HtmlId("asc", stem),
                    Rows = new List<SpeechRow>()
                };

                string text;
                Script loadedScript;
                if (scriptByFileName.TryGetValue(fileName, out loadedScript) && loadedScript.Text != null)
                {
                    text = loadedScript.Text;
                }
                else
                {
                    try
                    {
                        text = File.ReadAllText(ascPath);
                    }
                    catch (Exception ex)
                    {
                        section.Error = ex.Message;
                        document.AscSections.Add(section);
                        continue;
                    }
                }

                var fileRows = new List<SpeechRow>();
                var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    var lineNo = lineIndex + 1;
                    var line = lines[lineIndex];

                    foreach (var call in AscSpeechParser.ExtractDisplaySpeechCalls(line, lineNo, stem))
                    {
                        fileRows.Add(new SpeechRow
                        {
                            LineId = call.LineId,
                            Speaker = AscSpeechParser.ResolveSpeakerExpression(call.CharacterExpression, playerId, characters),
                            Text = call.Text,
                            Notes = call.Note ?? string.Empty
                        });
                    }

                    var sayIndex = 0;
                    foreach (var call in AscSpeechParser.ExtractPlayerSayCalls(line, lineNo, stem))
                    {
                        var lineId = call.LineId;
                        if (sayIndex > 0)
                        {
                            lineId = lineId + "-" + (sayIndex + 1);
                        }

                        fileRows.Add(new SpeechRow
                        {
                            LineId = lineId,
                            Speaker = "player (Say)",
                            Text = call.Text,
                            Notes = call.Note ?? string.Empty
                        });
                        sayIndex++;
                    }
                }

                if (fileRows.Count == 0)
                {
                    continue;
                }

                foreach (var row in fileRows)
                {
                    if (row.LineId != null && row.LineId.EndsWith("-say", StringComparison.Ordinal))
                    {
                        document.ScriptSayRows++;
                    }
                    else
                    {
                        document.ScriptDisplaySpeechRows++;
                    }
                }

                section.Rows = fileRows;
                document.AscSections.Add(section);
            }
        }

        private static string HtmlId(params string[] parts)
        {
            var raw = string.Join("-", parts);
            var s = Regex.Replace(raw.Trim(), @"[^a-zA-Z0-9_-]+", "-").Trim('-').ToLowerInvariant();
            return s.Length > 0 ? s : "section";
        }
    }
}
