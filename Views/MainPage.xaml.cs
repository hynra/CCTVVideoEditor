using CCTVVideoEditor.Models;
using CCTVVideoEditor.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace CCTVVideoEditor.Views
{
    /// <summary>
    /// Main page of the application
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private VideoLoaderService _videoLoaderService;
        private TimelineData _timelineData;
        private VideoSegment _currentSegment;

        public MainPage()
        {
            this.InitializeComponent();

            // Initialize services
            _videoLoaderService = new VideoLoaderService();

            // Set up event handlers
            VideoPlayer.RequestNextSegment += VideoPlayer_RequestNextSegment;
            VideoPlayer.RequestPreviousSegment += VideoPlayer_RequestPreviousSegment;
            VideoPlayer.PlaybackEnded += VideoPlayer_PlaybackEnded;
        }

        /// <summary>
        /// Handles click on the Open Folder button
        /// </summary>
        private async void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadVideosFromFolderAsync();
        }

        /// <summary>
        /// Handles click on the Export button
        /// </summary>
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            // Export functionality will be implemented in Generation 3
            ShowStatus("Export functionality will be available in Generation 3");
        }

        /// <summary>
        /// Load videos from a user-selected folder
        /// </summary>
        private async Task LoadVideosFromFolderAsync()
        {
            try
            {
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
                    // Show loading status
                    ShowStatus("Loading videos...");

                    // Load videos from the selected folder
                    _timelineData = await _videoLoaderService.LoadVideosFromDirectoryAsync(folder.Path);

                    // Check if any videos were found
                    if (_timelineData.SegmentCount > 0)
                    {
                        // Get first segment
                        var firstSegment = _timelineData.GetAllSegments()[0];

                        // Load the first video
                        await LoadVideoSegmentAsync(firstSegment);

                        // Enable export button
                        ExportButton.IsEnabled = true;

                        // Show success status
                        ShowStatus($"Loaded {_timelineData.SegmentCount} videos");
                    }
                    else
                    {
                        // No videos found
                        ShowStatus("No valid CCTV videos found in the selected folder");

                        // Show empty state
                        EmptyStatePanel.Visibility = Visibility.Visible;
                        VideoPlayer.Visibility = Visibility.Collapsed;

                        // Disable export button
                        ExportButton.IsEnabled = false;
                    }
                }
            }
            catch (Exception ex)
            {
                // Show error status
                ShowStatus($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Load and play a video segment
        /// </summary>
        private async Task LoadVideoSegmentAsync(VideoSegment segment)
        {
            if (segment == null || !segment.IsAvailable)
                return;

            try
            {
                // Store current segment
                _currentSegment = segment;

                // Hide empty state and show video player
                EmptyStatePanel.Visibility = Visibility.Collapsed;
                VideoPlayer.Visibility = Visibility.Visible;

                // Load the video
                await VideoPlayer.LoadVideoAsync(segment);

                // Update video info text
                VideoInfoTextBlock.Text = segment.GetDisplayName();
            }
            catch (Exception ex)
            {
                ShowStatus($"Error loading video: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a status message in the status bar
        /// </summary>
        private void ShowStatus(string message)
        {
            StatusTextBlock.Text = message;
        }

        #region Event Handlers

        /// <summary>
        /// Handles request to play the next video segment
        /// </summary>
        private async void VideoPlayer_RequestNextSegment(object sender, VideoSegment e)
        {
            if (_timelineData == null || _currentSegment == null)
                return;

            var nextSegment = _timelineData.GetNextSegment(_currentSegment);

            if (nextSegment != null)
            {
                await LoadVideoSegmentAsync(nextSegment);
            }
            else
            {
                ShowStatus("This is the last video segment");
            }
        }

        /// <summary>
        /// Handles request to play the previous video segment
        /// </summary>
        private async void VideoPlayer_RequestPreviousSegment(object sender, VideoSegment e)
        {
            if (_timelineData == null || _currentSegment == null)
                return;

            var prevSegment = _timelineData.GetPreviousSegment(_currentSegment);

            if (prevSegment != null)
            {
                await LoadVideoSegmentAsync(prevSegment);
            }
            else
            {
                ShowStatus("This is the first video segment");
            }
        }

        /// <summary>
        /// Handles playback ended event
        /// </summary>
        private async void VideoPlayer_PlaybackEnded(object sender, EventArgs e)
        {
            // Automatically play the next segment if available
            if (_timelineData != null && _currentSegment != null)
            {
                var nextSegment = _timelineData.GetNextSegment(_currentSegment);

                if (nextSegment != null)
                {
                    await LoadVideoSegmentAsync(nextSegment);
                }
                else
                {
                    ShowStatus("Playback completed");
                }
            }
        }

        #endregion
    }
}