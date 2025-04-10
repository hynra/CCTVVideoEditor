﻿using CCTVVideoEditor.Models;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace CCTVVideoEditor.Services
{
    /// <summary>
    /// Service for handling video playback across segments
    /// </summary>
    public class PlaybackService
    {
        private readonly TimelineService _timelineService;
        private MediaPlayer _mediaPlayer;
        private VideoSegment _currentSegment;
        private VideoSegment _nextSegment;
        private MediaSource _nextMediaSource;
        private bool _isPreloadingNext;
        private bool _isTransitioning;
        private bool _continuousPlayback;

        // Events
        public event EventHandler<VideoSegment> PlaybackStarted;
        public event EventHandler<VideoSegment> PlaybackEnded;
        public event EventHandler<VideoSegment> SegmentChanged;
        public event EventHandler<DateTime> PositionChanged;
        public event EventHandler<string> PlaybackError;

        /// <summary>
        /// Gets the media player
        /// </summary>
        public MediaPlayer MediaPlayer => _mediaPlayer;

        /// <summary>
        /// Gets or sets whether playback should continue to next segment
        /// </summary>
        public bool ContinuousPlayback
        {
            get => _continuousPlayback;
            set => _continuousPlayback = value;
        }

        /// <summary>
        /// Creates a new PlaybackService
        /// </summary>
        /// <param name="timelineService">Timeline service</param>
        public PlaybackService(TimelineService timelineService)
        {
            _timelineService = timelineService;
            _continuousPlayback = true;
            _isPreloadingNext = false;
            _isTransitioning = false;

            InitializeMediaPlayer();
        }

        private void InitializeMediaPlayer()
        {
            // Create media player
            _mediaPlayer = new MediaPlayer();

            // Set up event handlers
            _mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
            _mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
            _mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;

            // Set up position tracking
            _mediaPlayer.PlaybackSession.PositionChanged += PlaybackSession_PositionChanged;
        }

        /// <summary>
        /// Loads and plays a video segment
        /// </summary>
        /// <param name="segment">Segment to play</param>
        /// <returns>Task</returns>
        public async Task PlaySegmentAsync(VideoSegment segment)
        {
            if (segment == null || !segment.IsAvailable)
            {
                PlaybackError?.Invoke(this, "Invalid or unavailable segment");
                return;
            }

            try
            {
                // Stop current playback
                _mediaPlayer.Pause();

                // Set current segment
                _currentSegment = segment;

                // Update timeline service
                _timelineService.SetPosition(segment.StartTime);

                // Load the media
                await LoadMediaSourceAsync(segment);

                // Start playback
                _mediaPlayer.Play();

                // Notify playback started
                PlaybackStarted?.Invoke(this, segment);

                // Preload next segment if continuous playback is enabled
                if (_continuousPlayback)
                {
                    await PreloadNextSegmentAsync();
                }
            }
            catch (Exception ex)
            {
                PlaybackError?.Invoke(this, $"Error playing segment: {ex.Message}");
            }
        }

        /// <summary>
        /// Plays or pauses the current video
        /// </summary>
        public void PlayPause()
        {
            if (_mediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
            {
                _mediaPlayer.Pause();
            }
            else
            {
                _mediaPlayer.Play();
            }
        }

        /// <summary>
        /// Stops playback and resets position
        /// </summary>
        public void Stop()
        {
            _mediaPlayer.Pause();
            _mediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
        }

        /// <summary>
        /// Seeks to a specific position in the current segment
        /// </summary>
        /// <param name="offsetSeconds">Offset in seconds from segment start</param>
        public void SeekToPosition(double offsetSeconds)
        {
            if (_currentSegment == null)
                return;

            // Ensure offset is within segment bounds
            double clampedOffset = Math.Min(Math.Max(0, offsetSeconds), _currentSegment.Duration);

            // Set player position
            _mediaPlayer.PlaybackSession.Position = TimeSpan.FromSeconds(clampedOffset);

            // Update timeline position
            _timelineService.UpdatePositionWithinSegment(clampedOffset);
        }

        /// <summary>
        /// Seeks to a specific time in the timeline
        /// </summary>
        /// <param name="targetTime">Time to seek to</param>
        /// <returns>True if successful</returns>
        public async Task<bool> SeekToTimeAsync(DateTime targetTime)
        {
            // Find segment at the target time
            var segment = _timelineService.FindSegmentAtTime(targetTime);

            if (segment == null)
            {
                // No segment at target time
                PlaybackError?.Invoke(this, "No video footage at the specified time");
                return false;
            }

            if (segment == _currentSegment)
            {
                // Target time is in current segment, just seek
                double offsetSeconds = (targetTime - segment.StartTime).TotalSeconds;
                SeekToPosition(offsetSeconds);
                return true;
            }
            else
            {
                // Target time is in different segment, load it
                await PlaySegmentAsync(segment);

                // Seek to offset within new segment
                double offsetSeconds = (targetTime - segment.StartTime).TotalSeconds;
                SeekToPosition(offsetSeconds);
                return true;
            }
        }

        /// <summary>
        /// Moves to the next segment
        /// </summary>
        /// <returns>Task<bool> indicating success</returns>
        public async Task<bool> MoveToNextSegmentAsync()
        {
            if (_timelineService.MoveToNextSegment())
            {
                await PlaySegmentAsync(_timelineService.CurrentSegment);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Moves to the previous segment
        /// </summary>
        /// <returns>Task<bool> indicating success</returns>
        public async Task<bool> MoveToPreviousSegmentAsync()
        {
            if (_timelineService.MoveToPreviousSegment())
            {
                await PlaySegmentAsync(_timelineService.CurrentSegment);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Cleans up resources
        /// </summary>
        public void Cleanup()
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Pause();
                _mediaPlayer.Source = null;

                // Unregister events
                _mediaPlayer.MediaOpened -= MediaPlayer_MediaOpened;
                _mediaPlayer.MediaEnded -= MediaPlayer_MediaEnded;
                _mediaPlayer.MediaFailed -= MediaPlayer_MediaFailed;
                _mediaPlayer.PlaybackSession.PositionChanged -= PlaybackSession_PositionChanged;

                _mediaPlayer.Dispose();
                _mediaPlayer = null;
            }

            _currentSegment = null;
            _nextSegment = null;
            _nextMediaSource = null;
        }

        #region Private Methods

        /// <summary>
        /// Loads a media source for a segment
        /// </summary>
        /// <param name="segment">Segment to load</param>
        /// <returns>Task</returns>
        private async Task LoadMediaSourceAsync(VideoSegment segment)
        {
            if (_isPreloadingNext && _nextSegment == segment && _nextMediaSource != null)
            {
                // Use preloaded media source
                _mediaPlayer.Source = _nextMediaSource;
                _isPreloadingNext = false;
                _nextMediaSource = null;
                _nextSegment = null;
            }
            else
            {
                // Load new media source
                try
                {
                    StorageFile file = await StorageFile.GetFileFromPathAsync(segment.FilePath);
                    MediaSource mediaSource = MediaSource.CreateFromStorageFile(file);
                    _mediaPlayer.Source = mediaSource;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to load media file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Preloads the next segment for seamless playback
        /// </summary>
        /// <returns>Task</returns>
        private async Task PreloadNextSegmentAsync()
        {
            if (_currentSegment == null || _isPreloadingNext)
                return;

            try
            {
                // Find next segment
                var nextSegment = _timelineService.TimelineData.GetNextSegment(_currentSegment);

                if (nextSegment != null)
                {
                    _isPreloadingNext = true;
                    _nextSegment = nextSegment;

                    // Preload media source
                    StorageFile file = await StorageFile.GetFileFromPathAsync(nextSegment.FilePath);
                    _nextMediaSource = MediaSource.CreateFromStorageFile(file);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error preloading next segment: {ex.Message}");
                _isPreloadingNext = false;
                _nextSegment = null;
                _nextMediaSource = null;
            }
        }

        #endregion

        #region Event Handlers

        private void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
        {
            // Media successfully opened
            SegmentChanged?.Invoke(this, _currentSegment);
        }

        private async void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            // Notify playback ended
            PlaybackEnded?.Invoke(this, _currentSegment);

            // If continuous playback is enabled, try to play the next segment
            if (_continuousPlayback && !_isTransitioning)
            {
                _isTransitioning = true;

                if (await MoveToNextSegmentAsync())
                {
                    // Next segment playing
                }
                else
                {
                    // No more segments
                    PlaybackError?.Invoke(this, "End of timeline reached");
                }

                _isTransitioning = false;
            }
        }

        private void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            // Use args.ErrorMessage instead of args.Error.Message
            PlaybackError?.Invoke(this, $"Media playback error: {args.ErrorMessage}");
        }

        private void PlaybackSession_PositionChanged(MediaPlaybackSession sender, object args)
        {
            if (_currentSegment == null)
                return;

            try
            {
                // Calculate current time
                var currentTime = _currentSegment.StartTime.Add(sender.Position);

                // Update timeline service
                _timelineService.UpdatePositionWithinSegment(sender.Position.TotalSeconds);

                // Notify position change
                PositionChanged?.Invoke(this, currentTime);

                // If we're nearing the end of this segment, preload the next one
                double remainingSeconds = _currentSegment.Duration - sender.Position.TotalSeconds;
                if (_continuousPlayback && !_isPreloadingNext && remainingSeconds < 5)
                {
                    Task.Run(() => PreloadNextSegmentAsync());
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in position changed: {ex.Message}");
            }
        }

        #endregion
    }
}