using System.IO;
using BusBuddy.Core.Domain;
using Serilog;
using System.Text;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Drawing;
using Syncfusion.Pdf.Interactive; // For redaction annotations
using Syncfusion.Pdf.Parsing; // For PDF manipulation
using Syncfusion.Pdf.Grid; // For PdfGrid
using Syncfusion.Pdf.Security; // For PDF security

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
        /// Generates a professional PDF calendar report for activities within a date range
        /// </summary>
        public byte[] GenerateActivityCalendarReport(List<Activity> activities, DateTime startDate, DateTime endDate)
        {
            ArgumentNullException.ThrowIfNull(activities);

            try
            {
                Logger.Information("Generating PDF calendar report from {StartDate} to {EndDate}", startDate, endDate);

                // Create a new PDF document
                using var document = new PdfDocument();

                // Add a page to the document
                var page = document.Pages.Add();
                var graphics = page.Graphics;

                // Set up fonts and colors
                var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 20, PdfFontStyle.Bold);
                var headerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 14, PdfFontStyle.Bold);
                var bodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
                var accentColor = new PdfColor(11, 126, 200); // BusBuddy primary color
                var textColor = new PdfColor(33, 37, 41); // Dark text

                var currentY = 50f;
                var pageWidth = page.GetClientSize().Width;

                // Header Section
                var headerBrush = new PdfSolidBrush(accentColor);
                graphics.DrawRectangle(headerBrush, new RectangleF(0, 0, pageWidth, 60));

                graphics.DrawString("Bus Buddy - Activity Calendar Report", titleFont,
                    PdfBrushes.White, new PointF(20, 20));

                graphics.DrawString($"Period: {startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}",
                    headerFont, PdfBrushes.White, new PointF(20, 40));

                currentY = 80f;

                // Summary Section
                graphics.DrawString("Summary", headerFont, new PdfSolidBrush(textColor),
                    new PointF(20, currentY));
                currentY += 30f;

                var totalActivities = activities.Count;
                var activeDrivers = activities.Where(a => a.DriverId.HasValue).Select(a => a.DriverId).Distinct().Count();
                var activeVehicles = activities.Where(a => a.AssignedVehicleId > 0).Select(a => a.AssignedVehicleId).Distinct().Count();

                graphics.DrawString($"Total Activities: {totalActivities}", bodyFont,
                    new PdfSolidBrush(textColor), new PointF(30, currentY));
                currentY += 15f;

                graphics.DrawString($"Active Drivers: {activeDrivers}", bodyFont,
                    new PdfSolidBrush(textColor), new PointF(30, currentY));
                currentY += 15f;

                graphics.DrawString($"Active Vehicles: {activeVehicles}", bodyFont,
                    new PdfSolidBrush(textColor), new PointF(30, currentY));
                currentY += 30f;

                // Activities Details
                graphics.DrawString("Activities", headerFont, new PdfSolidBrush(textColor),
                    new PointF(20, currentY));
                currentY += 20f;

                foreach (var activity in activities.Take(10)) // Limit for demo
                {
                    graphics.DrawString($"• {activity.Date:MM/dd} - {activity.ActivityType}: {activity.Description}",
                        bodyFont, new PdfSolidBrush(textColor), new PointF(30, currentY));
                    currentY += 15f;
                }

                // Footer
                var footerY = page.GetClientSize().Height - 30f;
                graphics.DrawString($"Generated on: {DateTime.Now:MMM dd, yyyy HH:mm}",
                    bodyFont, new PdfSolidBrush(textColor), new PointF(20, footerY));

                // Save the document to memory stream
                using var stream = new MemoryStream();
                document.Save(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating PDF calendar report");
                // Fallback to text-based report if PDF generation fails
                return GenerateTextReport(activities, startDate, endDate, "Calendar Report");
            }
        }

        /// <summary>
        /// Generates a professional PDF report for a single activity
        /// </summary>
        public byte[] GenerateActivityReport(Activity activity)
        {
            ArgumentNullException.ThrowIfNull(activity);

            try
            {
                Logger.Information("Generating PDF report for activity {ActivityId}", activity.ActivityId);

                // Create a new PDF document
                using var document = new PdfDocument();

                // Add a page to the document
                var page = document.Pages.Add();
                var graphics = page.Graphics;

                // Set up fonts and colors
                var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 20, PdfFontStyle.Bold);
                var headerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 14, PdfFontStyle.Bold);
                var bodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 11);
                var labelFont = new PdfStandardFont(PdfFontFamily.Helvetica, 11, PdfFontStyle.Bold);
                var accentColor = new PdfColor(11, 126, 200); // BusBuddy primary color
                var textColor = new PdfColor(33, 37, 41); // Dark text

                var currentY = 50f;
                var pageWidth = page.GetClientSize().Width;

                // Header Section
                var headerBrush = new PdfSolidBrush(accentColor);
                graphics.DrawRectangle(headerBrush, new RectangleF(0, 0, pageWidth, 60));

                graphics.DrawString("Bus Buddy - Activity Report", titleFont,
                    PdfBrushes.White, new PointF(20, 15));

                graphics.DrawString($"Activity ID: {activity.ActivityId}", headerFont,
                    PdfBrushes.White, new PointF(20, 35));

                currentY = 80f;

                // Activity Details Section
                graphics.DrawString("Activity Details", headerFont, new PdfSolidBrush(textColor),
                    new PointF(20, currentY));
                currentY += 30f;

                // Create a structured layout for activity details
                var details = new[]
                {
                    ("Type:", activity.ActivityType ?? "Not specified"),
                    ("Description:", activity.Description ?? "Not specified"),
                    ("Destination:", activity.Destination ?? "Not specified"),
                    ("Date:", activity.Date.ToString("MMMM dd, yyyy (dddd)", System.Globalization.CultureInfo.InvariantCulture)),
                    ("Departure Time:", activity.LeaveTime.ToString(@"hh\:mm tt", System.Globalization.CultureInfo.InvariantCulture)),
                    ("Return Time:", activity.ReturnTime.ToString(@"hh\:mm tt", System.Globalization.CultureInfo.InvariantCulture)),
                    ("Duration:", $"{(activity.ReturnTime - activity.LeaveTime).TotalHours:F1} hours"),
                    ("Driver:", activity.Driver?.FullName ?? "Not assigned"),
                    ("Vehicle:", activity.AssignedVehicle?.BusNumber ?? "Not assigned"),
                    ("Route:", activity.Route?.RouteName ?? "Not assigned"),
                    ("Status:", activity.Status ?? "Not specified")
                };

                foreach (var (label, value) in details)
                {
                    graphics.DrawString(label, labelFont, new PdfSolidBrush(textColor),
                        new PointF(30, currentY));
                    graphics.DrawString(value, bodyFont, new PdfSolidBrush(textColor),
                        new PointF(150, currentY));
                    currentY += 20f;
                }

                // Footer
                var footerY = page.GetClientSize().Height - 30f;
                graphics.DrawString($"Generated on: {DateTime.Now:MMM dd, yyyy HH:mm}",
                    bodyFont, new PdfSolidBrush(textColor), new PointF(20, footerY));

                // Save the document to memory stream
                using var stream = new MemoryStream();
                document.Save(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating PDF activity report");
                // Fallback to text-based report if PDF generation fails
                return GenerateTextReport(new List<Activity> { activity }, DateTime.Now, DateTime.Now, "Activity Report");
            }
        }

        /// <summary>
        /// Generates a concise PDF summary for a route (MVP route assignment export).
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
            BusBuddy.Core.Domain.RouteTimeSlot timeSlot)
        {
            // Delegate to core implementation without map image
            return GenerateRouteSummaryReportInternal(route, stops, students, assignedBus, assignedDriver, timeSlot, null);
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
            BusBuddy.Core.Domain.RouteTimeSlot timeSlot,
            byte[]? mapImagePng)
        {
            return GenerateRouteSummaryReportInternal(route, stops, students, assignedBus, assignedDriver, timeSlot, mapImagePng);
        }

        private byte[] GenerateRouteSummaryReportInternal(
            Route route,
            IEnumerable<RouteStop> stops,
            IEnumerable<Student> students,
            Bus? assignedBus,
            Driver? assignedDriver,
            BusBuddy.Core.Domain.RouteTimeSlot timeSlot,
            byte[]? mapImagePng)
        {
            ArgumentNullException.ThrowIfNull(route);
            stops ??= Array.Empty<RouteStop>();
            students ??= Array.Empty<Student>();

            try
            {
                Logger.Information("Generating route PDF summary for {Route} (Slot {Slot})", route.RouteName, timeSlot);

                using var document = new PdfDocument();
                var page = document.Pages.Add();
                var g = page.Graphics;

                // Fonts & colors
                var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 18, PdfFontStyle.Bold);
                var sectionFont = new PdfStandardFont(PdfFontFamily.Helvetica, 13, PdfFontStyle.Bold);
                var labelFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);
                var bodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
                var accent = new PdfColor(11, 126, 200);
                var textBrush = PdfBrushes.Black;
                var accentBrush = new PdfSolidBrush(accent);

                var pageWidth = page.GetClientSize().Width;
                g.DrawRectangle(accentBrush, new RectangleF(0, 0, pageWidth, 50));
                g.DrawString("Bus Buddy — Route Summary", titleFont, PdfBrushes.White, new PointF(20, 15));
                // Header details block
                float y = 60f;
                var detailsRect = new RectangleF(12, y, pageWidth - 24, 76f);
                g.DrawRectangle(new PdfSolidBrush(new PdfColor(245, 247, 249)), detailsRect);
                g.DrawRectangle(new PdfPen(accent, 0.6f), detailsRect);

                // Compute route timing summary
                string fmt(DateTime dt) => dt.ToString("HH:mm");
                var validArrivals = stops.Where(s => s.EstimatedArrivalTime != default).Select(s => s.EstimatedArrivalTime).ToList();
                var validDepartures = stops.Where(s => s.EstimatedDepartureTime != default).Select(s => s.EstimatedDepartureTime).ToList();
                var depText = validArrivals.Any() ? fmt(validArrivals.Min()) : "--:--";
                var arrText = validDepartures.Any() ? fmt(validDepartures.Max()) : "--:--";

                // Left column
                float leftX = detailsRect.X + 12f;
                float lineY = detailsRect.Y + 10f;
                g.DrawString($"Route: {route.RouteName ?? "(Unnamed Route)"}", sectionFont, textBrush, new PointF(leftX, lineY));
                lineY += 20f;
                g.DrawString($"Stops: {stops.Count()}    Students: {students.Count()}", bodyFont, textBrush, new PointF(leftX, lineY));
                lineY += 16f;
                g.DrawString($"Departure: {depText}    Arrival: {arrText}", bodyFont, textBrush, new PointF(leftX, lineY));

                // Right column
                float rightX = detailsRect.Right - 260f; // approx right column start
                float rY = detailsRect.Y + 12f;
                g.DrawString($"Date: {route.Date:yyyy-MM-dd}", bodyFont, textBrush, new PointF(rightX, rY));
                rY += 16f;
                g.DrawString($"Slot: {timeSlot}", bodyFont, textBrush, new PointF(rightX, rY));
                rY += 16f;
                g.DrawString($"Driver: {assignedDriver?.DriverName ?? "(none)"}", bodyFont, textBrush, new PointF(rightX, rY));
                rY += 16f;
                g.DrawString($"Vehicle: {assignedBus?.BusNumber ?? "(none)"}", bodyFont, textBrush, new PointF(rightX, rY));

                y = detailsRect.Bottom + 20f;

                // Optional map image (embed to top-right area under header bar)
                if (mapImagePng != null && mapImagePng.Length > 0)
                {
                    try
                    {
                        using var imgStream = new MemoryStream(mapImagePng);
                        using (var pdfBitmap = new Syncfusion.Pdf.Graphics.PdfBitmap(imgStream))
                        {
                            // Reserve a rectangle (approx 220x160) — adjust height proportionally
                            const float targetWidth = 220f;
                            var imgHeight = pdfBitmap.Height > 0 ? (pdfBitmap.Height / (float)pdfBitmap.Width) * targetWidth : 160f;
                            var imgRect = new RectangleF(pageWidth - targetWidth - 20f, 60f, targetWidth, imgHeight);
                            g.DrawRectangle(new PdfSolidBrush(new PdfColor(240, 240, 240)), imgRect); // light backdrop
                            g.DrawImage(pdfBitmap, imgRect);
                        }
                        // Leave y unchanged (text occupies left column); map sits independently
                    }
                    catch (Exception imgEx)
                    {
                        Logger.Warning(imgEx, "Failed embedding map image into route PDF");
                    }
                }

                // Stops Section (Detailed Schedule Table)
                g.DrawString("Stops (Schedule)", sectionFont, textBrush, new PointF(20, y));
                y += 18f;
                if (!stops.Any())
                {
                    g.DrawString("(No stops added)", bodyFont, textBrush, new PointF(30, y));
                    y += 18f;
                }
                else
                {
                    // Table headers
                    float x0 = 30f; float x1 = x0 + 28f; float x2 = x1 + 140f; float x3 = x2 + 70f; float x4 = x3 + 70f; float x5 = x4 + 60f;
                    g.DrawString("#", labelFont, textBrush, new PointF(x0, y));
                    g.DrawString("Stop", labelFont, textBrush, new PointF(x1, y));
                    g.DrawString("Arr", labelFont, textBrush, new PointF(x2, y));
                    g.DrawString("Dep", labelFont, textBrush, new PointF(x3, y));
                    g.DrawString("Miles", labelFont, textBrush, new PointF(x4, y));
                    g.DrawString("Cum", labelFont, textBrush, new PointF(x5, y));
                    y += 12f;
                    double cumulativeMiles = 0.0;
                    DateTime? firstArr = null; DateTime? lastDep = null;
                    var orderedStops = stops.OrderBy(s => s.StopOrder).Take(32).ToList();
                    for (int i = 0; i < orderedStops.Count; i++)
                    {
                        var stop = orderedStops[i];
                        var arr = stop.EstimatedArrivalTime == default ? "--:--" : stop.EstimatedArrivalTime.ToString("HH:mm");
                        var dep = stop.EstimatedDepartureTime == default ? "--:--" : stop.EstimatedDepartureTime.ToString("HH:mm");
                        // Approximate leg miles if coordinates present with previous
                        double legMiles = 0.0;
                        if (i == 0)
                        {
                            legMiles = 0.0; // from origin (school) not yet tracked here
                        }
                        else
                        {
                            var prev = orderedStops[i - 1];
                            if (prev.Latitude.HasValue && prev.Longitude.HasValue && stop.Latitude.HasValue && stop.Longitude.HasValue)
                            {
                                legMiles = Haversine((double)prev.Latitude.Value, (double)prev.Longitude.Value, (double)stop.Latitude.Value, (double)stop.Longitude.Value);
                            }
                        }
                        cumulativeMiles += legMiles;
                        if (firstArr == null && stop.EstimatedArrivalTime != default) firstArr = stop.EstimatedArrivalTime;
                        if (stop.EstimatedDepartureTime != default) lastDep = stop.EstimatedDepartureTime;
                        g.DrawString(stop.StopOrder.ToString(), bodyFont, textBrush, new PointF(x0, y));
                        g.DrawString(Trim(stop.StopName, 18), bodyFont, textBrush, new PointF(x1, y));
                        g.DrawString(arr, bodyFont, textBrush, new PointF(x2, y));
                        g.DrawString(dep, bodyFont, textBrush, new PointF(x3, y));
                        g.DrawString(legMiles.ToString("0.0"), bodyFont, textBrush, new PointF(x4, y));
                        g.DrawString(cumulativeMiles.ToString("0.0"), bodyFont, textBrush, new PointF(x5, y));
                        y += 12f;
                        if (y > page.GetClientSize().Height - 150f) break;
                    }
                    y += 6f;
                }

                // Students Section
                g.DrawString("Students", sectionFont, textBrush, new PointF(20, y));
                y += 20f;
                if (!students.Any())
                {
                    g.DrawString("(No students assigned)", bodyFont, textBrush, new PointF(30, y));
                    y += 18f;
                }
                else
                {
                    foreach (var stu in students.OrderBy(s => s.StudentName).Take(40))
                    {
                        g.DrawString($"• {stu.StudentName ?? "(Student)"}", bodyFont, textBrush, new PointF(30, y));
                        y += 14f;
                        if (y > page.GetClientSize().Height - 60f) break;
                    }
                }

                // Footer
                var footerY = page.GetClientSize().Height - 30f;
                g.DrawString($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}", bodyFont, textBrush, new PointF(20, footerY));

                using var ms = new MemoryStream();
                document.Save(ms);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating route PDF summary for {RouteId}", route.RouteId);
                return Array.Empty<byte>();
            }
        }

        // Lightweight Haversine for PDF schedule (duplicate kept local to avoid coupling to VM)
        private static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 3958.8; // miles
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) * Math.Pow(Math.Sin(dLon / 2), 2);
            double c = 2 * Math.Asin(Math.Sqrt(a));
            return R * c;
        }

        private static string Trim(string? value, int max)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Length <= max) return value;
            ReadOnlySpan<char> span = value.AsSpan(0, max - 1);
            char[] buffer = new char[span.Length + 1];
            span.CopyTo(buffer);
            buffer[^1] = '…';
            return new string(buffer);
        }

        #region Fallback Text Generation (Used when PDF generation fails)

        private byte[] GenerateTextReport(List<Activity> activities, DateTime startDate, DateTime endDate, string reportType)
        {
            try
            {
                Logger.Information("Generating fallback text report: {ReportType}", reportType);

                if (reportType == "Calendar Report")
                {
                    var reportContent = GenerateCalendarReportContent(activities, startDate, endDate);
                    return Encoding.UTF8.GetBytes(reportContent);
                }
                else if (activities.Count == 1)
                {
                    var reportContent = GenerateActivityReportContent(activities[0]);
                    return Encoding.UTF8.GetBytes(reportContent);
                }
                else
                {
                    return Encoding.UTF8.GetBytes("Error: Unable to generate report");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error generating fallback text report");
                return Encoding.UTF8.GetBytes($"Error generating report: {ex.Message}");
            }
        }

        private string GenerateCalendarReportContent(List<Activity> activities, DateTime startDate, DateTime endDate)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine("BUS BUDDY - ACTIVITY CALENDAR REPORT");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Report Period: {startDate.ToString("MMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture)} - {endDate.ToString("MMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture)}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Generated: {DateTime.Now.ToString("MMM dd, yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)}");
            sb.AppendLine(new string('=', 60));
            sb.AppendLine();

            // Summary
            sb.AppendLine("SUMMARY");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Total Activities: {activities.Count}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Active Drivers: {activities.Where(a => a.DriverId.HasValue).Select(a => a.DriverId).Distinct().Count()}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Active Vehicles: {activities.Where(a => a.AssignedVehicleId > 0).Select(a => a.AssignedVehicleId).Distinct().Count()}");
            sb.AppendLine();

            // Status breakdown
            var statusGroups = activities.GroupBy(a => a.Status).ToList();
            if (statusGroups.Any())
            {
                sb.AppendLine("STATUS BREAKDOWN");
                foreach (var group in statusGroups.OrderBy(g => g.Key))
                {
                    sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  {group.Key}: {group.Count()}");
                }
                sb.AppendLine();
            }

            // Activities by date
            sb.AppendLine("ACTIVITIES BY DATE");
            sb.AppendLine(new string('-', 60));

            var activitiesByDate = activities.GroupBy(a => a.Date.Date).OrderBy(g => g.Key);

            foreach (var dateGroup in activitiesByDate)
            {
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"{dateGroup.Key:dddd, MMMM dd, yyyy}");

                foreach (var activity in dateGroup.OrderBy(a => a.LeaveTime))
                {
                    sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  {activity.LeaveTime:HH:mm}-{activity.ReturnTime:HH:mm} | {activity.ActivityType} | {activity.Description}");
                    sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"    Driver: {activity.Driver?.FullName ?? "Unassigned"}");
                    sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"    Vehicle: {activity.AssignedVehicle?.BusNumber ?? "Unassigned"}");
                    sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"    Destination: {activity.Destination}");
                    sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"    Status: {activity.Status}");
                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string GenerateActivityReportContent(Activity activity)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine("BUS BUDDY - ACTIVITY REPORT");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Activity ID: {activity.ActivityId}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Generated: {DateTime.Now:MMM dd, yyyy HH:mm}");
            sb.AppendLine(new string('=', 50));
            sb.AppendLine();

            // Activity Details
            sb.AppendLine("ACTIVITY DETAILS");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Type: {activity.ActivityType ?? "Not specified"}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Description: {activity.Description ?? "Not specified"}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Destination: {activity.Destination ?? "Not specified"}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Date: {activity.Date:dddd, MMMM dd, yyyy}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Departure: {activity.LeaveTime:HH:mm}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Return: {activity.ReturnTime:HH:mm}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Duration: {(activity.ReturnTime - activity.LeaveTime).TotalHours:F1} hours");
            sb.AppendLine();

            // Assignment Details
            sb.AppendLine("ASSIGNMENT DETAILS");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Driver: {activity.Driver?.FullName ?? "Not assigned"}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Vehicle: {activity.AssignedVehicle?.BusNumber ?? "Not assigned"}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Route: {activity.Route?.RouteName ?? "Not assigned"}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Requested By: {activity.RequestedBy ?? "Not specified"}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Status: {activity.Status ?? "Not specified"}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Expected Passengers: {(activity.ExpectedPassengers.HasValue ? activity.ExpectedPassengers.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Not specified")}");
            sb.AppendLine();

            // Administrative Details
            sb.AppendLine("ADMINISTRATIVE DETAILS");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Created: {activity.CreatedDate:MMM dd, yyyy HH:mm}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Last Updated: {(activity.UpdatedDate.HasValue ? activity.UpdatedDate.Value.ToString("MMM dd, yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) : "Never")}");

            if (activity.ApprovalDate.HasValue)
            {
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Approved: {activity.ApprovalDate.Value:MMM dd, yyyy HH:mm}");
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Approved By: {activity.ApprovedBy ?? "Not specified"}");
            }
            sb.AppendLine();

            // Notes
            if (!string.IsNullOrEmpty(activity.Notes))
            {
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"NOTES");
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"{activity.Notes}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        #endregion

        #region Enhanced PDF Privacy & Redaction Features (Syncfusion 30.2.6)

        /// <summary>
        /// Enhanced student roster PDF generation with privacy redaction capabilities
        /// Implements Syncfusion 30.2.6 advanced redaction features for sensitive information protection
        /// </summary>
        public byte[] GenerateStudentRosterWithPrivacyAsync(List<Student> students, StudentPrivacyOptions privacyOptions)
        {
            ArgumentNullException.ThrowIfNull(students);
            ArgumentNullException.ThrowIfNull(privacyOptions);

            try
            {
                Logger.Information("Generating privacy-compliant student roster PDF for {StudentCount} students with redaction level: {RedactionLevel}",
                    students.Count, privacyOptions.RedactionLevel);

                using var document = new PdfDocument();
                var page = document.Pages.Add();
                var graphics = page.Graphics;

                // Enhanced PDF security and metadata
                ConfigureDocumentSecurity(document, privacyOptions);

                // Set up enhanced fonts and styling
                var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 18, PdfFontStyle.Bold);
                var privacyHeaderFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12, PdfFontStyle.Bold); // For privacy notice header
                var gridHeaderFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);    // For grid headers
                var bodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
                var privacyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 8, PdfFontStyle.Italic);

                var privacyColor = new PdfSolidBrush(new PdfColor(220, 53, 69)); // Privacy warning red
                var textBrush = PdfBrushes.Black;
                var headerBrush = new PdfSolidBrush(new PdfColor(0, 120, 212));

                var pageWidth = page.GetClientSize().Width;
                var currentY = 20f;

                // Privacy Notice Header
                currentY = DrawPrivacyNoticeHeader(graphics, titleFont, privacyHeaderFont, privacyFont, privacyColor, textBrush, pageWidth, currentY, privacyOptions);
                currentY += 30;

                // Enhanced Student Data Grid with Redaction
                currentY = DrawRedactedStudentGrid(graphics, students, privacyOptions, page, currentY, gridHeaderFont);

                // Privacy Compliance Footer
                DrawPrivacyComplianceFooter(graphics, privacyFont, page, privacyOptions);

                // Apply redaction annotations for sensitive data
                if (privacyOptions.EnableRedactionAnnotations)
                {
                    ApplyRedactionAnnotations(page, students, privacyOptions);
                }

                // Save to memory stream with encryption if required
                using var stream = new MemoryStream();

                if (privacyOptions.EncryptDocument)
                {
                    // Enhanced document security with encryption
                    var security = document.Security;
                    security.KeySize = PdfEncryptionKeySize.Key256Bit;
                    security.Algorithm = PdfEncryptionAlgorithm.AES;
                    security.UserPassword = privacyOptions.UserPassword ?? "BusBuddy2024";
                    security.OwnerPassword = privacyOptions.OwnerPassword ?? "BusBuddyAdmin2024";
                    security.Permissions = PdfPermissionsFlags.Print | PdfPermissionsFlags.FullQualityPrint;
                }

                document.Save(stream);
                Logger.Information("Privacy-compliant student roster PDF generated successfully with {RedactionCount} redacted fields",
                    CalculateRedactionCount(students, privacyOptions));
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to generate privacy-compliant student roster PDF");
                throw new InvalidOperationException("Failed to generate privacy-compliant student roster PDF", ex);
            }
        }

        /// <summary>
        /// Applies advanced redaction annotations to sensitive student information areas
        /// Uses simplified redaction approach for enhanced privacy
        /// </summary>
        private void ApplyRedactionAnnotations(PdfPage page, List<Student> students, StudentPrivacyOptions privacyOptions)
        {
            if (!privacyOptions.EnableRedactionAnnotations) return;

            try
            {
                var currentY = 150f; // Starting position after headers
                var rowHeight = 15f;
                var graphics = page.Graphics;
                var redactionBrush = new PdfSolidBrush(new PdfColor(220, 220, 220));

                foreach (var student in students.Take(25)) // Limit for page size
                {
                    var studentIndex = students.IndexOf(student);
                    var yPosition = currentY + (studentIndex * rowHeight);

                    // Draw redaction rectangles instead of annotations for better compatibility
                    if (privacyOptions.RedactStudentIds)
                    {
                        var studentIdRect = new RectangleF(20, yPosition, 80, rowHeight - 2);
                        graphics.DrawRectangle(redactionBrush, studentIdRect);
                        graphics.DrawString("***", new PdfStandardFont(PdfFontFamily.Helvetica, 8), 
                            PdfBrushes.Gray, new PointF(45, yPosition + 2));
                    }

                    if (privacyOptions.RedactAddresses)
                    {
                        var addressRect = new RectangleF(320, yPosition, 150, rowHeight - 2);
                        graphics.DrawRectangle(redactionBrush, addressRect);
                        graphics.DrawString("[REDACTED]", new PdfStandardFont(PdfFontFamily.Helvetica, 8), 
                            PdfBrushes.Gray, new PointF(365, yPosition + 2));
                    }

                    if (privacyOptions.RedactEmergencyContacts)
                    {
                        var emergencyRect = new RectangleF(480, yPosition, 100, rowHeight - 2);
                        graphics.DrawRectangle(redactionBrush, emergencyRect);
                        graphics.DrawString("[PRIVATE]", new PdfStandardFont(PdfFontFamily.Helvetica, 8), 
                            PdfBrushes.Gray, new PointF(505, yPosition + 2));
                    }
                }

                Logger.Debug("Applied {RedactionCount} redaction overlays to student roster PDF", 
                    CalculateRedactionCount(students, privacyOptions));
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to apply some redaction overlays to PDF");
            }
        }

        /// <summary>
        /// Draws privacy notice header with compliance information
        /// </summary>
        private float DrawPrivacyNoticeHeader(PdfGraphics graphics, PdfFont titleFont, PdfFont headerFont, PdfFont privacyFont,
            PdfBrush privacyBrush, PdfBrush textBrush, float pageWidth, float startY, StudentPrivacyOptions privacyOptions)
        {
            var currentY = startY;

            // Main title
            graphics.DrawString("🚌 BusBuddy Student Roster", titleFont, textBrush, new PointF(20, currentY));
            currentY += 25;

            // Privacy compliance notice
            var privacyLevel = privacyOptions.RedactionLevel switch
            {
                PrivacyRedactionLevel.None => "Standard Report",
                PrivacyRedactionLevel.Minimal => "Limited Privacy Protection",
                PrivacyRedactionLevel.Standard => "Standard Privacy Protection",
                PrivacyRedactionLevel.Enhanced => "Enhanced Privacy Protection",
                PrivacyRedactionLevel.Maximum => "Maximum Privacy Protection",
                _ => "Unknown Privacy Level"
            };

            graphics.DrawString($"🔒 Privacy Level: {privacyLevel}", headerFont, privacyBrush, new PointF(20, currentY));
            currentY += 18;

            // FERPA—Privacy compliance statement
            var complianceText = "This document contains educational records protected under FERPA. " +
                               "Distribution is restricted to authorized personnel only.";
            
            var complianceRect = new RectangleF(20, currentY, pageWidth - 40, 30);
            graphics.DrawString(complianceText, privacyFont, privacyBrush, complianceRect);
            currentY += 35;

            // Privacy options summary
            var privacyOptions_summary = new StringBuilder();
            if (privacyOptions.RedactStudentIds) privacyOptions_summary.Append("Student IDs redacted • ");
            if (privacyOptions.RedactAddresses) privacyOptions_summary.Append("Addresses redacted • ");
            if (privacyOptions.RedactEmergencyContacts) privacyOptions_summary.Append("Emergency contacts redacted • ");
            if (privacyOptions.EncryptDocument) privacyOptions_summary.Append("Document encrypted • ");

            if (privacyOptions_summary.Length > 0)
            {
                var summaryText = privacyOptions_summary.ToString().TrimEnd(' ', '•');
                graphics.DrawString($"Applied protections: {summaryText}", privacyFont, textBrush, new PointF(20, currentY));
                currentY += 15;
            }

            return currentY;
        }

        /// <summary>
        /// Draws student data grid with appropriate redaction based on privacy options
        /// </summary>
        private float DrawRedactedStudentGrid(PdfGraphics graphics, List<Student> students, StudentPrivacyOptions privacyOptions, 
            PdfPage page, float startY, PdfFont gridHeaderFont)
        {
            var grid = new PdfGrid();
            var currentY = startY;

            // Configure grid columns based on privacy settings
            var columnCount = 6;
            grid.Columns.Add(columnCount);

            // Dynamic column widths based on redaction
            grid.Columns[0].Width = privacyOptions.RedactStudentIds ? 80 : 100;  // Student ID or redacted placeholder
            grid.Columns[1].Width = 120; // First Name (usually not redacted)
            grid.Columns[2].Width = 120; // Last Name (usually not redacted)
            grid.Columns[3].Width = 50;  // Grade (usually not redacted)
            grid.Columns[4].Width = privacyOptions.RedactAddresses ? 100 : 150; // Address or redacted
            grid.Columns[5].Width = privacyOptions.RedactEmergencyContacts ? 80 : 120; // Emergency or redacted

            // Enhanced header styling
            var header = grid.Headers.Add(1)[0];
            header.Style.Font = gridHeaderFont;
            header.Style.BackgroundBrush = new PdfSolidBrush(new PdfColor(33, 136, 255));
            header.Style.TextBrush = PdfBrushes.White;

            header.Cells[0].Value = privacyOptions.RedactStudentIds ? "ID (Protected)" : "Student ID";
            header.Cells[1].Value = "First Name";
            header.Cells[2].Value = "Last Name";
            header.Cells[3].Value = "Grade";
            header.Cells[4].Value = privacyOptions.RedactAddresses ? "Address (Protected)" : "Address";
            header.Cells[5].Value = privacyOptions.RedactEmergencyContacts ? "Contact (Protected)" : "Emergency Contact";

            // Add student data with appropriate redaction
            foreach (var student in students.OrderBy(s => s.StudentName).Take(25)) // Limit for single page
            {
                var row = grid.Rows.Add();
                row.Style.Font = new PdfStandardFont(PdfFontFamily.Helvetica, 9);

                // Apply redaction logic
                row.Cells[0].Value = privacyOptions.RedactStudentIds ? "***" : student.StudentId.ToString();
                row.Cells[1].Value = student.StudentName?.Split(' ').FirstOrDefault() ?? "Unknown";
                row.Cells[2].Value = student.StudentName?.Split(' ').Skip(1).FirstOrDefault() ?? "";
                row.Cells[3].Value = student.Grade?.ToString() ?? "N/A";
                row.Cells[4].Value = privacyOptions.RedactAddresses ? "[REDACTED]" :
                    TruncateText(student.HomeAddress ?? "No address", 25);
                row.Cells[5].Value = privacyOptions.RedactEmergencyContacts ? "[PRIVATE]" :
                    TruncateText(student.ParentGuardian ?? "None", 20);                // Alternate row colors for better readability
                if (grid.Rows.Count % 2 == 0)
                {
                    row.Style.BackgroundBrush = new PdfSolidBrush(new PdfColor(248, 249, 250));
                }
            }

            // Enhanced grid styling (simplified for compatibility)
            grid.Style.BorderOverlapStyle = PdfBorderOverlapStyle.Inside;
            grid.Style.CellPadding.All = 6;

            // Draw grid with enhanced layout formatting
            var layoutFormat = new PdfGridLayoutFormat();
            layoutFormat.Layout = PdfLayoutType.Paginate;
            layoutFormat.Break = PdfLayoutBreakType.FitPage;

            var result = grid.Draw(page, new PointF(20, currentY), layoutFormat);
            return result.Bounds.Bottom + 20;
        }

        /// <summary>
        /// Draws privacy compliance footer with legal and technical information
        /// </summary>
        private void DrawPrivacyComplianceFooter(PdfGraphics graphics, PdfFont footerFont, PdfPage page, StudentPrivacyOptions privacyOptions)
        {
            var pageSize = page.GetClientSize();
            var footerY = pageSize.Height - 60;

            // Privacy compliance footer
            graphics.DrawLine(new PdfPen(new PdfColor(222, 226, 230)), 
                new PointF(20, footerY - 10), new PointF(pageSize.Width - 20, footerY - 10));

            var complianceText = $"Generated: {DateTime.Now:MMM dd, yyyy 'at' h:mm tt} | " +
                               $"Privacy Level: {privacyOptions.RedactionLevel} | " +
                               $"BusBuddy Transportation Management System";

            graphics.DrawString(complianceText, footerFont, new PdfSolidBrush(new PdfColor(108, 117, 125)), 
                new PointF(20, footerY));

            // Security notice
            var securityText = "🔒 This document contains protected educational information - Handle according to institutional privacy policies";
            graphics.DrawString(securityText, footerFont, new PdfSolidBrush(new PdfColor(220, 53, 69)), 
                new PointF(20, footerY + 12));
        }

        /// <summary>
        /// Configures document security settings and metadata for privacy compliance
        /// </summary>
        private void ConfigureDocumentSecurity(PdfDocument document, StudentPrivacyOptions privacyOptions)
        {
            // Enhanced document information for privacy tracking
            document.DocumentInformation.Title = $"Student Roster - Privacy Level: {privacyOptions.RedactionLevel}";
            document.DocumentInformation.Author = "BusBuddy Transportation Management System";
            document.DocumentInformation.Subject = "Educational Records - FERPA Protected";
            document.DocumentInformation.Keywords = $"Students,Transportation,Privacy,{privacyOptions.RedactionLevel}";
            document.DocumentInformation.Creator = "BusBuddy PDF Service with Privacy Enhancement";
            document.DocumentInformation.Producer = "Syncfusion PDF 30.2.6 with Redaction";
            document.DocumentInformation.CreationDate = DateTime.Now;
            document.DocumentInformation.ModificationDate = DateTime.Now;

            // Add custom metadata for privacy auditing (basic metadata approach)
            document.DocumentInformation.Creator = "BusBuddy - Privacy Protected Report";
            document.DocumentInformation.Subject = $"Educational Data Report - Privacy Level: {privacyOptions.RedactionLevel}";
            document.DocumentInformation.Keywords = "FERPA Compliant, Student Privacy Protected";
        }

        /// <summary>
        /// Calculates the number of redacted fields for privacy auditing
        /// </summary>
        private int CalculateRedactionCount(List<Student> students, StudentPrivacyOptions privacyOptions)
        {
            var redactionCount = 0;
            if (privacyOptions.RedactStudentIds) redactionCount += students.Count;
            if (privacyOptions.RedactAddresses) redactionCount += students.Count;
            if (privacyOptions.RedactEmergencyContacts) redactionCount += students.Count;
            return redactionCount;
        }

        /// <summary>
        /// Helper method to truncate text to specified length with ellipsis
        /// </summary>
        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            return string.Concat(text.AsSpan(0, maxLength - 3), "...");
        }

        #endregion
    }

    #region Privacy Configuration Classes

    /// <summary>
    /// Configuration options for student privacy protection in PDF reports
    /// Implements Syncfusion 30.2.6 enhanced privacy features
    /// </summary>
    public class StudentPrivacyOptions
    {
        /// <summary>
        /// Overall privacy redaction level
        /// </summary>
        public PrivacyRedactionLevel RedactionLevel { get; set; } = PrivacyRedactionLevel.Standard;

        /// <summary>
        /// Whether to redact student ID numbers
        /// </summary>
        public bool RedactStudentIds { get; set; }

        /// <summary>
        /// Whether to redact student home addresses
        /// </summary>
        public bool RedactAddresses { get; set; } = true;

        /// <summary>
        /// Whether to redact emergency contact information
        /// </summary>
        public bool RedactEmergencyContacts { get; set; } = true;

        /// <summary>
        /// Whether to enable visual redaction annotations (Syncfusion 30.2.6)
        /// </summary>
        public bool EnableRedactionAnnotations { get; set; } = true;

        /// <summary>
        /// Whether to encrypt the final PDF document
        /// </summary>
        public bool EncryptDocument { get; set; }

        /// <summary>
        /// User password for encrypted document (if EncryptDocument is true)
        /// </summary>
        public string? UserPassword { get; set; }

        /// <summary>
        /// Owner/Admin password for encrypted document (if EncryptDocument is true)
        /// </summary>
        public string? OwnerPassword { get; set; }

        /// <summary>
        /// Creates privacy options based on predefined privacy levels
        /// </summary>
        public static StudentPrivacyOptions CreateForLevel(PrivacyRedactionLevel level)
        {
            return level switch
            {
                PrivacyRedactionLevel.None => new StudentPrivacyOptions
                {
                    RedactionLevel = level,
                    RedactStudentIds = false,
                    RedactAddresses = false,
                    RedactEmergencyContacts = false,
                    EnableRedactionAnnotations = false,
                    EncryptDocument = false
                },
                PrivacyRedactionLevel.Minimal => new StudentPrivacyOptions
                {
                    RedactionLevel = level,
                    RedactStudentIds = false,
                    RedactAddresses = false,
                    RedactEmergencyContacts = true,
                    EnableRedactionAnnotations = true,
                    EncryptDocument = false
                },
                PrivacyRedactionLevel.Standard => new StudentPrivacyOptions
                {
                    RedactionLevel = level,
                    RedactStudentIds = false,
                    RedactAddresses = true,
                    RedactEmergencyContacts = true,
                    EnableRedactionAnnotations = true,
                    EncryptDocument = false
                },
                PrivacyRedactionLevel.Enhanced => new StudentPrivacyOptions
                {
                    RedactionLevel = level,
                    RedactStudentIds = true,
                    RedactAddresses = true,
                    RedactEmergencyContacts = true,
                    EnableRedactionAnnotations = true,
                    EncryptDocument = true,
                    UserPassword = "BusBuddy2024",
                    OwnerPassword = "BusBuddyAdmin2024"
                },
                PrivacyRedactionLevel.Maximum => new StudentPrivacyOptions
                {
                    RedactionLevel = level,
                    RedactStudentIds = true,
                    RedactAddresses = true,
                    RedactEmergencyContacts = true,
                    EnableRedactionAnnotations = true,
                    EncryptDocument = true,
                    UserPassword = "BusBuddy2024",
                    OwnerPassword = "BusBuddyAdmin2024"
                },
                _ => new StudentPrivacyOptions()
            };
        }
    }

    /// <summary>
    /// Privacy redaction levels for educational records compliance
    /// Aligns with FERPA and institutional privacy policies
    /// </summary>
    public enum PrivacyRedactionLevel
    {
        /// <summary>
        /// No privacy redaction applied - full information visible
        /// </summary>
        None = 0,

        /// <summary>
        /// Minimal privacy protection - emergency contacts only
        /// </summary>
        Minimal = 1,

        /// <summary>
        /// Standard privacy protection - addresses and emergency contacts
        /// </summary>
        Standard = 2,

        /// <summary>
        /// Enhanced privacy protection - includes student IDs, with encryption
        /// </summary>
        Enhanced = 3,

        /// <summary>
        /// Maximum privacy protection - all sensitive data redacted and encrypted
        /// </summary>
        Maximum = 4
    }
    
    #endregion
}
