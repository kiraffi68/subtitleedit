using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Core.PodcastTranslate
{
    /// <summary>
    /// A timed speaker turn:
    /// <code>[18:43] **GUMI:** That's what I thought too.</code>
    /// The text may continue over several physical lines (until a blank line or the next block).
    /// The speaker is any non-empty label; it is not restricted to the regular members.
    /// </summary>
    public class PodcastDialogueBlock : PodcastTimedBlock
    {
        private string _speaker = string.Empty;
        private string _text = string.Empty;

        /// <summary>
        /// Speaker label as written between "**" and ":**", e.g. "GUMI" or "REIJI".
        /// </summary>
        public string Speaker
        {
            get => _speaker;
            set
            {
                var v = value ?? string.Empty;
                if (_speaker != v)
                {
                    _speaker = v;
                    MarkModified();
                }
            }
        }

        /// <summary>
        /// The translated text of the turn. Physical line breaks inside the turn are represented as "\n".
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

        internal void SetFieldsWithoutModifying(int timeMs, string speaker, string text)
        {
            SetTimeMsWithoutModifying(timeMs);
            _speaker = speaker ?? string.Empty;
            _text = text ?? string.Empty;
        }

        protected override List<string> GenerateLines()
        {
            var lines = SplitTextLines(_text);
            var header = PodcastTimestamp.FormatBracketed(TimeMs) + " **" + _speaker + ":**";
            lines[0] = lines[0].Length > 0 ? header + " " + lines[0] : header;
            return lines;
        }
    }
}
