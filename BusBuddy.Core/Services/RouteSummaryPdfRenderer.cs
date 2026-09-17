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
/// </summary>
public static class RouteSummaryPdfRenderer
{
    private static readonly ILogger Logger = Log.ForContext(typeof(RouteSummaryPdfRenderer));

    private static readonly PdfColor Banner = new(15, 76, 129);
    private static readonly PdfColor Hairline = new(196, 160, 64);
    private static readonly PdfColor HeaderCell = new(15, 76, 129);
    private static readonly PdfColor Zebra = new(245, 247, 250);
    private static readonly PdfColor Rule = new(210, 216, 222);
    private static readonly PdfColor Body = new(33, 37, 41);
    private static readonly PdfColor Muted = new(90, 98, 108);

    /// <summary>Caption under an embedded District Map snapshot — published path, not live GPS.</summary>
    public const string PublishedPathCaption = "Published path (not live tracking)";

    public static byte[] Render(
        Route route,
        IEnumerable<RouteStop> stops,
        IEnumerable<Student> students,
        Bus? assignedBus,
        Driver? assignedDriver,
        RouteTimeSlot timeSlot,
        byte[]? mapImagePng = null,
        string? districtName = null)
    {
        ArgumentNullException.ThrowIfNull(route);
        var sheet = RouteSummarySheetBuilder.Build(
            route, stops, students, assignedBus, assignedDriver, timeSlot, districtName);

        try
        {
            using var pdf = new PdfDocument();
            pdf.PageSettings.Margins.All = 36;
            AttachFooter(pdf, sheet);

            var page = pdf.Pages.Add();
            var g = page.Graphics;
            var pageSize = page.GetClientSize();

            // PdfStandardFont Helvetica (incl. Regular) draws space glyphs at width 0 in this
            // Syncfusion pin — live 2026-09-17 sheet read as LamarHighSchoolSpecialNeedsRouteSheet.
            var titleFont = PdfSheetFonts.Bold(11);
            var labelFont = PdfSheetFonts.Bold(8);
            var valueFont = PdfSheetFonts.Regular(9);
            var sectionFont = PdfSheetFonts.Bold(11);
            var smallFont = PdfSheetFonts.Regular(8);
            var bodyBrush = new PdfSolidBrush(Body);
            var mutedBrush = new PdfSolidBrush(Muted);

            g.DrawRectangle(new PdfSolidBrush(Banner), new RectangleF(0, 0, pageSize.Width, 28));
            g.DrawRectangle(new PdfSolidBrush(Hairline), new RectangleF(0, 28, pageSize.Width, 1.2f));
            g.DrawString(sheet.Title, titleFont, PdfBrushes.White, new PointF(0, 8));

            float y = 38f;
            var mapWidth = 0f;
            var mapBlockBottom = y;
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
                    g.DrawString(
                        PublishedPathCaption,
                        PdfSheetFonts.Regular(7),
                        mutedBrush,
                        new RectangleF(imgRect.X, imgRect.Bottom + 2f, mapWidth, 14f));
                    mapBlockBottom = imgRect.Bottom + 16f;
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Route sheet map embed failed");
                    mapWidth = 0f;
                }
            }

            var metaWidth = pageSize.Width - (mapWidth > 0 ? mapWidth + 12f : 0);
            y = DrawMeta(g, labelFont, valueFont, smallFont, bodyBrush, mutedBrush, sheet, y, metaWidth);
            y = Math.Max(y, mapBlockBottom) + 10f;

            y = DrawSection(g, sectionFont, bodyBrush, "Stops", y);
            var stopGrid = BuildStopGrid(sheet);
            var layout = new PdfGridLayoutFormat
            {
                Layout = PdfLayoutType.Paginate,
                Break = PdfLayoutBreakType.FitPage
            };
            var stopResult = stopGrid.Draw(page, new RectangleF(0, y, pageSize.Width, pageSize.Height - y - 28), layout);
            y = (stopResult?.Bounds.Bottom ?? y) + 14f;

            var studentPage = stopResult?.Page ?? page;
            if (y > studentPage.GetClientSize().Height - 72)
            {
                studentPage = pdf.Pages.Add();
                y = 12f;
            }

            y = DrawSection(studentPage.Graphics, sectionFont, bodyBrush, "Students", y);
            if (!string.IsNullOrWhiteSpace(sheet.GenerateStopsOnlyNote))
            {
                studentPage.Graphics.DrawString(
                    sheet.GenerateStopsOnlyNote,
                    smallFont,
                    mutedBrush,
                    new RectangleF(0, y, studentPage.GetClientSize().Width, 28));
                y += 22f;
            }

            var studentGrid = BuildStudentGrid(sheet);
            studentGrid.Draw(
                studentPage,
                new RectangleF(0, y, studentPage.GetClientSize().Width, studentPage.GetClientSize().Height - y - 20),
                layout);

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

    private static void AttachFooter(PdfDocument pdf, RouteSummarySheet sheet)
    {
        var contentWidth = pdf.Pages.Count > 0
            ? pdf.Pages[0].GetClientSize().Width
            : Math.Max(72f, pdf.PageSettings.Size.Width - 72);
        var bounds = new RectangleF(0, 0, contentWidth, 22);

        var footer = new PdfPageTemplateElement(bounds);
        var font = PdfSheetFonts.Regular(8);
        var brush = new PdfSolidBrush(Muted);
        footer.Graphics.DrawLine(new PdfPen(Rule, 0.5f), new PointF(0, 2), new PointF(bounds.Width, 2));
        footer.Graphics.DrawString(
            $"{sheet.District}    generated {DateTime.Now:yyyy-MM-dd HH:mm}".ToUpperInvariant(),
            font,
            brush,
            new PointF(0, 6));

        var pageNumber = new PdfPageNumberField(font, brush);
        var pageCount = new PdfPageCountField(font, brush);
        var pages = new PdfCompositeField(font, brush, "PAGE {0} OF {1}", pageNumber, pageCount)
        {
            Bounds = new RectangleF(bounds.Width - 90, 6, 90, 12)
        };
        pages.Draw(footer.Graphics);
        pdf.Template.Bottom = footer;
    }

    private static float DrawMeta(
        PdfGraphics g,
        PdfFont labelFont,
        PdfFont valueFont,
        PdfFont smallFont,
        PdfBrush body,
        PdfBrush muted,
        RouteSummarySheet model,
        float y,
        float width)
    {
        g.DrawString(model.DisplayName, PdfSheetFonts.Bold(13), body, new PointF(0, y));
        y += 18f;
        if (model.IsGeneratedName && !string.IsNullOrWhiteSpace(model.FullRouteName))
        {
            g.DrawString(model.FullRouteName, smallFont, muted, new PointF(0, y));
            y += 11f;
        }

        var col = width / 2f;
        DrawPair(g, labelFont, valueFont, body, muted, "Date", model.ServiceDate, 0, y);
        DrawPair(g, labelFont, valueFont, body, muted, "School", model.School, col, y);
        y += 16f;
        DrawPair(g, labelFont, valueFont, body, muted, "Slot", model.SessionLabel, 0, y);
        DrawPair(g, labelFont, valueFont, body, muted, "Departure", model.DepartureText, col / 2f, y);
        DrawPair(g, labelFont, valueFont, body, muted, "Arrive school", model.ArrivalText, col, y);
        y += 16f;
        DrawPair(g, labelFont, valueFont, body, muted, "Driver", model.DriverLabel, 0, y);
        DrawPair(g, labelFont, valueFont, body, muted, "Vehicle", model.BusLabel, col, y);
        y += 16f;
        DrawPair(g, labelFont, valueFont, body, muted, "Stops", model.Stops.Count.ToString(), 0, y);
        DrawPair(g, labelFont, valueFont, body, muted, "Students", model.RosterCount.ToString(), col / 2f, y);
        DrawPair(g, labelFont, valueFont, body, muted, "Miles", model.TotalMilesText, col, y);
        return y + 14f;
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
        var heading = label.ToUpperInvariant();
        g.DrawString(heading, labelFont, muted, new PointF(x, y));
        var labelWidth = labelFont.MeasureString(heading).Width;
        g.DrawString(value, valueFont, body, new PointF(x + labelWidth + 6f, y));
    }

    private static float DrawSection(PdfGraphics g, PdfFont font, PdfBrush brush, string title, float y)
    {
        g.DrawString(title, font, brush, new PointF(0, y));
        return y + 14f;
    }

    private static PdfGrid BuildStopGrid(RouteSummarySheet model)
    {
        var grid = NewGrid(8);
        grid.Columns[0].Width = 22;
        grid.Columns[3].Width = 36;
        grid.Columns[4].Width = 36;
        grid.Columns[5].Width = 36;
        grid.Columns[6].Width = 36;
        grid.Columns[7].Width = 36;
        SetHeader(grid, "#", "Stop", "Address", "Arr", "Dep", "Miles", "Cum", "Riders");

        if (model.Stops.Count == 0)
        {
            AddRow(grid, string.Empty, "No stops on this route", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        }
        else
        {
            foreach (var stop in model.Stops)
            {
                AddRow(
                    grid,
                    stop.Sequence.ToString(),
                    stop.Name,
                    stop.Address,
                    stop.Arrival,
                    stop.Departure,
                    stop.Miles,
                    stop.Cumulative,
                    stop.Riders);
            }
        }

        StyleGrid(grid, rightAlignFrom: 3);
        return grid;
    }

    private static PdfGrid BuildStudentGrid(RouteSummarySheet model)
    {
        var grid = NewGrid(3);
        grid.Columns[1].Width = 48;
        SetHeader(grid, "Student", "Grade", "Stop");
        if (model.Students.Count == 0)
        {
            var empty = string.IsNullOrWhiteSpace(model.GenerateStopsOnlyNote)
                ? "(No students assigned)"
                : "Roster empty — generate stops only";
            AddRow(grid, empty, string.Empty, string.Empty);
        }
        else
        {
            foreach (var student in model.Students)
            {
                AddRow(grid, student.Name, student.Grade, student.Stop);
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
        grid.Style.CellPadding = new PdfPaddings(4, 4, 3, 3);
        grid.Style.Font = PdfSheetFonts.Regular(9);
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
            Font = PdfSheetFonts.Bold(8)
        };
        headerStyle.Borders.All = new PdfPen(HeaderCell, 0.4f);
        var cellPen = new PdfPen(Rule, 0.4f);
        var cellStyle = new PdfGridCellStyle
        {
            Font = PdfSheetFonts.Regular(9),
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
