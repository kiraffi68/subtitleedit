using System.Collections.Generic;
using System.Text;

namespace Nikse.SubtitleEdit.Core.PodcastTranslate
{
    /// <summary>
    /// One physical source line of the Markdown document: its content and the exact
    /// line terminator that followed it ("\r\n", "\n", "\r" or "" at end of file).
    /// </summary>
    public sealed class PodcastSourceLine
    {
        public PodcastSourceLine(string content, string terminator)
        {
            Content = content;
            Terminator = terminator;
        }

        public string Content { get; }

        public string Terminator { get; }
    }

    /// <summary>
    /// Base class for every block of a <see cref="PodcastDocument"/>.
    /// A block remembers the exact source lines it was parsed from. As long as none of its
    /// structured fields are changed, serialization writes those source lines back verbatim.
    /// Only a modified block is regenerated from its fields, and even then the original
    /// line terminators are reused so that unrelated bytes are never touched.
    /// </summary>
    public abstract class PodcastBlock
    {
        private List<PodcastSourceLine> _originalLines;

        /// <summary>
        /// 1-based line number of the first source line, or 0 if the block was created in code.
        /// </summary>
        public int LineNumber { get; internal set; }

        /// <summary>
        /// True when a structured field was changed after parsing (or the block was created in code),
        /// meaning the block will be regenerated rather than written back verbatim.
        /// </summary>
        public bool IsModified { get; private set; }

        /// <summary>
        /// The original source lines this block was parsed from, or null for blocks created in code.
        /// </summary>
        public IReadOnlyList<PodcastSourceLine> OriginalLines => _originalLines;

        /// <summary>
        /// The exact original source text (including line terminators), or null for blocks created in code.
        /// </summary>
        public string OriginalText
        {
            get
            {
                if (_originalLines == null)
                {
                    return null;
                }

                var sb = new StringBuilder();
                foreach (var line in _originalLines)
                {
                    sb.Append(line.Content);
                    sb.Append(line.Terminator);
                }

                return sb.ToString();
            }
        }

        internal void SetOriginalLines(List<PodcastSourceLine> lines, int lineNumber)
        {
            _originalLines = lines;
            LineNumber = lineNumber;
            IsModified = false;
        }

        protected void MarkModified()
        {
            IsModified = true;
        }

        /// <summary>
        /// Produces the content lines (without terminators) for this block from its structured fields.
        /// Only used when the block is modified or has no original source.
        /// </summary>
        protected abstract List<string> GenerateLines();

        /// <summary>
        /// Serializes this block. Unmodified blocks return their original text character-for-character.
        /// The last line keeps its original terminator state (possibly none, when the block ended the
        /// source without a trailing newline); <see cref="PodcastDocument.ToMarkdown"/> is responsible for
        /// separating such a block from a successor if it is no longer last.
        /// </summary>
        /// <param name="newLine">Line terminator to use for lines that have no original terminator.</param>
        public string ToMarkdown(string newLine)
        {
            if (!IsModified && _originalLines != null)
            {
                return OriginalText;
            }

            var lines = GenerateLines();
            var sb = new StringBuilder();
            var originalCount = _originalLines?.Count ?? 0;
            for (var i = 0; i < lines.Count; i++)
            {
                sb.Append(lines[i]);
                var isLast = i == lines.Count - 1;
                if (isLast)
                {
                    // The last generated line inherits the terminator of the last original line,
                    // which is empty when the block ended the file without a trailing newline.
                    sb.Append(originalCount > 0 ? _originalLines[originalCount - 1].Terminator : newLine);
                }
                else if (i < originalCount - 1)
                {
                    sb.Append(_originalLines[i].Terminator);
                }
                else
                {
                    sb.Append(newLine);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Splits a multi-line text value on "\n" (tolerating "\r\n") into content lines.
        /// </summary>
        protected static List<string> SplitTextLines(string text)
        {
            var result = new List<string>();
            if (text == null)
            {
                result.Add(string.Empty);
                return result;
            }

            foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                result.Add(line);
            }

            return result;
        }
    }

    /// <summary>
    /// A block that carries a synchronization timestamp (dialogue or audible event).
    /// These are the only blocks a later phase should attach to the waveform.
    /// </summary>
    public abstract class PodcastTimedBlock : PodcastBlock
    {
        private int _timeMs;

        /// <summary>
        /// Synchronization time in milliseconds. Always a non-negative whole number of seconds
        /// (the setter rejects anything else); round before assigning from a waveform position.
        /// </summary>
        public int TimeMs
        {
            get => _timeMs;
            set
            {
                PodcastTimestamp.EnsureCanonicalMilliseconds(value, nameof(value));
                if (_timeMs != value)
                {
                    _timeMs = value;
                    MarkModified();
                }
            }
        }

        public double TimeSeconds => _timeMs / 1000.0;

        internal void SetTimeMsWithoutModifying(int timeMs)
        {
            _timeMs = timeMs;
        }
    }
}
