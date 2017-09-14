using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using iText.IO.Log;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Ical.Net;
using Ical.Net.DataTypes;
using Path = System.IO.Path;

namespace ThunderPrint
{
    // https://dxr.mozilla.org/comm-central/source/calendar/providers/storage/calStorageCalendar.js
    // https://dxr.mozilla.org/comm-central/source/obj-x86_64-pc-linux-gnu/dist/bin/extensions/%7Be2fda1a4-762b-4020-b5ad-a41df1933103%7D/modules/calStorageHelpers.jsm

    public class Program
    {
        private const string ProfilesPath = @"%USERPROFILE%\AppData\Roaming\Thunderbird\Profiles";

        private static readonly PdfFont Font1 = PdfFontFactory.CreateFont(@"c:\Windows\Fonts\LTe50920_0.ttf", embedded: true);
        private static readonly PdfFont Font2 = PdfFontFactory.CreateFont(@"c:\Windows\Fonts\LTe50872_0.ttf", embedded: true);

        private static readonly Color Color1 = new DeviceCmyk(0, 0, 0, 10);
        private static readonly Color Color2 = new DeviceCmyk(10, 0, 0, 0);
        private static readonly Color Color3 = new DeviceCmyk(0, 0, 0, 0);


        private static string GetProfile()
        {
            var profilesPath = Environment.ExpandEnvironmentVariables(ProfilesPath);

            var s = string.Empty;
            foreach (var d in Directory.EnumerateDirectories(profilesPath))
            {
                if (s == string.Empty)
                    s = d;
                else
                    return null;
            }
            return s;
        }

        public static void Main(string[] args)
        {
            string profile;
            switch (args.Length)
            {
                case 0:
                    profile = GetProfile();
                    if (profile == null) return;
                    break;
                case 1:
                    var profilesPath = Environment.ExpandEnvironmentVariables(ProfilesPath);
                    profile = Path.Combine(profilesPath, args[0]);
                    break;
                default:
                    Console.WriteLine("Usage.");
                    return;
            }

            var tp = new ThunderPrinter(profile);
            var calendars = tp.GetCalendars();

            var calendar = new Calendar();
            foreach (var e in tp.GetEvents(calendars))
                calendar.Events.Add(e);

            Console.WriteLine("Got {0} events.", calendar.Events.Count);
            Console.WriteLine("Creating PDF...");

            LoggerFactory.BindFactory(new NoOpLoggerFactory());

            var pdfDoc = new PdfDocument(new PdfWriter("calendar.pdf"));
            var doc = new Document(pdfDoc, PageSize.A4.Rotate());
            doc.SetMargins(20, 20, 20, 20);

            var month = DateTime.Now.Date;
            month = month.AddDays(1 - month.Day); // first day of this month

            for (var i = 0; i < 6; i++)
            {
                if (i > 0)
                    doc.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));

                doc.Add(new Paragraph(month.ToString("MMMM").ToUpper())
                    .SetFont(Font2).SetBold());

                var table = GetMonthTable(tp, calendar, month);
                doc.Add(table);

                month = month.AddMonths(1);
            }

            doc.Close();

            Process.Start("calendar.pdf");

            Console.WriteLine("Done.");
        }

        private static Table GetMonthTable(ThunderPrinter tp, Calendar calendar, DateTime month)
        {
            var day = month;
            float topM, rowH;
            if (month.DayOfWeek == DayOfWeek.Monday)
            {
                topM = 0;
                var c = month.AddMonths(1).AddDays(-1).Day - day.Day + 1;
                var rows = Math.Ceiling(Convert.ToSingle(c) / 7);
                rowH = Convert.ToSingle(490f / rows);
            }
            else
            {
                topM = -20f;
                while (day.DayOfWeek != DayOfWeek.Monday) day = day.AddDays(1);
                var c = month.AddMonths(1).AddDays(-1).Day - day.Day + 1;
                var rows = Math.Ceiling(Convert.ToSingle(c) / 7) + 1;
                rowH = Convert.ToSingle(510f / rows);
            }

            var table = new Table(new float[] { 1, 1, 1, 1, 1, 1, 1 })
                .SetFixedLayout()
                .SetWidthPercent(100)
                .SetMarginTop(topM);

            day = month;
            while (day.DayOfWeek != DayOfWeek.Monday) day = day.AddDays(-1);
            table.StartNewRow();
            while (day.Month != month.Month)
            {
                table.AddCell(new Cell()
                    .SetHeight(rowH).SetBorder(Border.NO_BORDER));
                day = day.AddDays(1);
            }

            var allDays = new List<Occurrence>();
            var today = DateTime.Now.Date;

            while (day.Month == month.Month)
            {
                if (day.DayOfWeek == DayOfWeek.Monday && day.Day != 1)
                {
                    table.StartNewRow();
                    allDays = new List<Occurrence>();
                }

                var cell = new Cell()
                    .SetHeight(rowH)
                    .Add(new Paragraph(day.Day.ToString("00"))
                        .SetMarginLeft(-1).SetMarginTop(-2).SetBold())
                    .SetFont(Font1).SetFontSize(7)
                    .SetWidthPercent(100);

                if (day < today)
                    cell.SetNextRenderer(new StrikedCellRenderer(cell));

                if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday)
                    cell.SetBackgroundColor(Color1);

                // this works but
                // still, cannot position another cell at bottom?
                var ctable = new Table(new float[] { 1 }).SetWidthPercent(100);
                ctable.StartNewRow();

                var occurences = tp.GetDayOccurences(calendar, day).ToList();

                var allDays2 = new List<Occurrence>();
                foreach (var occurrence in occurences) // gather today's all-day events
                {
                    var e = occurrence.GetEvent();
                    if (!e.IsAllDay) continue;
                    allDays2.Add(occurrence);
                }
                for (var i = 0; i < allDays.Count; i++) // clear know all-day events (remove those that are gone)
                {
                    var o = allDays[i];
                    if (o == null) continue;
                    var e = o.GetEvent();
                    if (allDays2.All(x => x.GetEvent().Uid != e.Uid))
                        allDays[i] = null;
                }
                for (var i = allDays.Count - 1; i >= 0; i--) // truncate the list (remove trailing nulls)
                {
                    if (allDays[i] != null) break;
                    allDays.RemoveAt(i);
                }
                var adc = 0;
                foreach (var o in allDays2) // add new all-day events
                {
                    var oUid = o.GetEvent().Uid;
                    var i = 0;
                    while (i < allDays.Count)
                    {
                        if (allDays[i] != null && allDays[i].GetEvent().Uid == oUid)
                            break;
                        i++;
                    }
                    if (i < allDays.Count)
                    {
                        allDays[i] = o;
                        continue;
                    }
                    while (adc < allDays.Count && allDays[adc] != null) adc++;
                    if (adc == allDays.Count)
                        allDays.Add(o);
                    else
                        allDays[adc] = o;
                }

                var ccell = new Cell().SetBorder(Border.NO_BORDER).SetWidthPercent(100);
                var top = 0;

                foreach (var o in allDays)
                {
                    if (o == null)
                    {
                        ccell.Add(new Paragraph("-")
                            .SetFontColor(Color3)
                            .SetMarginTop(0).SetMarginBottom(1).SetPaddingTop(-2).SetPaddingBottom(-1));
                    }
                    else
                    {
                        TextAlignment align;
                        var prefix = string.Empty;
                        var postfix = string.Empty;
                        if (o.Period.EndTime.Date == day.Date.AddDays(1))
                        {
                            align = TextAlignment.RIGHT;
                            if (o.Period.Duration > TimeSpan.FromDays(1))
                                postfix = " ]";
                        }
                        else if (o.Period.StartTime.Date == day.Date)
                        {
                            align = TextAlignment.LEFT;
                            if (o.Period.Duration > TimeSpan.FromDays(1))
                                prefix = "[ ";
                        }
                        else
                        {
                            align = TextAlignment.CENTER;
                        }

                        var e = o.GetEvent();

                        var para = new Paragraph()
                            .SetWidthPercent(100).SetTextAlignment(align).SetBackgroundColor(Color2)
                            .SetMarginTop(0).SetMarginBottom(1).SetPaddingTop(-2).SetPaddingBottom(-1)
                            .SetPaddingLeft(1).SetPaddingRight(1);
                        if (prefix != string.Empty)
                            para.Add(new Text(prefix));
                        para.Add(new Text(e.Name).SetItalic());
                        if (postfix != string.Empty)
                            para.Add(new Text(postfix));

                        ccell
                            .SetWidthPercent(100)
                            .Add(para);
                    }
                    top = 5; // we have at least 1 all days event
                }

                foreach (var occurrence in occurences)
                {
                    var e = occurrence.GetEvent();

                    var calendarName = e.Properties["CALNAME"].Value.ToString();

                    if (e.IsAllDay) continue;

                    if (calendarName == "Tasks")
                    {
                        ccell.Add(new Paragraph($"{occurrence.Period.StartTime.AsUtc:t} {e.Name}")
                            .SetFirstLineIndent(-5).SetMultipliedLeading(.9f)
                            .SetMarginLeft(5).SetMarginTop(top).SetMarginBottom(0).SetPaddingTop(0).SetPaddingBottom(0));
                    }
                    else
                    {
                        ccell.Add(new Paragraph($"{occurrence.Period.StartTime.AsUtc:t}-{occurrence.Period.EndTime.AsUtc:t} {e.Name}")
                            .SetFirstLineIndent(-5).SetMultipliedLeading(.9f)
                            .SetMarginLeft(5).SetMarginTop(top).SetMarginBottom(0).SetPaddingTop(0).SetPaddingBottom(0));
                    }

                    top = 0;
                }

                ctable.AddCell(ccell);
                cell.Add(ctable);

                table.AddCell(cell);
                day = day.AddDays(1);
            }

            while (day.DayOfWeek != DayOfWeek.Monday)
            {
                table.AddCell(new Cell()
                    .SetHeight(rowH).SetBorder(Border.NO_BORDER));
                day = day.AddDays(1);
            }

            return table;
        }
    }
}
