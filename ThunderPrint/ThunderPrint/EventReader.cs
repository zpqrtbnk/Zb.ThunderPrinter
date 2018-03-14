using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
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
            var recurrenceId = _reader.IsDBNull(9) ? (long?) null : _reader.GetInt64(9);
            var stamp = _reader.IsDBNull(10) ? (long?) null : _reader.GetInt64(10);

            start = ApplyTimeZone(start, startTz);
            end = ApplyTimeZone(end, endTz);

            var e = new Event
            {
                DtStart = new CalDateTime(start),
                DtEnd = new CalDateTime(end),
                Name = title,
                Uid = id,
                DtStamp = stamp == null ? null : new CalDateTime(FromUnixTime(stamp.Value)),
                RecurrenceId = recurrenceId == null ? null : new CalDateTime(FromUnixTime(recurrenceId.Value))
            };

            e.Properties.Add(new CalendarProperty("CALNAME", calendarName));
            if ((flags & ItemFlags.AllDay) > 0)
                e.IsAllDay = true;

            GetICalString(e);

            _readReady = false;
            while (_reader.Read()) // read other iCal strings belonging to that event
            {
                // soon as ids differ, or recurrentId != null, it's another event
                if (_reader.GetString(5) == id && _reader.IsDBNull(9))
                {
                    // keep reading
                    GetICalString(e);
                }
                else
                {
                    // on to next event
                    _readReady = true;
                    while (_readReady && _reader.IsDBNull(1))
                    {
                        Console.WriteLine("Got NULL title for event \"{0}\", skipping.", _reader.GetString(5));
                        _readReady = _reader.Read();
                    }
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
                // for some reason, we need to round dates else iCal.NET ignores the exclusions
                if (e.ExceptionDates == null)
                    e.ExceptionDates = new List<IPeriodList>();
                var dt = DateTime.ParseExact(icalString.Substring("EXDATE:".Length), "yyyyMMddTHHmmssK", CultureInfo.InvariantCulture).Date;
                var item = new PeriodList { new Ical.Net.DataTypes.Period(new CalDateTime(dt)) };
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
                    Console.WriteLine("Failed to parse rule \"{0}\" for event \"{1}\", skipping rule.", icalString, e.Name);
                    return;
                }

                if (e.RecurrenceRules == null)
                    e.RecurrenceRules = new List<IRecurrencePattern>();
                e.RecurrenceRules.Add(rule);
            }
            else if (icalString.StartsWith("RDATE;"))
            {
                // fixme handle, or ?
                Console.WriteLine("What shall we do with RDATE? for event \"{1}\", skipping rule.", icalString, e.Name);
            }
            else
            {
                Console.WriteLine("Unknown rule \"{0}\" for event \"{1}\", skipping rule.", icalString, e.Name);
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