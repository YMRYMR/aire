namespace Aire.Providers;

public static partial class SharedToolDefinitions
{
    private static readonly ToolDescriptor[] FileSystemTools =
    [
        new()
        {
            Name = "execute_command",
            Category = "filesystem",
            Description =
                "Execute a command or launch any application on the user's system. " +
                "This tool can launch GUI desktop applications (e.g. gimp, notepad, chrome, vlc) " +
                "even if they are not in PATH — the system automatically finds installed apps. " +
                "To open GIMP: command=\"gimp\". To open GIMP with a file: command=\"gimp \\\"C:/path/file.png\\\"\". " +
                "Use this tool whenever the user asks to open, launch, or start any application.",
            Parameters = new()
            {
                { "command",           new ToolParam("string",  "Command or application name to execute/launch") },
                { "working_directory", new ToolParam("string",  "Working directory (optional)") },
                { "timeout_seconds",   new ToolParam("integer", "Timeout in seconds (default: 30)") },
                { "shell",             new ToolParam("string",  "Shell to use: auto, cmd, powershell, bash (default: auto)") },
            },
            Required = ["command"],
        },
        new()
        {
            Name = "list_directory", Category = "filesystem",
            Description = "List the contents of a directory on the user's file system.",
            Parameters = new() { { "path", new ToolParam("string", "Absolute path to the directory") } },
            Required = ["path"],
        },
        new()
        {
            Name = "read_file", Category = "filesystem",
            Description = "Read the contents of a file. Supports chunked reading for large files via offset and length. " +
                          "The result always reports total file size and how many characters were returned, so you know if more remains. " +
                          "For files larger than 100 000 chars, read in chunks: call read_file repeatedly with increasing offset until done.",
            Parameters = new()
            {
                { "path",   new ToolParam("string",  "Absolute path to the file") },
                { "offset", new ToolParam("integer", "Character offset to start reading from (default 0)") },
                { "length", new ToolParam("integer", "Maximum number of characters to read (default 100 000)") },
            },
            Required = ["path"],
        },
        new()
        {
            Name = "write_file", Category = "filesystem",
            Description = "Write content to a file. By default overwrites the file. " +
                          "Set append=true to add content to the end of an existing file instead of replacing it. " +
                          "Use append mode to write large content in multiple chunks.",
            Parameters = new()
            {
                { "path",    new ToolParam("string",  "Absolute path to the file") },
                { "content", new ToolParam("string",  "Content to write") },
                { "append",  new ToolParam("boolean", "If true, append to the file instead of overwriting (default false)") },
            },
            Required = ["path", "content"],
        },
        new()
        {
            Name = "create_directory", Category = "filesystem",
            Description = "Create a new directory (including any missing parent directories).",
            Parameters = new() { { "path", new ToolParam("string", "Absolute path of the directory to create") } },
            Required = ["path"],
        },
        new()
        {
            Name = "delete_file", Category = "filesystem",
            Description = "Delete a file or directory.",
            Parameters = new() { { "path", new ToolParam("string", "Absolute path to the file or directory") } },
            Required = ["path"],
        },
        new()
        {
            Name = "move_file", Category = "filesystem",
            Description = "Move or rename a file or directory.",
            Parameters = new()
            {
                { "from", new ToolParam("string", "Source path") },
                { "to",   new ToolParam("string", "Destination path") },
            },
            Required = ["from", "to"],
        },
        new()
        {
            Name = "search_files", Category = "filesystem",
            Description = "Search for files matching a glob pattern inside a directory.",
            Parameters = new()
            {
                { "directory", new ToolParam("string",  "Directory to search in") },
                { "pattern",   new ToolParam("string",  "Glob pattern, e.g. *.txt") },
                { "recursive", new ToolParam("boolean", "Search subdirectories (default: true)") },
            },
            Required = ["directory", "pattern"],
        },
        new()
        {
            Name        = "search_file_content",
            Category    = "filesystem",
            Description =
                "Search for text or a regex pattern inside the CONTENT of files (like grep/ripgrep). " +
                "Returns file paths and the matching lines with line numbers. " +
                "Use this when you need to find which files contain specific code, text, or patterns.",
            Parameters  = new()
            {
                { "directory",    new ToolParam("string",  "Root directory to search in (recursive)") },
                { "pattern",      new ToolParam("string",  "Text or regex pattern to search for inside files") },
                { "file_pattern", new ToolParam("string",  "Optional glob/extension filter, e.g. *.cs or *.txt") },
                { "max_results",  new ToolParam("integer", "Maximum matching lines to return (default: 50)") },
            },
            Required = ["directory", "pattern"],
        },
    ];
}
