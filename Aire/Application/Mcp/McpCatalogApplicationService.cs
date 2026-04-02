using System;
using System.Collections.Generic;
using System.Linq;
using Aire.Services.Mcp;

namespace Aire.AppLayer.Mcp
{
    /// <summary>
    /// Curated MCP server catalog used by the Settings UI for one-click discovery and install.
    /// </summary>
    public sealed class McpCatalogApplicationService
    {
        public sealed record McpCatalogEntry(
            string Key,
            string Name,
            string Description,
            string Command,
            string Arguments,
            string EnvironmentHint,
            string Category);

        public IReadOnlyList<McpCatalogEntry> GetCatalog()
            => Catalog;

        public McpCatalogEntry GetEntry(string key)
            => Catalog.First(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));

        public McpServerConfig BuildConfig(string key)
        {
            var entry = GetEntry(key);
            return new McpServerConfig
            {
                Name = entry.Name,
                Command = entry.Command,
                Arguments = entry.Arguments,
                EnvVars = ParseEnvironmentHint(entry.EnvironmentHint),
                IsEnabled = true
            };
        }

        public McpServerConfig? FindInstalledConfig(string key, IEnumerable<McpServerConfig> installedConfigs)
        {
            var entry = GetEntry(key);
            return installedConfigs.FirstOrDefault(config =>
                string.Equals(Normalize(config.Name), Normalize(entry.Name), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Normalize(config.Command), Normalize(entry.Command), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Normalize(config.Arguments), Normalize(entry.Arguments), StringComparison.OrdinalIgnoreCase));
        }

        private static Dictionary<string, string> ParseEnvironmentHint(string text)
        {
            var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = line.IndexOf('=');
                if (idx <= 0)
                    continue;

                env[line[..idx].Trim()] = line[(idx + 1)..].Trim();
            }

            return env;
        }

        private static string Normalize(string? value)
            => (value ?? string.Empty).Trim();

        private static readonly IReadOnlyList<McpCatalogEntry> Catalog =
        [
            // ── Local / files ──────────────────────────────────────────────
            new("filesystem",          "Filesystem",          "Browse and edit local files and directories.",                                  "npx", "-y @modelcontextprotocol/server-filesystem",         "",                                                                                                                "Local"),
            new("sqlite",              "SQLite",              "Inspect and query SQLite database files.",                                      "npx", "-y @modelcontextprotocol/server-sqlite",              "",                                                                                                                "Data"),
            new("postgres",            "PostgreSQL",          "Run queries and inspect a PostgreSQL database.",                                "npx", "-y @modelcontextprotocol/server-postgres",            "POSTGRES_CONNECTION_STRING=<paste-connection-string-here>",                                                       "Data"),

            // ── Web ────────────────────────────────────────────────────────
            new("fetch",               "Web Fetch",           "Fetch and extract content from any web page.",                                  "npx", "-y @modelcontextprotocol/server-fetch",               "",                                                                                                                "Web"),
            new("brave-search",        "Brave Search",        "Search the web using the Brave Search API.",                                    "npx", "-y @modelcontextprotocol/server-brave-search",        "BRAVE_API_KEY=<paste-api-key-here>",                                                                               "Web"),
            new("puppeteer",           "Puppeteer",           "Control a headless Chrome browser for scraping and automation.",                "npx", "-y @modelcontextprotocol/server-puppeteer",           "",                                                                                                                "Web"),

            // ── Developer tools ────────────────────────────────────────────
            new("github",              "GitHub",              "Inspect repositories, issues, pull requests, and code.",                        "npx", "-y @modelcontextprotocol/server-github",              "GITHUB_PERSONAL_ACCESS_TOKEN=<paste-token-here>",                                                                 "Developer"),
            new("gitlab",              "GitLab",              "Interact with GitLab projects, issues, and merge requests.",                    "npx", "-y @modelcontextprotocol/server-gitlab",              "GITLAB_PERSONAL_ACCESS_TOKEN=<paste-token-here>\nGITLAB_API_URL=https://gitlab.com",                              "Developer"),
            new("sentry",              "Sentry",              "Look up errors, events, and stack traces from Sentry.",                         "npx", "-y @modelcontextprotocol/server-sentry",              "SENTRY_AUTH_TOKEN=<paste-auth-token-here>\nSENTRY_ORG=<paste-org-slug-here>",                                     "Developer"),

            // ── Productivity / communication ────────────────────────────────
            new("slack",               "Slack",               "Read and interact with Slack channels and messages.",                           "npx", "-y @modelcontextprotocol/server-slack",               "SLACK_BOT_TOKEN=<paste-bot-token-here>\nSLACK_TEAM_ID=<paste-team-id-here>",                                      "Communication"),
            new("gmail",               "Gmail (OAuth)",        "Read and manage Gmail messages and threads.",                                   "npx", "-y @modelcontextprotocol/server-gmail",               "GMAIL_CLIENT_ID=<paste-client-id-here>\nGMAIL_CLIENT_SECRET=<paste-client-secret-here>\nGMAIL_REFRESH_TOKEN=<paste-refresh-token-here>", "Communication"),
            new("notion",              "Notion",              "Read and write Notion pages, databases, and blocks.",                           "npx", "-y @notionhq/notion-mcp-server",                     "OPENAPI_MCP_HEADERS={\"Authorization\":\"Bearer <paste-token-here>\",\"Notion-Version\":\"2022-06-28\"}",              "Productivity"),
            new("google-maps",         "Google Maps",         "Geocode addresses, search places, and get directions.",                         "npx", "-y @modelcontextprotocol/server-google-maps",         "GOOGLE_MAPS_API_KEY=<paste-api-key-here>",                                                                         "Location"),

            // ── Agent / reasoning ──────────────────────────────────────────
            new("memory",              "Memory",              "Persist and recall facts across conversations.",                                "npx", "-y @modelcontextprotocol/server-memory",               "",                                                                                                                "Agent"),
            new("sequential-thinking", "Sequential Thinking", "Break complex problems into explicit reasoning steps before answering.",        "npx", "-y @modelcontextprotocol/server-sequential-thinking", "",                                                                                                                "Agent"),
        ];
    }
}
