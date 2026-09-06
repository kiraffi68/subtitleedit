using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Core.PodcastTranslate
{
    /// <summary>
    /// Any Markdown that Podcast Translate does not understand semantically: document title,
    /// body metadata, blockquotes, translator's notes, prose, blank-line runs, etc.
    /// It is preserved verbatim. A raw block is either a run of blank lines or a run of
    /// consecutive non-blank unrecognized lines (a Markdown paragraph-like unit).
    /// </summary>
    public class PodcastRawMarkdownBlock : PodcastBlock
    {
        private string _text = string.Empty;

        /// <summary>
        /// The raw Markdown content of the block, with physical line breaks as "\n" and no trailing terminator.
        /// </summary>
        public string Text
        {
            get => _text;
            set
            {
                var v = value ?? string.Empty;
                if (_text != v)
                {
                    _text = v;
                    MarkModified();
                }
            }
        }

        /// <summary>
        /// True when the block consists only of blank (empty or whitespace-only) lines.
        /// </summary>
        public bool IsBlank { get; internal set; }

        internal void SetTextWithoutModifying(string text)
        {
            _text = text ?? string.Empty;
        }

        protected override List<string> GenerateLines()
        {
            return SplitTextLines(_text);
        }
    }
}
