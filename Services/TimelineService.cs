using CCTVVideoEditor.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CCTVVideoEditor.Services
{
    public class TimelineService
    {
        private TimelineData _timelineData;
        private VideoSegment _currentSegment;
        private DateTime _currentPosition;

        // Events
        public event EventHandler<VideoSegment> CurrentSegmentChanged;
        public event EventHandler<DateTime> CurrentPositionChanged;

        public TimelineData TimelineData => _timelineData;

        public VideoSegment CurrentSegment => _currentSegment;

        public DateTime CurrentPosition => _currentPosition;

        public void Initialize(TimelineData timelineData)
        {
            _timelineData = timelineData;

            // Set initial position to start of day
            _currentPosition = _timelineData?.Date ?? DateTime.Today;

            // Find segment at current position
            if (_timelineData != null)
            {
                _currentSegment = _timelineData.GetSegmentAtTime(_currentPosition);
            }
            else
            {
                _currentSegment = null;
            }

            // Notify changes
            CurrentPositionChanged?.Invoke(this, _currentPosition);
            CurrentSegmentChanged?.Invoke(this, _currentSegment);
        }

        public bool SetPosition(DateTime position)
        {
            if (_timelineData == null)
                return false;

            Debug.WriteLine($"Setting position to: {position:HH:mm:ss}");
            // Update current position
            _currentPosition = position;

            // Find segment at position
            var segment = _timelineData.GetSegmentAtTime(position);

            // Only notify segment change if it's different
            if (segment != _currentSegment)
            {
                Debug.WriteLine($"Changing segment from {_currentSegment?.StartTime:HH:mm:ss} to {segment?.StartTime:HH:mm:ss}");
                _currentSegment = segment;
                CurrentSegmentChanged?.Invoke(this, _currentSegment);
            }

            // Notify position change
            CurrentPositionChanged?.Invoke(this, _currentPosition);

            // Return true if segment found
            return segment != null;
        }


        public void ForceSegmentChange(VideoSegment segment)
        {
            if (segment != null)
            {
                _currentSegment = segment;
                _currentPosition = segment.StartTime;

                // Notify changes
                CurrentSegmentChanged?.Invoke(this, _currentSegment);
                CurrentPositionChanged?.Invoke(this, _currentPosition);
            }
        }

        public bool MoveToNextSegment()
        {
            if (_timelineData == null || _currentSegment == null)
                return false;

            var nextSegment = _timelineData.GetNextSegment(_currentSegment);
            if (nextSegment != null)
            {
                _currentSegment = nextSegment;
                _currentPosition = nextSegment.StartTime;

                // Notify changes
                CurrentSegmentChanged?.Invoke(this, _currentSegment);
                CurrentPositionChanged?.Invoke(this, _currentPosition);

                return true;
            }

            return false;
        }

        public bool MoveToPreviousSegment()
        {
            if (_timelineData == null || _currentSegment == null)
                return false;

            var prevSegment = _timelineData.GetPreviousSegment(_currentSegment);
            if (prevSegment != null)
            {
                _currentSegment = prevSegment;
                _currentPosition = prevSegment.StartTime;

                // Notify changes
                CurrentSegmentChanged?.Invoke(this, _currentSegment);
                CurrentPositionChanged?.Invoke(this, _currentPosition);

                return true;
            }

            return false;
        }

        public void UpdatePositionWithinSegment(double offsetSeconds)
        {
            if (_currentSegment == null)
                return;

            // Calculate new position
            _currentPosition = _currentSegment.StartTime.AddSeconds(
                Math.Min(offsetSeconds, _currentSegment.Duration));

            // Notify position change
            CurrentPositionChanged?.Invoke(this, _currentPosition);
        }

        public List<VideoSegment> GetSegmentsInRange(DateTime startTime, DateTime endTime)
        {
            return _timelineData?.GetSegmentsInRange(startTime, endTime) ?? new List<VideoSegment>();
        }

        public VideoSegment FindSegmentAtTime(DateTime time)
        {
            return _timelineData?.GetSegmentAtTime(time);
        }

        public double GetTotalDurationInRange(DateTime startTime, DateTime endTime)
        {
            if (_timelineData == null)
                return 0;

            var segments = _timelineData.GetSegmentsInRange(startTime, endTime);
            double totalDuration = 0;

            foreach (var segment in segments)
            {
                // Calculate the overlap between segment and range
                DateTime overlapStart = segment.StartTime > startTime ? segment.StartTime : startTime;
                DateTime overlapEnd = segment.EndTime < endTime ? segment.EndTime : endTime;

                // Add overlap duration
                if (overlapEnd > overlapStart)
                {
                    totalDuration += (overlapEnd - overlapStart).TotalSeconds;
                }
            }

            return totalDuration;
        }

        public List<(DateTime start, DateTime end)> FindGapsInRange(DateTime startTime, DateTime endTime)
        {
            if (_timelineData == null)
                return new List<(DateTime, DateTime)>();

            // Get all gaps
            var allGaps = _timelineData.FindGaps();

            // Filter to gaps that overlap with range
            return allGaps.Where(gap =>
                (gap.start >= startTime && gap.start < endTime) || // Gap starts in range
                (gap.end > startTime && gap.end <= endTime) ||    // Gap ends in range
                (gap.start <= startTime && gap.end >= endTime)     // Gap contains range
            ).ToList();
        }
    }
}