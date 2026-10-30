using System.IO;
using System.Text;
using Serilog;

namespace BusBuddy.WPF.Services
{
    /// <summary>
    /// Loads short BusBuddy project snippets for AI Assist context and offline keyword answers.
    /// </summary>
    public static class BusBuddyProjectKnowledge
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(BusBuddyProjectKnowledge));

        private static readonly string[] RelativeDocPaths =
        {
            "docs/clerk-path.md",
            "docs/instructor-packet/00-brief.md",
            "AGENTS.md",
            "Documentation/diagrams/busbuddy-3-architecture.md",
            ".specify/memory/constitution.md",
        };

        private static string? _cachedContext;
        private static readonly object CacheGate = new();

        public static string BuildContextSnippet(int maxChars = 6000)
        {
            lock (CacheGate)
            {
                if (!string.IsNullOrEmpty(_cachedContext))
                {
                    return Truncate(_cachedContext!, maxChars);
                }
            }

            var root = FindRepoRoot();
            var sb = new StringBuilder();
            sb.AppendLine("BusBuddy is a Syncfusion WPF school transportation app (students, routes, map, fleet).");
            sb.AppendLine("Clerk path: school → students → generate routes → bus/driver on route → fuel/maintenance.");
            sb.AppendLine("Map code: BusBuddy.WPF/Views/Map/MapView.xaml(.cs), ViewModels/Map/MapViewModel.cs, Utilities/DistrictSfMap.cs.");
            sb.AppendLine("Route generation: IRouteDeterminationService; assignment: Route Assignments / RouteService.");
            sb.AppendLine("Local AI: Ollama via OllamaChatService (IAiChatService).");
            sb.AppendLine();

            if (root != null)
            {
                foreach (var rel in RelativeDocPaths)
                {
                    var path = Path.Combine(root, rel);
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    try
                    {
                        var text = File.ReadAllText(path);
                        sb.AppendLine($"--- {rel} ---");
                        sb.AppendLine(Truncate(text, 1800));
                        sb.AppendLine();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning(ex, "Failed reading project knowledge file {Path}", path);
                    }
                }
            }
            else
            {
                Logger.Debug("BusBuddy repo root not found; using built-in AI Assist context only");
            }

            var built = sb.ToString();
            lock (CacheGate)
            {
                _cachedContext = built;
            }

            return Truncate(built, maxChars);
        }

        /// <summary>
        /// Offline keyword answers when Ollama is unavailable — still project-relevant.
        /// </summary>
        public static string AnswerOffline(string question)
        {
            var q = (question ?? string.Empty).Trim().ToLowerInvariant();
            if (q.Length == 0)
            {
                return "Ask about routes, students, the map, architecture, or docs.";
            }

            if (ContainsAny(q, "generate route", "how do i generate", "route generation", "auto route"))
            {
                return "Generate Routes: use the Routes dock pane → **Generate Routes**. That calls `IRouteDeterminationService` (clerk hop 3). Optimize order is separate (`IRouteOptimizationService`). See `docs/clerk-path.md`.";
            }

            if (ContainsAny(q, "student assignment", "assign student", "amroute", "pmroute", "student flow"))
            {
                return "Student assignment flow: catalog a school (Destinations) → add students with that school → Generate Routes → assign bus/driver on Route Assignments. Kids link via `Student.AmRouteId` / `PmRouteId` (`RouteService.AssignStudentToRouteAsync`). Dock Students grid is a summary of Postgres.";
            }

            if (ContainsAny(q, "map code", "where is the map", "sfmap", "district map"))
            {
                return "Map code lives under `BusBuddy.WPF/Views/Map/` (`MapView`), `ViewModels/Map/MapViewModel.cs`, and helpers `Utilities/DistrictSfMap.cs`, `MapTileBootstrap.cs`, `GoogleMapTilesImageryLayer.cs`. Open the live map from the header **Map** button.";
            }

            if (ContainsAny(q, "architect", "overview", "how is busbuddy structured", "composition"))
            {
                return "Architecture: WPF UI (Syncfusion) → Core services/EF → Postgres. Diagram: `Documentation/diagrams/busbuddy-3-architecture.md`. Constitution: `.specify/memory/constitution.md`. Agent pointers: `AGENTS.md`.";
            }

            if (ContainsAny(q, "doc", "readme", "spec", "clerk path", "action item"))
            {
                return "Key docs: `docs/clerk-path.md` (clerk spine), `docs/action-items.md` (open work), `AGENTS.md` (agent rules), `specs/` (domain contracts), `.specify/memory/constitution.md`.";
            }

            if (ContainsAny(q, "ollama", "local ai", "chat"))
            {
                return "App AI is local Ollama (`OllamaChatService` / `IAiChatService`). Configure `Ollama` in appsettings (`BaseUrl` default http://localhost:11434/v1). When Ollama is down, Assist uses this offline project knowledge fallback.";
            }

            var ctx = BuildContextSnippet(2500);
            return "Ollama is offline, so here is a short project briefing related to your question:\n\n"
                + Truncate(ctx, 1200)
                + "\n\nStart Ollama (`ollama serve` + `ollama pull llama3.2`) for fuller answers.";
        }

        private static bool ContainsAny(string haystack, params string[] needles) =>
            needles.Any(n => haystack.Contains(n, StringComparison.Ordinal));

        private static string Truncate(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            {
                return text;
            }

            return text[..maxChars] + "\n…";
        }

        private static string? FindRepoRoot()
        {
            try
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                for (var i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
                {
                    if (File.Exists(Path.Combine(dir.FullName, "BusBuddy.sln"))
                        || File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
                    {
                        return dir.FullName;
                    }
                }

                var cwd = new DirectoryInfo(Directory.GetCurrentDirectory());
                for (var i = 0; i < 6 && cwd != null; i++, cwd = cwd.Parent)
                {
                    if (File.Exists(Path.Combine(cwd.FullName, "BusBuddy.sln"))
                        || File.Exists(Path.Combine(cwd.FullName, "AGENTS.md")))
                    {
                        return cwd.FullName;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "FindRepoRoot failed");
            }

            return null;
        }
    }
}
