using System;

namespace CCTVVideoEditor.Models
{
    public class ExportSettings
    {

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public string OutputPath { get; set; }
        public int Quality { get; set; } = 80;

        public bool FillGaps { get; set; } = false;

        public bool IncludeTimestamp { get; set; } = true;

        public ExportSettings()
        {
            StartTime = DateTime.Now;
            EndTime = DateTime.Now.AddHours(1);
            OutputPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        }

        public ExportSettings(DateTime startTime, DateTime endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
            OutputPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        }

        public double GetDurationInSeconds()
        {
            return (EndTime - StartTime).TotalSeconds;
        }

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

        public string GetDefaultOutputFilename()
        {
            return $"CCTV_Export_{StartTime:yyyy-MM-dd_HH-mm-ss}_to_{EndTime:HH-mm-ss}.mp4";
        }
    }
}