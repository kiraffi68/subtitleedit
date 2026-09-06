using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Core.PodcastTranslate
{
    /// <summary>
    /// A timed audible event with no speaker:
    /// <code>[18:46] [everyone laughs]</code>
    /// </summary>
    public class PodcastAudibleEventBlock : PodcastTimedBlock
    {
        private string _description = string.Empty;

        /// <summary>
        /// The description inside the second pair of brackets, e.g. "everyone laughs".
        /// </summary>
        public string Description
        {
            get => _description;
            set
            {
                var v = value ?? string.Empty;
                if (_description != v)
                {
                    _description = v;
                    MarkModified();
                }
            }
        }

        internal void SetFieldsWithoutModifying(int timeMs, string description)
        {
            SetTimeMsWithoutModifying(timeMs);
            _description = description ?? string.Empty;
        }

        protected override List<string> GenerateLines()
        {
            return new List<string> { PodcastTimestamp.FormatBracketed(TimeMs) + " [" + _description + "]" };
        }
    }
}
