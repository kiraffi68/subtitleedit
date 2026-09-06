using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Nikse.SubtitleEdit.Core.PodcastTranslate
{
    /// <summary>
    /// Helpers for the canonical "[MM:SS]" synchronization timestamp used in Urunemu Markdown.
    /// The minute field has at least two digits and may exceed 59 (e.g. "125:03");
    /// there is never an hours field, milliseconds, end time or range.
    /// Canonical times are whole seconds: a stored millisecond value must be non-negative and
    /// divisible by 1000 (<see cref="IsCanonicalMilliseconds"/>); the UI is expected to round a
    /// waveform position to the nearest second before committing it.
    /// Times are stored as whole milliseconds and are never routed through
    /// Subtitle Edit's <see cref="Common.TimeCode"/> formatting, which would turn "61:03" into "01:01:03".
    /// </summary>
    public static class PodcastTimestamp
    {
        // ASCII digits only: "\d" would also accept Unicode digits such as U+0663 and then blow up numeric parsing.
        private static readonly Regex CanonicalRegex = new Regex(@"^([0-9]{2,}):([0-9][0-9])$", RegexOptions.Compiled);

        /// <summary>
        /// Largest millisecond value that still fits an int; anything above is rejected as pathological.
        /// </summary>
        public const int MaxMilliseconds = int.MaxValue / 1000 * 1000;

        /// <summary>
        /// Tries to parse a canonical "MM:SS" time (without brackets) into milliseconds.
        /// Rejects one-digit minutes, non-ASCII digits, seconds above 59, hours, fractions, ranges and values too large for an int.
        /// Never throws for malformed input.
        /// </summary>
        public static bool TryParse(string text, out int milliseconds)
        {
            milliseconds = 0;
            if (text == null)
            {
                return false;
            }

            var match = CanonicalRegex.Match(text);
            if (!match.Success)
            {
                return false;
            }

            // long.TryParse fails cleanly on digit strings that do not fit; the range check below
            // then guards the multiplication so a huge minute field can never wrap around.
            if (!long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
            {
                return false;
            }

            if (!int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds > 59)
            {
                return false;
            }

            if (minutes > MaxMilliseconds / 60000L)
            {
                return false;
            }

            var totalMilliseconds = (minutes * 60L + seconds) * 1000L;
            if (totalMilliseconds > MaxMilliseconds)
            {
                return false;
            }

            milliseconds = (int)totalMilliseconds;
            return true;
        }

        /// <summary>
        /// True when <paramref name="milliseconds"/> is a valid canonical time: non-negative and a whole number of seconds.
        /// </summary>
        public static bool IsCanonicalMilliseconds(int milliseconds)
        {
            return milliseconds >= 0 && milliseconds % 1000 == 0;
        }

        /// <summary>
        /// Throws when <paramref name="milliseconds"/> is not a canonical whole-second, non-negative time.
        /// </summary>
        public static void EnsureCanonicalMilliseconds(int milliseconds, string parameterName)
        {
            if (!IsCanonicalMilliseconds(milliseconds))
            {
                throw new ArgumentOutOfRangeException(parameterName, milliseconds, "Podcast timestamps must be non-negative whole seconds (milliseconds divisible by 1000)");
            }
        }

        /// <summary>
        /// Formats milliseconds as canonical "MM:SS" (minutes padded to at least two digits, never wrapped into hours).
        /// Throws for negative values or values that are not a whole number of seconds; nothing is silently truncated.
        /// </summary>
        public static string Format(int milliseconds)
        {
            EnsureCanonicalMilliseconds(milliseconds, nameof(milliseconds));

            var totalSeconds = milliseconds / 1000;
            var minutes = totalSeconds / 60;
            var seconds = totalSeconds % 60;
            return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + seconds.ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats milliseconds as a bracketed synchronization timestamp, e.g. "[61:03]".
        /// </summary>
        public static string FormatBracketed(int milliseconds)
        {
            return "[" + Format(milliseconds) + "]";
        }
    }
}
