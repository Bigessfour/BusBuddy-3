using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;

namespace BusBuddy.Core.Services;

/// <summary>
/// Shared Syncfusion PdfGrid setup matching
/// https://help.syncfusion.com/document-processing/pdf/pdf-library/net/pdfgrid
/// and page templates from
/// https://help.syncfusion.com/document-processing/pdf/pdf-library/net/working-with-headers-and-footers
/// </summary>
internal static class PdfGridSupport
{
    internal static readonly PdfColor Banner = new(15, 76, 129);
    internal static readonly PdfColor BannerAccent = new(196, 160, 64);
    internal static readonly PdfColor HeaderCell = new(15, 76, 129);
    internal static readonly PdfColor Zebra = new(245, 247, 250);
    internal static readonly PdfColor Rule = new(210, 216, 222);
    internal static readonly PdfColor Body = new(33, 37, 41);
    internal static readonly PdfColor Muted = new(90, 98, 108);

    internal const float HeaderTemplateHeight = 44f;
    internal const float FooterTemplateHeight = 28f;

    internal static PdfGrid CreateGrid(int columns)
    {
        var grid = new PdfGrid();
        grid.RepeatHeader = true;
        grid.AllowRowBreakAcrossPages = false;
        grid.Columns.Add(columns);
        grid.Headers.Add(1);
        grid.Style.CellPadding = new PdfPaddings(5, 5, 4, 4);
        grid.Style.Font = new PdfStandardFont(PdfFontFamily.Helvetica, 8.5f);
        grid.Style.BorderOverlapStyle = PdfBorderOverlapStyle.Inside;
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

        for (var i = 0; i < grid.Columns.Count; i++)
        {
            grid.Columns[i].Format = i >= rightAlignFrom ? right : left;
        }

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

    internal static PdfGridLayoutFormat CreateLayoutFormat(PdfPage page, float topOffset)
    {
        var size = page.GetClientSize();
        _ = topOffset;
        return new PdfGridLayoutFormat
        {
            Layout = PdfLayoutType.Paginate,
            Break = PdfLayoutBreakType.FitPage,
            PaginateBounds = new RectangleF(0, 8f, size.Width, size.Height - 16f)
        };
    }

    internal static void ApplyPageTemplates(PdfDocument pdf, string headerTitle, string headerMeta)
    {
        var pageWidth = pdf.PageSettings.Size.Width
                        - pdf.PageSettings.Margins.Left
                        - pdf.PageSettings.Margins.Right;

        var header = new PdfPageTemplateElement(new RectangleF(0, 0, pageWidth, HeaderTemplateHeight));
        header.Graphics.DrawRectangle(new PdfSolidBrush(Banner), new RectangleF(0, 0, pageWidth, 40));
        header.Graphics.DrawRectangle(new PdfSolidBrush(BannerAccent), new RectangleF(0, 40, pageWidth, 3));
        header.Graphics.DrawString(
            headerTitle,
            new PdfStandardFont(PdfFontFamily.Helvetica, 14, PdfFontStyle.Bold),
            PdfBrushes.White,
            new PointF(0, 6));
        header.Graphics.DrawString(
            headerMeta,
            new PdfStandardFont(PdfFontFamily.Helvetica, 9),
            PdfBrushes.White,
            new PointF(0, 24));
        pdf.Template.Top = header;

        var footer = new PdfPageTemplateElement(new RectangleF(0, 0, pageWidth, FooterTemplateHeight));
        var muted = new PdfSolidBrush(Muted);
        var small = new PdfStandardFont(PdfFontFamily.Helvetica, 8);
        footer.Graphics.DrawLine(new PdfPen(Rule, 0.6f), new PointF(0, 4), new PointF(pageWidth, 4));
        footer.Graphics.DrawString(
            $"Generated {DateTime.Now:MMM d, yyyy h:mm tt}",
            small,
            muted,
            new PointF(0, 10));

        var pageNumber = new PdfPageNumberField(small, muted);
        var pageCount = new PdfPageCountField(small, muted);
        var composite = new PdfCompositeField(small, muted, "Page {0} of {1}", pageNumber, pageCount)
        {
            Bounds = new RectangleF(pageWidth - 90, 10, 90, 14),
            StringFormat = new PdfStringFormat(PdfTextAlignment.Right)
        };
        composite.Draw(footer.Graphics, new PointF(pageWidth - 90, 10));
        pdf.Template.Bottom = footer;
    }
}
