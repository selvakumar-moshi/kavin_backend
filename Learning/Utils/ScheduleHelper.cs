using System.Globalization;

namespace LearningBackendAPI.Utils
{
    // Date / start time / end time scheduling shared by Live Classes and Quiz publishing.
    // "Local time" is the institute's time zone (LiveClass:TimeZone, defaults to India), not the server's.
    public static class ScheduleHelper
    {
        private static readonly string[] TimeFormats = { "HH:mm", "H:mm", "HH:mm:ss", "h:mm tt", "hh:mm tt", "h:mmtt", "hh:mmtt" };

        public static TimeZoneInfo ResolveTimeZone(string? configured)
        {
            foreach (var id in new[] { configured, "India Standard Time", "Asia/Kolkata" })
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }
            return TimeZoneInfo.Local;
        }

        public static DateTime ParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("Date is required");
            }
            var text = value.Trim();
            // An ISO date-time ("2026-11-20T00:00:00Z"): keep the calendar day as written, no time zone shifting
            if (text.Length >= 10 && DateTime.TryParseExact(text[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
            {
                return iso;
            }
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed.Date;
            }
            throw new InvalidOperationException("Date must look like 2026-11-20");
        }

        public static TimeSpan ParseTime(string? value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"{label} is required");
            }
            if (DateTime.TryParseExact(value.Trim().ToUpperInvariant(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed.TimeOfDay;
            }
            throw new InvalidOperationException($"{label} must look like 14:30 or 2:30 PM");
        }

        public static void EnsureEndAfterStart(TimeSpan start, TimeSpan end)
        {
            if (end <= start)
            {
                throw new InvalidOperationException("End time must be after the start time");
            }
        }

        // The institute-local date + time of day as a UTC instant
        public static DateTime ToUtc(DateTime date, TimeSpan timeOfDay, TimeZoneInfo zone)
        {
            var local = DateTime.SpecifyKind(date.Date + timeOfDay, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(local, zone);
        }
    }
}
