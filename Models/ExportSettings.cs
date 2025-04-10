using System;

namespace CCTVVideoEditor.Models
{
    /// <summary>
    /// Settings for video export operation
    /// </summary>
    public class ExportSettings
    {
        /// <summary>
        /// Start time for the export range
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// End time for the export range
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Path where the exported video will be saved
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// Video quality setting (0-100)
        /// </summary>
        public int Quality { get; set; } = 80;

        /// <summary>
        /// Whether to fill gaps in video with blank frames
        /// </summary>
        public bool FillGaps { get; set; } = false;

        /// <summary>
        /// Whether to include timestamp overlay in the exported video
        /// </summary>
        public bool IncludeTimestamp { get; set; } = true;

        /// <summary>
        /// Creates default export settings
        /// </summary>
        public ExportSettings()
        {
            StartTime = DateTime.Now;
            EndTime = DateTime.Now.AddHours(1);
            OutputPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        }

        /// <summary>
        /// Creates export settings with specified time range
        /// </summary>
        /// <param name="startTime">Start time for export</param>
        /// <param name="endTime">End time for export</param>
        public ExportSettings(DateTime startTime, DateTime endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
            OutputPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        }

        /// <summary>
        /// Gets the total duration of the export in seconds
        /// </summary>
        public double GetDurationInSeconds()
        {
            return (EndTime - StartTime).TotalSeconds;
        }

        /// <summary>
        /// Validates export settings
        /// </summary>
        /// <returns>True if settings are valid, false otherwise</returns>
        public bool Validate()
        {
            // Basic validation
            if (StartTime >= EndTime)
                return false;

            if (string.IsNullOrEmpty(OutputPath))
                return false;

            if (Quality < 0 || Quality > 100)
                return false;

            return true;
        }

        /// <summary>
        /// Generates the default output filename based on the time range
        /// </summary>
        /// <returns>Suggested filename for the export</returns>
        public string GetDefaultOutputFilename()
        {
            return $"CCTV_Export_{StartTime:yyyy-MM-dd_HH-mm-ss}_to_{EndTime:HH-mm-ss}.mp4";
        }
    }
}