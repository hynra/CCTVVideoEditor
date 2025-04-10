using System;
using System.Globalization;

namespace CCTVVideoEditor.Helpers
{
    public static class TimeHelper
    {
        public static string FormatTimestamp(DateTime time, bool includeDate = true)
        {
            return includeDate ?
                time.ToString("yyyy-MM-dd HH:mm:ss") :
                time.ToString("HH:mm:ss");
        }

        public static string FormatDuration(TimeSpan duration)
        {
            return duration.TotalHours >= 1 ?
                duration.ToString(@"hh\:mm\:ss") :
                duration.ToString(@"mm\:ss");
        }
        public static string FormatDurationFromSeconds(double seconds)
        {
            return FormatDuration(TimeSpan.FromSeconds(seconds));
        }

        public static DateTime RoundToSecond(DateTime time)
        {
            return new DateTime(time.Year, time.Month, time.Day,
                               time.Hour, time.Minute, time.Second,
                               0, time.Kind);
        }

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

        public static bool IsTimeInRange(DateTime time, DateTime startTime, DateTime endTime)
        {
            return time >= startTime && time <= endTime;
        }

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

        public static string TimestampToFilename(DateTime time)
        {
            string datePart = time.ToString("yyyy-MM-dd");
            string timePart = time.ToString("HH-mm-ss");

            return $"{datePart}_{timePart}";
        }

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