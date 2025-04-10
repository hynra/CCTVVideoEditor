using CCTVVideoEditor.Helpers;
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
    /// ViewModel for the main page with timeline support
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        // Services
        private readonly VideoLoaderService _videoLoaderService;
        private readonly TimelineService _timelineService;
        private readonly PlaybackService _playbackService;

        // State
        private TimelineData _timelineData;
        private VideoSegment _currentSegment;
        private string _statusMessage = "Ready";
        private string _loadingMessage = "Loading...";
        private bool _isVideoLoaded = false;
        private bool _isLoading = false;
        private bool _isSelectionActive = false;
        private DateTime _selectionStartTime;
        private DateTime _selectionEndTime;
        private DateTime _currentTime;
        private string _currentTimeText = "--:--:--";
        private string _currentSegmentInfo = "No video loaded";

        /// <summary>
        /// Event that fires when a property changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #region Properties

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
            private set
            {
                if (SetProperty(ref _currentSegment, value))
                {
                    // Update segment info
                    CurrentSegmentInfo = value != null ?
                        $"{value.StartTime:HH:mm:ss} - {value.Duration / 60:F1} min" :
                        "No video loaded";
                }
            }
        }

        /// <summary>
        /// Status message to display
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Loading message to display
        /// </summary>
        public string LoadingMessage
        {
            get => _loadingMessage;
            private set => SetProperty(ref _loadingMessage, value);
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
        /// Whether the empty state should be visible
        /// </summary>
        public bool IsEmptyStateVisible => !IsVideoLoaded && !IsLoading;

        /// <summary>
        /// Whether a time range is selected
        /// </summary>
        public bool IsSelectionActive
        {
            get => _isSelectionActive;
            private set => SetProperty(ref _isSelectionActive, value);
        }

        /// <summary>
        /// Selection start time
        /// </summary>
        public DateTime SelectionStartTime
        {
            get => _selectionStartTime;
            private set => SetProperty(ref _selectionStartTime, value);
        }

        /// <summary>
        /// Selection end time
        /// </summary>
        public DateTime SelectionEndTime
        {
            get => _selectionEndTime;
            private set => SetProperty(ref _selectionEndTime, value);
        }

        /// <summary>
        /// Current playback time
        /// </summary>
        public DateTime CurrentTime
        {
            get => _currentTime;
            private set
            {
                if (SetProperty(ref _currentTime, value))
                {
                    // Update time text
                    CurrentTimeText = TimeHelper.FormatTimestamp(value, false);
                }
            }
        }

        /// <summary>
        /// Current time as text
        /// </summary>
        public string CurrentTimeText
        {
            get => _currentTimeText;
            private set => SetProperty(ref _currentTimeText, value);
        }

        /// <summary>
        /// Current segment info
        /// </summary>
        public string CurrentSegmentInfo
        {
            get => _currentSegmentInfo;
            private set => SetProperty(ref _currentSegmentInfo, value);
        }

        /// <summary>
        /// Gets the media player for binding
        /// </summary>
        public Windows.Media.Playback.MediaPlayer MediaPlayer => _playbackService?.MediaPlayer;

        #endregion

        /// <summary>
        /// Creates a new MainViewModel
        /// </summary>
        public MainViewModel()
        {
            _videoLoaderService = new VideoLoaderService();
            _timelineService = new TimelineService();
            _playbackService = new PlaybackService(_timelineService);

            // Set up event handlers
            _timelineService.CurrentSegmentChanged += TimelineService_CurrentSegmentChanged;
            _timelineService.CurrentPositionChanged += TimelineService_CurrentPositionChanged;

            _playbackService.PlaybackStarted += PlaybackService_PlaybackStarted;
            _playbackService.PlaybackEnded += PlaybackService_PlaybackEnded;
            _playbackService.SegmentChanged += PlaybackService_SegmentChanged;
            _playbackService.PositionChanged += PlaybackService_PositionChanged;
            _playbackService.PlaybackError += PlaybackService_PlaybackError;
        }

        #region Public Methods

        /// <summary>
        /// Loads videos from a selected folder
        /// </summary>
        public async Task<bool> LoadVideosFromFolderAsync()
        {
            try
            {
                IsLoading = true;
                LoadingMessage = "Selecting folder...";
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
                    LoadingMessage = "Loading videos...";
                    StatusMessage = "Loading videos...";

                    // Load videos from the selected folder
                    TimelineData = await _videoLoaderService.LoadVideosFromDirectoryAsync(folder.Path);

                    // Check if any videos were found
                    if (TimelineData.SegmentCount > 0)
                    {
                        // Initialize timeline service
                        _timelineService.Initialize(TimelineData);

                        // Get first segment
                        var firstSegment = TimelineData.GetAllSegments()[0];

                        // Play the first video
                        await _playbackService.PlaySegmentAsync(firstSegment);

                        // Mark video as loaded
                        IsVideoLoaded = true;

                        // Clear selection
                        ClearSelection();

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
        /// Plays or pauses the current video
        /// </summary>
        public void PlayPause()
        {
            _playbackService.PlayPause();
        }

        /// <summary>
        /// Stops playback
        /// </summary>
        public void Stop()
        {
            _playbackService.Stop();
        }

        /// <summary>
        /// Moves to the next segment
        /// </summary>
        /// <returns>Task</returns>
        public async Task MoveToNextSegmentAsync()
        {
            await _playbackService.MoveToNextSegmentAsync();
        }

        /// <summary>
        /// Moves to the previous segment
        /// </summary>
        /// <returns>Task</returns>
        public async Task MoveToPreviousSegmentAsync()
        {
            await _playbackService.MoveToPreviousSegmentAsync();
        }

        /// <summary>
        /// Seek to a specific time in the timeline
        /// </summary>
        /// <param name="time">Time to seek to</param>
        /// <returns>Task</returns>
        public async Task SeekToTimeAsync(DateTime time)
        {
            await _playbackService.SeekToTimeAsync(time);
        }

        /// <summary>
        /// Sets the selection range
        /// </summary>
        /// <param name="startTime">Start time</param>
        /// <param name="endTime">End time</param>
        public void SetSelectionRange(DateTime startTime, DateTime endTime)
        {
            if (startTime < endTime)
            {
                SelectionStartTime = startTime;
                SelectionEndTime = endTime;
                IsSelectionActive = true;

                StatusMessage = $"Selected: {TimeHelper.FormatTimestamp(startTime)} - {TimeHelper.FormatTimestamp(endTime)}";
            }
        }

        /// <summary>
        /// Clears the current selection
        /// </summary>
        public void ClearSelection()
        {
            IsSelectionActive = false;
            SelectionStartTime = DateTime.MinValue;
            SelectionEndTime = DateTime.MinValue;

            StatusMessage = "Selection cleared";
        }

        /// <summary>
        /// Gets the selected time range
        /// </summary>
        /// <returns>Tuple of start and end times</returns>
        public (DateTime start, DateTime end) GetSelectionRange()
        {
            return (SelectionStartTime, SelectionEndTime);
        }

        /// <summary>
        /// Gets segments in the selected range
        /// </summary>
        /// <returns>List of segments</returns>
        public List<VideoSegment> GetSegmentsInSelectionRange()
        {
            if (!IsSelectionActive || TimelineData == null)
                return new List<VideoSegment>();

            return TimelineData.GetSegmentsInRange(SelectionStartTime, SelectionEndTime);
        }

        /// <summary>
        /// Gets the total duration of video in the selection
        /// </summary>
        /// <returns>Duration in seconds</returns>
        public double GetTotalDurationInSelection()
        {
            if (!IsSelectionActive || _timelineService == null)
                return 0;

            return _timelineService.GetTotalDurationInRange(SelectionStartTime, SelectionEndTime);
        }

        /// <summary>
        /// Checks if there are gaps in the selection
        /// </summary>
        /// <returns>True if gaps exist</returns>
        public bool HasGapsInSelection()
        {
            if (!IsSelectionActive || _timelineService == null)
                return false;

            var gaps = _timelineService.FindGapsInRange(SelectionStartTime, SelectionEndTime);
            return gaps.Count > 0;
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Cleanup()
        {
            _playbackService.Cleanup();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Sets a property and notifies property changed
        /// </summary>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;
            NotifyPropertyChanged(propertyName);

            // Handle special cases
            if (propertyName == nameof(IsVideoLoaded) || propertyName == nameof(IsLoading))
            {
                NotifyPropertyChanged(nameof(IsEmptyStateVisible));
            }

            return true;
        }

        /// <summary>
        /// Notifies property changed
        /// </summary>
        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Event Handlers

        private void TimelineService_CurrentSegmentChanged(object sender, VideoSegment e)
        {
            CurrentSegment = e;
        }

        private void TimelineService_CurrentPositionChanged(object sender, DateTime e)
        {
            CurrentTime = e;
        }

        private void PlaybackService_PlaybackStarted(object sender, VideoSegment e)
        {
            StatusMessage = $"Playing: {TimeHelper.FormatTimestamp(e.StartTime)}";
        }

        private void PlaybackService_PlaybackEnded(object sender, VideoSegment e)
        {
            StatusMessage = $"Playback ended: {TimeHelper.FormatTimestamp(e.EndTime)}";
        }

        private void PlaybackService_SegmentChanged(object sender, VideoSegment e)
        {
            // Already handled by TimelineService
        }

        private void PlaybackService_PositionChanged(object sender, DateTime e)
        {
            // Already handled by TimelineService
        }

        private void PlaybackService_PlaybackError(object sender, string e)
        {
            StatusMessage = $"Error: {e}";
        }

        #endregion
    }
}