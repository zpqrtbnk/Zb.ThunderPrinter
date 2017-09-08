using System;
using System.Collections.Generic;
using System.Data.SQLite;
using Ical.Net;
using Ical.Net.DataTypes;
using Ical.Net.General;
using Ical.Net.Interfaces.DataTypes;
using NodaTime;

namespace ThunderPrint
{
    public class EventReader
    {
        private readonly SQLiteDataReader _reader;
        private readonly Dictionary<Guid, string> _calendars;
        private bool _readReady;

        public EventReader(SQLiteDataReader reader, Dictionary<Guid, string> calendars)
        {
            _reader = reader;
            _calendars = calendars;
        }

        public bool Read()
        {
            return _readReady || _reader.Read();
        }

        public Event GetEvent()
        {
            var calendarId = _reader.GetString(0);
            _calendars.TryGetValue(Guid.Parse(calendarId), out var calendarName);

            var title = _reader.GetString(1);
            var start = FromUnixTime(_reader.GetInt64(2));
            var end = FromUnixTime(_reader.GetInt64(3));
            var id = _reader.GetString(5);
            var flags = (ItemFlags)Convert.ToUInt32(_reader.GetInt64(6));
            var startTz = _reader.GetString(7);
            var endTz = _reader.GetString(8);
            //var recurrenceId = _reader.GetValue(9);

            start = ApplyTimeZone(start, startTz);
            end = ApplyTimeZone(end, endTz);

            var e = new Event
            {
                DtStart = new CalDateTime(start),
                DtEnd = new CalDateTime(end),
                Name = title,
                Uid = id
            };

            e.Properties.Add(new CalendarProperty("CALNAME", calendarName));
            if ((flags & ItemFlags.AllDay) > 0)
                e.IsAllDay = true;

            GetICalString(e);

            _readReady = false;
            while (_reader.Read())
            {
                if (_reader.GetString(5) == id)
                {
                    GetICalString(e);
                }
                else
                {
                    _readReady = true;
                    break;
                }
            }

            return e;
        }

        private void GetICalString(Event e)
        {
            if (_reader.IsDBNull(4)) return;

            var icalString = _reader.GetString(4).Trim(); // trim! bogus \n

            if (icalString.StartsWith("EXDATE:"))
            {
                // fixme - test that this actually works
                if (e.ExceptionDates == null)
                    e.ExceptionDates = new List<IPeriodList>();
                var item = new PeriodList(icalString);
                e.ExceptionDates.Add(item);
            }
            else if (icalString.StartsWith("RRULE:"))
            {
                RecurrencePattern rule;
                try
                {
                    rule = new RecurrencePattern(icalString);
                }
                catch
                {
                    Console.WriteLine("Failed to parse rule \"{0}\" for event \"{1}\", skipping.", icalString, e.Name);
                    return;
                }

                if (e.RecurrenceRules == null)
                    e.RecurrenceRules = new List<IRecurrencePattern>();
                e.RecurrenceRules.Add(rule);
            }
            else
            {
                Console.WriteLine("Unknown rule \"{0}\" for event \"{1}\", skipping.", icalString, e.Name);
            }
        }

        // fixme is it a UTC datetime?
        private static DateTime FromUnixTime(long unixTime)
        {
            return ThunderPrinter.Epoch.AddSeconds(unixTime / 1000000);
        }

        // fixme local datetime?
        private static DateTime ApplyTimeZone(DateTime dateTime, string timeZoneName)
        {
            var tzp = DateTimeZoneProviders.Tzdb;
            var instantNow = Instant.FromDateTimeUtc(dateTime);
            var tz = tzp.GetZoneOrNull(timeZoneName);
            if (tz != null)
            {
                var tzo = tz.GetUtcOffset(instantNow);
                dateTime += tzo.ToTimeSpan();
            }
            else if (timeZoneName.StartsWith("BEGIN:VTIMEZONE"))
            {
                // do nothing for now
                // fixme but..?
            }
            else if (timeZoneName != "floating")
                throw new Exception("TZ: " + timeZoneName);

            return dateTime;
        }
    }
}