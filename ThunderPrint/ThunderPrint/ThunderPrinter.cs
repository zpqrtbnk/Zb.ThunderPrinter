using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ical.Net;
using Ical.Net.DataTypes;

namespace ThunderPrint
{
    public class ThunderPrinter
    {
        private const string DatabasePath = @"calendar-data\local.sqlite";

        private static readonly Regex UserPrefCalendarNameEx;
        public static readonly DateTime Epoch;

        private readonly string _profilePath;

        static ThunderPrinter()
        {
            UserPrefCalendarNameEx = new Regex("user_pref\\(\"calendar\\.registry\\.([a-zA-Z0-9-]+)\\.name\", \"([^\"]+)\"\\)", RegexOptions.Compiled);
            Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        public ThunderPrinter(string profilePath)
        {
            _profilePath = profilePath;
        }

        public Dictionary<Guid, string> GetCalendars()
        {
            var calendars = new Dictionary<Guid, string>();
            var prefsFilename = Path.Combine(_profilePath, "prefs.js");
            var prefsText = File.ReadAllText(prefsFilename);
            foreach (Match m in UserPrefCalendarNameEx.Matches(prefsText))
                calendars[Guid.Parse(m.Groups[1].Value)] = m.Groups[2].Value;
            return calendars;
        }

        public IEnumerable<Event> GetEvents(Dictionary<Guid, string> calendars)
        {
            var databaseFilename = Path.Combine(_profilePath, DatabasePath);
            using (var conn = new SQLiteConnection($"Data Source={databaseFilename};Version=3;"))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT e.cal_id, e.title, e.event_start, e.event_end, r.icalString, e.id, e.flags, e.event_start_tz, e.event_end_tz, e.recurrence_id
                        FROM cal_events e
                        LEFT OUTER JOIN cal_recurrence r ON e.id=r.item_id AND e.cal_id=r.cal_id
                        ORDER BY e.id;";

                    using (var reader = cmd.ExecuteReader())
                    {
                        var ereader = new EventReader(reader, calendars);
                        while (ereader.Read())
                        {
                            yield return ereader.GetEvent();
                        }
                    }
                }

                conn.Close();
            }
        }

        public IEnumerable<Occurrence> GetDayOccurences(Calendar calendar, DateTime day)
        {
            return calendar.GetOccurrences(day, day.AddDays(1).AddSeconds(-1))
                .OrderByDescending(x => x.GetEvent().IsAllDay)
                .ThenBy(x => x.Period.StartTime)
                .ThenBy(x => x.GetEvent().Name);
        }
    }
}
