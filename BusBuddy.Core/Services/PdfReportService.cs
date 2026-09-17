using System.IO;
using BusBuddy.Core.Models;
using BusBuddy.Core.Models.Trips;
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

        #endregion
    }
}
