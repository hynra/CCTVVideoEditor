using CCTVVideoEditor.Models;
using CCTVVideoEditor.Services;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace CCTVVideoEditor.ViewModels
{
    /// <summary>
    /// ViewModel for the main page
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly VideoLoaderService _videoLoaderService;

        private TimelineData _timelineData;
        private VideoSegment _currentSegment;
        private string _statusMessage = "Ready";
        private bool _isVideoLoaded = false;
        private bool _isLoading = false;

        /// <summary>
        /// Event that fires when a property changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// The current timeline data
        /// </summary>
        public TimelineData TimelineData
        {
            get => _timelineData;
            private set => SetProperty(ref _timelineData, value);
        }

        /// <summary>
        /// The currently selected video segment
        /// </summary>
        public VideoSegment CurrentSegment
        {
            get => _currentSegment;
            private set => SetProperty(ref _currentSegment, value);
        }

        /// <summary>
        /// Status message to display
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Whether a video is loaded
        /// </summary>
        public bool IsVideoLoaded
        {
            get => _isVideoLoaded;
            private set => SetProperty(ref _isVideoLoaded, value);
        }

        /// <summary>
        /// Whether the app is currently loading data
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Creates a new MainViewModel
        /// </summary>
        public MainViewModel()
        {
            _videoLoaderService = new VideoLoaderService();
        }

        /// <summary>
        /// Loads videos from a selected folder
        /// </summary>
        public async Task<bool> LoadVideosFromFolderAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Selecting folder...";

                // Show folder picker
                FolderPicker folderPicker = new FolderPicker();
                folderPicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
                folderPicker.FileTypeFilter.Add("*");

                // WinUI 3 requires setting the window handle for pickers
                var window = new Window();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

                StorageFolder folder = await folderPicker.PickSingleFolderAsync();

                if (folder != null)
                {
                    StatusMessage = "Loading videos...";

                    // Load videos from the selected folder
                    TimelineData = await _videoLoaderService.LoadVideosFromDirectoryAsync(folder.Path);

                    // Check if any videos were found
                    if (TimelineData.SegmentCount > 0)
                    {
                        // Get first segment
                        var firstSegment = TimelineData.GetAllSegments()[0];

                        // Set current segment
                        CurrentSegment = firstSegment;

                        // Mark video as loaded
                        IsVideoLoaded = true;

                        // Show success status
                        StatusMessage = $"Loaded {TimelineData.SegmentCount} videos";
                        return true;
                    }
                    else
                    {
                        // No videos found
                        StatusMessage = "No valid CCTV videos found in the selected folder";
                        IsVideoLoaded = false;
                        return false;
                    }
                }
                else
                {
                    // User cancelled
                    StatusMessage = "Folder selection cancelled";
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Show error status
                StatusMessage = $"Error: {ex.Message}";
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Gets the next video segment
        /// </summary>
        public VideoSegment GetNextSegment()
        {
            if (TimelineData == null || CurrentSegment == null)
                return null;

            var nextSegment = TimelineData.GetNextSegment(CurrentSegment);

            if (nextSegment != null)
            {
                CurrentSegment = nextSegment;
                return nextSegment;
            }
            else
            {
                StatusMessage = "This is the last video segment";
                return null;
            }
        }

        /// <summary>
        /// Gets the previous video segment
        /// </summary>
        public VideoSegment GetPreviousSegment()
        {
            if (TimelineData == null || CurrentSegment == null)
                return null;

            var prevSegment = TimelineData.GetPreviousSegment(CurrentSegment);

            if (prevSegment != null)
            {
                CurrentSegment = prevSegment;
                return prevSegment;
            }
            else
            {
                StatusMessage = "This is the first video segment";
                return null;
            }
        }

        /// <summary>
        /// Gets all video segments
        /// </summary>
        public IReadOnlyList<VideoSegment> GetAllSegments()
        {
            return TimelineData?.GetAllSegments();
        }

        /// <summary>
        /// Gets segments in a time range
        /// </summary>
        public List<VideoSegment> GetSegmentsInRange(DateTime startTime, DateTime endTime)
        {
            return TimelineData?.GetSegmentsInRange(startTime, endTime);
        }

        /// <summary>
        /// Finds gaps in the timeline
        /// </summary>
        public List<(DateTime start, DateTime end)> FindGaps()
        {
            return TimelineData?.FindGaps();
        }

        /// <summary>
        /// Sets a property and notifies property changed
        /// </summary>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;
            NotifyPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Notifies property changed
        /// </summary>
        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}