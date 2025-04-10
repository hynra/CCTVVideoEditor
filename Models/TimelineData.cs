using System;
using System.Collections.Generic;
using System.Linq;

namespace CCTVVideoEditor.Models
{
    /// <summary>
    /// Represents the entire timeline of CCTV video segments for a day
    /// </summary>
    public class TimelineData
    {
        /// <summary>
        /// Collection of video segments in chronological order
        /// </summary>
        private List<VideoSegment> _segments;

        /// <summary>
        /// The date represented by this timeline
        /// </summary>
        public DateTime Date { get; private set; }

        /// <summary>
        /// The number of segments in this timeline
        /// </summary>
        public int SegmentCount => _segments.Count;

        /// <summary>
        /// Creates a new TimelineData for the specified date
        /// </summary>
        /// <param name="date">The date for this timeline</param>
        public TimelineData(DateTime date)
        {
            Date = date.Date; // Strip time part
            _segments = new List<VideoSegment>();
        }

        /// <summary>
        /// Creates a new TimelineData from a collection of video segments
        /// </summary>
        /// <param name="segments">Collection of video segments</param>
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

        /// <summary>
        /// Adds a video segment to the timeline
        /// </summary>
        /// <param name="segment">The segment to add</param>
        public void AddSegment(VideoSegment segment)
        {
            if (segment == null)
                return;

            _segments.Add(segment);

            // Keep segments sorted by start time
            _segments = _segments.OrderBy(s => s.StartTime).ToList();
        }

        /// <summary>
        /// Gets all segments in the timeline
        /// </summary>
        /// <returns>All video segments</returns>
        public IReadOnlyList<VideoSegment> GetAllSegments()
        {
            return _segments.AsReadOnly();
        }

        /// <summary>
        /// Gets the segment that contains the specified timestamp
        /// </summary>
        /// <param name="timestamp">The timestamp to look for</param>
        /// <returns>The matching segment, or null if not found</returns>
        public VideoSegment GetSegmentAtTime(DateTime timestamp)
        {
            return _segments.FirstOrDefault(s => s.ContainsTime(timestamp));
        }

        /// <summary>
        /// Gets segments within a time range
        /// </summary>
        /// <param name="startTime">Start of the range</param>
        /// <param name="endTime">End of the range</param>
        /// <returns>List of segments in the time range</returns>
        public List<VideoSegment> GetSegmentsInRange(DateTime startTime, DateTime endTime)
        {
            return _segments.Where(s =>
                (s.StartTime >= startTime && s.StartTime < endTime) || // Segment starts in range
                (s.EndTime > startTime && s.EndTime <= endTime) ||    // Segment ends in range
                (s.StartTime <= startTime && s.EndTime >= endTime)     // Segment contains range
            ).ToList();
        }

        /// <summary>
        /// Finds time gaps in the timeline (periods with no video footage)
        /// </summary>
        /// <returns>List of time ranges with no footage</returns>
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

        /// <summary>
        /// Gets the next segment after the specified segment
        /// </summary>
        /// <param name="currentSegment">Current segment</param>
        /// <returns>Next segment or null if this is the last one</returns>
        public VideoSegment GetNextSegment(VideoSegment currentSegment)
        {
            int index = _segments.IndexOf(currentSegment);
            if (index == -1 || index == _segments.Count - 1)
                return null;

            return _segments[index + 1];
        }

        /// <summary>
        /// Gets the previous segment before the specified segment
        /// </summary>
        /// <param name="currentSegment">Current segment</param>
        /// <returns>Previous segment or null if this is the first one</returns>
        public VideoSegment GetPreviousSegment(VideoSegment currentSegment)
        {
            int index = _segments.IndexOf(currentSegment);
            if (index <= 0)
                return null;

            return _segments[index - 1];
        }
    }
}