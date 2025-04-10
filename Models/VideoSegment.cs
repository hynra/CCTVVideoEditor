using System;
using System.IO;
using System.Text.RegularExpressions;

namespace CCTVVideoEditor.Models
{
    /// <summary>
    /// Represents a single 5-minute CCTV video segment
    /// </summary>
    public class VideoSegment
    {
        /// <summary>
        /// Full path to the video file
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// The starting timestamp of the video segment
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Duration of the video segment in seconds
        /// </summary>
        public double Duration { get; set; }

        /// <summary>
        /// Whether the video segment exists and is available for playback
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// The end timestamp of the video segment (calculated from StartTime + Duration)
        /// </summary>
        public DateTime EndTime => StartTime.AddSeconds(Duration);

        /// <summary>
        /// File name without the directory path
        /// </summary>
        public string FileName => Path.GetFileName(FilePath);

        /// <summary>
        /// Creates a new VideoSegment
        /// </summary>
        /// <param name="filePath">Full path to the video file</param>
        /// <param name="startTime">Starting timestamp</param>
        /// <param name="duration">Duration in seconds</param>
        /// <param name="isAvailable">Whether the segment is available</param>
        public VideoSegment(string filePath, DateTime startTime, double duration, bool isAvailable)
        {
            FilePath = filePath;
            StartTime = startTime;
            Duration = duration;
            IsAvailable = isAvailable;
        }

        /// <summary>
        /// Attempts to parse a video file name to extract its timestamp
        /// Expected format: 2024-12-18_13-54-23.mp4
        /// </summary>
        /// <param name="filePath">Path to the video file</param>
        /// <returns>VideoSegment if parsing succeeds, null otherwise</returns>
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

        /// <summary>
        /// Checks if this segment contains the specified timestamp
        /// </summary>
        /// <param name="timestamp">Timestamp to check</param>
        /// <returns>True if the segment contains the timestamp</returns>
        public bool ContainsTime(DateTime timestamp)
        {
            return timestamp >= StartTime && timestamp < EndTime;
        }

        /// <summary>
        /// Gets a display name for this segment based on its start time
        /// </summary>
        /// <returns>Formatted display name</returns>
        public string GetDisplayName()
        {
            return $"{StartTime:yyyy-MM-dd HH:mm:ss} ({Duration / 60:F1} min)";
        }
    }
}