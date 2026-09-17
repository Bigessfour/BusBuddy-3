using System.IO;
using Serilog;
using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;

namespace BusBuddy.Core.Services;

/// <summary>
/// Operational tabular PDF via the same documented PdfGrid path as the route sheet.
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
            pdf.PageSettings.Margins.Top = PdfGridSupport.HeaderTemplateHeight + 12f;
            pdf.PageSettings.Margins.Bottom = PdfGridSupport.FooterTemplateHeight + 12f;
            pdf.PageSettings.Margins.Left = 36;
            pdf.PageSettings.Margins.Right = 36;
            pdf.DocumentInformation.Title = title;
            pdf.DocumentInformation.Author = "BusBuddy";
            PdfGridSupport.ApplyPageTemplates(pdf, title, DateTime.Now.ToString("MMM d, yyyy"));

            var page = pdf.Pages.Add();
            var size = page.GetClientSize();
            var grid = PdfGridSupport.CreateGrid(Math.Max(1, headers.Count));
            PdfGridSupport.SetHeader(grid, headers.Count == 0 ? new[] { "Value" } : headers.ToArray());
            if (rows.Count == 0)
            {
                var empty = new string[Math.Max(1, headers.Count)];
                empty[0] = "No rows";
                PdfGridSupport.AddRow(grid, empty);
            }
            else
            {
                foreach (var row in rows)
                {
                    var cells = new string[Math.Max(1, headers.Count)];
                    for (var i = 0; i < cells.Length; i++)
                    {
                        cells[i] = i < row.Count ? row[i] ?? string.Empty : string.Empty;
                    }

                    PdfGridSupport.AddRow(grid, cells);
                }
            }

            PdfGridSupport.StyleGrid(grid, rightAlignFrom: headers.Count);
            var layout = PdfGridSupport.CreateLayoutFormat(page, 0);
            var result = grid.Draw(page, new RectangleF(0, 0, size.Width, size.Height), layout);

            if (!string.IsNullOrWhiteSpace(notes))
            {
                var notePage = result?.Page ?? page;
                var y = (result?.Bounds.Bottom ?? 0) + 14;
                if (y > notePage.GetClientSize().Height - 40)
                {
                    notePage = pdf.Pages.Add();
                    y = 8;
                }

                notePage.Graphics.DrawString(
                    notes,
                    new PdfStandardFont(PdfFontFamily.Helvetica, 9),
                    new PdfSolidBrush(PdfGridSupport.Muted),
                    new RectangleF(0, y, notePage.GetClientSize().Width, 48));
            }

            using var stream = new MemoryStream();
            pdf.Save(stream);
            pdf.Close(true);
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Tabular PDF failed Title={Title}", title);
            throw;
        }
    }
}
