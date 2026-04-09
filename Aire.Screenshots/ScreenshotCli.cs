using System.Text.Json;
using System.IO;
using System.Diagnostics;

namespace Aire.Screenshots;

internal static class ScreenshotCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || HasHelpFlag(args))
        {
            PrintUsage();
            return 1;
        }

        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "list-windows":
                    ListWindows();
                    return 0;

                case "capture-window":
                    await CaptureWindowAsync(ParseWindowRequest(args[1..]));
                    return 0;

                case "capture-active":
                    await CaptureWindowAsync(ParseActiveRequest(args[1..]));
                    return 0;

                case "run-plan":
                    var planPath = ParseRequiredValue(args[1..], "--plan");
                    var language = ParseOptionalValue(args[1..], "--language") ?? "en";
                    await RunPlanAsync(planPath, language);
                    return 0;

                case "run-plan-all":
                    var planPathAll = ParseRequiredValue(args[1..], "--plan");
                    await RunPlanAllAsync(planPathAll);
                    return 0;

                default:
                    Console.Error.WriteLine($"Unknown command '{args[0]}'.");
                    PrintUsage();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static void ListWindows()
    {
        foreach (var window in NativeWindowFinder.ListWindows())
            Console.WriteLine($"{window.ProcessName}\t{window.Title}");
    }

    private static string AdjustOutputPathForLanguage(string outputPath, string language)
    {
        // Always place screenshots in a per-language subfolder (including English).
        var directory = Path.GetDirectoryName(outputPath) ?? string.Empty;
        var fileName = Path.GetFileName(outputPath);
        return Path.Combine(directory, language, fileName);
    }

    private static async Task RunPlanAsync(string planPath, string language = "en")
    {
        if (!File.Exists(planPath))
            throw new FileNotFoundException("Plan file not found.", planPath);

        // Persist language so Aire picks it up on (re)start.
        LanguageHelper.SetAppStateLanguage(language);

        await using var stream = File.OpenRead(planPath);
        var plan = await JsonSerializer.DeserializeAsync<ScreenshotPlan>(stream, JsonOptions.Default)
            ?? throw new InvalidOperationException("Failed to deserialize screenshot plan.");

        await UiAutomationRunner.RunActionsAsync(plan.SetupActions);

        // Switch language in the running app via Local API, then wait for the UI to redraw.
        await UiAutomationRunner.SetLanguageAsync(language);
        await Task.Delay(1500);

        foreach (var request in plan.Screenshots)
        {
            var adjustedRequest = request with
            {
                OutputPath = AdjustOutputPathForLanguage(request.OutputPath, language)
            };
            await CaptureWindowAsync(adjustedRequest);
        }
    }

    private static async Task RunPlanAllAsync(string planPath)
    {
        if (!File.Exists(planPath))
            throw new FileNotFoundException("Plan file not found.", planPath);

        await using var stream = File.OpenRead(planPath);
        var plan = await JsonSerializer.DeserializeAsync<ScreenshotPlan>(stream, JsonOptions.Default)
            ?? throw new InvalidOperationException("Failed to deserialize screenshot plan.");

        // Run setup actions once (start app, close update dialog, show main window).
        await UiAutomationRunner.RunActionsAsync(plan.SetupActions);

        var languages = plan.LanguageBatch?.Languages.Count > 0
            ? plan.LanguageBatch.Languages
            : LanguageHelper.GetAvailableLanguageCodes();

        var switchDelay = plan.LanguageBatch?.SwitchDelayMs ?? 1500;

        foreach (var lang in languages)
        {
            Console.WriteLine($"--- Language: {lang} ---");

            await RestartAppForLanguageAsync(plan, lang);

            // Give the fresh app instance time to settle before screenshots start.
            await Task.Delay(switchDelay);

            foreach (var request in plan.Screenshots)
            {
                var adjusted = request with
                {
                    OutputPath = AdjustOutputPathForLanguage(request.OutputPath, lang)
                };
                await CaptureWindowAsync(adjusted);
            }
        }
    }

    private static async Task RestartAppForLanguageAsync(ScreenshotPlan plan, string lang)
    {
        KillAireProcesses();

        // Persist language so Aire picks it up on the fresh launch.
        LanguageHelper.SetAppStateLanguage(lang);

        // Start from a clean slate for each language so we don't carry over
        // provider / conversation state from the previous batch.
        await UiAutomationRunner.RunActionsAsync(plan.SetupActions);

        await UiAutomationRunner.SetLanguageAsync(lang);
    }

    private static void KillAireProcesses()
    {
        foreach (var process in Process.GetProcessesByName("Aire"))
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort cleanup. The fresh launch below will fail fast if the old instance is still alive.
            }
            finally
            {
                try { process.Dispose(); } catch { }
            }
        }
    }

    private static async Task CaptureWindowAsync(ScreenshotRequest request)
    {
        if (IsMainWindowCapture(request))
            await UiAutomationRunner.RunActionsAsync([new UiAutomationAction { Kind = "close-update-window", DelayMs = 250 }], request);

        await UiAutomationRunner.RunActionsAsync(request.Actions, request);

        if (IsMainWindowCapture(request))
            await UiAutomationRunner.RunActionsAsync([new UiAutomationAction { Kind = "close-update-window", DelayMs = 250 }], request);

        if (request.DelayMs > 0)
            await Task.Delay(request.DelayMs);

        using var bitmap = WindowCaptureService.Capture(request);
        var outputPath = Path.GetFullPath(request.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine(outputPath);
    }

    private static bool IsMainWindowCapture(ScreenshotRequest request)
        => string.Equals(request.ProcessName, "Aire", StringComparison.OrdinalIgnoreCase)
           && (string.Equals(request.ExactTitle, "Aire", StringComparison.OrdinalIgnoreCase)
               || string.Equals(request.TitleContains, "Aire", StringComparison.OrdinalIgnoreCase));

    private static ScreenshotRequest ParseWindowRequest(string[] args)
    {
        var titleContains = ParseOptionalValue(args, "--title-contains");
        var exactTitle = ParseOptionalValue(args, "--exact-title");
        var processName = ParseOptionalValue(args, "--process");
        var output = ParseRequiredValue(args, "--output");
        var delayMs = ParseIntValue(args, "--delay-ms", 0);
        var padding = ParseIntValue(args, "--padding", 16);
        var activate = HasFlag(args, "--activate");

        if (string.IsNullOrWhiteSpace(titleContains) && string.IsNullOrWhiteSpace(exactTitle))
            throw new InvalidOperationException("capture-window requires --title-contains or --exact-title.");

        return new ScreenshotRequest(
            output,
            exactTitle,
            titleContains,
            processName,
            delayMs,
            padding,
            activate,
            UseActiveWindow: false,
            Actions: null);
    }

    private static ScreenshotRequest ParseActiveRequest(string[] args)
    {
        var output = ParseRequiredValue(args, "--output");
        var delayMs = ParseIntValue(args, "--delay-ms", 0);
        var padding = ParseIntValue(args, "--padding", 16);

        return new ScreenshotRequest(
            output,
            ExactTitle: null,
            TitleContains: null,
            ProcessName: null,
            delayMs,
            padding,
            ActivateWindow: false,
            UseActiveWindow: true,
            Actions: null);
    }

    private static bool HasHelpFlag(string[] args)
        => args.Any(arg => string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase));

    private static bool HasFlag(string[] args, string flag)
        => args.Any(arg => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase));

    private static string ParseRequiredValue(string[] args, string key)
        => ParseOptionalValue(args, key)
           ?? throw new InvalidOperationException($"Missing required argument {key}.");

    private static string? ParseOptionalValue(string[] args, string key)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static int ParseIntValue(string[] args, string key, int defaultValue)
    {
        var value = ParseOptionalValue(args, key);
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            Aire.Screenshots

            Commands:
              list-windows
              capture-window --title-contains "<text>" --output "<path>" [--process Aire] [--delay-ms 500] [--padding 16] [--activate]
              capture-window --exact-title "<text>" --output "<path>" [--delay-ms 500] [--padding 16] [--activate]
              capture-active --output "<path>" [--delay-ms 500] [--padding 16]
              run-plan --plan "<path-to-json>" [--language <code>]
              run-plan-all --plan "<path-to-json>"

            Plan JSON shape:
              {
                "setupActions": [
                  {
                    "kind": "start-process",
                    "executablePath": "C:/dev/aire/Aire/bin/Debug/net10.0-windows10.0.17763.0/Aire.exe",
                    "delayMs": 1500
                  }
                ],
                "screenshots": [
                  {
                    "outputPath": "Assets/Help/main-window.png",
                    "titleContains": "Aire",
                    "processName": "Aire",
                    "delayMs": 750,
                    "padding": 12,
                    "activateWindow": true,
                    "actions": [
                      {
                        "kind": "invoke",
                        "automationId": "PART_ModeButton",
                        "delayMs": 250
                      }
                    ]
                  }
                ]
              }
            """);
    }
}
