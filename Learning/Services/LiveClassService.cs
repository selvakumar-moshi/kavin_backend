using System.Globalization;
using LearningBackendAPI.DTOs;
using LearningBackendAPI.Models;
using LearningBackendAPI.Repositories;
using LearningBackendAPI.Utils;

namespace LearningBackendAPI.Services
{
    public class LiveClassService : ILiveClassService
    {
        private static readonly string[] TimeFormats = { "HH:mm", "H:mm", "HH:mm:ss", "h:mm tt", "hh:mm tt", "h:mmtt", "hh:mmtt" };

        private readonly ILiveClassRepository _repository;
        private readonly TimeZoneInfo _timeZone;

        public LiveClassService(ILiveClassRepository repository, IConfiguration configuration)
        {
            _repository = repository;
            _timeZone = ResolveTimeZone(configuration["LiveClass:TimeZone"]);
        }

        // "Local time" is the institute's time zone, not the server's (servers usually run in UTC).
        // Configure LiveClass:TimeZone (Windows or IANA id); defaults to India.
        private static TimeZoneInfo ResolveTimeZone(string? configured)
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

        private DateTime LocalNow() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);

        public async Task<LiveClass> CreateAsync(LiveClassRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                throw new InvalidOperationException("Title is required");
            }

            var date = ParseDate(request.Date);
            var start = ParseTime(request.StartTime, "Start time");
            var end = ParseTime(request.EndTime, "End time");
            EnsureEndAfterStart(start, end);

            var liveClass = new LiveClass
            {
                Title = request.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                StartTime = start.ToString(@"hh\:mm"),
                EndTime = end.ToString(@"hh\:mm"),
                CreatedAt = DateTime.UtcNow
            };
            // Starts as Not Started unless it was scheduled for a time that is already here or past
            liveClass.Status = Resolve(liveClass);

            return await _repository.CreateAsync(liveClass);
        }

        public async Task<List<LiveClass>> GetAllAsync(bool isAdmin)
        {
            var all = await _repository.GetAllAsync();
            foreach (var liveClass in all)
            {
                liveClass.Status = Resolve(liveClass);
            }

            return all
                .Where(l => isAdmin || l.Status != Constants.LiveClassStatuses.Expired)
                .OrderBy(l => l.Date, StringComparer.Ordinal)
                .ThenBy(l => l.StartTime, StringComparer.Ordinal)
                .ToList();
        }

        public async Task<LiveClass> GetByIdAsync(string id, bool isAdmin)
        {
            var liveClass = await _repository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Live class not found");
            liveClass.Status = Resolve(liveClass);

            if (!isAdmin && liveClass.Status == Constants.LiveClassStatuses.Expired)
            {
                throw new KeyNotFoundException("Live class not found");
            }
            return liveClass;
        }

        public async Task<LiveClass> UpdateAsync(string id, LiveClassRequest request)
        {
            var existing = await _repository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Live class not found");

            if (request.Title != null)
            {
                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    throw new InvalidOperationException("Title can't be empty");
                }
                existing.Title = request.Title.Trim();
            }
            if (request.Description != null)
            {
                existing.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            }
            if (!string.IsNullOrWhiteSpace(request.Date))
            {
                existing.Date = ParseDate(request.Date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            if (!string.IsNullOrWhiteSpace(request.StartTime))
            {
                existing.StartTime = ParseTime(request.StartTime, "Start time").ToString(@"hh\:mm");
            }
            if (!string.IsNullOrWhiteSpace(request.EndTime))
            {
                existing.EndTime = ParseTime(request.EndTime, "End time").ToString(@"hh\:mm");
            }
            EnsureEndAfterStart(
                TimeSpan.Parse(existing.StartTime, CultureInfo.InvariantCulture),
                TimeSpan.Parse(existing.EndTime, CultureInfo.InvariantCulture));

            // Rescheduling brings an Expired class back to Not Started / Online automatically
            existing.Status = Resolve(existing);
            await _repository.UpdateAsync(id, existing);
            return existing;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            _ = await _repository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Live class not found");
            return await _repository.DeleteAsync(id);
        }

        // Not Started before startTime, Online from startTime until endTime, Expired once endTime is reached
        private string Resolve(LiveClass liveClass)
        {
            var date = DateTime.ParseExact(liveClass.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var start = date + TimeSpan.Parse(liveClass.StartTime, CultureInfo.InvariantCulture);
            var end = date + TimeSpan.Parse(liveClass.EndTime, CultureInfo.InvariantCulture);
            var now = LocalNow();

            if (now >= end)
            {
                return Constants.LiveClassStatuses.Expired;
            }
            return now >= start ? Constants.LiveClassStatuses.Online : Constants.LiveClassStatuses.NotStarted;
        }

        private static DateTime ParseDate(string? value)
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

        private static TimeSpan ParseTime(string? value, string label)
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

        private static void EnsureEndAfterStart(TimeSpan start, TimeSpan end)
        {
            if (end <= start)
            {
                throw new InvalidOperationException("End time must be after the start time");
            }
        }
    }
}
