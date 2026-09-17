using Syncfusion.Pdf.Graphics;

namespace BusBuddy.Core.Services;

/// <summary>
/// TrueType faces for route-sheet PDFs. Built-in Helvetica Standard fonts drop
/// space advance in this Syncfusion pin (headers render as one run-on word).
/// </summary>
internal static class PdfSheetFonts
{
    public static PdfFont Regular(float size) => Create(size, PdfFontStyle.Regular);

    public static PdfFont Bold(float size) => Create(size, PdfFontStyle.Bold);

    private static PdfFont Create(float size, PdfFontStyle style)
    {
        foreach (var path in FontPaths(style))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                return new PdfTrueTypeFont(path, size, style);
            }
            catch
            {
                // Try the next installed face.
            }
        }

        return new PdfStandardFont(PdfFontFamily.TimesRoman, size, style);
    }

    private static IEnumerable<string> FontPaths(PdfFontStyle style)
    {
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var bold = style == PdfFontStyle.Bold;
        if (!string.IsNullOrWhiteSpace(fonts))
        {
            yield return Path.Combine(fonts, bold ? "arialbd.ttf" : "arial.ttf");
            yield return Path.Combine(fonts, bold ? "seguisb.ttf" : "segoeui.ttf");
            yield return Path.Combine(fonts, "arial.ttf");
        }

        if (bold)
        {
            yield return "/System/Library/Fonts/Supplemental/Arial Bold.ttf";
            yield return "/Library/Fonts/Arial Bold.ttf";
        }

        yield return "/System/Library/Fonts/Supplemental/Arial.ttf";
        yield return "/Library/Fonts/Arial.ttf";
        yield return "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf";
        yield return "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";
    }
}
