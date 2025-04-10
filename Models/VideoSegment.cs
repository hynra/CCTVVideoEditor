using System;
using System.IO;
using System.Text.RegularExpressions;

namespace CCTVVideoEditor.Models
{
    public class VideoSegment
    {
        public string FilePath { get; set; }

        public DateTime StartTime { get; set; }

        public double Duration { get; set; }

        public bool IsAvailable { get; set; }

        public DateTime EndTime => StartTime.AddSeconds(Duration);

        public string FileName => Path.GetFileName(FilePath);

        public VideoSegment(string filePath, DateTime startTime, double duration, bool isAvailable)
        {
            FilePath = filePath;
            StartTime = startTime;
            Duration = duration;
            IsAvailable = isAvailable;
        }

        public static VideoSegment TryParseFromFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            string fileName = Path.GetFileName(filePath);
            // Pattern for 2024-12-18_13-54-23.mp4
            var regex = new Regex(@"^(\d{4}-\d{2}-\d{2})_(\d{2})-(\d{2})-(\d{2})\.mp4$");
            var match = regex.Match(fileName);

            if (!match.Success)
            {
                return null;
            }

            try
            {
                string dateString = match.Groups[1].Value;
                string hourString = match.Groups[2].Value;
                string minuteString = match.Groups[3].Value;
                string secondString = match.Groups[4].Value;

                var startTime = DateTime.ParseExact(
                    $"{dateString} {hourString}:{minuteString}:{secondString}",
                    "yyyy-MM-dd HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture);

                // Assume standard 5-minute segments (300 seconds)
                // In a real application, you would get the actual duration from the video file
                const double defaultDuration = 300.0;

                return new VideoSegment(filePath, startTime, defaultDuration, true);
            }
            catch
            {
                return null;
            }
        }

        public bool ContainsTime(DateTime timestamp)
        {
            return timestamp >= StartTime && timestamp < EndTime;
        }

        public string GetDisplayName()
        {
            return $"{StartTime:yyyy-MM-dd HH:mm:ss} ({Duration / 60:F1} min)";
        }
    }
}