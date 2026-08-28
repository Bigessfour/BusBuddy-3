using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace BusBuddy.XamlCompliance.Tests;

/// <summary>
/// Static analysis of BusBuddy WPF XAML for FluentDark/FluentLight theme misalignment.
/// Catches blank ButtonAdv tiles, invisible labels, and light-chrome toolbars under FluentDark
/// (and dark-on-primary under FluentLight).
/// </summary>
public static class XamlThemeComplianceScanner
{
    public sealed record Finding(string Rule, string RelativePath, int Line, string Detail);

    private static readonly Regex LightChromeBackgroundRegex = new(
        @"\bBackground\s*=\s*""#(?:F5F5F5|E9ECEF|E6F7FF|FFF9E6|FFFFFF|FFF)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HardcodedWhiteForegroundRegex = new(
        @"\bForeground\s*=\s*""(?:White|#FFF(?:FFF)?|#FFFFFF)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HardcodedHexForegroundRegex = new(
        @"\bForeground\s*=\s*""#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})""",
        RegexOptions.Compiled);

    private static readonly Regex ButtonAdvOpenRegex = new(
        @"<(?:[A-Za-z_][\w.-]*:)?ButtonAdv\b(?<attrs>[\s\S]*?)(?:/>|>)",
        RegexOptions.Compiled);

    private static readonly Regex LabelAttrRegex = new(
        @"\bLabel\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled);

    private static readonly Regex WatermarkAttrRegex = new(
        @"\bWatermark(?:Text)?\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled);

    private static readonly Regex TextAttrRegex = new(
        @"\bText\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled);

    private static readonly Regex TextBlockOpenRegex = new(
        @"<TextBlock\b(?<attrs>[\s\S]*?)(?:/>|>)",
        RegexOptions.Compiled);

    private static readonly Regex ColoredBackgroundRegex = new(
        @"\bBackground\s*=\s*""\{DynamicResource\s+BusBuddy\.Brush\.(?:Primary|FleetGreen|SafetyOrange|Semantic\.(?:Error|Warning|Success|Info|Danger))\}""",
        RegexOptions.Compiled);

    private static readonly Regex TextPrimaryForegroundRegex = new(
        @"\bForeground\s*=\s*""\{DynamicResource\s+BusBuddy\.Brush\.Text\.Primary\}""",
        RegexOptions.Compiled);

    private static readonly Regex StaticBusBuddyBrushRegex = new(
        @"\{StaticResource\s+BusBuddy\.",
        RegexOptions.Compiled);

    private static readonly Regex BrushKeyRegex = new(
        @"x:Key=""(BusBuddy\.Brush\.[^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex AllowCommentRegex = new(
        @"theme-compliance:allow\s+(?<rules>[A-Za-z0-9_,\s]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] RequiredBrushKeys =
    [
        "BusBuddy.Brush.Text.Primary",
        "BusBuddy.Brush.Text.OnPrimary",
        "BusBuddy.Brush.Text.Secondary",
        "BusBuddy.Brush.Primary",
        "BusBuddy.Brush.Header.Background",
        "BusBuddy.Brush.Panel.Header",
        "BusBuddy.Brush.Panel.Content",
        "BusBuddy.Brush.Panel.Border",
        "BusBuddy.Brush.Semantic.Error",
        "BusBuddy.Brush.Semantic.Danger",
        "BusBuddy.Brush.Semantic.Warning",
        "BusBuddy.Brush.Semantic.Success",
        "BusBuddy.Brush.Semantic.Info",
    ];

    public static string FindRepoRoot()
    {
        var start = TestContext.CurrentContext.TestDirectory;
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BusBuddy.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate BusBuddy.sln from test directory '{start}'.");
    }

    public static IReadOnlyList<string> EnumerateUiXamlFiles(string repoRoot)
    {
        var roots = new[]
        {
            Path.Combine(repoRoot, "BusBuddy.WPF", "Views"),
            Path.Combine(repoRoot, "BusBuddy.WPF", "Controls"),
        };

        return roots
            .Where(Directory.Exists)
            .SelectMany(r => Directory.EnumerateFiles(r, "*.xaml", SearchOption.AllDirectories))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<Finding> ScanUiXaml(string repoRoot)
    {
        var findings = new List<Finding>();
        foreach (var path in EnumerateUiXamlFiles(repoRoot))
        {
            findings.AddRange(ScanFile(repoRoot, path));
        }

        return Deduplicate(findings);
    }

    public static IReadOnlyList<Finding> ScanFile(string repoRoot, string absolutePath)
    {
        var relative = Path.GetRelativePath(repoRoot, absolutePath).Replace('\\', '/');
        var text = File.ReadAllText(absolutePath);
        var findings = new List<Finding>();

        AddLineMatches(findings, relative, text, HardcodedWhiteForegroundRegex,
            "HardcodedWhiteForeground",
            "Use BusBuddy.Brush.Text.OnPrimary or theme foreground brushes instead of White.");

        AddLineMatches(findings, relative, text, HardcodedHexForegroundRegex,
            "HardcodedHexForeground",
            "Use DynamicResource theme text brushes instead of hardcoded hex Foreground.",
            skip: m =>
            {
                var hex = m.Groups[1].Value;
                return hex.Equals("FFF", StringComparison.OrdinalIgnoreCase) ||
                       hex.Equals("FFFFFF", StringComparison.OrdinalIgnoreCase);
            });

        AddLineMatches(findings, relative, text, LightChromeBackgroundRegex,
            "HardcodedLightChrome",
            "Use BusBuddy.Brush.Panel.Header / Panel.Content (or theme brushes) instead of light hex chrome.");

        AddLineMatches(findings, relative, text, StaticBusBuddyBrushRegex,
            "StaticBusBuddyBrush",
            "Use DynamicResource for BusBuddy.* brushes so FluentDark/Light switch updates live.");

        foreach (Match m in ButtonAdvOpenRegex.Matches(text))
        {
            var attrs = m.Groups["attrs"].Value;
            var line = LineNumberAt(text, m.Index);

            if (Regex.IsMatch(attrs, @"\bContent\s*="))
            {
                findings.Add(new Finding(
                    "ButtonAdvContentIgnored",
                    relative,
                    line,
                    "ButtonAdv uses Content=; Syncfusion renders Label= — captions will be blank."));
            }

            var labelMatch = LabelAttrRegex.Match(attrs);
            if (labelMatch.Success)
            {
                var label = labelMatch.Groups["value"].Value;
                if (ContainsEmojiOrLiteralEscape(label))
                {
                    findings.Add(new Finding(
                        "EmojiInButtonAdvLabel",
                        relative,
                        line,
                        $"Label contains emoji/escape that Fluent often fails to render: \"{label}\""));
                }
            }
        }

        foreach (Match m in WatermarkAttrRegex.Matches(text))
        {
            var value = m.Groups["value"].Value;
            if (ContainsEmojiOrLiteralEscape(value))
            {
                findings.Add(new Finding(
                    "EmojiInWatermark",
                    relative,
                    LineNumberAt(text, m.Index),
                    $"Watermark contains emoji/escape: \"{value}\""));
            }
        }

        foreach (Match m in TextBlockOpenRegex.Matches(text))
        {
            var attrs = m.Groups["attrs"].Value;
            var textMatch = TextAttrRegex.Match(attrs);
            if (textMatch.Success && ContainsEmojiOrLiteralEscape(textMatch.Groups["value"].Value))
            {
                findings.Add(new Finding(
                    "EmojiInTextBlock",
                    relative,
                    LineNumberAt(text, m.Index),
                    $"TextBlock Text contains emoji/escape: \"{textMatch.Groups["value"].Value}\""));
            }
        }

        foreach (Match m in ButtonAdvOpenRegex.Matches(text))
        {
            var attrs = m.Groups["attrs"].Value;
            if (ColoredBackgroundRegex.IsMatch(attrs) && TextPrimaryForegroundRegex.IsMatch(attrs))
            {
                findings.Add(new Finding(
                    "TextPrimaryOnColoredBackground",
                    relative,
                    LineNumberAt(text, m.Index),
                    "ButtonAdv uses Text.Primary on a saturated BusBuddy brush — use Text.OnPrimary."));
            }
        }

        foreach (var (index, body) in FindColoredBorderBodies(text))
        {
            foreach (var lineOffset in FindDirectChildTextPrimaryLines(body))
            {
                findings.Add(new Finding(
                    "TextPrimaryOnPrimaryHeader",
                    relative,
                    LineNumberAt(text, index) + lineOffset,
                    "Colored header Border has a direct-child text element with Text.Primary — use Text.OnPrimary."));
            }
        }

        ScanNamedButtonAdvStyles(findings, relative, text);

        return FilterAllowed(text, findings);
    }

    private static readonly Regex NamedStyleOpenRegex = new(
        @"<Style\s+(?<attrs>[^>]+)>",
        RegexOptions.Compiled);

    private static void ScanNamedButtonAdvStyles(List<Finding> findings, string relative, string text)
    {
        foreach (Match open in NamedStyleOpenRegex.Matches(text))
        {
            var attrs = open.Groups["attrs"].Value;
            if (!attrs.Contains("x:Key=", StringComparison.Ordinal) ||
                !attrs.Contains("ButtonAdv", StringComparison.Ordinal))
            {
                continue;
            }

            if (attrs.Contains("BasedOn=", StringComparison.Ordinal))
            {
                continue;
            }

            var styleEnd = text.IndexOf("</Style>", open.Index, StringComparison.Ordinal);
            if (styleEnd < 0)
            {
                continue;
            }

            var styleBody = text[open.Index..styleEnd];
            var iconWidthMatch = Regex.Match(
                styleBody,
                @"Property=""IconWidth""\s+Value=""(?<value>[^""]+)""");
            if (iconWidthMatch.Success &&
                !string.Equals(iconWidthMatch.Groups["value"].Value, "0", StringComparison.Ordinal))
            {
                continue;
            }

            if (!styleBody.Contains("IconWidth", StringComparison.Ordinal) ||
                !styleBody.Contains("SmallIcon", StringComparison.Ordinal))
            {
                var keyMatch = Regex.Match(attrs, @"x:Key=""(?<key>[^""]+)""");
                var styleKey = keyMatch.Success ? keyMatch.Groups["key"].Value : "(unnamed)";
                findings.Add(new Finding(
                    "NamedButtonAdvMissingIconSuppression",
                    relative,
                    LineNumberAt(text, open.Index),
                    $"Style '{styleKey}' should set IconWidth/Height=0 and SmallIcon={{x:Null}} so Fluent Label text is visible."));
            }
        }
    }

    /// <summary>
    /// Depth-aware extraction of Border elements whose Background is a saturated BusBuddy brush.
    /// Avoids non-greedy regex that stops at the first nested &lt;/Border&gt;.
    /// </summary>
    internal static IEnumerable<(int Index, string Body)> FindColoredBorderBodies(string text)
    {
        var i = 0;
        while (i < text.Length)
        {
            var start = text.IndexOf("<Border", i, StringComparison.Ordinal);
            if (start < 0)
            {
                yield break;
            }

            if (start + 7 < text.Length && char.IsLetterOrDigit(text[start + 7]))
            {
                i = start + 7;
                continue;
            }

            var openEnd = text.IndexOf('>', start);
            if (openEnd < 0)
            {
                yield break;
            }

            var openTag = text[start..openEnd];
            if (openTag.EndsWith('/'))
            {
                i = openEnd + 1;
                continue;
            }

            if (!ColoredBackgroundRegex.IsMatch(openTag))
            {
                i = openEnd + 1;
                continue;
            }

            var depth = 1;
            var pos = openEnd + 1;
            while (pos < text.Length && depth > 0)
            {
                var nextOpen = text.IndexOf("<Border", pos, StringComparison.Ordinal);
                var nextClose = text.IndexOf("</Border>", pos, StringComparison.Ordinal);
                if (nextClose < 0)
                {
                    break;
                }

                if (nextOpen >= 0 && nextOpen < nextClose)
                {
                    if (nextOpen + 7 >= text.Length || !char.IsLetterOrDigit(text[nextOpen + 7]))
                    {
                        depth++;
                    }

                    pos = nextOpen + 7;
                    continue;
                }

                depth--;
                if (depth == 0)
                {
                    var body = text.Substring(openEnd + 1, nextClose - (openEnd + 1));
                    yield return (start, body);
                    pos = nextClose + "</Border>".Length;
                    break;
                }

                pos = nextClose + "</Border>".Length;
            }

            i = pos;
        }
    }

    /// <summary>
    /// Returns line offsets (0-based within body) for TextBlock/Label using Text.Primary
    /// that are not nested inside an inner Border (chips/inputs stay on Text.Primary).
    /// </summary>
    internal static IEnumerable<int> FindDirectChildTextPrimaryLines(string body)
    {
        var depth = 0;
        var line = 0;
        for (var i = 0; i < body.Length; i++)
        {
            if (body[i] == '\n')
            {
                line++;
            }

            if (body[i] != '<')
            {
                continue;
            }

            if (body.AsSpan(i).StartsWith("</Border>", StringComparison.Ordinal))
            {
                depth = Math.Max(0, depth - 1);
                continue;
            }

            if (body.AsSpan(i).StartsWith("<Border", StringComparison.Ordinal) &&
                (i + 7 >= body.Length || !char.IsLetterOrDigit(body[i + 7])))
            {
                depth++;
                continue;
            }

            if (depth != 0)
            {
                continue;
            }

            if (body.AsSpan(i).StartsWith("<TextBlock", StringComparison.Ordinal) ||
                body.AsSpan(i).StartsWith("<Label", StringComparison.Ordinal))
            {
                var end = body.IndexOf('>', i);
                if (end < 0)
                {
                    continue;
                }

                var attrs = body[i..end];
                if (TextPrimaryForegroundRegex.IsMatch(attrs))
                {
                    yield return line;
                }
            }
        }
    }

    public static IReadOnlyList<Finding> ScanThemeDictionaryParity(string repoRoot)
    {
        var findings = new List<Finding>();
        var darkPath = Path.Combine(repoRoot, "BusBuddy.WPF", "Resources", "Themes", "FluentDarkTheme.xaml");
        var lightPath = Path.Combine(repoRoot, "BusBuddy.WPF", "Resources", "Themes", "FluentLightTheme.xaml");
        var basePath = Path.Combine(repoRoot, "BusBuddy.WPF", "Resources", "SyncfusionV30_Validated_ResourceDictionary.xaml");

        var dark = ExtractBrushKeys(darkPath);
        var light = ExtractBrushKeys(lightPath);
        var baseline = ExtractBrushKeys(basePath);

        foreach (var key in dark.Except(light).OrderBy(k => k))
        {
            findings.Add(new Finding(
                "ThemeBrushKeyParity",
                "BusBuddy.WPF/Resources/Themes/FluentDarkTheme.xaml",
                1,
                $"Key '{key}' exists in FluentDark but not FluentLight."));
        }

        foreach (var key in light.Except(dark).OrderBy(k => k))
        {
            findings.Add(new Finding(
                "ThemeBrushKeyParity",
                "BusBuddy.WPF/Resources/Themes/FluentLightTheme.xaml",
                1,
                $"Key '{key}' exists in FluentLight but not FluentDark."));
        }

        var available = new HashSet<string>(dark, StringComparer.Ordinal);
        available.UnionWith(light);
        available.UnionWith(baseline);

        foreach (var required in RequiredBrushKeys)
        {
            if (!available.Contains(required))
            {
                findings.Add(new Finding(
                    "MissingRequiredThemeBrush",
                    "BusBuddy.WPF/Resources",
                    1,
                    $"Required brush key '{required}' is missing from FluentDark, FluentLight, and validated dictionary."));
            }
        }

        // Named semantic ButtonAdv styles must suppress Fluent default glyph slot.
        foreach (var themeRel in new[]
                 {
                     "BusBuddy.WPF/Resources/Themes/FluentDarkTheme.xaml",
                     "BusBuddy.WPF/Resources/Themes/FluentLightTheme.xaml",
                 })
        {
            var path = Path.Combine(repoRoot, themeRel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                continue;
            }

            var themeText = File.ReadAllText(path);
            foreach (var styleKey in new[]
                     {
                         "BusBuddy.ButtonAdv.Success",
                         "BusBuddy.ButtonAdv.Warning",
                         "BusBuddy.ButtonAdv.Danger",
                         "BusBuddy.ButtonAdv.Info",
                     })
            {
                if (!themeText.Contains($"x:Key=\"{styleKey}\"", StringComparison.Ordinal))
                {
                    continue;
                }

                var styleMatch = Regex.Match(
                    themeText,
                    $@"<Style\s+x:Key=""{Regex.Escape(styleKey)}""[\s\S]*?</Style>",
                    RegexOptions.Compiled);
                if (!styleMatch.Success)
                {
                    continue;
                }

                var styleBody = styleMatch.Value;
                if (!styleBody.Contains("IconWidth", StringComparison.Ordinal) ||
                    !styleBody.Contains("SmallIcon", StringComparison.Ordinal))
                {
                    findings.Add(new Finding(
                        "NamedButtonAdvMissingIconSuppression",
                        themeRel,
                        LineNumberAt(themeText, styleMatch.Index),
                        $"Style '{styleKey}' should set IconWidth/Height=0 and SmallIcon={{x:Null}} (or BasedOn text-only ButtonAdv)."));
                }
            }

            var implicitMatch = Regex.Match(
                themeText,
                @"<Style\s+TargetType=""\{x:Type syncfusion:ButtonAdv\}""[\s\S]*?</Style>",
                RegexOptions.Compiled);
            if (implicitMatch.Success)
            {
                var body = implicitMatch.Value;
                if (!body.Contains("Property=\"IconWidth\"", StringComparison.Ordinal) ||
                    !body.Contains("Property=\"SmallIcon\"", StringComparison.Ordinal) ||
                    body.Contains("Style.Triggers", StringComparison.Ordinal))
                {
                    findings.Add(new Finding(
                        "ImplicitButtonAdvMissingIconSuppression",
                        themeRel,
                        LineNumberAt(themeText, implicitMatch.Index),
                        "Implicit ButtonAdv style must set IconWidth/Height=0 and SmallIcon={x:Null} as setters (Fluent default glyph is not null, so a trigger never fires)."));
                }
            }
        }

        return findings;
    }

    public static string FormatFindings(IEnumerable<Finding> findings)
    {
        var list = findings.ToList();
        if (list.Count == 0)
        {
            return "(none)";
        }

        var sb = new StringBuilder();
        foreach (var group in list.GroupBy(f => f.Rule).OrderBy(g => g.Key))
        {
            sb.AppendLine($"## {group.Key} ({group.Count()})");
            foreach (var f in group.OrderBy(x => x.RelativePath).ThenBy(x => x.Line))
            {
                sb.AppendLine($"- {f.RelativePath}:{f.Line} — {f.Detail}");
            }
        }

        return sb.ToString();
    }

    private static List<Finding> FilterAllowed(string text, List<Finding> findings)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        return findings.Where(f =>
        {
            var idx = Math.Clamp(f.Line - 1, 0, lines.Length - 1);
            for (var i = Math.Max(0, idx - 3); i <= idx; i++)
            {
                var allow = AllowCommentRegex.Match(lines[i]);
                if (!allow.Success)
                {
                    continue;
                }

                var rules = allow.Groups["rules"].Value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (rules.Any(r => r.Equals(f.Rule, StringComparison.OrdinalIgnoreCase) ||
                                   r.Equals("*", StringComparison.Ordinal)))
                {
                    return false;
                }
            }

            return true;
        }).ToList();
    }

    private static List<Finding> Deduplicate(IEnumerable<Finding> findings) =>
        findings
            .GroupBy(f => (f.Rule, f.RelativePath, f.Line, f.Detail))
            .Select(g => g.First())
            .OrderBy(f => f.Rule)
            .ThenBy(f => f.RelativePath)
            .ThenBy(f => f.Line)
            .ToList();

    private static HashSet<string> ExtractBrushKeys(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        return BrushKeyRegex.Matches(File.ReadAllText(path))
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool ContainsEmojiOrLiteralEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.Contains("\\u", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("\\U", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var rune in value.EnumerateRunes())
        {
            var v = rune.Value;
            if (v is >= 0x1F300 and <= 0x1FAFF)
            {
                return true;
            }

            if (v is >= 0x2600 and <= 0x27BF)
            {
                return true;
            }
        }

        return false;
    }

    private static void AddLineMatches(
        List<Finding> findings,
        string relative,
        string text,
        Regex regex,
        string rule,
        string detail,
        Func<Match, bool>? skip = null)
    {
        foreach (Match m in regex.Matches(text))
        {
            if (skip?.Invoke(m) == true)
            {
                continue;
            }

            findings.Add(new Finding(rule, relative, LineNumberAt(text, m.Index), detail));
        }
    }

    private static int LineNumberAt(string text, int index)
    {
        var line = 1;
        for (var i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }
}
