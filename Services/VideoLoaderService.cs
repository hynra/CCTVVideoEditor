using CCTVVideoEditor.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace CCTVVideoEditor.Services
{
    /// <summary>
    /// Service to load and manage CCTV video files
    /// </summary>
    public class VideoLoaderService
    {
        /// <summary>
        /// Load all video segments from the specified directory
        /// </summary>
        /// <param name="directoryPath">Path to directory containing CCTV videos</param>
        /// <returns>TimelineData containing all valid video segments</returns>
        public async Task<TimelineData> LoadVideosFromDirectoryAsync(string directoryPath)
        {
            var segments = new List<VideoSegment>();

            try
            {
                // Validate the directory exists
                if (!Directory.Exists(directoryPath))
                {
                    throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
                }

                // Get all mp4 files in the directory
                string[] videoFiles = Directory.GetFiles(directoryPath, "*.mp4");

                foreach (string filePath in videoFiles)
                {
                    // Try to parse the filename to extract the timestamp
                    var segment = VideoSegment.TryParseFromFilePath(filePath);

                    if (segment != null)
                    {
                        // For a complete implementation, get the actual duration from the media file
                        try
                        {
                            double actualDuration = await GetVideoDurationAsync(filePath);
                            segment.Duration = actualDuration;
                        }
                        catch
                        {
                            // If we can't get the actual duration, keep the default
                        }

                        segments.Add(segment);
                    }
                }

                // Group segments by date
                var groupedByDate = segments.GroupBy(s => s.StartTime.Date);

                // For simplicity in this POC, we just return the first date if there are multiple dates
                // In a real app, you might want to handle multiple days differently
                var firstDateGroup = groupedByDate.FirstOrDefault();

                if (firstDateGroup != null)
                {
                    return new TimelineData(firstDateGroup);
                }
                else
                {
                    // Return an empty timeline for today if no videos were found
                    return new TimelineData(DateTime.Today);
                }
            }
            catch (Exception ex)
            {
                // In a real app, you would log this exception and perhaps notify the user
                System.Diagnostics.Debug.WriteLine($"Error loading videos: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets the actual duration of a video file
        /// </summary>
        /// <param name="filePath">Path to the video file</param>
        /// <returns>Duration in seconds</returns>
        private async Task<double> GetVideoDurationAsync(string filePath)
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);

                // Create a MediaSource from the file
                MediaSource mediaSource = MediaSource.CreateFromStorageFile(file);

                // In WinUI 3, we can use MediaSource directly to get the duration
                // Wait for the media to be opened, which happens asynchronously
                var openOperation = mediaSource.OpenAsync();

                // Set a timeout to avoid hanging forever
                var timeoutTask = Task.Delay(5000);
                var completedTask = await Task.WhenAny(openOperation.AsTask(), timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // Timeout occurred
                    throw new TimeoutException("Timed out while opening media source");
                }

                // Check if the open operation was successful
                await openOperation;

                // Now we can access the duration
                if (mediaSource.Duration.HasValue)
                {
                    double duration = mediaSource.Duration.Value.TotalSeconds;

                    // Clean up
                    mediaSource.Dispose();

                    return duration;
                }
                else
                {
                    // If duration is not available, use a default value
                    // Clean up
                    mediaSource.Dispose();

                    // Default to 5 minutes (300 seconds)
                    return 300.0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting video duration: {ex.Message}");
                // Return a default duration if there's an error
                return 300.0; // Default to 5 minutes (300 seconds)
            }
        }

        /// <summary>
        /// Check if a directory contains valid CCTV video files
        /// </summary>
        /// <param name="directoryPath">Directory to check</param>
        /// <returns>True if valid videos are found</returns>
        public bool ContainsValidVideos(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    return false;
                }

                string[] videoFiles = Directory.GetFiles(directoryPath, "*.mp4");

                // Check if at least one file has a valid format
                return videoFiles.Any(file => VideoSegment.TryParseFromFilePath(file) != null);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Counts the number of valid video files in a directory
        /// </summary>
        /// <param name="directoryPath">Directory to check</param>
        /// <returns>Count of valid video files</returns>
        public int CountValidVideos(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    return 0;
                }

                string[] videoFiles = Directory.GetFiles(directoryPath, "*.mp4");

                // Count only files with valid format
                return videoFiles.Count(file => VideoSegment.TryParseFromFilePath(file) != null);
            }
            catch
            {
                return 0;
            }
        }
    }
}