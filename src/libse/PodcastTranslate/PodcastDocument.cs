using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Nikse.SubtitleEdit.Core.PodcastTranslate
{
    /// <summary>
    /// In-memory model of one canonical Urunemu English Markdown document (".en.md").
    /// This is intentionally separate from <see cref="Common.Subtitle"/>/<see cref="Common.Paragraph"/>:
    /// the document is an authored publication source, not a subtitle file, and most of it
    /// (front matter, title, notes, prose) is untimed structure that must round-trip verbatim.
    /// The document owns its block list: all structural changes go through the mutation
    /// methods below so that they are tracked by <see cref="IsStructureModified"/>.
    /// </summary>
    public class PodcastDocument
    {
        private readonly List<PodcastBlock> _blocks = new List<PodcastBlock>();
        private readonly ReadOnlyCollection<PodcastBlock> _readOnlyBlocks;

        public PodcastDocument()
        {
            _readOnlyBlocks = new ReadOnlyCollection<PodcastBlock>(_blocks);
        }

        /// <summary>
        /// Line terminator used when a regenerated line has no original terminator to reuse, and to separate
        /// blocks that lost their terminator through structural editing.
        /// Detected from the first line break in the parsed text; "\n" for a new document.
        /// A parsed characteristic, not user-editable content, so it does not participate in <see cref="IsModified"/>.
        /// </summary>
        public string NewLine { get; private set; } = "\n";

        /// <summary>
        /// True when the parsed text started with a byte order mark character (U+FEFF); it is re-emitted on serialization.
        /// A parsed characteristic, not user-editable content, so it does not participate in <see cref="IsModified"/>.
        /// </summary>
        public bool HasByteOrderMark { get; private set; }

        /// <summary>
        /// Parser/file-layer only: records how the source was written.
        /// </summary>
        internal void SetSourceCharacteristics(string newLine, bool hasByteOrderMark)
        {
            if (newLine != "\n" && newLine != "\r\n" && newLine != "\r")
            {
                throw new ArgumentException("Line terminator must be \\n, \\r\\n or \\r", nameof(newLine));
            }

            NewLine = newLine;
            HasByteOrderMark = hasByteOrderMark;
        }

        /// <summary>
        /// The blocks in document order. A genuine read-only wrapper (mutating it throws); use <see cref="AddBlock"/>,
        /// <see cref="InsertBlock"/>, <see cref="RemoveBlock"/>, <see cref="RemoveBlockAt"/>, <see cref="MoveBlock"/>
        /// and <see cref="ClearBlocks"/> to change the document.
        /// </summary>
        public IReadOnlyList<PodcastBlock> Blocks => _readOnlyBlocks;

        /// <summary>
        /// Structural issues found while parsing (e.g. malformed timestamps). Also included by <see cref="Validate"/>.
        /// </summary>
        public List<PodcastValidationIssue> ParseIssues { get; } = new List<PodcastValidationIssue>();

        /// <summary>
        /// True when blocks were added, inserted, removed, moved or cleared after parsing.
        /// </summary>
        public bool IsStructureModified { get; private set; }

        /// <summary>
        /// True when the structure changed or any block's structured fields changed.
        /// </summary>
        public bool IsModified => IsStructureModified || _blocks.Any(b => b.IsModified);

        /// <summary>
        /// The document's front matter, which by definition is the first block; null when the first block is anything else
        /// (including when a front-matter block exists later in the document after structural editing).
        /// </summary>
        public PodcastFrontMatterBlock FrontMatter => _blocks.Count > 0 ? _blocks[0] as PodcastFrontMatterBlock : null;

        /// <summary>
        /// Dialogue and audible-event blocks in document order (the only blocks with synchronization timestamps).
        /// </summary>
        public IEnumerable<PodcastTimedBlock> TimedBlocks => _blocks.OfType<PodcastTimedBlock>();

        public IEnumerable<PodcastDialogueBlock> DialogueBlocks => _blocks.OfType<PodcastDialogueBlock>();

        public IEnumerable<PodcastSectionBlock> SectionBlocks => _blocks.OfType<PodcastSectionBlock>();

        public static PodcastDocument Parse(string markdown)
        {
            return PodcastMarkdownParser.Parse(markdown);
        }

        /// <summary>
        /// Parser-only: appends a block without marking the document as modified.
        /// </summary>
        internal void AddParsedBlock(PodcastBlock block)
        {
            _blocks.Add(block);
        }

        public int IndexOf(PodcastBlock block)
        {
            return _blocks.IndexOf(block);
        }

        public void AddBlock(PodcastBlock block)
        {
            InsertBlock(_blocks.Count, block);
        }

        public void InsertBlock(int index, PodcastBlock block)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            if (index < 0 || index > _blocks.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (_blocks.Contains(block))
            {
                throw new ArgumentException("Block is already part of this document", nameof(block));
            }

            _blocks.Insert(index, block);
            IsStructureModified = true;
        }

        /// <summary>
        /// Removes the block; returns false (and leaves the document untouched) if it is not part of this document.
        /// </summary>
        public bool RemoveBlock(PodcastBlock block)
        {
            var index = _blocks.IndexOf(block);
            if (index < 0)
            {
                return false;
            }

            RemoveBlockAt(index);
            return true;
        }

        public PodcastBlock RemoveBlockAt(int index)
        {
            if (index < 0 || index >= _blocks.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var block = _blocks[index];
            _blocks.RemoveAt(index);
            IsStructureModified = true;
            return block;
        }

        /// <summary>
        /// Moves the block at <paramref name="fromIndex"/> so that it ends up at <paramref name="toIndex"/>
        /// (indices refer to the block list before and after the move respectively).
        /// </summary>
        public void MoveBlock(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _blocks.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(fromIndex));
            }

            if (toIndex < 0 || toIndex >= _blocks.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(toIndex));
            }

            if (fromIndex == toIndex)
            {
                return;
            }

            var block = _blocks[fromIndex];
            _blocks.RemoveAt(fromIndex);
            _blocks.Insert(toIndex, block);
            IsStructureModified = true;
        }

        public void ClearBlocks()
        {
            if (_blocks.Count == 0)
            {
                return;
            }

            _blocks.Clear();
            IsStructureModified = true;
        }

        /// <summary>
        /// Serializes the document. Unmodified blocks are written back character-for-character
        /// (actual byte preservation is the job of the encoding-aware file layer).
        /// Whether a block is last is document-level knowledge, so the inter-block boundary is enforced here:
        /// a block whose text does not end with a line terminator (it ended the original source, or was created
        /// without one) is followed by <see cref="NewLine"/> unless it is the final block. The final block keeps
        /// its own trailing-newline state, so an untouched document without a final newline never gains one.
        /// </summary>
        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            if (HasByteOrderMark)
            {
                sb.Append('\uFEFF');
            }

            for (var i = 0; i < _blocks.Count; i++)
            {
                var text = _blocks[i].ToMarkdown(NewLine);
                sb.Append(text);
                var isLast = i == _blocks.Count - 1;
                if (!isLast && !EndsWithLineTerminator(text))
                {
                    sb.Append(NewLine);
                }
            }

            return sb.ToString();
        }

        private static bool EndsWithLineTerminator(string text)
        {
            if (text.Length == 0)
            {
                return false;
            }

            var last = text[text.Length - 1];
            return last == '\n' || last == '\r';
        }

        /// <summary>
        /// Structural validation only: parse issues, synchronization timestamp order and empty speakers.
        /// Front matter is checked for schema if present but is not required. Suitable for fragments.
        /// </summary>
        public List<PodcastValidationIssue> Validate()
        {
            return PodcastValidator.Validate(this);
        }

        /// <summary>
        /// Full validation of a canonical Urunemu English ".en.md": everything in <see cref="Validate"/>
        /// plus the requirement that the document begins with schema-valid front matter.
        /// </summary>
        public List<PodcastValidationIssue> ValidateCanonical()
        {
            return PodcastValidator.ValidateCanonical(this);
        }
    }
}
