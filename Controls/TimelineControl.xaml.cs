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
        private double _pixelsPerSecond = 2.0;
        private const int HourTickHeight = 15;
        private const int HalfHourTickHeight = 10;
        private const int MinuteTickHeight = 5;

        // Timeline dimensions
        private double _timelineWidth;

        // Navigator drag state
        private bool _isNavigatorDragging = false;
        private double _navigatorDragStartX;
        private double _navigatorViewWidth;
        private double _navigatorViewPosition;

        // Event handlers
        public event EventHandler<DateTime> TimeSelected;
        public event EventHandler<VideoSegment> SegmentSelected;
        public event EventHandler<(DateTime start, DateTime end)> RangeSelected;

        private bool _isDragging = false;
        private Point _dragStartPoint;
        private double _dragStartOffset;

        // Position handle drag state
        private bool _isPositionHandleDragging = false;
        private double _handleDragStartX;

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

            // Make the timeline draggable directly
            TimelineContainer.PointerPressed += TimelineContainer_PointerPressed;
            TimelineContainer.PointerMoved += TimelineContainer_PointerMoved;
            TimelineContainer.PointerReleased += TimelineContainer_PointerReleased;
            TimelineContainer.PointerCaptureLost += TimelineContainer_PointerCaptureLost;

            // Add Loaded event handler
            this.Loaded += TimelineControl_Loaded;
        }

        private void TimelineControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure the ScrollViewer has the right settings
            TimelineScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
            TimelineScrollViewer.HorizontalScrollMode = ScrollMode.Enabled;
            // Set initial zoom level after control is loaded
            UpdateZoom(ZoomSlider.Value);
        }

        #region Public Properties and Methods

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

                    // Render mini navigator
                    RenderNavigator();

                    // Update selection text
                    UpdateSelectionText();
                }
            }
        }

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

        public void UpdateCurrentTime(DateTime time)
        {
            _currentTime = time;
            UpdateCurrentPositionLine();
        }

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

        public void ClearSelection()
        {
            _isSelectionActive = false;
            _selectionStartTime = DateTime.MinValue;
            _selectionEndTime = DateTime.MinValue;

            SelectionCanvas.Children.Remove(_selectionRectangle);
            UpdateSelectionText();
        }

        public (DateTime start, DateTime end) GetSelectionRange()
        {
            return (_selectionStartTime, _selectionEndTime);
        }

        public void EnsureTimeVisible(DateTime time)
        {
            if (time >= _viewStartTime && time <= _viewEndTime)
            {
                double position = TimeToPosition(time);
                double scrollViewerWidth = TimelineScrollViewer.ActualWidth;

                // Calculate scroll position to center the time
                double scrollPosition = Math.Max(0, position - (scrollViewerWidth / 2));

                // Set horizontal scroll offset
                TimelineScrollViewer.ChangeView(scrollPosition, null, null);

                // Update navigator view
                UpdateNavigatorViewFromScroll();
            }
        }

        #endregion

        #region Timeline Rendering

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

            // Ensure the timeline is always wider than the viewport to enable scrolling
            if (TimelineScrollViewer != null && TimelineScrollViewer.ViewportWidth > 0)
            {
                _timelineWidth = Math.Max(_timelineWidth, TimelineScrollViewer.ViewportWidth * 1.5);
            }

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

        private void RenderNavigator()
        {
            if (_timelineData == null || NavigatorCanvas == null)
                return;

            // Clear existing elements
            NavigatorCanvas.Children.Clear();

            // Setup navigator dimensions
            double navigatorWidth = NavigatorCanvas.ActualWidth;
            if (navigatorWidth <= 0)
                navigatorWidth = this.ActualWidth;

            // Draw segments in navigator
            var segments = _timelineData.GetAllSegments();
            foreach (var segment in segments)
            {
                // Calculate segment position and width in the navigator
                double dayDuration = 24 * 3600; // seconds in a day
                double startOffset = (segment.StartTime - _timelineData.Date).TotalSeconds / dayDuration;
                double segmentWidth = segment.Duration / dayDuration;

                // Create rectangle for segment
                Rectangle segmentRect = new Rectangle
                {
                    Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 215)),
                    Height = 20,
                    Width = Math.Max(1, navigatorWidth * segmentWidth)
                };

                // Set position
                Canvas.SetLeft(segmentRect, navigatorWidth * startOffset);

                // Add to canvas
                NavigatorCanvas.Children.Add(segmentRect);
            }

            // Setup navigator view (visible portion indicator)
            UpdateNavigatorViewFromScroll();
        }

        private void UpdateNavigatorViewFromScroll()
        {
            if (NavigatorCanvas == null || NavigatorView == null || TimelineScrollViewer == null)
                return;

            double navigatorWidth = NavigatorCanvas.ActualWidth;
            if (navigatorWidth <= 0)
                navigatorWidth = this.ActualWidth;

            // Calculate visible portion as percentage of total timeline
            double visiblePortion = TimelineScrollViewer.ViewportWidth / _timelineWidth;
            _navigatorViewWidth = Math.Max(30, navigatorWidth * visiblePortion); // Minimum 30px width

            // Calculate position in navigator
            double scrollPercentage = TimelineScrollViewer.HorizontalOffset / _timelineWidth;
            _navigatorViewPosition = navigatorWidth * scrollPercentage;

            // Update navigator view
            NavigatorView.Width = _navigatorViewWidth;
            Canvas.SetLeft(NavigatorView, _navigatorViewPosition);
        }

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

        private void UpdateCurrentPositionLine()
        {
            double position = TimeToPosition(_currentTime);

            // Update line position
            CurrentPositionLine.X1 = position;
            CurrentPositionLine.X2 = position;

            // Update position handle location
            Canvas.SetLeft(PositionHandle, position - (PositionHandle.Width / 2));
            Canvas.SetTop(PositionHandle, 0); // Place at the top
        }

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

        private double TimeToPosition(DateTime time)
        {
            // Calculate seconds from view start
            double seconds = (time - _viewStartTime).TotalSeconds;

            // Convert to pixels
            return seconds * _pixelsPerSecond;
        }

        private DateTime PositionToTime(double position)
        {
            // Calculate seconds from position
            double seconds = position / _pixelsPerSecond;

            // Convert to time
            return _viewStartTime.AddSeconds(seconds);
        }

        private void UpdateZoom(double zoomLevel)
        {
            // Calculate pixels per second based on zoom level
            // At zoom level 1, we show 24 hours in the available width
            // At zoom level 24, we show 1 hour in the available width
            double viewportWidth = 800; // Default width

            if (TimelineScrollViewer != null)
            {
                viewportWidth = TimelineScrollViewer.ActualWidth > 0 ?
                    TimelineScrollViewer.ActualWidth : 800;
            }

            // Hours to display in viewport at this zoom level
            double hoursInView = 24 / zoomLevel;

            // Calculate pixels per second (increase for better visibility)
            _pixelsPerSecond = viewportWidth / (hoursInView * 3600);

            // Make sure we have a minimum value for good dragging
            _pixelsPerSecond = Math.Max(_pixelsPerSecond, 0.05);

            // Re-render timeline
            RenderTimeline();
        }

        #endregion

        #region Position Handle Interaction

        private void PositionHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Start position handle dragging
            _isPositionHandleDragging = true;
            _handleDragStartX = e.GetCurrentPoint(TimelineContainer).Position.X;

            // Capture pointer for dragging
            PositionHandle.CapturePointer(e.Pointer);

            e.Handled = true;
        }

        private void PositionHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isPositionHandleDragging)
            {
                // Get current position
                Point point = e.GetCurrentPoint(TimelineContainer).Position;

                // Calculate new position 
                double newPosition = Math.Max(0, Math.Min(point.X, _timelineWidth));

                // Convert position to time
                DateTime newTime = PositionToTime(newPosition);

                // Update the internal current time
                _currentTime = newTime;

                // Update UI
                UpdateCurrentPositionLine();

                // Notify time selected - will be used to update video playback
                TimeSelected?.Invoke(this, newTime);

                e.Handled = true;
            }
        }

        private void PositionHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isPositionHandleDragging)
            {
                // End dragging
                _isPositionHandleDragging = false;
                PositionHandle.ReleasePointerCapture(e.Pointer);

                // Get final position
                Point point = e.GetCurrentPoint(TimelineContainer).Position;

                // Calculate new position (clamped to timeline width)
                double newPosition = Math.Max(0, Math.Min(point.X, _timelineWidth));

                // Convert position to time
                DateTime newTime = PositionToTime(newPosition);

                // Update current time
                _currentTime = newTime;

                // Update UI
                UpdateCurrentPositionLine();

                // Find segment at this position
                var segment = _timelineData?.GetSegmentAtTime(newTime);

                if (segment != null)
                {
                    // Notify segment selected
                    SegmentSelected?.Invoke(this, segment);
                }
                else
                {
                    // Just notify time selected
                    TimeSelected?.Invoke(this, newTime);
                }

                e.Handled = true;
            }
        }

        private void PositionHandle_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            // Reset dragging state
            _isPositionHandleDragging = false;
        }

        #endregion

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

        private void TimelineScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            // Update the navigator view to reflect current scroll position
            UpdateNavigatorViewFromScroll();

            // If we're dragging the scrollbar, update view bounds
            if (!e.IsIntermediate)
            {
                double scrollPosition = TimelineScrollViewer.HorizontalOffset;
                double viewportWidth = TimelineScrollViewer.ViewportWidth;

                // Calculate visible time range
                DateTime visibleStart = PositionToTime(scrollPosition);
                DateTime visibleEnd = PositionToTime(scrollPosition + viewportWidth);

                // For debugging
                // System.Diagnostics.Debug.WriteLine($"Visible: {visibleStart:HH:mm:ss} - {visibleEnd:HH:mm:ss}");
            }
        }

        private void SegmentsCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Start selection
            Point point = e.GetCurrentPoint(SegmentsCanvas).Position;

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
            // Only update if selection is active and pointer is captured
            if (_isSelectionActive && SegmentsCanvas.PointerCaptures?.Any(c => c.PointerId == e.Pointer.PointerId) == true)
            {
                // Get current position
                Point point = e.GetCurrentPoint(SegmentsCanvas).Position;

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

        #region Timeline Drag and Navigator Handlers

        // Enable dragging the timeline directly
        private void TimelineContainer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Only handle direct interaction with the timeline container, not its children
            if (e.OriginalSource == TimelineContainer ||
                e.OriginalSource == TicksCanvas ||
                e.OriginalSource == SelectionCanvas)
            {
                _isDragging = true;
                _dragStartPoint = e.GetCurrentPoint(TimelineContainer).Position;
                _dragStartOffset = TimelineScrollViewer.HorizontalOffset;
                TimelineContainer.CapturePointer(e.Pointer);

                // Prevent event propagation
                e.Handled = true;
            }
        }


        private void TimelineContainer_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                Point currentPoint = e.GetCurrentPoint(TimelineContainer).Position;
                double horizontalDelta = _dragStartPoint.X - currentPoint.X;

                // Update scroll position
                double newOffset = _dragStartOffset + horizontalDelta;
                TimelineScrollViewer.ChangeView(newOffset, null, null);

                // Prevent event propagation
                e.Handled = true;
            }
        }


        private void TimelineContainer_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                TimelineContainer.ReleasePointerCapture(e.Pointer);
                e.Handled = true;
            }
        }

        private void TimelineContainer_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isDragging = false;
        }

        private double _dragStartPosition = 0;
        private double _lastScrollPosition = 0;

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            base.OnPointerMoved(e);

            // If timeline is being dragged
            if (_isDragging && TimelineContainer.PointerCaptures?.Any(c => c.PointerId == e.Pointer.PointerId) == true)
            {
                // Calculate the drag delta
                double currentX = e.GetCurrentPoint(TimelineContainer).Position.X;
                double delta = _dragStartPosition - currentX;

                // Calculate new scroll position
                double newPosition = _lastScrollPosition + delta;

                // Ensure it stays within bounds
                newPosition = Math.Max(0, Math.Min(newPosition, _timelineWidth - TimelineScrollViewer.ViewportWidth));

                // Apply the new scroll position
                TimelineScrollViewer.ChangeView(newPosition, null, null);

                e.Handled = true;
            }
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);

            // End timeline dragging
            if (_isDragging && TimelineContainer.PointerCaptures?.Any(c => c.PointerId == e.Pointer.PointerId) == true)
            {
                TimelineContainer.ReleasePointerCapture(e.Pointer);
                _isDragging = false;
                e.Handled = true;
            }
        }

        // Navigator drag handlers
        private void NavigatorView_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (NavigatorCanvas == null) return;

            // Start navigator dragging
            _isNavigatorDragging = true;
            _navigatorDragStartX = e.GetCurrentPoint(NavigatorCanvas).Position.X;
            NavigatorView.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void NavigatorView_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isNavigatorDragging || NavigatorCanvas == null || TimelineScrollViewer == null)
                return;
            // Calculate the new position
            double navigatorWidth = NavigatorCanvas.ActualWidth;
            if (navigatorWidth <= 0)
                navigatorWidth = this.ActualWidth;

            double currentX = e.GetCurrentPoint(NavigatorCanvas).Position.X;
            double newPosition = _navigatorViewPosition + (currentX - _navigatorDragStartX);

            // Ensure it stays within bounds
            newPosition = Math.Max(0, Math.Min(newPosition, navigatorWidth - _navigatorViewWidth));

            // Update the visual position
            Canvas.SetLeft(NavigatorView, newPosition);

            // Calculate the corresponding scroll position
            double scrollPercentage = newPosition / navigatorWidth;
            double newScrollPosition = scrollPercentage * _timelineWidth;

            // Apply the scroll
            TimelineScrollViewer.ChangeView(newScrollPosition, null, null);

            // Update drag start for the next move
            _navigatorDragStartX = currentX;
            _navigatorViewPosition = newPosition;

            e.Handled = true;

        }

        private void NavigatorView_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            // End navigator dragging
            if (_isNavigatorDragging)
            {
                _isNavigatorDragging = false;
                NavigatorView.ReleasePointerCapture(e.Pointer);
                e.Handled = true;
            }
        }

        #endregion
    }
}