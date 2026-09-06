using Nikse.SubtitleEdit.Core.PodcastTranslate;
using System.Collections.Generic;
using System.Linq;

namespace Tests.PodcastTranslate
{
    [TestClass]
    public class PodcastMarkdownTest
    {
        /// <summary>
        /// Representative complete document: front matter, title, body metadata, intro blockquote,
        /// several sections, all four members, a guest, equal timestamps, a timestamp above 59 minutes,
        /// an audible event, a translator's note, multiline dialogue and arbitrary preserved Markdown.
        /// </summary>
        private const string SampleDocument =
            "---\n" +
            "run: fmyoko\n" +
            "episode: 43\n" +
            "date: 2026-08-27\n" +
            "youtube_id: fkDjjg6DYoQ\n" +
            "---\n" +
            "\n" +
            "# Urunemu #43 - MOMO × GUMI\n" +
            "\n" +
            "**Broadcast:** 2026-08-27  \n" +
            "**Members:** MOMO, GUMI  \n" +
            "**Guests:** REIJI\n" +
            "\n" +
            "> Complete English translation.  \n" +
            "> Translated from the original Japanese audio with machine-assisted transcription.\n" +
            "\n" +
            "## 00:00 - Opening\n" +
            "\n" +
            "[00:07] **GUMI:** First speaker turn.\n" +
            "\n" +
            "[00:12] **SOYO:** Another speaker turn. This may be a long\n" +
            "paragraph if necessary.\n" +
            "\n" +
            "[00:12] **KANO:** Overlapping reply with an equal timestamp.\n" +
            "\n" +
            "[00:18] [everyone laughs]\n" +
            "\n" +
            "## 04:35 - Topic / segment name\n" +
            "\n" +
            "[04:38] **MOMO:** Another translated turn.\n" +
            "\n" +
            "> **Translator's note:** Brief explanation where needed.\n" +
            "\n" +
            "[04:41] **REIJI:** A guest speaks.\n" +
            "\n" +
            "Some arbitrary *prose* that Podcast Translate does not understand.\n" +
            "\n" +
            "- a list item\n" +
            "- another list item\n" +
            "\n" +
            "## 61:00 - Listener message\n" +
            "\n" +
            "[61:03] **KANO:** Past the hour mark.\n" +
            "\n" +
            "[125:03] **GUMI:** Very late timestamp.\n";

        [TestMethod]
        public void Timestamp_Parse_Valid()
        {
            Assert.IsTrue(PodcastTimestamp.TryParse("00:07", out var ms1));
            Assert.AreEqual(7000, ms1);
            Assert.IsTrue(PodcastTimestamp.TryParse("26:09", out var ms2));
            Assert.AreEqual((26 * 60 + 9) * 1000, ms2);
            Assert.IsTrue(PodcastTimestamp.TryParse("61:03", out var ms3));
            Assert.AreEqual((61 * 60 + 3) * 1000, ms3);
            Assert.IsTrue(PodcastTimestamp.TryParse("125:03", out var ms4));
            Assert.AreEqual((125 * 60 + 3) * 1000, ms4);
        }

        [TestMethod]
        public void Timestamp_Parse_Invalid()
        {
            Assert.IsFalse(PodcastTimestamp.TryParse("01:01:03", out _), "no hours field");
            Assert.IsFalse(PodcastTimestamp.TryParse("05:60", out _), "seconds must be 00-59");
            Assert.IsFalse(PodcastTimestamp.TryParse("05:03.200", out _), "no milliseconds");
            Assert.IsFalse(PodcastTimestamp.TryParse("05:03-05:09", out _), "no ranges");
            Assert.IsFalse(PodcastTimestamp.TryParse("5:03", out _), "minutes need at least two digits");
            Assert.IsFalse(PodcastTimestamp.TryParse("05:3", out _), "seconds need two digits");
            Assert.IsFalse(PodcastTimestamp.TryParse("-05:03", out _));
            Assert.IsFalse(PodcastTimestamp.TryParse(" 05:03", out _));
            Assert.IsFalse(PodcastTimestamp.TryParse("", out _));
        }

        [TestMethod]
        public void Timestamp_Parse_Rejects_Non_Ascii_Digits_Without_Throwing()
        {
            // Arabic-Indic digits: "\d" would match these and int.Parse would throw.
            Assert.IsFalse(PodcastTimestamp.TryParse("05:\u0660\u0663", out var ms1));
            Assert.AreEqual(0, ms1);
            Assert.IsFalse(PodcastTimestamp.TryParse("\u0660\u0665:03", out _));
            Assert.IsFalse(PodcastTimestamp.TryParse("\uFF10\uFF15:03", out _), "fullwidth digits");

            // In a document such a line is reported as a bad timestamp and preserved as raw Markdown.
            const string text = "[05:\u0660\u0663] **MOMO:** x\n## \u0660\u0665:03 - T\n";
            var doc = PodcastDocument.Parse(text);
            Assert.AreEqual(text, doc.ToMarkdown());
            Assert.AreEqual(0, doc.TimedBlocks.Count());
            Assert.AreEqual(0, doc.SectionBlocks.Count());
            var issues = doc.Validate();
            Assert.AreEqual(2, issues.Count);
            Assert.AreEqual(PodcastIssueCode.InvalidTimestamp, issues[0].Code);
            Assert.AreEqual(PodcastIssueCode.InvalidSectionTime, issues[1].Code);
        }

        [TestMethod]
        public void Timestamp_Parse_Minute_Digit_Count()
        {
            Assert.IsFalse(PodcastTimestamp.TryParse("5:03", out _), "one-digit minutes rejected");
            Assert.IsTrue(PodcastTimestamp.TryParse("05:03", out var twoDigit), "two-digit minutes accepted");
            Assert.AreEqual((5 * 60 + 3) * 1000, twoDigit);
            Assert.IsTrue(PodcastTimestamp.TryParse("61:03", out _), "> 59 minutes accepted");
            Assert.IsTrue(PodcastTimestamp.TryParse("125:03", out _), "three-digit minutes accepted");
            Assert.IsTrue(PodcastTimestamp.TryParse("0007:03", out var padded), "extra leading zeros are still two-or-more digits");
            Assert.AreEqual((7 * 60 + 3) * 1000, padded);
        }

        [TestMethod]
        public void Timestamp_Parse_Huge_Values_Rejected_Without_Overflow()
        {
            Assert.IsFalse(PodcastTimestamp.TryParse("99999999999999999999:00", out var ms1), "does not fit a long");
            Assert.AreEqual(0, ms1);
            Assert.IsFalse(PodcastTimestamp.TryParse("2147483647:00", out var ms2), "fits a long but not an int after * 60000");
            Assert.AreEqual(0, ms2);
            Assert.IsFalse(PodcastTimestamp.TryParse("35792:00", out _), "minutes * 60000 just above int.MaxValue");
            Assert.IsTrue(PodcastTimestamp.TryParse("35791:23", out var ms3), "largest whole second that fits an int");
            Assert.AreEqual(2147483000, ms3);
            Assert.AreEqual("35791:23", PodcastTimestamp.Format(ms3));
            Assert.IsFalse(PodcastTimestamp.TryParse("35791:24", out _), "seconds push it past int.MaxValue");
        }

        [TestMethod]
        public void Timestamp_Format_Does_Not_Wrap_Into_Hours()
        {
            Assert.AreEqual("00:07", PodcastTimestamp.Format(7000));
            Assert.AreEqual("61:03", PodcastTimestamp.Format((61 * 60 + 3) * 1000));
            Assert.AreEqual("125:03", PodcastTimestamp.Format((125 * 60 + 3) * 1000));
            Assert.AreEqual("[61:03]", PodcastTimestamp.FormatBracketed((61 * 60 + 3) * 1000));
        }

        [TestMethod]
        public void Timestamp_Format_Rejects_Non_Whole_Seconds_And_Negative()
        {
            Assert.IsTrue(PodcastTimestamp.IsCanonicalMilliseconds(0));
            Assert.IsTrue(PodcastTimestamp.IsCanonicalMilliseconds(12000));
            Assert.IsFalse(PodcastTimestamp.IsCanonicalMilliseconds(12345));
            Assert.IsFalse(PodcastTimestamp.IsCanonicalMilliseconds(-1000));

            AssertThrowsArgumentOutOfRange(() => PodcastTimestamp.Format(12345), "12345 ms must not be silently truncated to 00:12");
            AssertThrowsArgumentOutOfRange(() => PodcastTimestamp.Format(-1000), "negative");
            AssertThrowsArgumentOutOfRange(() => PodcastTimestamp.FormatBracketed(999), "sub-second");
        }

        [TestMethod]
        public void TimeMs_Setters_Enforce_Whole_Seconds()
        {
            var dialogue = new PodcastDialogueBlock();
            AssertThrowsArgumentOutOfRange(() => dialogue.TimeMs = 12345, "dialogue");
            AssertThrowsArgumentOutOfRange(() => dialogue.TimeMs = -1000, "dialogue negative");
            Assert.AreEqual(0, dialogue.TimeMs);
            dialogue.TimeMs = 12000;
            Assert.AreEqual(12000, dialogue.TimeMs);

            var ev = new PodcastAudibleEventBlock();
            AssertThrowsArgumentOutOfRange(() => ev.TimeMs = 500, "event");

            var section = new PodcastSectionBlock();
            AssertThrowsArgumentOutOfRange(() => section.EditorialTimeMs = 1500, "section");
            section.EditorialTimeMs = 60000;
            Assert.AreEqual(60000, section.EditorialTimeMs);
        }

        [TestMethod]
        public void Dialogue_Is_Parsed_Into_Time_Speaker_Text()
        {
            var doc = PodcastDocument.Parse("[18:43] **GUMI:** That's what I thought too.\n");

            Assert.AreEqual(1, doc.Blocks.Count);
            var dialogue = doc.Blocks[0] as PodcastDialogueBlock;
            Assert.IsNotNull(dialogue);
            Assert.AreEqual((18 * 60 + 43) * 1000, dialogue.TimeMs);
            Assert.AreEqual("GUMI", dialogue.Speaker);
            Assert.AreEqual("That's what I thought too.", dialogue.Text);
            Assert.IsFalse(dialogue.IsModified);
            Assert.AreEqual(1, dialogue.LineNumber);
        }

        [TestMethod]
        public void Dialogue_Accepts_Arbitrary_Speaker_Names()
        {
            var doc = PodcastDocument.Parse(
                "[04:21] **REIJI:** A guest.\n" +
                "[04:22] **Unknown (staff?):** Uncertain label.\n" +
                "[04:23] **Reiji Fujii:** Lower case and spaces.\n");

            var dialogues = doc.DialogueBlocks.ToList();
            Assert.AreEqual(3, dialogues.Count);
            Assert.AreEqual("REIJI", dialogues[0].Speaker);
            Assert.AreEqual("Unknown (staff?)", dialogues[1].Speaker);
            Assert.AreEqual("Reiji Fujii", dialogues[2].Speaker);
            Assert.AreEqual(0, doc.Validate().Count);
        }

        [TestMethod]
        public void Dialogue_Multiline_Is_One_Block()
        {
            var doc = PodcastDocument.Parse(
                "[00:12] **SOYO:** Another speaker turn. This may be a long\n" +
                "paragraph if necessary.\n" +
                "And a third line.\n" +
                "\n" +
                "[00:15] **MOMO:** Next.\n");

            var dialogues = doc.DialogueBlocks.ToList();
            Assert.AreEqual(2, dialogues.Count);
            Assert.AreEqual("Another speaker turn. This may be a long\nparagraph if necessary.\nAnd a third line.", dialogues[0].Text);
            Assert.AreEqual(3, dialogues[0].OriginalLines.Count);
            Assert.AreEqual("Next.", dialogues[1].Text);
            Assert.AreEqual(5, dialogues[1].LineNumber);
        }

        [TestMethod]
        public void Dialogue_Continuation_Stops_At_Blockquote_And_Heading()
        {
            var doc = PodcastDocument.Parse(
                "[04:38] **MOMO:** Another translated turn.\n" +
                "> **Translator's note:** directly below.\n" +
                "## 05:00 - Next\n");

            Assert.AreEqual(3, doc.Blocks.Count);
            Assert.IsInstanceOfType(doc.Blocks[0], typeof(PodcastDialogueBlock));
            Assert.IsInstanceOfType(doc.Blocks[1], typeof(PodcastRawMarkdownBlock));
            Assert.IsInstanceOfType(doc.Blocks[2], typeof(PodcastSectionBlock));
            Assert.AreEqual("Another translated turn.", ((PodcastDialogueBlock)doc.Blocks[0]).Text);
        }

        [TestMethod]
        public void AudibleEvent_Is_Parsed()
        {
            var doc = PodcastDocument.Parse("[18:46] [everyone laughs]\n");

            var ev = doc.Blocks[0] as PodcastAudibleEventBlock;
            Assert.IsNotNull(ev);
            Assert.AreEqual((18 * 60 + 46) * 1000, ev.TimeMs);
            Assert.AreEqual("everyone laughs", ev.Description);
            Assert.AreEqual(1, doc.TimedBlocks.Count());
        }

        [TestMethod]
        public void Section_Is_Parsed_And_Is_Not_Timed()
        {
            var doc = PodcastDocument.Parse("## 18:30 - Listener message\n");

            var section = doc.Blocks[0] as PodcastSectionBlock;
            Assert.IsNotNull(section);
            Assert.AreEqual((18 * 60 + 30) * 1000, section.EditorialTimeMs);
            Assert.AreEqual("Listener message", section.Title);
            Assert.AreEqual(0, doc.TimedBlocks.Count());
        }

        [TestMethod]
        public void Section_Time_Does_Not_Participate_In_Ordering_Validation()
        {
            // The heading time (00:00) is far below the previous sync timestamp; that must not be an error.
            var doc = PodcastDocument.Parse(
                "[10:00] **MOMO:** Late.\n" +
                "\n" +
                "## 00:00 - Heading with an early editorial time\n" +
                "\n" +
                "[10:05] **KANO:** Later.\n");

            Assert.AreEqual(0, doc.Validate().Count);
            Assert.AreEqual(2, doc.TimedBlocks.Count());
        }

        [TestMethod]
        public void Heading_Without_Time_Is_Raw()
        {
            var doc = PodcastDocument.Parse("# Urunemu #43 - Title\n## Plain heading\n");

            Assert.IsTrue(doc.Blocks.All(b => b is PodcastRawMarkdownBlock));
            Assert.AreEqual(0, doc.SectionBlocks.Count());
        }

        [TestMethod]
        public void Section_Malformed_Time_Is_Reported_And_Preserved()
        {
            const string text =
                "## 05:60 - Topic\n" +
                "## 5:03 - Topic\n" +
                "## 01:01:03 - Topic\n" +
                "## 05:03.200 - Topic\n" +
                "## 05:03 - Valid\n" +
                "## Plain heading - with a dash\n";
            var doc = PodcastDocument.Parse(text);

            Assert.AreEqual(text, doc.ToMarkdown());
            Assert.AreEqual(1, doc.SectionBlocks.Count());
            Assert.AreEqual("Valid", doc.SectionBlocks.First().Title);

            var issues = doc.Validate();
            Assert.AreEqual(4, issues.Count);
            Assert.IsTrue(issues.All(i => i.Code == PodcastIssueCode.InvalidSectionTime && i.Severity == PodcastIssueSeverity.Error));
            Assert.AreEqual(1, issues[0].LineNumber);
            Assert.AreEqual(2, issues[1].LineNumber);
            Assert.AreEqual(3, issues[2].LineNumber);
            Assert.AreEqual(4, issues[3].LineNumber);

            // Malformed headings are raw blocks, each on its own so the line numbers stay precise.
            for (var i = 0; i < 4; i++)
            {
                Assert.IsInstanceOfType(doc.Blocks[i], typeof(PodcastRawMarkdownBlock));
            }
        }

        [TestMethod]
        public void Section_Malformed_Time_Does_Not_Affect_Sync_Ordering()
        {
            var doc = PodcastDocument.Parse(
                "[10:00] **MOMO:** Late.\n" +
                "## 5:03 - malformed but early\n" +
                "[10:05] **KANO:** Later.\n");

            var issues = doc.Validate();
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(PodcastIssueCode.InvalidSectionTime, issues[0].Code);
            Assert.AreEqual(2, doc.TimedBlocks.Count());
        }

        [TestMethod]
        public void Raw_Markdown_Is_Preserved_And_Grouped()
        {
            var doc = PodcastDocument.Parse(
                "> Complete English translation.  \n" +
                "> Second quote line.\n" +
                "\n" +
                "\n" +
                "Prose.\n");

            Assert.AreEqual(3, doc.Blocks.Count);
            var quote = (PodcastRawMarkdownBlock)doc.Blocks[0];
            Assert.IsFalse(quote.IsBlank);
            Assert.AreEqual("> Complete English translation.  \n> Second quote line.", quote.Text);
            var blank = (PodcastRawMarkdownBlock)doc.Blocks[1];
            Assert.IsTrue(blank.IsBlank);
            Assert.AreEqual(2, blank.OriginalLines.Count);
            Assert.AreEqual("Prose.", ((PodcastRawMarkdownBlock)doc.Blocks[2]).Text);
        }

        [TestMethod]
        public void FrontMatter_Is_Parsed_And_Preserved()
        {
            var doc = PodcastDocument.Parse(SampleDocument);

            var fm = doc.FrontMatter;
            Assert.IsNotNull(fm);
            Assert.AreEqual(1, fm.LineNumber);
            Assert.AreEqual(6, fm.OriginalLines.Count);
            Assert.AreEqual("fmyoko", fm.GetValue("run"));
            Assert.AreEqual("43", fm.GetValue("episode"));
            Assert.AreEqual("2026-08-27", fm.GetValue("date"));
            Assert.AreEqual("fkDjjg6DYoQ", fm.GetValue("youtube_id"));
            Assert.IsNull(fm.GetValue("missing"));
            Assert.AreEqual("---\nrun: fmyoko\nepisode: 43\ndate: 2026-08-27\nyoutube_id: fkDjjg6DYoQ\n---\n", fm.OriginalText);
        }

        [TestMethod]
        public void FrontMatter_Validation_Schema()
        {
            var ok = PodcastDocument.Parse("---\nrun: youtube\nepisode: 7\ndate: 2026-01-02\n---\n");
            Assert.AreEqual(0, ok.Validate().Count);
            Assert.AreEqual(0, ok.ValidateCanonical().Count);

            var bad = PodcastDocument.Parse("---\nrun: twitch\nepisode: 0\ndate: 27/08/2026\nextra: x\n---\n");
            var issues = bad.ValidateCanonical();
            Assert.AreEqual(4, issues.Count);
            Assert.IsTrue(issues.All(i => i.Severity == PodcastIssueSeverity.Error));
            Assert.AreEqual(3, issues.Count(i => i.Code == PodcastIssueCode.FrontMatterInvalidValue));
            Assert.AreEqual(1, issues.Count(i => i.Code == PodcastIssueCode.FrontMatterUnknownKey));

            var missing = PodcastDocument.Parse("---\nrun: fmyoko\n---\n");
            Assert.AreEqual(2, missing.ValidateCanonical().Count(i => i.Code == PodcastIssueCode.FrontMatterMissingKey));

            var duplicate = PodcastDocument.Parse("---\nrun: fmyoko\nepisode: 1\ndate: 2026-01-01\nrun: youtube\n---\n");
            var dupIssues = duplicate.ValidateCanonical();
            Assert.AreEqual(1, dupIssues.Count);
            Assert.AreEqual(PodcastIssueCode.FrontMatterDuplicateKey, dupIssues[0].Code);

            Assert.AreEqual(1, PodcastDocument.Parse("---\nrun: fmyoko\nepisode: -3\ndate: 2026-01-01\n---\n").ValidateCanonical().Count, "negative episode");
            Assert.AreEqual(1, PodcastDocument.Parse("---\nrun: fmyoko\nepisode: 4.5\ndate: 2026-01-01\n---\n").ValidateCanonical().Count, "fractional episode");
            Assert.AreEqual(1, PodcastDocument.Parse("---\nrun: Fmyoko\nepisode: 1\ndate: 2026-01-01\n---\n").ValidateCanonical().Count, "run is case-sensitive");
        }

        [TestMethod]
        public void FrontMatter_Malformed_Lines_Are_Preserved_But_Rejected()
        {
            const string text = "---\nrun: fmyoko\nepisode: 43\ndate: 2026-08-27\nthis is garbage\n---\n";
            var doc = PodcastDocument.Parse(text);

            Assert.AreEqual(text, doc.ToMarkdown());
            var fm = doc.FrontMatter;
            Assert.IsNotNull(fm);
            Assert.AreEqual(3, fm.Values.Count);
            Assert.AreEqual(1, fm.UnparsedLines.Count);
            Assert.AreEqual(5, fm.UnparsedLines[0].Key);
            Assert.AreEqual("this is garbage", fm.UnparsedLines[0].Value);

            var issues = doc.ValidateCanonical();
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(PodcastIssueCode.FrontMatterMalformedLine, issues[0].Code);
            Assert.AreEqual(PodcastIssueSeverity.Error, issues[0].Severity);
            Assert.AreEqual(5, issues[0].LineNumber);

            // A key-like line with an illegal key character is malformed, not a scalar.
            var hyphen = PodcastDocument.Parse("---\nrun: fmyoko\nepisode: 1\ndate: 2026-01-01\nyoutube-id: abc\n---\n");
            var hyphenIssues = hyphen.ValidateCanonical();
            Assert.AreEqual(1, hyphenIssues.Count);
            Assert.AreEqual(PodcastIssueCode.FrontMatterMalformedLine, hyphenIssues[0].Code);
            Assert.AreEqual(5, hyphenIssues[0].LineNumber);
            Assert.IsNull(hyphen.FrontMatter.GetValue("youtube_id"));

            // Blank physical lines inside front matter are tolerated; a lone "key:" with an empty value is a scalar.
            var blank = PodcastDocument.Parse("---\nrun: fmyoko\n\nepisode: 1\ndate: 2026-01-01\n---\n");
            Assert.AreEqual(0, blank.ValidateCanonical().Count);
            Assert.AreEqual(0, blank.FrontMatter.UnparsedLines.Count);
        }

        [TestMethod]
        public void FrontMatter_Validation_Date_Must_Be_Real_Calendar_Date()
        {
            Assert.AreEqual(0, DateIssues("2026-08-27"));
            Assert.AreEqual(0, DateIssues("2024-02-29"), "leap year");
            Assert.AreEqual(1, DateIssues("2026-02-29"), "not a leap year");
            Assert.AreEqual(1, DateIssues("2026-13-01"), "month 13");
            Assert.AreEqual(1, DateIssues("2026-00-10"), "month 0");
            Assert.AreEqual(1, DateIssues("2026-04-31"), "April has 30 days");
            Assert.AreEqual(1, DateIssues("2026-8-27"), "must be zero padded");
            Assert.AreEqual(1, DateIssues("26-08-27"), "four-digit year");
            Assert.AreEqual(1, DateIssues("2026/08/27"), "wrong separator");
            Assert.AreEqual(1, DateIssues("2026-08-27T00:00"), "no time part");
        }

        [TestMethod]
        public void FrontMatter_Validation_YouTubeId()
        {
            Assert.AreEqual(0, YouTubeIdIssues("fkDjjg6DYoQ"));
            Assert.AreEqual(0, YouTubeIdIssues("a-b_c-d_e-f"));
            Assert.AreEqual(1, YouTubeIdIssues("fkDjjg6DYo"), "10 characters");
            Assert.AreEqual(1, YouTubeIdIssues("fkDjjg6DYoQx"), "12 characters");
            Assert.AreEqual(1, YouTubeIdIssues("fkDjjg6DYo!"), "bad character");
            Assert.AreEqual(1, YouTubeIdIssues("https://youtu.be/fkDjjg6DYoQ"), "urls are not ids");
            Assert.AreEqual(0, PodcastDocument.Parse("---\nrun: fmyoko\nepisode: 1\ndate: 2026-01-01\n---\n").ValidateCanonical().Count, "youtube_id is optional");
        }

        [TestMethod]
        public void Canonical_Validation_Requires_FrontMatter_But_Structural_Does_Not()
        {
            var fragment = PodcastDocument.Parse("[00:07] **GUMI:** Hi.\n");
            Assert.AreEqual(0, fragment.Validate().Count);

            var canonical = fragment.ValidateCanonical();
            Assert.AreEqual(1, canonical.Count);
            Assert.AreEqual(PodcastIssueCode.FrontMatterMissing, canonical[0].Code);
            Assert.AreEqual(PodcastIssueSeverity.Error, canonical[0].Severity);

            // Front matter that is not on line 1 does not count.
            var late = PodcastDocument.Parse("\n---\nrun: fmyoko\nepisode: 1\ndate: 2026-01-01\n---\n");
            Assert.AreEqual(1, late.ValidateCanonical().Count(i => i.Code == PodcastIssueCode.FrontMatterMissing));

            // Front matter must be the first block even after structural edits.
            var canonicalDoc = PodcastDocument.Parse(SampleDocument);
            canonicalDoc.InsertBlock(0, new PodcastRawMarkdownBlock { Text = "Intro line" });
            Assert.IsNull(canonicalDoc.FrontMatter);
            var afterInsert = canonicalDoc.ValidateCanonical();
            Assert.AreEqual(1, afterInsert.Count);
            Assert.AreEqual(PodcastIssueCode.FrontMatterMissing, afterInsert[0].Code);
            Assert.IsTrue(afterInsert[0].Message.Contains("not the first block"));

            var movedDoc = PodcastDocument.Parse(SampleDocument);
            Assert.IsNotNull(movedDoc.FrontMatter);
            movedDoc.MoveBlock(0, 2);
            Assert.IsNull(movedDoc.FrontMatter);
            Assert.AreEqual(1, movedDoc.ValidateCanonical().Count(i => i.Code == PodcastIssueCode.FrontMatterMissing));
            Assert.AreEqual(0, movedDoc.Validate().Count, "structural validation stays permissive");

            // Structural problems are reported by both paths.
            var broken = PodcastDocument.Parse("---\nrun: fmyoko\nepisode: 1\ndate: 2026-01-01\n---\n[00:20] **A:** x\n[00:10] **B:** y\n");
            Assert.AreEqual(1, broken.Validate().Count(i => i.Code == PodcastIssueCode.TimestampDecreasing));
            Assert.AreEqual(1, broken.ValidateCanonical().Count(i => i.Code == PodcastIssueCode.TimestampDecreasing));
        }

        [TestMethod]
        public void FrontMatter_Only_Recognized_On_First_Line()
        {
            var doc = PodcastDocument.Parse("\n---\nrun: fmyoko\n---\n");
            Assert.IsNull(doc.FrontMatter);
            Assert.AreEqual("\n---\nrun: fmyoko\n---\n", SerializeRoundTrip("\n---\nrun: fmyoko\n---\n"));
        }

        [TestMethod]
        public void Validation_Invalid_Timestamps_Are_Reported_And_Preserved()
        {
            const string text =
                "[05:60] **MOMO:** bad seconds\n" +
                "[01:01:03] **MOMO:** hours\n" +
                "[05:03.200] **MOMO:** millis\n" +
                "[05:03-05:09] **MOMO:** range\n" +
                "[5:03] **MOMO:** one-digit minutes\n" +
                "[99999999999999999999:00] **MOMO:** huge\n";
            var doc = PodcastDocument.Parse(text);

            var issues = doc.Validate();
            Assert.AreEqual(6, issues.Count);
            Assert.IsTrue(issues.All(i => i.Code == PodcastIssueCode.InvalidTimestamp && i.Severity == PodcastIssueSeverity.Error));
            for (var i = 0; i < issues.Count; i++)
            {
                Assert.AreEqual(i + 1, issues[i].LineNumber);
            }

            Assert.IsTrue(doc.Blocks.All(b => b is PodcastRawMarkdownBlock));
            Assert.AreEqual(0, doc.TimedBlocks.Count());
            Assert.AreEqual(text, doc.ToMarkdown(), "malformed lines must still round-trip");
        }

        [TestMethod]
        public void Validation_Minutes_Over_59_Are_Valid()
        {
            var doc = PodcastDocument.Parse("[61:03] **KANO:** Past the hour.\n[125:03] **GUMI:** Later.\n");

            Assert.AreEqual(0, doc.Validate().Count);
            Assert.AreEqual((125 * 60 + 3) * 1000, doc.TimedBlocks.Last().TimeMs);
        }

        [TestMethod]
        public void Validation_Equal_Adjacent_Timestamps_Are_Valid()
        {
            var doc = PodcastDocument.Parse(
                "[00:12] **SOYO:** One.\n" +
                "[00:12] **KANO:** Two.\n" +
                "[00:12] [everyone laughs]\n");

            Assert.AreEqual(0, doc.Validate().Count);
        }

        [TestMethod]
        public void Validation_Decreasing_Timestamp_Is_Error()
        {
            var doc = PodcastDocument.Parse(
                "[00:20] **SOYO:** One.\n" +
                "\n" +
                "[00:19] [everyone laughs]\n");

            var issues = doc.Validate();
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(PodcastIssueCode.TimestampDecreasing, issues[0].Code);
            Assert.AreEqual(3, issues[0].LineNumber);
        }

        [TestMethod]
        public void Validation_Unrecognized_Timed_Line_Is_Warning_And_Raw()
        {
            var doc = PodcastDocument.Parse("[05:42] text without a speaker\n");

            Assert.IsInstanceOfType(doc.Blocks[0], typeof(PodcastRawMarkdownBlock));
            var issues = doc.Validate();
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(PodcastIssueCode.UnrecognizedTimedLine, issues[0].Code);
            Assert.AreEqual(PodcastIssueSeverity.Warning, issues[0].Severity);
        }

        [TestMethod]
        public void RoundTrip_Sample_Document_Is_Character_Identical()
        {
            var doc = PodcastDocument.Parse(SampleDocument);

            Assert.IsFalse(doc.IsModified);
            Assert.AreEqual(SampleDocument, doc.ToMarkdown());

            // Sanity-check that the document was actually understood, not just copied.
            Assert.AreEqual(3, doc.SectionBlocks.Count());
            Assert.AreEqual(7, doc.DialogueBlocks.Count());
            Assert.AreEqual(8, doc.TimedBlocks.Count());
            Assert.IsNotNull(doc.FrontMatter);
            Assert.AreEqual(0, doc.Validate().Count);
            var speakers = doc.DialogueBlocks.Select(d => d.Speaker).Distinct().ToList();
            CollectionAssert.AreEquivalent(new[] { "GUMI", "SOYO", "KANO", "MOMO", "REIJI" }, speakers);
        }

        [TestMethod]
        public void RoundTrip_Preserves_Crlf_Mixed_Endings_And_Missing_Final_Newline()
        {
            const string crlf = "---\r\nrun: fmyoko\r\nepisode: 1\r\ndate: 2026-01-01\r\n---\r\n\r\n## 00:00 - Opening\r\n\r\n[00:07] **GUMI:** Hi.\r\nWrapped.\r\n\r\n[00:09] [laughs]";
            var doc = PodcastDocument.Parse(crlf);
            Assert.AreEqual("\r\n", doc.NewLine);
            Assert.AreEqual(crlf, doc.ToMarkdown());

            const string mixed = "[00:07] **GUMI:** Hi.\r\nWrapped.\n\n[00:09] **MOMO:** Old mac\rstyle.\n";
            Assert.AreEqual(mixed, SerializeRoundTrip(mixed));

            const string noTrailing = "[00:07] **GUMI:** Hi.";
            Assert.AreEqual(noTrailing, SerializeRoundTrip(noTrailing));

            const string bom = "\uFEFF---\nrun: fmyoko\nepisode: 1\ndate: 2026-01-01\n---\n";
            var bomDoc = PodcastDocument.Parse(bom);
            Assert.IsTrue(bomDoc.HasByteOrderMark);
            Assert.IsNotNull(bomDoc.FrontMatter);
            Assert.AreEqual(bom, bomDoc.ToMarkdown());

            Assert.AreEqual(string.Empty, SerializeRoundTrip(string.Empty));
        }

        [TestMethod]
        public void RoundTrip_Preserves_Odd_Spacing_When_Unmodified()
        {
            // Trailing spaces, tabs, double spaces after the speaker: all preserved as long as nothing is edited.
            const string text = "[18:43] **GUMI:**  two spaces  \n\t  indented continuation\n[18:46] [laughs]   \n##  05:00 - not a section (two spaces)\n";
            var doc = PodcastDocument.Parse(text);
            Assert.AreEqual(text, doc.ToMarkdown());
            Assert.AreEqual(" two spaces  \n\t  indented continuation", ((PodcastDialogueBlock)doc.Blocks[0]).Text);
            Assert.IsInstanceOfType(doc.Blocks[2], typeof(PodcastRawMarkdownBlock));
        }

        [TestMethod]
        public void Mutation_Speaker_Changes_Only_That_Block()
        {
            var doc = PodcastDocument.Parse(SampleDocument);
            var target = doc.DialogueBlocks.First(d => d.Speaker == "GUMI" && d.Text == "First speaker turn.");

            target.Speaker = "MOMO";

            Assert.IsTrue(target.IsModified);
            Assert.AreEqual(1, doc.Blocks.Count(b => b.IsModified));
            var expected = SampleDocument.Replace("[00:07] **GUMI:** First speaker turn.", "[00:07] **MOMO:** First speaker turn.");
            Assert.AreNotEqual(SampleDocument, expected);
            Assert.AreEqual(expected, doc.ToMarkdown());
        }

        [TestMethod]
        public void Mutation_Text_Changes_Only_That_Block()
        {
            var doc = PodcastDocument.Parse(SampleDocument);
            var target = doc.DialogueBlocks.First(d => d.Speaker == "MOMO");

            target.Text = "A revised translation.";

            var expected = SampleDocument.Replace("[04:38] **MOMO:** Another translated turn.", "[04:38] **MOMO:** A revised translation.");
            Assert.AreEqual(expected, doc.ToMarkdown());
        }

        [TestMethod]
        public void Mutation_Multiline_Text_Keeps_Block_Shape()
        {
            var doc = PodcastDocument.Parse(SampleDocument);
            var target = doc.DialogueBlocks.First(d => d.Speaker == "SOYO");

            // Grow from two physical lines to three; the surrounding blank lines and terminators are untouched.
            target.Text = "Line one\nline two\nline three.";
            var expected = SampleDocument.Replace(
                "[00:12] **SOYO:** Another speaker turn. This may be a long\nparagraph if necessary.\n",
                "[00:12] **SOYO:** Line one\nline two\nline three.\n");
            Assert.AreEqual(expected, doc.ToMarkdown());

            // Shrink to a single line.
            target.Text = "Single.";
            expected = SampleDocument.Replace(
                "[00:12] **SOYO:** Another speaker turn. This may be a long\nparagraph if necessary.\n",
                "[00:12] **SOYO:** Single.\n");
            Assert.AreEqual(expected, doc.ToMarkdown());
        }

        [TestMethod]
        public void Mutation_Timestamp_Changes_Only_The_Timestamp()
        {
            var doc = PodcastDocument.Parse(SampleDocument);
            var target = doc.DialogueBlocks.First(d => d.Speaker == "REIJI");

            target.TimeMs += 1000;

            var expected = SampleDocument.Replace("[04:41] **REIJI:** A guest speaks.", "[04:42] **REIJI:** A guest speaks.");
            Assert.AreEqual(expected, doc.ToMarkdown());
            Assert.AreEqual(0, doc.Validate().Count);
        }

        [TestMethod]
        public void Mutation_Timestamp_Past_Hour_Serializes_As_Minutes()
        {
            var doc = PodcastDocument.Parse("[59:59] **MOMO:** Almost.\n");
            var target = doc.DialogueBlocks.First();

            target.TimeMs += 4000;

            Assert.AreEqual("[60:03] **MOMO:** Almost.\n", doc.ToMarkdown());
        }

        [TestMethod]
        public void Mutation_AudibleEvent_Description()
        {
            var doc = PodcastDocument.Parse(SampleDocument);
            var ev = doc.TimedBlocks.OfType<PodcastAudibleEventBlock>().First();

            ev.Description = "MOMO laughs";

            var expected = SampleDocument.Replace("[00:18] [everyone laughs]", "[00:18] [MOMO laughs]");
            Assert.AreEqual(expected, doc.ToMarkdown());
        }

        [TestMethod]
        public void Mutation_Section_Title_Changes_Only_Heading()
        {
            var doc = PodcastDocument.Parse(SampleDocument);
            var section = doc.SectionBlocks.First(s => s.Title == "Topic / segment name");

            section.Title = "Renamed segment";

            var expected = SampleDocument.Replace("## 04:35 - Topic / segment name", "## 04:35 - Renamed segment");
            Assert.AreEqual(expected, doc.ToMarkdown());
            Assert.AreEqual(1, doc.Blocks.Count(b => b.IsModified));
        }

        [TestMethod]
        public void Mutation_Setting_Same_Value_Does_Not_Mark_Modified()
        {
            var doc = PodcastDocument.Parse(SampleDocument);
            var dialogue = doc.DialogueBlocks.First();
            var section = doc.SectionBlocks.First();

            dialogue.Speaker = dialogue.Speaker;
            dialogue.Text = dialogue.Text;
            dialogue.TimeMs = dialogue.TimeMs;
            section.Title = section.Title;

            Assert.IsFalse(doc.IsModified);
            Assert.AreEqual(SampleDocument, doc.ToMarkdown());
        }

        [TestMethod]
        public void Mutation_Raw_Block_Text()
        {
            var doc = PodcastDocument.Parse(SampleDocument);
            var note = doc.Blocks.OfType<PodcastRawMarkdownBlock>().First(b => b.Text.StartsWith("> **Translator's note:**"));

            note.Text = "> **Translator's note:** Rewritten.";

            var expected = SampleDocument.Replace("> **Translator's note:** Brief explanation where needed.", "> **Translator's note:** Rewritten.");
            Assert.AreEqual(expected, doc.ToMarkdown());
        }

        [TestMethod]
        public void Mutation_Of_Block_Without_Trailing_Newline_Adds_None()
        {
            var doc = PodcastDocument.Parse("[00:07] **GUMI:** Hi.");
            doc.DialogueBlocks.First().Speaker = "MOMO";
            Assert.AreEqual("[00:07] **MOMO:** Hi.", doc.ToMarkdown());
        }

        [TestMethod]
        public void New_Block_Inserted_In_Code_Uses_Document_NewLine()
        {
            var doc = PodcastDocument.Parse("[00:07] **GUMI:** Hi.\r\n");
            var inserted = new PodcastDialogueBlock { TimeMs = 8000, Speaker = "MOMO", Text = "Hello.\nSecond line." };
            doc.AddBlock(inserted);

            Assert.IsTrue(inserted.IsModified);
            Assert.IsNull(inserted.OriginalText);
            Assert.AreEqual("[00:07] **GUMI:** Hi.\r\n[00:08] **MOMO:** Hello.\r\nSecond line.\r\n", doc.ToMarkdown());
        }

        [TestMethod]
        public void Document_IsModified_Untouched_Is_False()
        {
            var doc = PodcastDocument.Parse(SampleDocument);
            Assert.IsFalse(doc.IsModified);
            Assert.IsFalse(doc.IsStructureModified);
        }

        [TestMethod]
        public void Document_IsModified_After_Editing_Speaker()
        {
            var doc = PodcastDocument.Parse(SampleDocument);
            doc.DialogueBlocks.First().Speaker = "MOMO";
            Assert.IsTrue(doc.IsModified);
            Assert.IsFalse(doc.IsStructureModified, "a field edit is not a structural change");
        }

        [TestMethod]
        public void Document_IsModified_After_Removing_Untouched_Block()
        {
            var doc = PodcastDocument.Parse(SampleDocument);
            var target = doc.DialogueBlocks.First(d => d.Speaker == "REIJI");
            var count = doc.Blocks.Count;

            Assert.IsTrue(doc.RemoveBlock(target));

            Assert.IsTrue(doc.IsStructureModified);
            Assert.IsTrue(doc.IsModified);
            Assert.AreEqual(count - 1, doc.Blocks.Count);
            Assert.IsFalse(doc.Blocks.Any(b => b.IsModified), "surviving blocks are untouched");
            Assert.AreEqual(SampleDocument.Replace("[04:41] **REIJI:** A guest speaks.\n", ""), doc.ToMarkdown());
            Assert.IsFalse(doc.RemoveBlock(target), "removing again is a no-op");
        }

        [TestMethod]
        public void Document_IsModified_After_RemoveBlockAt()
        {
            var doc = PodcastDocument.Parse("A\n\nB\n");
            var removed = doc.RemoveBlockAt(0);
            Assert.AreEqual("A", ((PodcastRawMarkdownBlock)removed).Text);
            Assert.IsTrue(doc.IsModified);
            Assert.AreEqual("\nB\n", doc.ToMarkdown());
        }

        [TestMethod]
        public void Document_IsModified_After_Reordering_Untouched_Blocks()
        {
            var doc = PodcastDocument.Parse("[00:07] **GUMI:** One.\n[00:08] **MOMO:** Two.\n[00:09] **KANO:** Three.\n");

            doc.MoveBlock(2, 0);

            Assert.IsTrue(doc.IsStructureModified);
            Assert.IsTrue(doc.IsModified);
            Assert.IsFalse(doc.Blocks.Any(b => b.IsModified));
            Assert.AreEqual("[00:09] **KANO:** Three.\n[00:07] **GUMI:** One.\n[00:08] **MOMO:** Two.\n", doc.ToMarkdown());
            Assert.AreEqual(1, doc.Validate().Count(i => i.Code == PodcastIssueCode.TimestampDecreasing));

            var doc2 = PodcastDocument.Parse("A\nB\n");
            doc2.MoveBlock(0, 0);
            Assert.IsFalse(doc2.IsModified, "moving a block onto itself changes nothing");
        }

        [TestMethod]
        public void Document_IsModified_After_Inserting_Block()
        {
            var doc = PodcastDocument.Parse("[00:07] **GUMI:** One.\n[00:09] **KANO:** Three.\n");
            var inserted = new PodcastDialogueBlock { TimeMs = 8000, Speaker = "MOMO", Text = "Two." };

            doc.InsertBlock(1, inserted);

            Assert.IsTrue(doc.IsStructureModified);
            Assert.IsTrue(doc.IsModified);
            Assert.AreEqual(1, doc.IndexOf(inserted));
            Assert.AreEqual("[00:07] **GUMI:** One.\n[00:08] **MOMO:** Two.\n[00:09] **KANO:** Three.\n", doc.ToMarkdown());
            Assert.AreEqual(0, doc.Validate().Count);
        }

        [TestMethod]
        public void Document_IsModified_After_Clearing_Blocks()
        {
            var doc = PodcastDocument.Parse(SampleDocument);
            doc.ClearBlocks();
            Assert.IsTrue(doc.IsModified);
            Assert.AreEqual(0, doc.Blocks.Count);
            Assert.AreEqual(string.Empty, doc.ToMarkdown());

            var empty = PodcastDocument.Parse(string.Empty);
            empty.ClearBlocks();
            Assert.IsFalse(empty.IsModified, "clearing an empty document changes nothing");
        }

        [TestMethod]
        public void Document_Mutation_Argument_Checks()
        {
            var doc = PodcastDocument.Parse("A\n");
            var block = doc.Blocks[0];
            AssertThrowsArgumentOutOfRange(() => doc.InsertBlock(2, new PodcastRawMarkdownBlock()), "insert past end");
            AssertThrowsArgumentOutOfRange(() => doc.RemoveBlockAt(1), "remove past end");
            AssertThrowsArgumentOutOfRange(() => doc.MoveBlock(0, 1), "move past end");
            AssertThrows<System.ArgumentException>(() => doc.AddBlock(block), "same block twice");
            AssertThrows<System.ArgumentNullException>(() => doc.AddBlock(null), "null block");
            Assert.IsFalse(doc.IsModified, "failed operations do not dirty the document");
        }

        [TestMethod]
        public void Blocks_View_Cannot_Mutate_Underlying_List()
        {
            var doc = PodcastDocument.Parse("A\nB\n");

            Assert.IsNull(doc.Blocks as List<PodcastBlock>, "the view must not be the backing list");
            var asCollection = doc.Blocks as ICollection<PodcastBlock>;
            Assert.IsNotNull(asCollection);
            Assert.IsTrue(asCollection.IsReadOnly);
            AssertThrows<System.NotSupportedException>(() => asCollection.Add(new PodcastRawMarkdownBlock()), "Add through the view");
            AssertThrows<System.NotSupportedException>(() => asCollection.Clear(), "Clear through the view");
            AssertThrows<System.NotSupportedException>(() => ((IList<PodcastBlock>)doc.Blocks).RemoveAt(0), "RemoveAt through the view");

            Assert.AreEqual(1, doc.Blocks.Count);
            Assert.IsFalse(doc.IsModified);
            Assert.AreEqual("A\nB\n", doc.ToMarkdown());

            // The view tracks changes made through the document's own operations.
            doc.AddBlock(new PodcastRawMarkdownBlock { Text = "C" });
            Assert.AreEqual(2, doc.Blocks.Count);
        }

        [TestMethod]
        public void Structural_NoFinalNewline_Untouched_RoundTrip_Is_Exact()
        {
            const string text = "[00:07] **GUMI:** One.\n\n[00:09] **KANO:** Two.";
            var doc = PodcastDocument.Parse(text);
            Assert.AreEqual(text, doc.ToMarkdown());

            const string crlf = "A\r\n\r\nB";
            Assert.AreEqual(crlf, SerializeRoundTrip(crlf));
        }

        [TestMethod]
        public void Structural_Append_After_Block_Without_Final_Newline_Adds_Boundary()
        {
            var doc = PodcastDocument.Parse("A");
            doc.AddBlock(new PodcastDialogueBlock { TimeMs = 7000, Speaker = "MOMO", Text = "Hi." });

            Assert.AreEqual("A\n[00:07] **MOMO:** Hi.\n", doc.ToMarkdown());

            var crlf = PodcastDocument.Parse("A\r\nB");
            crlf.AddBlock(new PodcastRawMarkdownBlock { Text = "C" });
            Assert.AreEqual("A\r\nB\r\nC\r\n", crlf.ToMarkdown(), "boundary uses the document newline");

            // Dialogue without a final newline followed by an inserted block at the end.
            var dialogue = PodcastDocument.Parse("[00:07] **GUMI:** Hi.");
            dialogue.AddBlock(new PodcastAudibleEventBlock { TimeMs = 8000, Description = "laughs" });
            Assert.AreEqual("[00:07] **GUMI:** Hi.\n[00:08] [laughs]\n", dialogue.ToMarkdown());
        }

        [TestMethod]
        public void Structural_Move_Final_Block_Without_Newline_Into_Middle_Is_Separated()
        {
            var doc = PodcastDocument.Parse("[00:07] **GUMI:** One.\n[00:08] **MOMO:** Two.\n[00:09] **KANO:** Three.");

            doc.MoveBlock(2, 0);

            // The moved block gains a boundary; the block that is now last keeps its own trailing newline.
            Assert.AreEqual("[00:09] **KANO:** Three.\n[00:07] **GUMI:** One.\n[00:08] **MOMO:** Two.\n", doc.ToMarkdown());
            Assert.IsFalse(doc.Blocks.Any(b => b.IsModified), "no block was regenerated to achieve this");

            // Move it to the middle instead (consecutive prose lines would merge into one raw block, so use events).
            var doc2 = PodcastDocument.Parse("[00:01] [a]\n[00:02] [b]\n[00:03] [c]");
            doc2.MoveBlock(2, 1);
            Assert.AreEqual("[00:01] [a]\n[00:03] [c]\n[00:02] [b]\n", doc2.ToMarkdown());

            // Moving back to the end restores the original no-final-newline shape.
            doc2.MoveBlock(1, 2);
            Assert.AreEqual("[00:01] [a]\n[00:02] [b]\n[00:03] [c]", doc2.ToMarkdown());
        }

        [TestMethod]
        public void Structural_New_Final_Block_Keeps_Its_Own_Final_Newline_State()
        {
            // Removing the last block (which had no newline) makes an earlier block final; it keeps its newline.
            var doc = PodcastDocument.Parse("[00:01] [a]\n[00:02] [b]");
            doc.RemoveBlockAt(1);
            Assert.AreEqual("[00:01] [a]\n", doc.ToMarkdown());

            // Moving a no-newline block to the front and a terminated block to the end.
            var doc2 = PodcastDocument.Parse("[00:01] [a]\r\n[00:02] [b]");
            doc2.MoveBlock(1, 0);
            Assert.AreEqual("[00:02] [b]\r\n[00:01] [a]\r\n", doc2.ToMarkdown());

            // A block created in code is always terminated with the document newline, even when final.
            var doc3 = PodcastDocument.Parse("A");
            doc3.InsertBlock(0, new PodcastRawMarkdownBlock { Text = "Z" });
            Assert.AreEqual("Z\nA", doc3.ToMarkdown(), "original final block still has no final newline");
        }

        [TestMethod]
        public void NewLine_And_Bom_Are_Parsed_Characteristics_Not_Editable_State()
        {
            var fresh = new PodcastDocument();
            Assert.AreEqual("\n", fresh.NewLine);
            Assert.IsFalse(fresh.HasByteOrderMark);
            Assert.IsFalse(fresh.IsModified);

            var crlf = PodcastDocument.Parse("\uFEFFA\r\nB\r\n");
            Assert.AreEqual("\r\n", crlf.NewLine);
            Assert.IsTrue(crlf.HasByteOrderMark);
            Assert.IsFalse(crlf.IsModified);

            // The setters are not public: the only way to change these is the parser / future file layer.
            Assert.IsNull(typeof(PodcastDocument).GetProperty("NewLine").GetSetMethod());
            Assert.IsNull(typeof(PodcastDocument).GetProperty("HasByteOrderMark").GetSetMethod());
        }

        private static int DateIssues(string date)
        {
            return PodcastDocument.Parse("---\nrun: fmyoko\nepisode: 1\ndate: " + date + "\n---\n").ValidateCanonical().Count;
        }

        private static int YouTubeIdIssues(string id)
        {
            return PodcastDocument.Parse("---\nrun: fmyoko\nepisode: 1\ndate: 2026-01-01\nyoutube_id: " + id + "\n---\n").ValidateCanonical().Count;
        }

        private static void AssertThrowsArgumentOutOfRange(System.Action action, string message)
        {
            AssertThrows<System.ArgumentOutOfRangeException>(action, message);
        }

        private static void AssertThrows<T>(System.Action action, string message) where T : System.Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            Assert.Fail("Expected " + typeof(T).Name + ": " + message);
        }

        private static string SerializeRoundTrip(string text)
        {
            return PodcastDocument.Parse(text).ToMarkdown();
        }
    }
}
