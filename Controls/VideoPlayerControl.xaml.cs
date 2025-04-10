using CCTVVideoEditor.Models;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CCTVVideoEditor.Controls
{
    public sealed partial class VideoPlayerControl : UserControl
    {
        // Media player for video playback
        private MediaPlayer _mediaPlayer;

        // Currently loaded video segment
        private VideoSegment _currentSegment;

        // Timer for updating timestamp display
        private DispatcherTimer _timestampTimer;

        // Event handlers
        public event EventHandler PlaybackEnded;
        public event EventHandler<VideoSegment> RequestNextSegment;
        public event EventHandler<VideoSegment> RequestPreviousSegment;

        public VideoPlayerControl()
        {
            this.InitializeComponent();
            InitializePlayer();
            InitializeTimestampTimer();
        }

        private void InitializePlayer()
        {
            // Create media player
            _mediaPlayer = new MediaPlayer();

            // Connect to MediaPlayerElement
            MediaPlayer.SetMediaPlayer(_mediaPlayer);

            // Handle media ended event
            _mediaPlayer.MediaEnded += (sender, args) =>
            {
                // Notify subscribers that playback has ended
                PlaybackEnded?.Invoke(this, EventArgs.Empty);
            };

            // Handle playback state changes to update UI
            _mediaPlayer.PlaybackSession.PlaybackStateChanged += (sender, args) =>
            {
                // Update UI based on playback state
                var playbackState = _mediaPlayer.PlaybackSession.PlaybackState;
                DispatcherQueue.TryEnqueue(() =>
                {
                    switch (playbackState)
                    {
                        case MediaPlaybackState.Playing:
                            PlayPauseButton.Content = "\uE769"; // Pause icon
                            break;
                        case MediaPlaybackState.Paused:
                        case MediaPlaybackState.None:
                            PlayPauseButton.Content = "\uE768"; // Play icon
                            break;
                    }
                });
            };
        }

        private void InitializeTimestampTimer()
        {
            // Create timer to update timestamp overlay
            _timestampTimer = new DispatcherTimer();
            _timestampTimer.Interval = TimeSpan.FromSeconds(1);
            _timestampTimer.Tick += (sender, args) =>
            {
                UpdateTimestampOverlay();
            };
        }

        public async Task LoadVideoAsync(VideoSegment segment)
        {
            if (segment == null || !segment.IsAvailable)
                return;

            try
            {
                // Store current segment
                _currentSegment = segment;

                // Stop current playback
                _mediaPlayer.Pause();

                // Load the video file
                StorageFile file = await StorageFile.GetFileFromPathAsync(segment.FilePath);

                // Create media source
                MediaSource mediaSource = MediaSource.CreateFromStorageFile(file);

                // Set media source
                _mediaPlayer.Source = mediaSource;

                // Update timestamp
                UpdateTimestampOverlay();

                // Start playback
                _mediaPlayer.Play();

                // Start timer to update timestamp
                _timestampTimer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading video: {ex.Message}");
                // In a real app, show an error message to the user
            }
        }

        private void UpdateTimestampOverlay()
        {
            if (_currentSegment == null || _mediaPlayer.PlaybackSession == null)
                return;

            try
            {
                // Get current playback position
                var position = _mediaPlayer.PlaybackSession.Position;

                // Calculate current timestamp based on segment start time plus playback position
                var currentTime = _currentSegment.StartTime.Add(position);

                // Update UI on UI thread
                DispatcherQueue.TryEnqueue(() =>
                {
                    TimestampOverlay.Text = currentTime.ToString("yyyy-MM-dd HH:mm:ss");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating timestamp: {ex.Message}");
            }
        }

        public void PlayPause()
        {
            if (_mediaPlayer == null)
                return;

            if (_mediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
            {
                _mediaPlayer.Pause();
                _timestampTimer.Stop();
            }
            else
            {
                _mediaPlayer.Play();
                _timestampTimer.Start();
            }
        }

        public void Stop()
        {
            if (_mediaPlayer == null)
                return;

            _mediaPlayer.Pause();
            _mediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
            _timestampTimer.Stop();

            // Update timestamp to show start of video
            UpdateTimestampOverlay();
        }

        public void SetMediaPlayer(Windows.Media.Playback.MediaPlayer mediaPlayer)
        {
            if (mediaPlayer != null)
            {
                _mediaPlayer = mediaPlayer;
                MediaPlayer.SetMediaPlayer(_mediaPlayer);
            }
        }

        // Event handlers for the playback control buttons
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            PlayPause();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            // Request previous segment
            RequestPreviousSegment?.Invoke(this, _currentSegment);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            // Request next segment
            RequestNextSegment?.Invoke(this, _currentSegment);
        }

        public void Cleanup()
        {
            _timestampTimer?.Stop();

            if (_mediaPlayer != null)
            {
                _mediaPlayer.Pause();
                _mediaPlayer.Source = null;
                _mediaPlayer.Dispose();
                _mediaPlayer = null;
            }
        }
    }
}