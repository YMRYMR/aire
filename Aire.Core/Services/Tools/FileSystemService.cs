using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Aire.Services
{
    public class FileSystemService
    {
        private const int MaxFileReadBytes = 100_000; // 100 KB

        public async Task<ToolExecutionResult> ExecuteAsync(ToolCallRequest request)
        {
            try
            {
                if (request.Tool == "list_directory")
                    return ExecuteListDirectory(GetParam(request, "path"));

                var text = request.Tool switch
                {
                    "read_file"        => await ReadFileAsync(GetParam(request, "path"), GetInt(request, "offset", 0), GetInt(request, "length", MaxFileReadBytes)),
                    "write_file"       => await WriteFileAsync(GetParam(request, "path"), GetParam(request, "content"), GetBool(request, "append", false)),
                    "write_to_file"    => await WriteFileAsync(GetParam(request, "path"), GetParam(request, "content"), false),
                    "apply_diff"       => await ApplyDiffAsync(GetParam(request, "path"), GetParam(request, "diff")),
                    "create_directory" => CreateDirectory(GetParam(request, "path")),
                    "delete_file"      => DeleteFile(GetParam(request, "path")),
                    "move_file"        => MoveFile(GetParam(request, "from"), GetParam(request, "to")),
                    "search_files"        => SearchFiles(GetParam(request, "directory"), GetParam(request, "pattern")),
                    "search_file_content" => SearchFileContent(request),
                    _                     => $"Error: Unknown tool '{request.Tool}'"
                };
                return new ToolExecutionResult { TextResult = text };
            }
        catch
            {
            return new ToolExecutionResult { TextResult = "File system operation failed." };
            }
        }

        private static string GetParam(ToolCallRequest req, string name) =>
            req.Parameters.TryGetProperty(name, out var v) ? v.GetString() ?? string.Empty : string.Empty;

        private static int GetInt(ToolCallRequest req, string name, int defaultValue) =>
            req.Parameters.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number
                ? v.GetInt32() : defaultValue;

        private static bool GetBool(ToolCallRequest req, string name, bool defaultValue) =>
            req.Parameters.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.True  ? true  :
            req.Parameters.TryGetProperty(name, out     v) && v.ValueKind == System.Text.Json.JsonValueKind.False ? false :
            defaultValue;

        // ── list_directory ─────────────────────────────────────────────────────

        private static ToolExecutionResult ExecuteListDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return new ToolExecutionResult { TextResult = "Error: No path provided." };
            if (!Directory.Exists(path))
                return new ToolExecutionResult { TextResult = $"Error: Directory not found: {path}" };

            var dirs  = new List<string>();
            var files = new List<string>();

            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(path))
                {
                    try
                    {
                        var attributes = File.GetAttributes(entry);
                        if ((attributes & FileAttributes.Directory) != 0)
                            dirs.Add(entry);
                        else
                            files.Add(entry);
                    }
                    catch
                    {
                        // Skip invalid or inaccessible entries instead of failing the whole listing.
                    }
                }
            }
            catch (Exception ex)
            {
                return new ToolExecutionResult { TextResult = $"Error: Directory listing failed: {ex.Message}" };
            }

            dirs.Sort(StringComparer.OrdinalIgnoreCase);
            files.Sort(StringComparer.OrdinalIgnoreCase);

            var listing = new DirectoryListing { Path = path };
            foreach (var d in dirs)
                listing.Entries.Add(new DirectoryEntry { IsDirectory = true, Name = System.IO.Path.GetFileName(d) });
            foreach (var f in files)
            {
                try
                {
                    var info = new FileInfo(f);
                    if (IsReservedDeviceName(info.Name))
                        continue;

                    listing.Entries.Add(new DirectoryEntry
                    {
                        IsDirectory = false,
                        Name = info.Name,
                        Size = FormatSize(info.Length),
                        Modified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
                    });
                }
                catch
                {
                    // Skip invalid or inaccessible entries instead of failing the whole listing.
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Contents of: {path}");
            foreach (var e in listing.Entries)
            {
                if (e.IsDirectory)
                    sb.AppendLine($"[DIR]  {e.Name}/");
                else
                    sb.AppendLine($"[FILE] {e.Name}  ({e.Size}, {e.Modified})");
            }
            if (listing.Entries.Count == 0)
                sb.AppendLine("(empty directory)");

            return new ToolExecutionResult
            {
                TextResult       = sb.ToString().TrimEnd(),
                DirectoryListing = listing
            };
        }

        // ── Other tools ────────────────────────────────────────────────────────

        private static async Task<string> ReadFileAsync(string path, int offset = 0, int length = MaxFileReadBytes)
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: No path provided.";
            if (!File.Exists(path)) return $"Error: File not found: {path}";

            length = Math.Clamp(length, 1, MaxFileReadBytes);
            offset = Math.Max(0, offset);

            var fullContent = await File.ReadAllTextAsync(path);
            var totalChars  = fullContent.Length;

            if (offset >= totalChars && totalChars > 0)
                return $"Error: Offset {offset} is beyond end of file ({totalChars} chars total).";

            var chunk      = fullContent.Substring(offset, Math.Min(length, totalChars - offset));
            var charsRead  = chunk.Length;
            var remaining  = totalChars - offset - charsRead;
            var nextOffset = offset + charsRead;

            var header = remaining > 0
                ? $"File: {path} | Total: {totalChars} chars | Read: {charsRead} chars (offset {offset}–{nextOffset - 1}) | Remaining: {remaining} chars — call read_file with offset={nextOffset} to continue\n---\n"
                : $"File: {path} | Total: {totalChars} chars | Read: {charsRead} chars (complete)\n---\n";

            return header + chunk;
        }

        private static async Task<string> WriteFileAsync(string path, string content, bool append = false)
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: No path provided.";
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            if (append)
                await File.AppendAllTextAsync(path, content);
            else
                await File.WriteAllTextAsync(path, content);
            var totalSize = new FileInfo(path).Length;
            var action    = append ? "Appended" : "Wrote";
            return $"{action} {content.Length} characters to: {path} (file is now {FormatSize(totalSize)})";
        }

        private static async Task<string> ApplyDiffAsync(string path, string diff)
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: No path provided.";
            if (string.IsNullOrWhiteSpace(diff)) return "Error: No diff provided.";
            if (!File.Exists(path)) return $"Error: File not found: {path}";

            var original = await File.ReadAllTextAsync(path);
            var newline = original.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var resultLines = NormalizeNewLines(original).Split('\n').ToList();
            var diffLines = NormalizeNewLines(diff).Split('\n');
            var changed = false;
            int searchIdx = 0;

            while (searchIdx < diffLines.Length)
            {
                // Find <<<<<<< marker
                int startMarker = -1;
                for (int i = searchIdx; i < diffLines.Length; i++)
                {
                    if (diffLines[i].StartsWith("<<<<<<<"))
                    { startMarker = i; break; }
                }
                if (startMarker < 0) break;

                // Find ======= separator
                int separator = -1;
                for (int i = startMarker + 1; i < diffLines.Length; i++)
                {
                    if (diffLines[i].StartsWith("======="))
                    { separator = i; break; }
                }
                if (separator < 0) break;

                // Find >>>>>>> end marker
                int endMarker = -1;
                for (int i = separator + 1; i < diffLines.Length; i++)
                {
                    if (diffLines[i].StartsWith(">>>>>>>"))
                    { endMarker = i; break; }
                }
                if (endMarker < 0) break;

                var oldBlockLines = diffLines[(startMarker + 1)..separator];
                var newBlockLines = diffLines[(separator + 1)..endMarker];

                if (TryReplaceBlock(resultLines, oldBlockLines, newBlockLines))
                    changed = true;

                searchIdx = endMarker + 1;
            }

            if (!changed)
                return "Warning: Diff applied but no changes were made (old text not found).";

            await File.WriteAllTextAsync(path, RestoreLineEndings(string.Join("\n", resultLines), newline));
            return $"Diff applied to: {path}";
        }

        private static string NormalizeNewLines(string text) =>
            text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

        private static string RestoreLineEndings(string text, string newline) =>
            newline == "\n" ? text : text.Replace("\n", newline, StringComparison.Ordinal);

        private static bool TryReplaceBlock(List<string> lines, string[] oldBlockLines, string[] newBlockLines)
        {
            if (TryReplaceBlockExact(lines, oldBlockLines, newBlockLines))
                return true;

            return TryReplaceBlockIgnoringIndentation(lines, oldBlockLines, newBlockLines);
        }

        private static bool TryReplaceBlockExact(List<string> lines, string[] oldBlockLines, string[] newBlockLines)
        {
            if (oldBlockLines.Length == 0)
                return false;

            for (int i = 0; i <= lines.Count - oldBlockLines.Length; i++)
            {
                bool matches = true;
                for (int j = 0; j < oldBlockLines.Length; j++)
                {
                    if (!string.Equals(lines[i + j].TrimEnd(), oldBlockLines[j].TrimEnd(), StringComparison.Ordinal))
                    {
                        matches = false;
                        break;
                    }
                }

                if (!matches)
                    continue;

                lines.RemoveRange(i, oldBlockLines.Length);
                lines.InsertRange(i, newBlockLines);
                return true;
            }

            return false;
        }

        private static bool TryReplaceBlockIgnoringIndentation(List<string> lines, string[] oldBlockLines, string[] newBlockLines)
        {
            if (oldBlockLines.Length == 0)
                return false;

            var normalizedOld = oldBlockLines.Select(NormalizeLineForLooseMatch).ToArray();
            for (int i = 0; i <= lines.Count - oldBlockLines.Length; i++)
            {
                bool matches = true;
                for (int j = 0; j < oldBlockLines.Length; j++)
                {
                    if (!string.Equals(NormalizeLineForLooseMatch(lines[i + j]), normalizedOld[j], StringComparison.Ordinal))
                    {
                        matches = false;
                        break;
                    }
                }

                if (!matches)
                    continue;

                string targetIndent = GetLeadingWhitespace(lines[i]);
                string[] reindentedNew = ReindentBlock(newBlockLines, targetIndent);
                lines.RemoveRange(i, oldBlockLines.Length);
                lines.InsertRange(i, reindentedNew);
                return true;
            }

            return false;
        }

        private static string NormalizeLineForLooseMatch(string line) =>
            line.Trim().Replace("\t", "    ", StringComparison.Ordinal);

        private static string GetLeadingWhitespace(string line)
        {
            int count = 0;
            while (count < line.Length && char.IsWhiteSpace(line[count]))
                count++;
            return count == 0 ? string.Empty : line[..count];
        }

        private static string[] ReindentBlock(string[] blockLines, string targetIndent)
        {
            int commonIndent = GetCommonIndentWidth(blockLines);
            return blockLines.Select(line =>
            {
                if (string.IsNullOrWhiteSpace(line))
                    return string.Empty;

                var trimmed = commonIndent > 0 && line.Length >= commonIndent
                    ? line[commonIndent..]
                    : line.TrimStart();
                return targetIndent + trimmed;
            }).ToArray();
        }

        private static int GetCommonIndentWidth(IEnumerable<string> lines)
        {
            int? common = null;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                int count = 0;
                while (count < line.Length && char.IsWhiteSpace(line[count]))
                    count++;

                common = common.HasValue ? Math.Min(common.Value, count) : count;
            }

            return common ?? 0;
        }

        private static string CreateDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: No path provided.";
            if (Directory.Exists(path)) return $"Directory already exists: {path}";
            Directory.CreateDirectory(path);
            return $"Created directory: {path}";
        }

        private static string DeleteFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: No path provided.";
            if (!File.Exists(path)) return $"Error: File not found: {path}";
            File.Delete(path);
            return $"Deleted: {path}";
        }

        private static string MoveFile(string from, string to)
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                return "Error: Source or destination path missing.";
            if (!File.Exists(from) && !Directory.Exists(from))
                return $"Error: Source not found: {from}";
            var toDir = System.IO.Path.GetDirectoryName(to);
            if (!string.IsNullOrEmpty(toDir) && !Directory.Exists(toDir))
                Directory.CreateDirectory(toDir);
            if (File.Exists(from)) File.Move(from, to);
            else Directory.Move(from, to);
            return $"Moved: '{from}' \u2192 '{to}'";
        }

        private static string SearchFiles(string directory, string pattern)
        {
            if (string.IsNullOrWhiteSpace(directory)) return "Error: No directory provided.";
            if (!Directory.Exists(directory)) return $"Error: Directory not found: {directory}";
            var searchPattern = string.IsNullOrWhiteSpace(pattern) ? "*" : pattern;
            var files = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories)
                                 .Take(200).ToArray();
            if (files.Length == 0)
                return $"No files found matching '{searchPattern}' in: {directory}";
            var sb = new StringBuilder();
            sb.AppendLine($"Found {files.Length} file(s) matching '{searchPattern}' in: {directory}");
            foreach (var f in files) sb.AppendLine($"  {f}");
            return sb.ToString().TrimEnd();
        }

        private static string SearchFileContent(ToolCallRequest request)
        {
            var directory   = GetParam(request, "directory");
            var pattern     = GetParam(request, "pattern");
            var filePattern = GetParam(request, "file_pattern");
            int maxResults  = 50;
            if (request.Parameters.TryGetProperty("max_results", out var mrEl) &&
                mrEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                maxResults = Math.Clamp(mrEl.GetInt32(), 1, 500);

            if (string.IsNullOrWhiteSpace(directory)) return "Error: directory is required.";
            if (string.IsNullOrWhiteSpace(pattern))   return "Error: pattern is required.";
            if (!Directory.Exists(directory))          return $"Error: Directory not found: {directory}";

            try
            {
                var searchPattern = string.IsNullOrWhiteSpace(filePattern) ? "*" : filePattern;
                var files         = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);

                Regex? regex = null;
                try { regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled); }
                catch { /* fall back to literal search */ }

                var sb      = new StringBuilder();
                int matches = 0;

                foreach (var file in files)
                {
                    if (matches >= maxResults) break;
                    try
                    {
                        var lines    = File.ReadAllLines(file);
                        bool fileHit = false;
                        for (int i = 0; i < lines.Length && matches < maxResults; i++)
                        {
                            bool hit = regex != null
                                ? regex.IsMatch(lines[i])
                                : lines[i].Contains(pattern, StringComparison.OrdinalIgnoreCase);
                            if (!hit) continue;

                            if (!fileHit)
                            {
                                sb.AppendLine(file);
                                fileHit = true;
                            }
                            sb.AppendLine($"  {i + 1}: {lines[i].Trim()}");
                            matches++;
                        }
                    }
                    catch { /* skip unreadable files */ }
                }

                if (matches == 0) return $"No matches found for '{pattern}' in {directory}";
                sb.Insert(0, $"{matches} match(es) for '{pattern}':\n");
                if (matches >= maxResults) sb.AppendLine($"\n[Limit of {maxResults} results reached]");
                return sb.ToString().TrimEnd();
            }
        catch
            {
            return "File system operation failed.";
            }
        }

        private static string FormatSize(long bytes) => bytes switch
        {
            < 1024                => $"{bytes} B",
            < 1024 * 1024         => $"{bytes / 1024.0:F1} KB",
            < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _                     => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
        };

        private static bool IsReservedDeviceName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var trimmed = name.Trim().TrimEnd('.');
            return trimmed.Equals("con", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.Equals("prn", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.Equals("aux", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.Equals("nul", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("com", StringComparison.OrdinalIgnoreCase) && trimmed.Length == 4 && char.IsDigit(trimmed[3]) ||
                   trimmed.StartsWith("lpt", StringComparison.OrdinalIgnoreCase) && trimmed.Length == 4 && char.IsDigit(trimmed[3]);
        }
    }
}
