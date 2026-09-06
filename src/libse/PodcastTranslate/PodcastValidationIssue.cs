namespace Nikse.SubtitleEdit.Core.PodcastTranslate
{
    public enum PodcastIssueSeverity
    {
        Warning,
        Error,
    }

    public enum PodcastIssueCode
    {
        InvalidTimestamp,
        InvalidSectionTime,
        UnrecognizedTimedLine,
        TimestampDecreasing,
        EmptySpeaker,
        FrontMatterMissing,
        FrontMatterMissingKey,
        FrontMatterUnknownKey,
        FrontMatterDuplicateKey,
        FrontMatterMalformedLine,
        FrontMatterInvalidValue,
        FrontMatterUnterminated,
    }

    public class PodcastValidationIssue
    {
        public PodcastValidationIssue(PodcastIssueSeverity severity, PodcastIssueCode code, int lineNumber, string message)
        {
            Severity = severity;
            Code = code;
            LineNumber = lineNumber;
            Message = message;
        }

        public PodcastIssueSeverity Severity { get; }

        public PodcastIssueCode Code { get; }

        /// <summary>
        /// 1-based source line number, or 0 if not tied to a line.
        /// </summary>
        public int LineNumber { get; }

        public string Message { get; }

        public override string ToString()
        {
            return Severity + " (line " + LineNumber + "): " + Message;
        }
    }
}
