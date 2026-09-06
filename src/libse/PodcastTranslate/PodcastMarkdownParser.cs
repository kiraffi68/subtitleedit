using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Nikse.SubtitleEdit.Core.PodcastTranslate
{
    /// <summary>
    /// Line-based, lossless parser for canonical Urunemu English Markdown.
    /// Every source line ends up in exactly one block, with its exact terminator,
    /// so concatenating the blocks reproduces the input.
    /// </summary>
    public static class PodcastMarkdownParser
    {
        private const string FrontMatterDelimiter = "---";

        // Canonical timestamps use ASCII digits only; the "-like" detectors below deliberately use \p{Nd}
        // so that a line with non-ASCII digits is reported as a bad timestamp rather than passing as prose.

        // "[MM:SS]" at the start of a line followed by a space or end of line.
        private static readonly Regex TimedLineRegex = new Regex(@"^\[([0-9]{2,}:[0-9][0-9])\](?: |$)", RegexOptions.Compiled);

        // Anything that looks like a bracketed time attempt at the start of a line, canonical or not.
        private static readonly Regex TimestampLikeRegex = new Regex(@"^\[[\p{Nd}:.,\-]+\](?: |$)", RegexOptions.Compiled);

        // "[MM:SS] **SPEAKER:** text" - text may be empty (turn continues on the next line).
        private static readonly Regex DialogueRegex = new Regex(@"^\[(?<time>[0-9]{2,}:[0-9][0-9])\] \*\*(?<speaker>[^*\r\n]+?):\*\*(?: (?<text>.*)|(?<text>))$", RegexOptions.Compiled);

        // "[MM:SS] [description]"
        private static readonly Regex AudibleEventRegex = new Regex(@"^\[(?<time>[0-9]{2,}:[0-9][0-9])\] \[(?<desc>[^\]\r\n]+)\]\s*$", RegexOptions.Compiled);

        // "## MM:SS - Title"
        private static readonly Regex SectionRegex = new Regex(@"^## (?<time>[0-9]{2,}:[0-9][0-9]) - (?<title>.*)$", RegexOptions.Compiled);

        // A heading that clearly attempts the canonical "## time - title" form, canonical or not.
        private static readonly Regex SectionLikeRegex = new Regex(@"^## (?<time>[\p{Nd}:.,\-]+) - ", RegexOptions.Compiled);

        private static readonly Regex FrontMatterPairRegex = new Regex(@"^(?<key>[A-Za-z_][A-Za-z0-9_]*):\s*(?<value>.*?)\s*$", RegexOptions.Compiled);

        public static PodcastDocument Parse(string markdown)
        {
            var doc = new PodcastDocument();
            if (markdown == null)
            {
                return doc;
            }

            var hasBom = markdown.Length > 0 && markdown[0] == '\uFEFF';
            if (hasBom)
            {
                markdown = markdown.Substring(1);
            }

            var lines = SplitSourceLines(markdown);
            doc.SetSourceCharacteristics(DetectNewLine(lines), hasBom);

            var index = 0;

            // YAML front matter: only recognized when the very first line is exactly "---".
            if (lines.Count > 0 && lines[0].Content == FrontMatterDelimiter)
            {
                var end = -1;
                for (var i = 1; i < lines.Count; i++)
                {
                    if (lines[i].Content == FrontMatterDelimiter)
                    {
                        end = i;
                        break;
                    }
                }

                if (end > 0)
                {
                    var block = new PodcastFrontMatterBlock();
                    for (var i = 1; i < end; i++)
                    {
                        var m = FrontMatterPairRegex.Match(lines[i].Content);
                        if (m.Success)
                        {
                            block.AddValue(m.Groups["key"].Value, m.Groups["value"].Value);
                        }
                        else if (!IsBlank(lines[i].Content))
                        {
                            // Preserved verbatim, but remembered so canonical validation can report it.
                            block.AddUnparsedLine(i + 1, lines[i].Content);
                        }
                    }

                    block.SetOriginalLines(lines.GetRange(0, end + 1), 1);
                    doc.AddParsedBlock(block);
                    index = end + 1;
                }
                else
                {
                    doc.ParseIssues.Add(new PodcastValidationIssue(PodcastIssueSeverity.Warning, PodcastIssueCode.FrontMatterUnterminated, 1, "Front matter opened with '---' but never closed; treated as raw Markdown"));
                }
            }

            while (index < lines.Count)
            {
                var line = lines[index];
                var content = line.Content;
                var lineNumber = index + 1;

                var sectionMatch = SectionRegex.Match(content);
                if (sectionMatch.Success && PodcastTimestamp.TryParse(sectionMatch.Groups["time"].Value, out var editorialMs))
                {
                    var section = new PodcastSectionBlock();
                    section.SetFieldsWithoutModifying(editorialMs, sectionMatch.Groups["title"].Value);
                    section.SetOriginalLines(new List<PodcastSourceLine> { line }, lineNumber);
                    doc.AddParsedBlock(section);
                    index++;
                    continue;
                }

                var sectionLikeMatch = SectionLikeRegex.Match(content);
                if (sectionLikeMatch.Success)
                {
                    doc.ParseIssues.Add(new PodcastValidationIssue(PodcastIssueSeverity.Error, PodcastIssueCode.InvalidSectionTime, lineNumber, "Invalid section heading time '" + sectionLikeMatch.Groups["time"].Value + "' (expected '## MM:SS - Title'); preserved as raw Markdown"));
                    index = AddRawRun(doc, lines, index);
                    continue;
                }

                var timedMatch = TimedLineRegex.Match(content);
                if (timedMatch.Success)
                {
                    if (!PodcastTimestamp.TryParse(timedMatch.Groups[1].Value, out var timeMs))
                    {
                        doc.ParseIssues.Add(new PodcastValidationIssue(PodcastIssueSeverity.Error, PodcastIssueCode.InvalidTimestamp, lineNumber, "Invalid synchronization timestamp '" + timedMatch.Groups[1].Value + "' (seconds must be 00-59 and the value must fit an int)"));
                        index = AddRawRun(doc, lines, index);
                        continue;
                    }

                    var eventMatch = AudibleEventRegex.Match(content);
                    if (eventMatch.Success)
                    {
                        var ev = new PodcastAudibleEventBlock();
                        ev.SetFieldsWithoutModifying(timeMs, eventMatch.Groups["desc"].Value);
                        ev.SetOriginalLines(new List<PodcastSourceLine> { line }, lineNumber);
                        doc.AddParsedBlock(ev);
                        index++;
                        continue;
                    }

                    var dialogueMatch = DialogueRegex.Match(content);
                    if (dialogueMatch.Success)
                    {
                        var blockLines = new List<PodcastSourceLine> { line };
                        var textLines = new List<string> { dialogueMatch.Groups["text"].Value };
                        var next = index + 1;
                        while (next < lines.Count && IsContinuationLine(lines[next].Content))
                        {
                            blockLines.Add(lines[next]);
                            textLines.Add(lines[next].Content);
                            next++;
                        }

                        var dialogue = new PodcastDialogueBlock();
                        dialogue.SetFieldsWithoutModifying(timeMs, dialogueMatch.Groups["speaker"].Value, string.Join("\n", textLines));
                        dialogue.SetOriginalLines(blockLines, lineNumber);
                        doc.AddParsedBlock(dialogue);
                        index = next;
                        continue;
                    }

                    doc.ParseIssues.Add(new PodcastValidationIssue(PodcastIssueSeverity.Warning, PodcastIssueCode.UnrecognizedTimedLine, lineNumber, "Timed line is neither '**SPEAKER:** text' nor '[event]'; preserved as raw Markdown"));
                    index = AddRawRun(doc, lines, index);
                    continue;
                }

                if (TimestampLikeRegex.IsMatch(content))
                {
                    var closeIdx = content.IndexOf(']');
                    doc.ParseIssues.Add(new PodcastValidationIssue(PodcastIssueSeverity.Error, PodcastIssueCode.InvalidTimestamp, lineNumber, "Invalid synchronization timestamp '" + content.Substring(1, closeIdx - 1) + "' (expected [MM:SS] with at least two minute digits)"));
                    index = AddRawRun(doc, lines, index);
                    continue;
                }

                index = AddRawRun(doc, lines, index);
            }

            return doc;
        }

        /// <summary>
        /// Adds a raw block starting at <paramref name="index"/>: either a run of blank lines,
        /// or a run of non-blank lines that ends before a blank line or a line the parser recognizes as a section/timed block.
        /// Returns the index of the first line not consumed.
        /// </summary>
        private static int AddRawRun(PodcastDocument doc, List<PodcastSourceLine> lines, int index)
        {
            var startsBlank = IsBlank(lines[index].Content);
            var blockLines = new List<PodcastSourceLine> { lines[index] };
            var textLines = new List<string> { lines[index].Content };
            var next = index + 1;
            while (next < lines.Count)
            {
                var c = lines[next].Content;
                if (IsBlank(c) != startsBlank || (!startsBlank && IsRecognizedBlockStart(c)))
                {
                    break;
                }

                blockLines.Add(lines[next]);
                textLines.Add(c);
                next++;
            }

            var raw = new PodcastRawMarkdownBlock { IsBlank = startsBlank };
            raw.SetTextWithoutModifying(string.Join("\n", textLines));
            raw.SetOriginalLines(blockLines, index + 1);
            doc.AddParsedBlock(raw);
            return next;
        }

        private static bool IsBlank(string content)
        {
            return content.Trim().Length == 0;
        }

        /// <summary>
        /// A dialogue turn continues over following physical lines until a blank line or a line
        /// that begins another block (timestamp, heading, blockquote or '---').
        /// </summary>
        private static bool IsContinuationLine(string content)
        {
            return !IsBlank(content) && !IsBlockStart(content);
        }

        /// <summary>
        /// True for lines the parser would turn into a section, dialogue or event block (or flag as a bad timestamp).
        /// </summary>
        private static bool IsRecognizedBlockStart(string content)
        {
            return SectionLikeRegex.IsMatch(content) || TimestampLikeRegex.IsMatch(content);
        }

        private static bool IsBlockStart(string content)
        {
            if (content.Length == 0)
            {
                return false;
            }

            // Only the constructs that occur in the canonical format end a turn; list markers and
            // emphasis are deliberately not treated as block starts so wrapped prose stays intact.
            var c = content[0];
            if (c == '#' || c == '>' || content == FrontMatterDelimiter)
            {
                return true;
            }

            return TimestampLikeRegex.IsMatch(content);
        }

        /// <summary>
        /// Splits text into lines, keeping each line's exact terminator ("\r\n", "\n" or "\r").
        /// A trailing terminator does not produce an extra empty line.
        /// </summary>
        internal static List<PodcastSourceLine> SplitSourceLines(string text)
        {
            var result = new List<PodcastSourceLine>();
            var start = 0;
            var i = 0;
            while (i < text.Length)
            {
                var c = text[i];
                if (c == '\r')
                {
                    var terminator = i + 1 < text.Length && text[i + 1] == '\n' ? "\r\n" : "\r";
                    result.Add(new PodcastSourceLine(text.Substring(start, i - start), terminator));
                    i += terminator.Length;
                    start = i;
                }
                else if (c == '\n')
                {
                    result.Add(new PodcastSourceLine(text.Substring(start, i - start), "\n"));
                    i++;
                    start = i;
                }
                else
                {
                    i++;
                }
            }

            if (start < text.Length)
            {
                result.Add(new PodcastSourceLine(text.Substring(start), string.Empty));
            }

            return result;
        }

        private static string DetectNewLine(List<PodcastSourceLine> lines)
        {
            foreach (var line in lines)
            {
                if (line.Terminator.Length > 0)
                {
                    return line.Terminator;
                }
            }

            return "\n";
        }
    }
}
