using System.IO;
using BusBuddy.Core.Models;
using Serilog;
using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;

namespace BusBuddy.Core.Services;

/// <summary>
/// Route sheet PDF. Tables are Syncfusion <see cref="PdfGrid"/> (not hand-placed DrawString rows).
/// Docs: https://help.syncfusion.com/document-processing/pdf/pdf-library/net/create-pdf-file-in-wpf
/// </summary>
public static class RouteSummaryPdfRenderer
{
    private static readonly ILogger Logger = Log.ForContext(typeof(RouteSummaryPdfRenderer));

    private static readonly PdfColor Banner = new(15, 76, 129);
    private static readonly PdfColor BannerAccent = new(196, 160, 64);
    private static readonly PdfColor HeaderCell = new(15, 76, 129);
    private static readonly PdfColor Zebra = new(245, 247, 250);
    private static readonly PdfColor Rule = new(210, 216, 222);
    private static readonly PdfColor Body = new(33, 37, 41);
    private static readonly PdfColor Muted = new(90, 98, 108);

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
        var documentModel = RouteSummaryDocument.From(
            route, stops, students, assignedBus, assignedDriver, timeSlot);

        try
        {
            using var pdf = new PdfDocument();
            pdf.PageSettings.Margins.All = 36;
            var page = pdf.Pages.Add();
            var g = page.Graphics;
            var pageSize = page.GetClientSize();

            var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 16, PdfFontStyle.Bold);
            var labelFont = new PdfStandardFont(PdfFontFamily.Helvetica, 8, PdfFontStyle.Bold);
            var valueFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
            var sectionFont = new PdfStandardFont(PdfFontFamily.Helvetica, 11, PdfFontStyle.Bold);
            var smallFont = new PdfStandardFont(PdfFontFamily.Helvetica, 8);
            var white = PdfBrushes.White;
            var bodyBrush = new PdfSolidBrush(Body);
            var mutedBrush = new PdfSolidBrush(Muted);

            g.DrawRectangle(new PdfSolidBrush(Banner), new RectangleF(0, 0, pageSize.Width, 44));
            g.DrawRectangle(new PdfSolidBrush(BannerAccent), new RectangleF(0, 44, pageSize.Width, 3));
            g.DrawString("Route sheet", titleFont, white, new PointF(0, 8));
            g.DrawString(
                documentModel.ServiceDate + "  ·  " + documentModel.SlotLabel,
                valueFont,
                white,
                new PointF(0, 28));

            float y = 58f;
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
                    g.DrawRectangle(new PdfPen(Rule, 0.5f), imgRect);
                    g.DrawImage(bitmap, imgRect);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Route sheet map embed failed");
                    mapWidth = 0f;
                }
            }

            var metaWidth = pageSize.Width - (mapWidth > 0 ? mapWidth + 12f : 0);
            DrawMeta(g, labelFont, valueFont, bodyBrush, mutedBrush, documentModel, y, metaWidth);
            y += 88f;

            y = DrawSection(g, sectionFont, bodyBrush, "Stops", y);
            var stopGrid = BuildStopGrid(documentModel);
            var layout = new PdfGridLayoutFormat
            {
                Layout = PdfLayoutType.Paginate,
                Break = PdfLayoutBreakType.FitPage
            };
            var stopResult = stopGrid.Draw(page, new RectangleF(0, y, pageSize.Width, pageSize.Height - y - 36), layout);
            y = (stopResult?.Bounds.Bottom ?? y) + 16f;

            var studentPage = stopResult?.Page ?? page;
            if (y > studentPage.GetClientSize().Height - 80)
            {
                studentPage = pdf.Pages.Add();
                y = 16f;
            }

            y = DrawSection(studentPage.Graphics, sectionFont, bodyBrush, "Students", y);
            var studentGrid = BuildStudentGrid(documentModel);
            studentGrid.Draw(
                studentPage,
                new RectangleF(0, y, studentPage.GetClientSize().Width, studentPage.GetClientSize().Height - y - 28),
                layout);

            for (var pageIndex = 0; pageIndex < pdf.Pages.Count; pageIndex++)
            {
                var p = pdf.Pages[pageIndex];
                var footerY = p.GetClientSize().Height - 16f;
                p.Graphics.DrawLine(new PdfPen(Rule, 0.6f), new PointF(0, footerY - 10), new PointF(p.GetClientSize().Width, footerY - 10));
                p.Graphics.DrawString(
                    $"Generated {DateTime.Now:MMM d, yyyy h:mm tt}",
                    smallFont,
                    mutedBrush,
                    new PointF(0, footerY - 6));
                p.Graphics.DrawString(
                    $"Page {pageIndex + 1} of {pdf.Pages.Count}",
                    smallFont,
                    mutedBrush,
                    new PointF(p.GetClientSize().Width - 72, footerY - 6));
            }

            using var stream = new MemoryStream();
            pdf.Save(stream);
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

    internal static PdfGrid NewGrid(int columns)
    {
        var grid = new PdfGrid();
        grid.Columns.Add(columns);
        grid.Headers.Add(1);
        grid.Style.CellPadding = new PdfPaddings(5, 5, 4, 4);
        grid.Style.Font = new PdfStandardFont(PdfFontFamily.Helvetica, 8.5f);
        return grid;
    }

    internal static void SetHeader(PdfGrid grid, params string[] titles)
    {
        var header = grid.Headers[0];
        for (var i = 0; i < titles.Length && i < header.Cells.Count; i++)
        {
            header.Cells[i].Value = titles[i];
        }
    }

    internal static void AddRow(PdfGrid grid, params string[] values)
    {
        var row = grid.Rows.Add();
        for (var i = 0; i < values.Length && i < row.Cells.Count; i++)
        {
            row.Cells[i].Value = values[i];
        }
    }

    internal static void StyleGrid(PdfGrid grid, int rightAlignFrom)
    {
        var headerStyle = new PdfGridCellStyle
        {
            BackgroundBrush = new PdfSolidBrush(HeaderCell),
            TextBrush = PdfBrushes.White,
            Font = new PdfStandardFont(PdfFontFamily.Helvetica, 8, PdfFontStyle.Bold)
        };
        headerStyle.Borders.All = new PdfPen(HeaderCell, 0.4f);
        var cellPen = new PdfPen(Rule, 0.4f);
        var cellStyle = new PdfGridCellStyle
        {
            Font = new PdfStandardFont(PdfFontFamily.Helvetica, 8.5f),
            TextBrush = new PdfSolidBrush(Body)
        };
        cellStyle.Borders.All = cellPen;
        var zebraStyle = new PdfGridCellStyle
        {
            Font = cellStyle.Font,
            TextBrush = cellStyle.TextBrush,
            BackgroundBrush = new PdfSolidBrush(Zebra)
        };
        zebraStyle.Borders.All = cellPen;

        grid.Headers[0].ApplyStyle(headerStyle);
        var right = new PdfStringFormat(PdfTextAlignment.Right, PdfVerticalAlignment.Middle);
        var left = new PdfStringFormat(PdfTextAlignment.Left, PdfVerticalAlignment.Middle);
        for (var i = 0; i < grid.Headers[0].Cells.Count; i++)
        {
            grid.Headers[0].Cells[i].StringFormat = i >= rightAlignFrom ? right : left;
        }

        for (var r = 0; r < grid.Rows.Count; r++)
        {
            var row = grid.Rows[r];
            row.ApplyStyle(r % 2 == 1 ? zebraStyle : cellStyle);
            for (var i = 0; i < row.Cells.Count; i++)
            {
                row.Cells[i].StringFormat = i >= rightAlignFrom ? right : left;
            }
        }
    }
}
