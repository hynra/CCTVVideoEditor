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
            await _viewModel.SeekToTimeAsync(e);
        }

        private async void Timeline_SegmentSelected(object sender, VideoSegment e)
        {
            await _viewModel.SeekToTimeAsync(e.StartTime);
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