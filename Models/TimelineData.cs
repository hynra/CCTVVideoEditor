using System;
using System.Collections.Generic;
using System.Linq;

namespace CCTVVideoEditor.Models
{
    public class TimelineData
    {
        private List<VideoSegment> _segments;

        public DateTime Date { get; private set; }

        public int SegmentCount => _segments.Count;

        public TimelineData(DateTime date)
        {
            Date = date.Date; // Strip time part
            _segments = new List<VideoSegment>();
        }

        public TimelineData(IEnumerable<VideoSegment> segments)
        {
            _segments = new List<VideoSegment>(segments);

            if (_segments.Count > 0)
            {
                // Set date based on the first segment's date
                Date = _segments.First().StartTime.Date;
            }
            else
            {
                Date = DateTime.Today;
            }

            // Sort segments by start time
            _segments = _segments.OrderBy(s => s.StartTime).ToList();
        }

        public void AddSegment(VideoSegment segment)
        {
            if (segment == null)
                return;

            _segments.Add(segment);

            // Keep segments sorted by start time
            _segments = _segments.OrderBy(s => s.StartTime).ToList();
        }

        public IReadOnlyList<VideoSegment> GetAllSegments()
        {
            return _segments.AsReadOnly();
        }

        public VideoSegment GetSegmentAtTime(DateTime timestamp)
        {
            return _segments.FirstOrDefault(s => s.ContainsTime(timestamp));
        }

        public List<VideoSegment> GetSegmentsInRange(DateTime startTime, DateTime endTime)
        {
            return _segments.Where(s =>
                (s.StartTime >= startTime && s.StartTime < endTime) || // Segment starts in range
                (s.EndTime > startTime && s.EndTime <= endTime) ||    // Segment ends in range
                (s.StartTime <= startTime && s.EndTime >= endTime)     // Segment contains range
            ).ToList();
        }

        public List<(DateTime start, DateTime end)> FindGaps()
        {
            var gaps = new List<(DateTime start, DateTime end)>();

            if (_segments.Count <= 1)
                return gaps;

            // Start day at midnight
            DateTime dayStart = Date;
            DateTime dayEnd = Date.AddDays(1);

            // Check gap at the beginning of the day
            if (_segments.First().StartTime > dayStart)
            {
                gaps.Add((dayStart, _segments.First().StartTime));
            }

            // Check gaps between segments
            for (int i = 0; i < _segments.Count - 1; i++)
            {
                VideoSegment current = _segments[i];
                VideoSegment next = _segments[i + 1];

                if (next.StartTime > current.EndTime)
                {
                    gaps.Add((current.EndTime, next.StartTime));
                }
            }

            // Check gap at the end of the day
            if (_segments.Last().EndTime < dayEnd)
            {
                gaps.Add((_segments.Last().EndTime, dayEnd));
            }

            return gaps;
        }

        public VideoSegment GetNextSegment(VideoSegment currentSegment)
        {
            int index = _segments.IndexOf(currentSegment);
            if (index == -1 || index == _segments.Count - 1)
                return null;

            return _segments[index + 1];
        }

        public VideoSegment GetPreviousSegment(VideoSegment currentSegment)
        {
            int index = _segments.IndexOf(currentSegment);
            if (index <= 0)
                return null;

            return _segments[index - 1];
        }
    }
}