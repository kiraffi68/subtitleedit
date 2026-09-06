using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Core.PodcastTranslate
{
    /// <summary>
    /// A section heading with an editorial time:
    /// <code>## 04:35 - Topic / segment name</code>
    /// The time is navigation information only and is deliberately NOT a <see cref="PodcastTimedBlock"/>.
    /// </summary>
    public class PodcastSectionBlock : PodcastBlock
    {
        private int _editorialTimeMs;
        private string _title = string.Empty;

        /// <summary>
        /// The heading time in milliseconds (non-negative whole seconds). This is not a synchronization cue.
        /// </summary>
        public int EditorialTimeMs
        {
            get => _editorialTimeMs;
            set
            {
                PodcastTimestamp.EnsureCanonicalMilliseconds(value, nameof(value));
                if (_editorialTimeMs != value)
                {
                    _editorialTimeMs = value;
                    MarkModified();
                }
            }
        }

        public string Title
        {
            get => _title;
            set
            {
                var v = value ?? string.Empty;
                if (_title != v)
                {
                    _title = v;
                    MarkModified();
                }
            }
        }

        internal void SetFieldsWithoutModifying(int editorialTimeMs, string title)
        {
            _editorialTimeMs = editorialTimeMs;
            _title = title ?? string.Empty;
        }

        protected override List<string> GenerateLines()
        {
            return new List<string> { "## " + PodcastTimestamp.Format(_editorialTimeMs) + " - " + _title };
        }
    }
}
