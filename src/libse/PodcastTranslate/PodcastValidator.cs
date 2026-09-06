using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Nikse.SubtitleEdit.Core.PodcastTranslate
{
    /// <summary>
    /// Validation relevant to Podcast Translate. This is deliberately not a Markdown linter
    /// or a full publishing validator. Parsing is always lossless and permissive; validation
    /// merely reports what a canonical Urunemu English document would not accept.
    /// </summary>
    public static class PodcastValidator
    {
        private static readonly string[] RequiredFrontMatterKeys = { "run", "episode", "date" };
        private static readonly string[] AllowedFrontMatterKeys = { "run", "episode", "date", "youtube_id" };
        private static readonly string[] AllowedRunValues = { "fmyoko", "youtube" };
        private static readonly Regex YouTubeIdRegex = new Regex(@"^[A-Za-z0-9_\-]{11}$", RegexOptions.Compiled);

        /// <summary>
        /// Structural validation: parse issues, non-decreasing synchronization timestamps and empty speakers.
        /// Front matter, if present, is checked against the schema, but a fragment without front matter is fine.
        /// </summary>
        public static List<PodcastValidationIssue> Validate(PodcastDocument doc)
        {
            var issues = new List<PodcastValidationIssue>(doc.ParseIssues);
            ValidateStructure(doc, issues);

            var frontMatter = doc.FrontMatter;
            if (frontMatter != null)
            {
                ValidateFrontMatter(frontMatter, issues);
            }

            return issues;
        }

        /// <summary>
        /// Canonical validation for a complete ".en.md": as <see cref="Validate"/>, but front matter is required
        /// and must be the first block.
        /// </summary>
        public static List<PodcastValidationIssue> ValidateCanonical(PodcastDocument doc)
        {
            var issues = new List<PodcastValidationIssue>(doc.ParseIssues);
            ValidateStructure(doc, issues);

            // Canonical means the front matter is the FIRST block; one that exists later (after a structural
            // edit, or preceded by other content) does not count.
            var frontMatter = doc.FrontMatter;
            if (frontMatter == null)
            {
                var message = doc.Blocks.Any(b => b is PodcastFrontMatterBlock)
                    ? "Canonical document must begin with YAML front matter, but the front matter is not the first block"
                    : "Canonical document must begin with YAML front matter ('---' on the first line)";
                issues.Add(new PodcastValidationIssue(PodcastIssueSeverity.Error, PodcastIssueCode.FrontMatterMissing, 1, message));
            }
            else
            {
                ValidateFrontMatter(frontMatter, issues);
            }

            return issues;
        }

        private static void ValidateStructure(PodcastDocument doc, List<PodcastValidationIssue> issues)
        {
            var previousMs = -1;
            var previousLine = 0;
            foreach (var block in doc.Blocks)
            {
                if (block is PodcastTimedBlock timed)
                {
                    if (previousMs >= 0 && timed.TimeMs < previousMs)
                    {
                        issues.Add(new PodcastValidationIssue(PodcastIssueSeverity.Error, PodcastIssueCode.TimestampDecreasing, block.LineNumber,
                            "Synchronization timestamp " + PodcastTimestamp.FormatBracketed(timed.TimeMs) + " is earlier than the previous timestamp " + PodcastTimestamp.FormatBracketed(previousMs) + " on line " + previousLine));
                    }

                    previousMs = timed.TimeMs;
                    previousLine = block.LineNumber;
                }

                if (block is PodcastDialogueBlock dialogue && dialogue.Speaker.Trim().Length == 0)
                {
                    issues.Add(new PodcastValidationIssue(PodcastIssueSeverity.Error, PodcastIssueCode.EmptySpeaker, block.LineNumber, "Dialogue block has an empty speaker"));
                }
            }
        }

        private static void ValidateFrontMatter(PodcastFrontMatterBlock frontMatter, List<PodcastValidationIssue> issues)
        {
            var line = frontMatter.LineNumber;
            foreach (var key in RequiredFrontMatterKeys)
            {
                if (frontMatter.GetValue(key) == null)
                {
                    issues.Add(new PodcastValidationIssue(PodcastIssueSeverity.Error, PodcastIssueCode.FrontMatterMissingKey, line, "Front matter is missing required key '" + key + "'"));
                }
            }

            foreach (var unparsed in frontMatter.UnparsedLines)
            {
                issues.Add(new PodcastValidationIssue(PodcastIssueSeverity.Error, PodcastIssueCode.FrontMatterMalformedLine, unparsed.Key, "Front matter line is not a 'key: value' scalar: '" + unparsed.Value + "'"));
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kvp in frontMatter.Values)
            {
                if (Array.IndexOf(AllowedFrontMatterKeys, kvp.Key) < 0)
                {
                    issues.Add(new PodcastValidationIssue(PodcastIssueSeverity.Error, PodcastIssueCode.FrontMatterUnknownKey, line, "Front matter has unknown key '" + kvp.Key + "'"));
                }

                if (!seen.Add(kvp.Key))
                {
                    issues.Add(new PodcastValidationIssue(PodcastIssueSeverity.Error, PodcastIssueCode.FrontMatterDuplicateKey, line, "Front matter has duplicate key '" + kvp.Key + "'"));
                }
            }

            var run = frontMatter.GetValue("run");
            if (run != null && Array.IndexOf(AllowedRunValues, run) < 0)
            {
                issues.Add(new PodcastValidationIssue(PodcastIssueSeverity.Error, PodcastIssueCode.FrontMatterInvalidValue, line, "Front matter 'run' must be 'fmyoko' or 'youtube', not '" + run + "'"));
            }

            var episode = frontMatter.GetValue("episode");
            if (episode != null && (!int.TryParse(episode, NumberStyles.None, CultureInfo.InvariantCulture, out var episodeNumber) || episodeNumber <= 0))
            {
                issues.Add(new PodcastValidationIssue(PodcastIssueSeverity.Error, PodcastIssueCode.FrontMatterInvalidValue, line, "Front matter 'episode' must be a positive integer, not '" + episode + "'"));
            }

            var date = frontMatter.GetValue("date");
            if (date != null && !IsValidIsoDate(date))
            {
                issues.Add(new PodcastValidationIssue(PodcastIssueSeverity.Error, PodcastIssueCode.FrontMatterInvalidValue, line, "Front matter 'date' must be a real calendar date in YYYY-MM-DD form, not '" + date + "'"));
            }

            var youtubeId = frontMatter.GetValue("youtube_id");
            if (youtubeId != null && !YouTubeIdRegex.IsMatch(youtubeId))
            {
                issues.Add(new PodcastValidationIssue(PodcastIssueSeverity.Error, PodcastIssueCode.FrontMatterInvalidValue, line, "Front matter 'youtube_id' must be an 11-character YouTube video id, not '" + youtubeId + "'"));
            }
        }

        /// <summary>
        /// Exact, invariant "yyyy-MM-dd" parse; rejects textual variants and impossible calendar dates (e.g. 2026-02-29).
        /// </summary>
        private static bool IsValidIsoDate(string text)
        {
            return text.Length == 10 &&
                   DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        }
    }
}
