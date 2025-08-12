using System.IO;
using BusBuddy.Core.Models;
using Serilog;
using System.Text;
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
            BusBuddy.Core.Models.RouteTimeSlot timeSlot)
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
            BusBuddy.Core.Models.RouteTimeSlot timeSlot,
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
            BusBuddy.Core.Models.RouteTimeSlot timeSlot,
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

                // Stops Section
                g.DrawString("Stops", sectionFont, textBrush, new PointF(20, y));
                y += 20f;
                if (!stops.Any())
                {
                    g.DrawString("(No stops added)", bodyFont, textBrush, new PointF(30, y));
                    y += 18f;
                }
                else
                {
                    foreach (var stop in stops.OrderBy(s => s.StopOrder).Take(25)) // limit to keep single page MVP
                    {
                        var eta = stop.EstimatedArrivalTime == default ? "--:--" : stop.EstimatedArrivalTime.ToString("HH:mm");
                        g.DrawString($"{stop.StopOrder,2}. {stop.StopName ?? "(Stop)"}  @ {eta}", bodyFont, textBrush, new PointF(30, y));
                        y += 14f;
                        if (y > page.GetClientSize().Height - 120f) break; // simple overflow guard MVP
                    }
                    y += 10f;
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
    }
}
