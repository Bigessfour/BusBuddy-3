using System.IO;
using Serilog;
using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;

namespace BusBuddy.Core.Services;

/// <summary>
/// Shared PdfGrid sheet used by operational tabular reports.
/// </summary>
public static class PdfTabularGridRenderer
{
    private static readonly ILogger Logger = Log.ForContext(typeof(PdfTabularGridRenderer));

    public static byte[] Render(
        string title,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        try
        {
            using var pdf = new PdfDocument();
            pdf.PageSettings.Margins.All = 36;
            var page = pdf.Pages.Add();
            var g = page.Graphics;
            var size = page.GetClientSize();
            var banner = new PdfColor(15, 76, 129);

            g.DrawRectangle(new PdfSolidBrush(banner), new RectangleF(0, 0, size.Width, 40));
            g.DrawString(
                title,
                new PdfStandardFont(PdfFontFamily.Helvetica, 16, PdfFontStyle.Bold),
                PdfBrushes.White,
                new PointF(0, 12));

            var grid = RouteSummaryPdfRenderer.NewGrid(Math.Max(1, headers.Count));
            RouteSummaryPdfRenderer.SetHeader(grid, headers.ToArray());
            if (rows.Count == 0)
            {
                var empty = new string[Math.Max(1, headers.Count)];
                empty[0] = "No rows";
                RouteSummaryPdfRenderer.AddRow(grid, empty);
            }
            else
            {
                foreach (var row in rows)
                {
                    var cells = new string[headers.Count];
                    for (var i = 0; i < headers.Count; i++)
                    {
                        cells[i] = i < row.Count ? row[i] ?? string.Empty : string.Empty;
                    }

                    RouteSummaryPdfRenderer.AddRow(grid, cells);
                }
            }

            RouteSummaryPdfRenderer.StyleGrid(grid, rightAlignFrom: headers.Count);
            var layout = new PdfGridLayoutFormat
            {
                Layout = PdfLayoutType.Paginate,
                Break = PdfLayoutBreakType.FitPage
            };
            var result = grid.Draw(page, new RectangleF(0, 52, size.Width, size.Height - 80), layout);

            if (!string.IsNullOrWhiteSpace(notes))
            {
                var notePage = result?.Page ?? page;
                var y = (result?.Bounds.Bottom ?? 52) + 14;
                if (y > notePage.GetClientSize().Height - 40)
                {
                    notePage = pdf.Pages.Add();
                    y = 16;
                }

                notePage.Graphics.DrawString(
                    notes,
                    new PdfStandardFont(PdfFontFamily.Helvetica, 9),
                    new PdfSolidBrush(new PdfColor(90, 98, 108)),
                    new RectangleF(0, y, notePage.GetClientSize().Width, 48));
            }

            using var stream = new MemoryStream();
            pdf.Save(stream);
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Tabular PDF failed Title={Title}", title);
            throw;
        }
    }
}
