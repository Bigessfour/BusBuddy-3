using System.IO;
using BusBuddy.Core.Models;
using BusBuddy.Core.Models.Trips;
using Serilog;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Drawing;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Service for generating PDF reports using Syncfusion PDF libraries
    /// Provides professional PDF generation for various report types
    /// </summary>
    public class PdfReportService
    {
        private static readonly ILogger Logger = Log.ForContext<PdfReportService>();


        /// <summary>
        /// Driver trip ticket for a sports or field trip. Paper blanks are not persisted columns.
        /// Includes the permanent 1.5 hour pre/post-trip inspection pad on billed hours.
        /// </summary>
        public byte[] GenerateTripTicket(TripEvent trip, byte[]? mapImagePng = null)
        {
            ArgumentNullException.ThrowIfNull(trip);

            try
            {
                Logger.Information("Generating trip ticket PDF for {TripId}", trip.TripEventId);

                using var document = new PdfDocument();
                var page = document.Pages.Add();
                var graphics = page.Graphics;

                var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 18, PdfFontStyle.Bold);
                var headerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 13, PdfFontStyle.Bold);
                var bodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
                var labelFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);
                var accentColor = new PdfColor(11, 126, 200);
                var textColor = new PdfColor(33, 37, 41);
                var textBrush = new PdfSolidBrush(textColor);
                var accentBrush = new PdfSolidBrush(accentColor);

                var pageWidth = page.GetClientSize().Width;
                graphics.DrawRectangle(accentBrush, new RectangleF(0, 0, pageWidth, 56));
                graphics.DrawString("Bus Buddy — Trip Ticket", titleFont, PdfBrushes.White, new PointF(20, 10));
                var ticketNo = string.IsNullOrWhiteSpace(trip.ExternalTicketNo)
                    ? trip.TripEventId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : trip.ExternalTicketNo;
                graphics.DrawString($"Ticket #: {ticketNo}", headerFont, PdfBrushes.White, new PointF(20, 34));

                var currentY = 70f;
                if (mapImagePng != null && mapImagePng.Length > 0)
                {
                    try
                    {
                        using var imgStream = new MemoryStream(mapImagePng);
                        using var pdfBitmap = new PdfBitmap(imgStream);
                        const float targetWidth = 200f;
                        var imgHeight = pdfBitmap.Height > 0
                            ? (pdfBitmap.Height / (float)pdfBitmap.Width) * targetWidth
                            : 140f;
                        var imgRect = new RectangleF(pageWidth - targetWidth - 16f, 64f, targetWidth, imgHeight);
                        graphics.DrawRectangle(new PdfSolidBrush(new PdfColor(240, 240, 240)), imgRect);
                        graphics.DrawImage(pdfBitmap, imgRect);
                    }
                    catch (Exception imgEx)
                    {
                        Logger.Warning(imgEx, "Failed embedding map image into trip ticket");
                    }
                }

                graphics.DrawString("Trip details", headerFont, textBrush, new PointF(20, currentY));
                currentY += 22f;

                var origin = !string.IsNullOrWhiteSpace(trip.OriginName)
                    ? trip.OriginName
                    : trip.OriginLocation?.Name ?? "Not specified";
                var destination = !string.IsNullOrWhiteSpace(trip.DestinationName)
                    ? trip.DestinationName
                    : trip.Destination ?? "Not specified";
                var pickup = origin;
                var pax = trip.PlannedHeadcount ?? trip.StudentCount;
                var details = new[]
                {
                    ("Date:", trip.TripDate == default ? trip.LeaveTime.ToString("MMMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture) : trip.TripDate.ToString("MMMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture)),
                    ("Purpose:", trip.Purpose),
                    ("Group:", trip.GroupOrActivity ?? "Not specified"),
                    ("School:", trip.RequestingSchool ?? "Not specified"),
                    ("Origin:", origin),
                    ("Pickup point:", pickup),
                    ("Destination:", destination),
                    ("Leave time:", trip.StartTime.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture)),
                    ("Return time:", trip.EndTime.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture)),
                    ("Planned miles:", trip.PlannedMiles?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) ?? "Not specified"),
                    ("Path miles:", trip.PathMiles?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) ?? "Not calculated"),
                    ("Bus #:", trip.Vehicle?.BusNumber ?? trip.AssignedBusNumber ?? "Not assigned"),
                    ("Driver:", trip.Driver?.DriverName ?? "Not assigned"),
                    ("Estimated travelers:", pax.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("Driver hours:", $"{trip.DriverHours:0.00} (includes {TripEvent.PrePostTripInspectionHours:0.0}h pre/post-trip inspection)")
                };

                foreach (var (label, value) in details)
                {
                    graphics.DrawString(label, labelFont, textBrush, new PointF(24, currentY));
                    graphics.DrawString(value, bodyFont, textBrush, new PointF(150, currentY));
                    currentY += 16f;
                }

                currentY += 10f;
                graphics.DrawString("Driver report (fill in by hand)", headerFont, textBrush, new PointF(20, currentY));
                currentY += 20f;
                var blanks = new[]
                {
                    "Beginning mileage: ____________________",
                    "Ending mileage: ______________________",
                    "Total mileage: ________________________",
                    "Fuel entered: ________________________",
                    "Time departed: ________________________",
                    "Time arrived back at bus barn: ______"
                };
                foreach (var line in blanks)
                {
                    graphics.DrawString(line, bodyFont, textBrush, new PointF(24, currentY));
                    currentY += 18f;
                }

                if (!string.IsNullOrWhiteSpace(trip.TripNotes))
                {
                    currentY += 8f;
                    graphics.DrawString("Notes", headerFont, textBrush, new PointF(20, currentY));
                    currentY += 16f;
                    graphics.DrawString(trip.TripNotes, bodyFont, textBrush, new PointF(24, currentY));
                }

                var footerY = page.GetClientSize().Height - 28f;
                graphics.DrawString(
                    $"Generated {DateTime.Now:MMM dd, yyyy HH:mm} — Route is not a trip.",
                    bodyFont,
                    textBrush,
                    new PointF(20, footerY));

                using var stream = new MemoryStream();
                document.Save(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating trip ticket PDF");
                throw;
            }
        }

        /// <summary>
        /// Generates a concise PDF summary for a route.
        /// Includes: Header (route name/date/time slot), assignment summary, stop list, student roster.
        /// Documentation references:
        /// - Syncfusion PDF Getting Started / API (PdfDocument, PdfStandardFont, PdfBrushes)
        /// - https://help.syncfusion.com/cr/wpf/Syncfusion.Pdf.html
        /// </summary>
        public byte[] GenerateRouteSummaryReport(
            Route route,
            IEnumerable<RouteStop> stops,
            IEnumerable<Student> students,
            Bus? assignedBus,
            Driver? assignedDriver,
            BusBuddy.Core.Models.RouteTimeSlot timeSlot)
        {
            return GenerateRouteSummaryReportInternal(route, stops, students, assignedBus, assignedDriver, timeSlot, null, null);
        }

        /// <summary>
        /// Overload including optional map snapshot (PNG bytes) to embed into the PDF (upper-right corner below header).
        /// Map image embedding uses documented Syncfusion PdfBitmap (API reference: https://help.syncfusion.com/cr/wpf/Syncfusion.Pdf.Graphics.PdfBitmap.html)
        /// When mapImagePng is null, falls back to standard summary layout.
        /// </summary>
        public byte[] GenerateRouteSummaryReport(
            Route route,
            IEnumerable<RouteStop> stops,
            IEnumerable<Student> students,
            Bus? assignedBus,
            Driver? assignedDriver,
            BusBuddy.Core.Models.RouteTimeSlot timeSlot,
            byte[]? mapImagePng)
        {
            return GenerateRouteSummaryReportInternal(route, stops, students, assignedBus, assignedDriver, timeSlot, mapImagePng, null);
        }

        public byte[] GenerateRouteSummaryReport(
            Route route,
            IEnumerable<RouteStop> stops,
            IEnumerable<Student> students,
            Bus? assignedBus,
            Driver? assignedDriver,
            BusBuddy.Core.Models.RouteTimeSlot timeSlot,
            byte[]? mapImagePng,
            string? districtName)
        {
            return GenerateRouteSummaryReportInternal(route, stops, students, assignedBus, assignedDriver, timeSlot, mapImagePng, districtName);
        }

        private byte[] GenerateRouteSummaryReportInternal(
            Route route,
            IEnumerable<RouteStop> stops,
            IEnumerable<Student> students,
            Bus? assignedBus,
            Driver? assignedDriver,
            BusBuddy.Core.Models.RouteTimeSlot timeSlot,
            byte[]? mapImagePng,
            string? districtName)
        {
            ArgumentNullException.ThrowIfNull(route);
            stops ??= Array.Empty<RouteStop>();
            students ??= Array.Empty<Student>();

            try
            {
                Logger.Information("Generating route PDF summary for {Route} (Slot {Slot})", route.RouteName, timeSlot);
                return RouteSummaryPdfRenderer.Render(
                    route, stops, students, assignedBus, assignedDriver, timeSlot, mapImagePng, districtName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating route PDF summary for {RouteId}", route.RouteId);
                return Array.Empty<byte>();
            }
        }


        /// <summary>
        /// Generic tabular PDF used by operational reports (roster, fleet, drivers, etc.).
        /// </summary>
        public byte[] GenerateTabularReport(
            string title,
            IReadOnlyList<string> headers,
            IReadOnlyList<IReadOnlyList<string>> rows,
            string? notes = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
            headers ??= Array.Empty<string>();
            rows ??= Array.Empty<IReadOnlyList<string>>();
            Logger.Information("Generating tabular PDF {Title} Headers={HeaderCount} Rows={RowCount}", title, headers.Count, rows.Count);

            using var document = new PdfDocument();
            var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 16, PdfFontStyle.Bold);
            var labelFont = new PdfStandardFont(PdfFontFamily.Helvetica, 9, PdfFontStyle.Bold);
            var bodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 9);
            var accent = new PdfSolidBrush(new PdfColor(11, 126, 200));

            var page = document.Pages.Add();
            var g = page.Graphics;
            var pageWidth = page.GetClientSize().Width;
            var pageHeight = page.GetClientSize().Height;
            var colCount = Math.Max(1, headers.Count);
            var colWidth = (pageWidth - 32f) / colCount;
            var y = DrawTableChrome(g, title, notes, rows.Count, headers, titleFont, labelFont, bodyFont, accent, pageWidth, includeNotes: true);

            foreach (var row in rows)
            {
                if (y > pageHeight - 36f)
                {
                    page = document.Pages.Add();
                    g = page.Graphics;
                    pageWidth = page.GetClientSize().Width;
                    pageHeight = page.GetClientSize().Height;
                    colWidth = (pageWidth - 32f) / colCount;
                    y = DrawTableChrome(g, title, notes, rows.Count, headers, titleFont, labelFont, bodyFont, accent, pageWidth, includeNotes: false);
                }

                for (var i = 0; i < colCount && i < row.Count; i++)
                {
                    var cell = FitCell(bodyFont, row[i] ?? string.Empty, colWidth - 4f);
                    g.DrawString(cell, bodyFont, PdfBrushes.Black, new PointF(16 + (i * colWidth), y));
                }

                y += 13f;
            }

            using var ms = new MemoryStream();
            document.Save(ms);
            var bytes = ms.ToArray();
            Logger.Information("Tabular PDF {Title} generated Bytes={Bytes} Pages={Pages}", title, bytes.Length, document.Pages.Count);
            return bytes;
        }

        private static float DrawTableChrome(
            PdfGraphics g,
            string title,
            string? notes,
            int rowCount,
            IReadOnlyList<string> headers,
            PdfFont titleFont,
            PdfFont labelFont,
            PdfFont bodyFont,
            PdfBrush accent,
            float pageWidth,
            bool includeNotes)
        {
            g.DrawRectangle(accent, new RectangleF(0, 0, pageWidth, 44));
            g.DrawString($"Bus Buddy — {title}", titleFont, PdfBrushes.White, new PointF(16, 12));

            float y = 56f;
            g.DrawString($"Generated {DateTime.Now:yyyy-MM-dd HH:mm}    Rows: {rowCount}", bodyFont, PdfBrushes.Black, new PointF(16, y));
            y += 18f;

            if (includeNotes && !string.IsNullOrWhiteSpace(notes))
            {
                g.DrawString(FitCell(bodyFont, notes, pageWidth - 32f), bodyFont, PdfBrushes.Black, new PointF(16, y));
                y += 16f;
            }

            var colCount = Math.Max(1, headers.Count);
            var colWidth = (pageWidth - 32f) / colCount;
            for (var i = 0; i < headers.Count; i++)
            {
                g.DrawString(FitCell(labelFont, headers[i], colWidth - 4f), labelFont, PdfBrushes.Black, new PointF(16 + (i * colWidth), y));
            }

            return y + 14f;
        }

        private static string FitCell(PdfFont font, string text, float maxWidth)
        {
            if (string.IsNullOrEmpty(text) || font.MeasureString(text).Width <= maxWidth)
            {
                return text;
            }

            const string ellipsis = "...";
            var ellipsisWidth = font.MeasureString(ellipsis).Width;
            if (ellipsisWidth >= maxWidth)
            {
                return ellipsis;
            }

            var available = maxWidth - ellipsisWidth;
            var lo = 0;
            var hi = text.Length;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) / 2;
                if (font.MeasureString(text[..mid]).Width <= available)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return lo == 0 ? ellipsis : text[..lo] + ellipsis;
        }

    }
}
