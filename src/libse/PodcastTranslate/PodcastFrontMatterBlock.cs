using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Core.PodcastTranslate
{
    /// <summary>
    /// The YAML front matter at the top of the document ("---" ... "---").
    /// Phase 1 keeps it read-only and verbatim; the scalar key/value pairs are exposed
    /// for inspection and validation only. This is deliberately not a YAML editor.
    /// </summary>
    public class PodcastFrontMatterBlock : PodcastBlock
    {
        private readonly List<KeyValuePair<string, string>> _values = new List<KeyValuePair<string, string>>();
        private readonly List<KeyValuePair<int, string>> _unparsedLines = new List<KeyValuePair<int, string>>();

        /// <summary>
        /// Scalar key/value pairs in document order. Values are trimmed; keys are as written.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, string>> Values => _values;

        /// <summary>
        /// Non-blank lines between the delimiters that are not "key: value" scalars (line number, content).
        /// They are preserved on serialization but rejected by validation.
        /// </summary>
        public IReadOnlyList<KeyValuePair<int, string>> UnparsedLines => _unparsedLines;

        public string GetValue(string key)
        {
            foreach (var kvp in _values)
            {
                if (kvp.Key == key)
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        internal void AddValue(string key, string value)
        {
            _values.Add(new KeyValuePair<string, string>(key, value));
        }

        internal void AddUnparsedLine(int lineNumber, string content)
        {
            _unparsedLines.Add(new KeyValuePair<int, string>(lineNumber, content));
        }

        protected override List<string> GenerateLines()
        {
            // Front matter is never modified in Phase 1; regenerate conservatively from the parsed pairs.
            var lines = new List<string> { "---" };
            foreach (var kvp in _values)
            {
                lines.Add(kvp.Key + ": " + kvp.Value);
            }

            lines.Add("---");
            return lines;
        }
    }
}
