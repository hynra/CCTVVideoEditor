using CCTVVideoEditor.Models;
using CCTVVideoEditor.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace CCTVVideoEditor.Views
{
    /// <summary>
    /// Main page of the application with timeline support
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MainViewModel _viewModel;

        /// <summary>
        /// Gets the view model
        /// </summary>
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
        }

        /// <summary>
        /// Handles click on the Open Folder button
        /// </summary>
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

        /// <summary>
        /// Handles click on the Export button
        /// </summary>
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

        /// <summary>
        /// Handles time selection in timeline
        /// </summary>
        private async void Timeline_TimeSelected(object sender, DateTime e)
        {
            await _viewModel.SeekToTimeAsync(e);
        }

        /// <summary>
        /// Handles segment selection in timeline
        /// </summary>
        private async void Timeline_SegmentSelected(object sender, VideoSegment e)
        {
            await _viewModel.SeekToTimeAsync(e.StartTime);
        }

        /// <summary>
        /// Handles range selection in timeline
        /// </summary>
        private void Timeline_RangeSelected(object sender, (DateTime start, DateTime end) e)
        {
            _viewModel.SetSelectionRange(e.start, e.end);
        }

        /// <summary>
        /// Page cleanup
        /// </summary>
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            // Clean up resources
            _viewModel.Cleanup();
        }
    }
}