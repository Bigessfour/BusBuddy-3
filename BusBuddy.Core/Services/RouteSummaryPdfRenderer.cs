using System.IO;
using BusBuddy.Core.Models;
using Serilog;
using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;

namespace BusBuddy.Core.Services;

/// <summary>
/// Route sheet PDF using the documented Syncfusion PdfGrid contract:
/// columns + header row, RepeatHeader, FitPage pagination, PaginateBounds,
/// PdfGridLayoutResult chaining, and PdfDocument.Template header/footer.
/// https://help.syncfusion.com/document-processing/pdf/pdf-library/net/pdfgrid
/// https://help.syncfusion.com/document-processing/pdf/pdf-library/net/working-with-headers-and-footers
/// https://help.syncfusion.com/document-processing/pdf/pdf-library/net/create-pdf-file-in-wpf
/// </summary>
public static class RouteSummaryPdfRenderer
{
    private static readonly ILogger Logger = Log.ForContext(typeof(RouteSummaryPdfRenderer));

    public static byte[] Render(
        Route route,
        IEnumerable<RouteStop> stops,
        IEnumerable<Student> students,
        Bus? assignedBus,
        Driver? assignedDriver,
        RouteTimeSlot timeSlot,
        byte[]? mapImagePng = null)
    {
        ArgumentNullException.ThrowIfNull(route);
        var model = RouteSummaryDocument.From(
            route, stops, students, assignedBus, assignedDriver, timeSlot);

        try
        {
            using var pdf = new PdfDocument();
            pdf.PageSettings.Margins.Top = PdfGridSupport.HeaderTemplateHeight + 12f;
            pdf.PageSettings.Margins.Bottom = PdfGridSupport.FooterTemplateHeight + 12f;
            pdf.PageSettings.Margins.Left = 36;
            pdf.PageSettings.Margins.Right = 36;
            pdf.DocumentInformation.Title = model.DisplayName;
            pdf.DocumentInformation.Author = "BusBuddy";
            PdfGridSupport.ApplyPageTemplates(
                pdf,
                "Route sheet",
                model.ServiceDate + "  \u00b7  " + model.SlotLabel);

            var page = pdf.Pages.Add();
            var g = page.Graphics;
            var pageSize = page.GetClientSize();

            var labelFont = new PdfStandardFont(PdfFontFamily.Helvetica, 8, PdfFontStyle.Bold);
            var valueFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
            var sectionFont = new PdfStandardFont(PdfFontFamily.Helvetica, 11, PdfFontStyle.Bold);
            var bodyBrush = new PdfSolidBrush(PdfGridSupport.Body);
            var mutedBrush = new PdfSolidBrush(PdfGridSupport.Muted);

            float y = 0f;
            var mapWidth = 0f;
            if (mapImagePng is { Length: > 0 })
            {
                try
                {
                    using var imgStream = new MemoryStream(mapImagePng);
                    using var bitmap = new PdfBitmap(imgStream);
                    mapWidth = 168f;
                    var imgHeight = bitmap.Height > 0
                        ? Math.Min(110f, bitmap.Height / (float)bitmap.Width * mapWidth)
                        : 90f;
                    var imgRect = new RectangleF(pageSize.Width - mapWidth, y, mapWidth, imgHeight);
                    g.DrawRectangle(new PdfPen(PdfGridSupport.Rule, 0.5f), imgRect);
                    g.DrawImage(bitmap, imgRect);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Route sheet map embed failed");
                    mapWidth = 0f;
                }
            }

            var metaWidth = pageSize.Width - (mapWidth > 0 ? mapWidth + 12f : 0);
            DrawMeta(g, labelFont, valueFont, bodyBrush, mutedBrush, model, y, metaWidth);
            y += 88f;

            y = DrawSection(g, sectionFont, bodyBrush, "Stops", y);
            var stopGrid = BuildStopGrid(model);
            var layout = PdfGridSupport.CreateLayoutFormat(page, y);
            var stopResult = stopGrid.Draw(
                page,
                new RectangleF(0, y, pageSize.Width, Math.Max(40f, pageSize.Height - y)),
                layout);

            var nextPage = stopResult?.Page ?? page;
            var nextY = (stopResult?.Bounds.Bottom ?? y) + 16f;
            if (nextY > nextPage.GetClientSize().Height - 64f)
            {
                nextPage = pdf.Pages.Add();
                nextY = 8f;
            }

            nextY = DrawSection(nextPage.Graphics, sectionFont, bodyBrush, "Students", nextY);
            var studentGrid = BuildStudentGrid(model);
            var studentLayout = PdfGridSupport.CreateLayoutFormat(nextPage, nextY);
            studentGrid.Draw(
                nextPage,
                new RectangleF(0, nextY, nextPage.GetClientSize().Width, Math.Max(40f, nextPage.GetClientSize().Height - nextY)),
                studentLayout);

            using var stream = new MemoryStream();
            pdf.Save(stream);
            pdf.Close(true);
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Route sheet PDF failed for {Route}", route.RouteName);
            throw;
        }
    }

    private static void DrawMeta(
        PdfGraphics g,
        PdfFont labelFont,
        PdfFont valueFont,
        PdfBrush body,
        PdfBrush muted,
        RouteSummaryDocument model,
        float y,
        float width)
    {
        g.DrawString(model.DisplayName, new PdfStandardFont(PdfFontFamily.Helvetica, 14, PdfFontStyle.Bold), body, new PointF(0, y));
        y += 18f;
        g.DrawString(model.School, valueFont, muted, new PointF(0, y));
        y += 16f;

        var col = width / 2f;
        DrawPair(g, labelFont, valueFont, body, muted, "Departure", model.DepartureText, 0, y);
        DrawPair(g, labelFont, valueFont, body, muted, "Arrive", model.ArrivalText, col, y);
        y += 22f;
        DrawPair(g, labelFont, valueFont, body, muted, "Driver", model.DriverLabel, 0, y);
        DrawPair(g, labelFont, valueFont, body, muted, "Vehicle", model.BusLabel, col, y);
        y += 22f;
        DrawPair(g, labelFont, valueFont, body, muted, "Stops", model.Stops.Count.ToString(), 0, y);
        DrawPair(g, labelFont, valueFont, body, muted, "Students", model.Students.Count.ToString(), col / 2f, y);
        DrawPair(g, labelFont, valueFont, body, muted, "Miles", model.TotalMilesText, col, y);
    }

    private static void DrawPair(
        PdfGraphics g,
        PdfFont labelFont,
        PdfFont valueFont,
        PdfBrush body,
        PdfBrush muted,
        string label,
        string value,
        float x,
        float y)
    {
        g.DrawString(label.ToUpperInvariant(), labelFont, muted, new PointF(x, y));
        g.DrawString(value, valueFont, body, new PointF(x, y + 9));
    }

    private static float DrawSection(PdfGraphics g, PdfFont font, PdfBrush brush, string title, float y)
    {
        g.DrawString(title, font, brush, new PointF(0, y));
        return y + 16f;
    }

    private static PdfGrid BuildStopGrid(RouteSummaryDocument model)
    {
        var grid = NewGrid(6);
        grid.Columns[0].Width = 28;
        grid.Columns[2].Width = 58;
        grid.Columns[3].Width = 58;
        grid.Columns[4].Width = 44;
        grid.Columns[5].Width = 44;
        SetHeader(grid, "#", "Stop", "Arr", "Dep", "Miles", "Cum");
        if (model.Stops.Count == 0)
        {
            AddRow(grid, string.Empty, "No stops on this route", string.Empty, string.Empty, string.Empty, string.Empty);
        }
        else
        {
            foreach (var stop in model.Stops)
            {
                AddRow(grid, stop.Sequence.ToString(), stop.Name, stop.Arrival, stop.Departure, stop.Miles, stop.Cumulative);
            }
        }

        StyleGrid(grid, rightAlignFrom: 2);
        return grid;
    }

    private static PdfGrid BuildStudentGrid(RouteSummaryDocument model)
    {
        var grid = NewGrid(3);
        grid.Columns[1].Width = 48;
        SetHeader(grid, "Student", "Grade", "Address");
        if (model.Students.Count == 0)
        {
            AddRow(grid, "No students assigned", string.Empty, string.Empty);
        }
        else
        {
            foreach (var student in model.Students)
            {
                AddRow(grid, student.Name, student.Grade, student.Address);
            }
        }

        StyleGrid(grid, rightAlignFrom: 99);
        return grid;
    }

    internal static PdfGrid NewGrid(int columns) => PdfGridSupport.CreateGrid(columns);
    internal static void SetHeader(PdfGrid grid, params string[] titles) => PdfGridSupport.SetHeader(grid, titles);
    internal static void AddRow(PdfGrid grid, params string[] values) => PdfGridSupport.AddRow(grid, values);
    internal static void StyleGrid(PdfGrid grid, int rightAlignFrom) => PdfGridSupport.StyleGrid(grid, rightAlignFrom);
}
