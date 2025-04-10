using System;
using System.Globalization;

namespace CCTVVideoEditor.Helpers
{
    /// <summary>
    /// Helper class for time-related operations
    /// </summary>
    public static class TimeHelper
    {
        /// <summary>
        /// Formats a timestamp for display
        /// </summary>
        /// <param name="time">Time to format</param>
        /// <param name="includeDate">Whether to include the date</param>
        /// <returns>Formatted timestamp</returns>
        public static string FormatTimestamp(DateTime time, bool includeDate = true)
        {
            return includeDate ?
                time.ToString("yyyy-MM-dd HH:mm:ss") :
                time.ToString("HH:mm:ss");
        }

        /// <summary>
        /// Formats a time span for display
        /// </summary>
        /// <param name="duration">Duration to format</param>
        /// <returns>Formatted duration</returns>
        public static string FormatDuration(TimeSpan duration)
        {
            return duration.TotalHours >= 1 ?
                duration.ToString(@"hh\:mm\:ss") :
                duration.ToString(@"mm\:ss");
        }

        /// <summary>
        /// Formats a duration in seconds for display
        /// </summary>
        /// <param name="seconds">Duration in seconds</param>
        /// <returns>Formatted duration</returns>
        public static string FormatDurationFromSeconds(double seconds)
        {
            return FormatDuration(TimeSpan.FromSeconds(seconds));
        }

        /// <summary>
        /// Rounds a DateTime to the nearest second
        /// </summary>
        /// <param name="time">Time to round</param>
        /// <returns>Rounded time</returns>
        public static DateTime RoundToSecond(DateTime time)
        {
            return new DateTime(time.Year, time.Month, time.Day,
                               time.Hour, time.Minute, time.Second,
                               0, time.Kind);
        }

        /// <summary>
        /// Rounds a DateTime to the nearest minute
        /// </summary>
        /// <param name="time">Time to round</param>
        /// <returns>Rounded time</returns>
        public static DateTime RoundToMinute(DateTime time)
        {
            int seconds = time.Second;
            if (seconds >= 30)
            {
                return new DateTime(time.Year, time.Month, time.Day,
                                  time.Hour, time.Minute, 0,
                                  0, time.Kind).AddMinutes(1);
            }
            else
            {
                return new DateTime(time.Year, time.Month, time.Day,
                                  time.Hour, time.Minute, 0,
                                  0, time.Kind);
            }
        }

        /// <summary>
        /// Checks if a time is within a range
        /// </summary>
        /// <param name="time">Time to check</param>
        /// <param name="startTime">Range start</param>
        /// <param name="endTime">Range end</param>
        /// <returns>True if time is in range</returns>
        public static bool IsTimeInRange(DateTime time, DateTime startTime, DateTime endTime)
        {
            return time >= startTime && time <= endTime;
        }

        /// <summary>
        /// Gets the time overlap between two ranges
        /// </summary>
        /// <param name="start1">First range start</param>
        /// <param name="end1">First range end</param>
        /// <param name="start2">Second range start</param>
        /// <param name="end2">Second range end</param>
        /// <returns>Tuple with overlap start and end, or null if no overlap</returns>
        public static (DateTime overlapStart, DateTime overlapEnd)? GetTimeOverlap(
            DateTime start1, DateTime end1, DateTime start2, DateTime end2)
        {
            // Check if ranges overlap
            if (end1 < start2 || end2 < start1)
            {
                return null;
            }

            // Calculate overlap
            DateTime overlapStart = start1 > start2 ? start1 : start2;
            DateTime overlapEnd = end1 < end2 ? end1 : end2;

            return (overlapStart, overlapEnd);
        }

        /// <summary>
        /// Parses a timestamp from the filename format
        /// </summary>
        /// <param name="filename">Filename with timestamp (2024-12-18_13-54-23.mp4)</param>
        /// <returns>Parsed timestamp or null if invalid</returns>
        public static DateTime? ParseTimestampFromFilename(string filename)
        {
            try
            {
                // Extract timestamp part
                string timestampPart = System.IO.Path.GetFileNameWithoutExtension(filename);

                // Split date and time
                string[] parts = timestampPart.Split('_');
                if (parts.Length != 2)
                {
                    return null;
                }

                string datePart = parts[0];
                string timePart = parts[1].Replace('-', ':');

                // Parse date and time
                return DateTime.ParseExact(
                    $"{datePart} {timePart}",
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Converts a timestamp to a filename
        /// </summary>
        /// <param name="time">Time to convert</param>
        /// <returns>Filename in format 2024-12-18_13-54-23</returns>
        public static string TimestampToFilename(DateTime time)
        {
            string datePart = time.ToString("yyyy-MM-dd");
            string timePart = time.ToString("HH-mm-ss");

            return $"{datePart}_{timePart}";
        }

        /// <summary>
        /// Gets readable time representation
        /// </summary>
        /// <param name="time">Time to format</param>
        /// <returns>Readable time (Today at 14:30, Yesterday at 09:15, etc.)</returns>
        public static string GetReadableTime(DateTime time)
        {
            DateTime today = DateTime.Today;
            DateTime yesterday = today.AddDays(-1);

            if (time.Date == today)
            {
                return $"Today at {time:HH:mm:ss}";
            }
            else if (time.Date == yesterday)
            {
                return $"Yesterday at {time:HH:mm:ss}";
            }
            else
            {
                return $"{time:yyyy-MM-dd} at {time:HH:mm:ss}";
            }
        }
    }
}