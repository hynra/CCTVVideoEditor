using CCTVVideoEditor.Models;
using CCTVVideoEditor.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace CCTVVideoEditor.Views
{
    public sealed partial class MainPage : Page
    {
        private MainViewModel _viewModel;

        // Track if we're currently seeking to avoid feedback loops
        private bool _isSeeking = false;

        public MainViewModel ViewModel => _viewModel;

        public MainPage()
        {
            // Create view model
            _viewModel = new MainViewModel();

            this.InitializeComponent();

            // Connect video player to media player from view model
            VideoPlayer.SetMediaPlayer(_viewModel.MediaPlayer);

            // Set up timeline events
            Timeline.TimeSelected += Timeline_TimeSelected;
            Timeline.SegmentSelected += Timeline_SegmentSelected;
            Timeline.RangeSelected += Timeline_RangeSelected;

            // Set data context for bindings
            this.DataContext = _viewModel;

            // Set up unloaded event to clean up resources
            this.Unloaded += Page_Unloaded;
        }

        private async void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            bool success = await _viewModel.LoadVideosFromFolderAsync();

            if (success)
            {
                // Update timeline with data
                Timeline.TimelineData = _viewModel.TimelineData;
                Timeline.CurrentSegment = _viewModel.CurrentSegment;
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            // Export functionality will be implemented in Generation 3
            _viewModel.StatusMessage = "Export functionality will be available in Generation 3";

            // Show selection details in status
            var (start, end) = _viewModel.GetSelectionRange();
            int segmentCount = _viewModel.GetSegmentsInSelectionRange().Count;
            double duration = _viewModel.GetTotalDurationInSelection();
            bool hasGaps = _viewModel.HasGapsInSelection();

            ContentDialog dialog = new ContentDialog
            {
                Title = "Export Preview",
                Content = $"Time Range: {start:HH:mm:ss} - {end:HH:mm:ss}\n" +
                          $"Video segments: {segmentCount}\n" +
                          $"Total duration: {TimeSpan.FromSeconds(duration):hh\\:mm\\:ss}\n" +
                          $"Contains gaps: {(hasGaps ? "Yes" : "No")}",
                PrimaryButtonText = "OK",
                DefaultButton = ContentDialogButton.Primary
            };

            // Show dialog
            dialog.XamlRoot = this.XamlRoot;
            _ = dialog.ShowAsync();
        }

        private async void Timeline_TimeSelected(object sender, DateTime e)
        {
            // Check if we're already seeking to avoid feedback loops
            if (!_isSeeking)
            {
                try
                {
                    _isSeeking = true;

                    // Seek to the selected time
                    await _viewModel.SeekToTimeAsync(e);

                    // If there's a segment at this time, update the timeline's current segment
                    var segment = _viewModel.TimelineData?.GetSegmentAtTime(e);
                    if (segment != null)
                    {
                        Timeline.UpdateCurrentTime(e);
                    }
                    else
                    {
                        // No segment at this time - still update the timeline position
                        Timeline.UpdateCurrentTime(e);

                        // Set status message to indicate no footage
                        _viewModel.StatusMessage = $"No video footage at {e:HH:mm:ss}";
                    }
                }
                finally
                {
                    _isSeeking = false;
                }
            }
        }

        private async void Timeline_SegmentSelected(object sender, VideoSegment e)
        {
            // Check if we're already seeking to avoid feedback loops
            if (!_isSeeking)
            {
                try
                {
                    _isSeeking = true;

                    // Seek to the segment start time
                    await _viewModel.SeekToTimeAsync(e.StartTime);
                }
                finally
                {
                    _isSeeking = false;
                }
            }
        }

        private void Timeline_RangeSelected(object sender, (DateTime start, DateTime end) e)
        {
            _viewModel.SetSelectionRange(e.start, e.end);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            // Clean up resources
            _viewModel.Cleanup();
        }
    }
}