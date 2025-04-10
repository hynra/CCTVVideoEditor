using CCTVVideoEditor.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;

namespace CCTVVideoEditor.Controls
{
    public sealed partial class TimelineControl : UserControl
    {
        #region Private Fields

        // Timeline data and state
        private TimelineData _timelineData;
        private VideoSegment _currentSegment;
        private DateTime _viewStartTime;
        private DateTime _viewEndTime;
        private DateTime _currentTime;

        // Selection state
        private DateTime _selectionStartTime;
        private DateTime _selectionEndTime;
        private bool _isSelectionActive;

        // UI elements for selection
        private Rectangle _selectionRectangle;

        // Timeline rendering parameters
        private double _pixelsPerSecond = 5.0; // Increased default for better visibility
        private const int HourTickHeight = 15;
        private const int HalfHourTickHeight = 10;
        private const int MinuteTickHeight = 5;

        // Timeline dimensions
        private double _timelineWidth;

        // Interaction flags
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private double _dragStartOffset;

        // Event handlers
        public event EventHandler<DateTime> TimeSelected;
        public event EventHandler<VideoSegment> SegmentSelected;
        public event EventHandler<(DateTime start, DateTime end)> RangeSelected;

        #endregion

        public TimelineControl()
        {
            this.InitializeComponent();

            // Initialize selection
            _isSelectionActive = false;
            _selectionRectangle = new Rectangle
            {
                Style = Resources["TimelineSelectionStyle"] as Style
            };

            // Initialize timeline view
            _viewStartTime = DateTime.Today;
            _viewEndTime = DateTime.Today.AddHours(24);
            _currentTime = DateTime.Now;

            // Add pointer event handlers to the segments canvas
            SegmentsCanvas.PointerPressed += SegmentsCanvas_PointerPressed;
            SegmentsCanvas.PointerMoved += SegmentsCanvas_PointerMoved;
            SegmentsCanvas.PointerReleased += SegmentsCanvas_PointerReleased;
            SegmentsCanvas.PointerExited += SegmentsCanvas_PointerExited;

            // Enable manipulation for horizontal scrolling
            SegmentsCanvas.ManipulationMode = ManipulationModes.TranslateX;

            // Add Loaded event handler
            this.Loaded += TimelineControl_Loaded;

            // Initialize time preset combo box
            InitializeTimePresets();
        }

        private void TimelineControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Set initial zoom level after the control is fully loaded
            UpdateZoom(ZoomSlider.Value);
        }

        private void InitializeTimePresets()
        {
            if (TimePresetComboBox != null)
            {
                // Already initialized
                return;
            }

            // Initialize with default items if not set in XAML
            if (TimePresetComboBox.Items.Count == 0)
            {
                TimePresetComboBox.Items.Add(new ComboBoxItem { Content = "View All Day" });
                TimePresetComboBox.Items.Add(new ComboBoxItem { Content = "Morning (6-12)" });
                TimePresetComboBox.Items.Add(new ComboBoxItem { Content = "Afternoon (12-18)" });
                TimePresetComboBox.Items.Add(new ComboBoxItem { Content = "Evening (18-24)" });
                TimePresetComboBox.Items.Add(new ComboBoxItem { Content = "Night (0-6)" });
                TimePresetComboBox.SelectedIndex = 0;
            }
        }

        #region Public Properties and Methods

        /// <summary>
        /// Gets or sets the timeline data
        /// </summary>
        public TimelineData TimelineData
        {
            get => _timelineData;
            set
            {
                _timelineData = value;
                if (_timelineData != null)
                {
                    // Initialize view to show the full day
                    _viewStartTime = _timelineData.Date;
                    _viewEndTime = _timelineData.Date.AddHours(24);

                    // Reset selection
                    _isSelectionActive = false;
                    _selectionStartTime = DateTime.MinValue;
                    _selectionEndTime = DateTime.MinValue;

                    // Render timeline
                    RenderTimeline();

                    // Update selection text
                    UpdateSelectionText();
                }
            }
        }

        /// <summary>
        /// Gets or sets the current segment
        /// </summary>
        public VideoSegment CurrentSegment
        {
            get => _currentSegment;
            set
            {
                _currentSegment = value;
                if (_currentSegment != null)
                {
                    // Update current time to segment start time
                    _currentTime = _currentSegment.StartTime;

                    // Update timeline position
                    UpdateCurrentPositionLine();

                    // Scroll to make current position visible
                    EnsureTimeVisible(_currentTime);
                }
            }
        }

        /// <summary>
        /// Updates the current time position in the timeline
        /// </summary>
        /// <param name="time">The current time</param>
        public void UpdateCurrentTime(DateTime time)
        {
            _currentTime = time;
            UpdateCurrentPositionLine();
        }

        /// <summary>
        /// Sets the selection range
        /// </summary>
        /// <param name="startTime">Selection start time</param>
        /// <param name="endTime">Selection end time</param>
        public void SetSelectionRange(DateTime startTime, DateTime endTime)
        {
            if (startTime < endTime)
            {
                _selectionStartTime = startTime;
                _selectionEndTime = endTime;
                _isSelectionActive = true;

                UpdateSelectionRectangle();
                UpdateSelectionText();

                // Notify selection changed
                RangeSelected?.Invoke(this, (_selectionStartTime, _selectionEndTime));
            }
        }

        /// <summary>
        /// Clears the current selection
        /// </summary>
        public void ClearSelection()
        {
            _isSelectionActive = false;
            _selectionStartTime = DateTime.MinValue;
            _selectionEndTime = DateTime.MinValue;

            SelectionCanvas.Children.Remove(_selectionRectangle);
            UpdateSelectionText();
        }

        /// <summary>
        /// Gets the selected time range
        /// </summary>
        /// <returns>Tuple of start and end times</returns>
        public (DateTime start, DateTime end) GetSelectionRange()
        {
            return (_selectionStartTime, _selectionEndTime);
        }

        /// <summary>
        /// Scrolls the timeline to make the specified time visible
        /// </summary>
        /// <param name="time">Time to show</param>
        public void EnsureTimeVisible(DateTime time)
        {
            if (time >= _viewStartTime && time <= _viewEndTime)
            {
                double position = TimeToPosition(time);

                // Check if TimelineScrollViewer is null before accessing its properties
                if (TimelineScrollViewer == null)
                    return;

                double scrollViewerWidth = TimelineScrollViewer.ActualWidth;

                // Use a default width if ActualWidth is not yet available
                if (scrollViewerWidth <= 0)
                    scrollViewerWidth = 800;

                // Calculate scroll position to center the time
                double scrollPosition = Math.Max(0, position - (scrollViewerWidth / 2));

                // Set horizontal scroll offset
                TimelineScrollViewer.ChangeView(scrollPosition, null, null);
            }
        }

        /// <summary>
        /// Sets the view to a specific time range
        /// </summary>
        /// <param name="startHour">Start hour (0-23)</param>
        /// <param name="endHour">End hour (0-23)</param>
        public void SetViewTimeRange(int startHour, int endHour)
        {
            if (_timelineData == null)
                return;

            DateTime date = _timelineData.Date;

            // Create start and end times
            DateTime startTime = new DateTime(date.Year, date.Month, date.Day, startHour, 0, 0);
            DateTime endTime = new DateTime(date.Year, date.Month, date.Day, endHour, 0, 0);

            // If end time is earlier than start time, assume it's the next day
            if (endHour <= startHour)
            {
                endTime = endTime.AddDays(1);
            }

            // Calculate appropriate zoom level
            double hoursInView = (endTime - startTime).TotalHours;
            double zoomLevel = 24 / hoursInView;

            // Update zoom slider
            ZoomSlider.Value = Math.Min(Math.Max(zoomLevel, ZoomSlider.Minimum), ZoomSlider.Maximum);

            // Ensure the time is visible
            EnsureTimeVisible(startTime);
        }

        #endregion

        #region Timeline Rendering

        /// <summary>
        /// Renders the complete timeline
        /// </summary>
        private void RenderTimeline()
        {
            if (_timelineData == null)
                return;

            // Clear existing elements
            TicksCanvas.Children.Clear();
            SegmentsCanvas.Children.Clear();
            SelectionCanvas.Children.Clear();

            // Calculate timeline width in pixels
            TimeSpan viewDuration = _viewEndTime - _viewStartTime;
            _timelineWidth = viewDuration.TotalSeconds * _pixelsPerSecond;

            // Set canvas widths
            TicksCanvas.Width = _timelineWidth;
            SegmentsCanvas.Width = _timelineWidth;
            SelectionCanvas.Width = _timelineWidth;
            TimelineContainer.Width = _timelineWidth;

            // Render time ticks and labels
            RenderTimeTicks();

            // Render video segments
            RenderVideoSegments();

            // Render gaps
            RenderGaps();

            // Update current position line
            UpdateCurrentPositionLine();

            // Update selection if active
            if (_isSelectionActive)
            {
                UpdateSelectionRectangle();
            }
        }

        /// <summary>
        /// Renders time ticks and labels on the timeline
        /// </summary>
        private void RenderTimeTicks()
        {
            // Start from the view start time rounded down to the nearest hour
            DateTime tickTime = new DateTime(
                _viewStartTime.Year,
                _viewStartTime.Month,
                _viewStartTime.Day,
                _viewStartTime.Hour,
                0,
                0);

            // Render ticks until we reach the view end time
            while (tickTime <= _viewEndTime)
            {
                double position = TimeToPosition(tickTime);

                // Hour tick
                if (tickTime.Minute == 0)
                {
                    // Create hour tick
                    Line hourTick = new Line
                    {
                        Style = Resources["TimelineTickStyle"] as Style,
                        X1 = position,
                        Y1 = 0,
                        X2 = position,
                        Y2 = HourTickHeight
                    };
                    TicksCanvas.Children.Add(hourTick);

                    // Create hour label
                    TextBlock hourLabel = new TextBlock
                    {
                        Style = Resources["TimelineTickLabelStyle"] as Style,
                        Text = tickTime.ToString("HH:mm")
                    };
                    Canvas.SetLeft(hourLabel, position - (hourLabel.ActualWidth / 2));
                    Canvas.SetTop(hourLabel, HourTickHeight + 2);
                    TicksCanvas.Children.Add(hourLabel);

                    // Move to next tick (30 mins)
                    tickTime = tickTime.AddMinutes(30);
                }
                // Half-hour tick
                else if (tickTime.Minute == 30)
                {
                    // Create half-hour tick
                    Line halfHourTick = new Line
                    {
                        Style = Resources["TimelineTickStyle"] as Style,
                        X1 = position,
                        Y1 = 0,
                        X2 = position,
                        Y2 = HalfHourTickHeight
                    };
                    TicksCanvas.Children.Add(halfHourTick);

                    // Create half-hour label
                    TextBlock halfHourLabel = new TextBlock
                    {
                        Style = Resources["TimelineTickLabelStyle"] as Style,
                        Text = tickTime.ToString("HH:mm")
                    };
                    Canvas.SetLeft(halfHourLabel, position - (halfHourLabel.ActualWidth / 2));
                    Canvas.SetTop(halfHourLabel, HalfHourTickHeight + 2);
                    TicksCanvas.Children.Add(halfHourLabel);

                    // Move to next tick (30 mins)
                    tickTime = tickTime.AddMinutes(30);
                }
                // 10-minute ticks
                else
                {
                    // Create minute tick
                    Line minuteTick = new Line
                    {
                        Style = Resources["TimelineTickStyle"] as Style,
                        X1 = position,
                        Y1 = 0,
                        X2 = position,
                        Y2 = MinuteTickHeight
                    };
                    TicksCanvas.Children.Add(minuteTick);

                    // Move to next tick (10 mins)
                    tickTime = tickTime.AddMinutes(10);
                }
            }
        }

        /// <summary>
        /// Renders video segments on the timeline
        /// </summary>
        private void RenderVideoSegments()
        {
            if (_timelineData == null)
                return;

            var segments = _timelineData.GetAllSegments();
            foreach (var segment in segments)
            {
                // Skip segments outside the view range
                if (segment.EndTime < _viewStartTime || segment.StartTime > _viewEndTime)
                    continue;

                // Calculate segment position and width
                double startPosition = TimeToPosition(segment.StartTime);
                double endPosition = TimeToPosition(segment.EndTime);
                double width = endPosition - startPosition;

                // Create rectangle for segment
                Rectangle segmentRect = new Rectangle
                {
                    Style = Resources["TimelineSegmentStyle"] as Style,
                    Width = Math.Max(1, width),
                    Tag = segment  // Store segment object for reference
                };

                // Set position
                Canvas.SetLeft(segmentRect, startPosition);
                Canvas.SetTop(segmentRect, 0);

                // Add tooltip with segment info
                ToolTipService.SetToolTip(segmentRect, $"{segment.StartTime:HH:mm:ss} - {segment.EndTime:HH:mm:ss}");

                // Add to canvas
                SegmentsCanvas.Children.Add(segmentRect);
            }
        }

        /// <summary>
        /// Renders gaps in the timeline
        /// </summary>
        private void RenderGaps()
        {
            if (_timelineData == null)
                return;

            var gaps = _timelineData.FindGaps();
            foreach (var (start, end) in gaps)
            {
                // Skip gaps outside the view range
                if (end < _viewStartTime || start > _viewEndTime)
                    continue;

                // Calculate gap position and width
                double startPosition = TimeToPosition(start);
                double endPosition = TimeToPosition(end);
                double width = endPosition - startPosition;

                // Create rectangle for gap
                Rectangle gapRect = new Rectangle
                {
                    Style = Resources["TimelineGapStyle"] as Style,
                    Width = Math.Max(1, width)
                };

                // Set position
                Canvas.SetLeft(gapRect, startPosition);
                Canvas.SetTop(gapRect, 0);

                // Add tooltip with gap info
                ToolTipService.SetToolTip(gapRect, $"No footage: {start:HH:mm:ss} - {end:HH:mm:ss}");

                // Add to canvas
                SegmentsCanvas.Children.Add(gapRect);
            }
        }

        /// <summary>
        /// Updates the current position line
        /// </summary>
        private void UpdateCurrentPositionLine()
        {
            double position = TimeToPosition(_currentTime);

            // Update line position
            CurrentPositionLine.X1 = position;
            CurrentPositionLine.X2 = position;
        }

        /// <summary>
        /// Updates the selection rectangle
        /// </summary>
        private void UpdateSelectionRectangle()
        {
            if (!_isSelectionActive)
                return;

            // Remove existing selection rectangle
            SelectionCanvas.Children.Remove(_selectionRectangle);

            // Calculate selection position and width
            double startPosition = TimeToPosition(_selectionStartTime);
            double endPosition = TimeToPosition(_selectionEndTime);
            double width = endPosition - startPosition;

            // Update selection rectangle
            _selectionRectangle.Width = Math.Max(1, width);

            // Add to canvas at calculated position
            Canvas.SetLeft(_selectionRectangle, startPosition);
            SelectionCanvas.Children.Add(_selectionRectangle);
        }

        /// <summary>
        /// Updates the selection time text
        /// </summary>
        private void UpdateSelectionText()
        {
            if (_isSelectionActive)
            {
                // Update selection time text
                SelectionStartText.Text = _selectionStartTime.ToString("HH:mm:ss");
                SelectionEndText.Text = _selectionEndTime.ToString("HH:mm:ss");

                // Calculate and display duration
                TimeSpan duration = _selectionEndTime - _selectionStartTime;
                SelectionDurationText.Text = $" ({duration.ToString(@"hh\:mm\:ss")})";
            }
            else
            {
                // Clear selection time text
                SelectionStartText.Text = "--:--:--";
                SelectionEndText.Text = "--:--:--";
                SelectionDurationText.Text = " (00:00:00)";
            }
        }

        #endregion

        #region Timeline Calculations

        /// <summary>
        /// Converts a time to a position on the timeline
        /// </summary>
        /// <param name="time">Time to convert</param>
        /// <returns>Position in pixels</returns>
        private double TimeToPosition(DateTime time)
        {
            // Calculate seconds from view start
            double seconds = (time - _viewStartTime).TotalSeconds;

            // Convert to pixels
            return seconds * _pixelsPerSecond;
        }

        /// <summary>
        /// Converts a position to a time on the timeline
        /// </summary>
        /// <param name="position">Position in pixels</param>
        /// <returns>Time at position</returns>
        private DateTime PositionToTime(double position)
        {
            // Calculate seconds from position
            double seconds = position / _pixelsPerSecond;

            // Convert to time
            return _viewStartTime.AddSeconds(seconds);
        }

        /// <summary>
        /// Updates the zoom level
        /// </summary>
        /// <param name="zoomLevel">Zoom level (1-24)</param>
        private void UpdateZoom(double zoomLevel)
        {
            // Calculate pixels per second based on zoom level
            // At zoom level 1, we show 24 hours in the available width
            // At zoom level 24, we show 1 hour in the available width
            double viewportWidth = 800; // Default width

            // Check if TimelineScrollViewer is available
            if (TimelineScrollViewer != null)
            {
                viewportWidth = TimelineScrollViewer.ActualWidth > 0 ?
                    TimelineScrollViewer.ActualWidth : 800;
            }

            // Hours to display in viewport
            double hoursInView = 24 / zoomLevel;

            // Calculate pixels per second
            _pixelsPerSecond = viewportWidth / (hoursInView * 3600);

            // Re-render timeline
            RenderTimeline();

            // Ensure current time is visible
            EnsureTimeVisible(_currentTime);
        }

        /// <summary>
        /// Moves the timeline view by the specified number of hours
        /// </summary>
        /// <param name="hours">Hours to move (positive for forward, negative for backward)</param>
        private void MoveTimelineView(int hours)
        {
            if (_timelineData == null)
                return;

            // Calculate the new time to center on
            DateTime centerTime = _currentTime.AddHours(hours);

            // Ensure it's within the day
            if (centerTime < _timelineData.Date)
                centerTime = _timelineData.Date;
            else if (centerTime > _timelineData.Date.AddDays(1))
                centerTime = _timelineData.Date.AddDays(1).AddSeconds(-1);

            // Update current time and ensure it's visible
            _currentTime = centerTime;
            UpdateCurrentPositionLine();
            EnsureTimeVisible(centerTime);
        }

        #region Event Handlers

        private void ZoomSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            UpdateZoom(e.NewValue);
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            // Increase zoom level
            ZoomSlider.Value = Math.Min(ZoomSlider.Maximum, ZoomSlider.Value + 1);
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            // Decrease zoom level
            ZoomSlider.Value = Math.Max(ZoomSlider.Minimum, ZoomSlider.Value - 1);
        }

        private void GoToStartButton_Click(object sender, RoutedEventArgs e)
        {
            // Go to the start of the day
            if (_timelineData != null)
            {
                EnsureTimeVisible(_timelineData.Date);
            }
        }

        private void GoToCurrentTimeButton_Click(object sender, RoutedEventArgs e)
        {
            // Go to the current position
            EnsureTimeVisible(_currentTime);
        }

        private void GoToEndButton_Click(object sender, RoutedEventArgs e)
        {
            // Go to the end of the day
            if (_timelineData != null)
            {
                EnsureTimeVisible(_timelineData.Date.AddDays(1).AddSeconds(-1));
            }
        }

        // New navigation event handlers
        private void GoBackHour_Click(object sender, RoutedEventArgs e)
        {
            MoveTimelineView(-1);
        }

        private void GoForwardHour_Click(object sender, RoutedEventArgs e)
        {
            MoveTimelineView(1);
        }

        private void TimeJump_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string timeText)
            {
                // Parse the time from the button's tag
                if (TimeSpan.TryParse(timeText, out TimeSpan timeSpan))
                {
                    // Create a datetime for today with the specified time
                    DateTime jumpTime = _timelineData.Date.Add(timeSpan);

                    // Update current time
                    _currentTime = jumpTime;
                    UpdateCurrentPositionLine();

                    // Ensure the time is visible
                    EnsureTimeVisible(jumpTime);
                }
            }
        }

        private void TimePresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TimePresetComboBox.SelectedIndex < 0 || _timelineData == null)
                return;

            switch (TimePresetComboBox.SelectedIndex)
            {
                case 0: // All Day
                    SetViewTimeRange(0, 24);
                    break;
                case 1: // Morning (6-12)
                    SetViewTimeRange(6, 12);
                    break;
                case 2: // Afternoon (12-18)
                    SetViewTimeRange(12, 18);
                    break;
                case 3: // Evening (18-24)
                    SetViewTimeRange(18, 24);
                    break;
                case 4: // Night (0-6)
                    SetViewTimeRange(0, 6);
                    break;
            }
        }

        private void TimelineCanvas_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            // Handle direct manipulation (touch/pen) on the timeline
            if (TimelineScrollViewer != null)
            {
                // Calculate new horizontal offset
                double newOffset = TimelineScrollViewer.HorizontalOffset - e.Delta.Translation.X;

                // Apply the new offset
                TimelineScrollViewer.ChangeView(newOffset, null, null);

                // Mark the event as handled to prevent it from bubbling up
                e.Handled = true;
            }
        }

        private void TimelineScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            // Make sure TimelineScrollViewer is not null
            if (TimelineScrollViewer == null)
                return;

            // If we're dragging the scrollbar, update view bounds
            if (!e.IsIntermediate)
            {
                double scrollPosition = TimelineScrollViewer.HorizontalOffset;
                double viewportWidth = TimelineScrollViewer.ActualWidth;

                // Calculate visible time range
                DateTime visibleStart = PositionToTime(scrollPosition);
                DateTime visibleEnd = PositionToTime(scrollPosition + viewportWidth);

                // For debugging
                // System.Diagnostics.Debug.WriteLine($"Visible: {visibleStart:HH:mm:ss} - {visibleEnd:HH:mm:ss}");
            }
        }

        private void SegmentsCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Point point = e.GetCurrentPoint(SegmentsCanvas).Position;

            // Check for right-click (used for scrolling/dragging the timeline)
            var properties = e.GetCurrentPoint(SegmentsCanvas).Properties;

            if (properties.IsRightButtonPressed)
            {
                // Start timeline dragging
                _isDragging = true;
                _dragStartPoint = point;
                _dragStartOffset = TimelineScrollViewer.HorizontalOffset;
                SegmentsCanvas.CapturePointer(e.Pointer);
                e.Handled = true;
                return;
            }

            // Otherwise, handle as selection
            // Convert position to time
            DateTime clickTime = PositionToTime(point.X);

            // Start selection
            _selectionStartTime = clickTime;
            _selectionEndTime = clickTime;
            _isSelectionActive = true;

            // Check if clicked on a segment
            bool hitSegment = false;
            foreach (var child in SegmentsCanvas.Children)
            {
                if (child is Rectangle rect && rect.Tag is VideoSegment segment)
                {
                    double left = Canvas.GetLeft(rect);
                    double width = rect.Width;

                    if (point.X >= left && point.X <= left + width)
                    {
                        // Clicked on a segment
                        SegmentSelected?.Invoke(this, segment);
                        hitSegment = true;
                        break;
                    }
                }
            }

            // If not on a segment, just notify of time selection
            if (!hitSegment)
            {
                TimeSelected?.Invoke(this, clickTime);
            }

            // Capture pointer for dragging
            SegmentsCanvas.CapturePointer(e.Pointer);
        }

        private void SegmentsCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // Make sure pointer is captured
            if (!SegmentsCanvas.PointerCaptures?.Any(c => c.PointerId == e.Pointer.PointerId) == true)
                return;

            Point point = e.GetCurrentPoint(SegmentsCanvas).Position;

            // Check if we're dragging the timeline
            if (_isDragging)
            {
                // Calculate drag distance
                double dragDeltaX = point.X - _dragStartPoint.X;

                // Make sure TimelineScrollViewer is not null
                if (TimelineScrollViewer == null)
                    return;

                // Calculate new scroll position
                double newOffset = _dragStartOffset - dragDeltaX;

                // Ensure offset is within bounds
                newOffset = Math.Max(0, newOffset);

                // Only use ViewportWidth if it's valid
                double maxOffset = TimelineContainer.Width;
                if (TimelineScrollViewer.ViewportWidth > 0)
                    maxOffset -= TimelineScrollViewer.ViewportWidth;

                newOffset = Math.Min(maxOffset, newOffset);

                // Update scroll position
                TimelineScrollViewer.ChangeView(newOffset, null, null);

                e.Handled = true;
                return;
            }

            // Otherwise handle selection
            if (_isSelectionActive)
            {
                // Convert position to time
                DateTime moveTime = PositionToTime(point.X);

                // Update selection end
                _selectionEndTime = moveTime;

                // If end is before start, swap them
                if (_selectionEndTime < _selectionStartTime)
                {
                    var temp = _selectionStartTime;
                    _selectionStartTime = _selectionEndTime;
                    _selectionEndTime = temp;
                }

                // Update UI
                UpdateSelectionRectangle();
                UpdateSelectionText();
            }
        }

        private void SegmentsCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            // Check if we were dragging the timeline
            if (_isDragging)
            {
                _isDragging = false;
                SegmentsCanvas.ReleasePointerCapture(e.Pointer);
                e.Handled = true;
                return;
            }

            // End selection
            if (_isSelectionActive)
            {
                // Get final position
                Point point = e.GetCurrentPoint(SegmentsCanvas).Position;

                // Convert position to time
                DateTime releaseTime = PositionToTime(point.X);

                // Update selection end
                _selectionEndTime = releaseTime;

                // If end is before start, swap them
                if (_selectionEndTime < _selectionStartTime)
                {
                    var temp = _selectionStartTime;
                    _selectionStartTime = _selectionEndTime;
                    _selectionEndTime = temp;
                }

                // If start and end are very close, treat as a single click
                if ((_selectionEndTime - _selectionStartTime).TotalSeconds < 0.5)
                {
                    // Don't show selection for single clicks
                    _isSelectionActive = false;
                    SelectionCanvas.Children.Remove(_selectionRectangle);
                }
                else
                {
                    // Notify of range selection
                    RangeSelected?.Invoke(this, (_selectionStartTime, _selectionEndTime));
                }

                // Update UI
                UpdateSelectionText();
            }

            // Release pointer
            SegmentsCanvas.ReleasePointerCapture(e.Pointer);
        }

        private void SegmentsCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            // If leaving canvas while dragging, continue the drag
            if (_isDragging && SegmentsCanvas.PointerCaptures?.Any(c => c.PointerId == e.Pointer.PointerId) == true)
            {
                // Keep dragging, don't release
                return;
            }

            // If we leave the canvas while selecting, finish selection
            if (_isSelectionActive && SegmentsCanvas.PointerCaptures?.Any(c => c.PointerId == e.Pointer.PointerId) == true)
            {
                // Get final position
                Point point = e.GetCurrentPoint(SegmentsCanvas).Position;

                // Constrain to canvas bounds
                point.X = Math.Max(0, Math.Min(point.X, SegmentsCanvas.ActualWidth));

                // Convert position to time
                DateTime exitTime = PositionToTime(point.X);

                // Update selection end
                _selectionEndTime = exitTime;

                // If end is before start, swap them
                if (_selectionEndTime < _selectionStartTime)
                {
                    var temp = _selectionStartTime;
                    _selectionStartTime = _selectionEndTime;
                    _selectionEndTime = temp;
                }

                // Notify of range selection
                RangeSelected?.Invoke(this, (_selectionStartTime, _selectionEndTime));

                // Update UI
                UpdateSelectionRectangle();
                UpdateSelectionText();

                // Release pointer
                SegmentsCanvas.ReleasePointerCapture(e.Pointer);
            }
        }

        #endregion
    }
    #endregion // End of Event Handlers Region
}