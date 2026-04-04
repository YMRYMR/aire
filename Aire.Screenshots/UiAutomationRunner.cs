using System.Diagnostics;
using System.Windows.Automation;

namespace Aire.Screenshots;

internal static class UiAutomationRunner
{
    private static readonly LocalApiClient LocalApiClient = new();

    public static async Task RunActionsAsync(IEnumerable<UiAutomationAction>? actions, ScreenshotRequest? defaultWindow = null)
    {
        if (actions == null)
            return;

        foreach (var action in actions)
            await RunActionAsync(action, defaultWindow);
    }

    private static async Task RunActionAsync(UiAutomationAction action, ScreenshotRequest? defaultWindow)
    {
        switch (Normalize(action.Kind))
        {
            case "wait":
                await Task.Delay(Math.Max(0, action.DelayMs));
                return;

            case "startprocess":
                StartProcess(action);
                await DelayIfNeededAsync(action);
                return;

            case "waitforwindow":
                await WaitForWindowAsync(BuildWindowSelector(action, defaultWindow), action.DelayMs);
                return;

            case "focuswindow":
                FocusWindow(BuildWindowSelector(action, defaultWindow));
                await DelayIfNeededAsync(action);
                return;

            case "invoke":
                InvokeElement(action, defaultWindow);
                await DelayIfNeededAsync(action);
                return;

            case "select":
                SelectElement(action, defaultWindow);
                await DelayIfNeededAsync(action);
                return;

            case "selectcomboitem":
                SelectComboItem(action, defaultWindow);
                await DelayIfNeededAsync(action);
                return;

            case "setactiveproviderbyname":
                await SetActiveProviderByNameAsync(action);
                await DelayIfNeededAsync(action);
                return;

            default:
                throw new InvalidOperationException($"Unsupported automation action kind '{action.Kind}'.");
        }
    }

    private static void StartProcess(UiAutomationAction action)
    {
        if (string.IsNullOrWhiteSpace(action.ExecutablePath))
            throw new InvalidOperationException("start-process action requires executablePath.");

        var startInfo = new ProcessStartInfo
        {
            FileName = action.ExecutablePath,
            Arguments = action.Arguments ?? string.Empty,
            UseShellExecute = true,
        };

        Process.Start(startInfo);
    }

    private static async Task WaitForWindowAsync(ScreenshotRequest selector, int timeoutMs)
    {
        var effectiveTimeoutMs = timeoutMs > 0 ? timeoutMs : 5000;
        var startedAt = Environment.TickCount64;

        while (Environment.TickCount64 - startedAt < effectiveTimeoutMs)
        {
            if (NativeWindowFinder.TryGetWindow(selector, out _))
                return;

            await Task.Delay(150);
        }

        throw new InvalidOperationException("Timed out waiting for the requested window.");
    }

    private static void FocusWindow(ScreenshotRequest selector)
    {
        var window = NativeWindowFinder.GetWindow(selector);
        NativeWindowFinder.ActivateWindow(window.Handle);
    }

    private static void InvokeElement(UiAutomationAction action, ScreenshotRequest? defaultWindow)
    {
        var element = FindTargetElement(action, defaultWindow);
        var invokable = FindInvokableElement(element);

        if (invokable.TryGetCurrentPattern(InvokePattern.Pattern, out var invokePattern))
        {
            ((InvokePattern)invokePattern).Invoke();
            return;
        }

        if (invokable.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandPattern))
        {
            ((ExpandCollapsePattern)expandPattern).Expand();
            return;
        }

        if (invokable.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionPattern))
        {
            ((SelectionItemPattern)selectionPattern).Select();
            return;
        }

        throw new InvalidOperationException($"Element '{DescribeTarget(action)}' does not support invoke-like patterns.");
    }

    private static void SelectElement(UiAutomationAction action, ScreenshotRequest? defaultWindow)
    {
        var element = FindTargetElement(action, defaultWindow);

        if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionPattern))
        {
            ((SelectionItemPattern)selectionPattern).Select();
            return;
        }

        if (element.TryGetCurrentPattern(TogglePattern.Pattern, out var togglePattern))
        {
            var toggle = (TogglePattern)togglePattern;
            if (toggle.Current.ToggleState != ToggleState.On)
                toggle.Toggle();
            return;
        }

        throw new InvalidOperationException($"Element '{DescribeTarget(action)}' does not support select-like patterns.");
    }

    private static void SelectComboItem(UiAutomationAction action, ScreenshotRequest? defaultWindow)
    {
        if (string.IsNullOrWhiteSpace(action.AutomationId))
            throw new InvalidOperationException("select-combo-item action requires automationId for the ComboBox.");

        if (string.IsNullOrWhiteSpace(action.Name))
            throw new InvalidOperationException("select-combo-item action requires name for the target item.");

        var selector = BuildWindowSelector(action, defaultWindow);
        var window = NativeWindowFinder.GetWindow(selector);
        NativeWindowFinder.ActivateWindow(window.Handle);

        var root = AutomationElement.FromHandle(window.Handle)
            ?? throw new InvalidOperationException("Failed to inspect the target window for combo-box automation.");

        var combo = root.FindFirst(
            TreeScope.Descendants,
            new AndCondition(
                new PropertyCondition(AutomationElement.AutomationIdProperty, action.AutomationId),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox)))
            ?? throw new InvalidOperationException($"Failed to find ComboBox '{action.AutomationId}' in window '{window.Title}'.");

        if (combo.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandPattern))
        {
            var expand = (ExpandCollapsePattern)expandPattern;
            if (expand.Current.ExpandCollapseState != ExpandCollapseState.Expanded)
            {
                expand.Expand();
                Thread.Sleep(150);
            }
        }
        else if (combo.TryGetCurrentPattern(InvokePattern.Pattern, out var invokePattern))
        {
            ((InvokePattern)invokePattern).Invoke();
            Thread.Sleep(150);
        }
        else
        {
            throw new InvalidOperationException($"ComboBox '{action.AutomationId}' cannot be expanded.");
        }

        var itemCondition = new AndCondition(
            new PropertyCondition(AutomationElement.NameProperty, action.Name),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));

        var item = root.FindFirst(TreeScope.Descendants, itemCondition)
                   ?? AutomationElement.RootElement.FindFirst(TreeScope.Descendants, itemCondition);

        if (item == null)
            throw new InvalidOperationException($"Failed to find ComboBox item '{action.Name}'.");

        if (item.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionPattern))
        {
            ((SelectionItemPattern)selectionPattern).Select();
            return;
        }

        if (item.TryGetCurrentPattern(InvokePattern.Pattern, out var itemInvokePattern))
        {
            ((InvokePattern)itemInvokePattern).Invoke();
            return;
        }

        throw new InvalidOperationException($"ComboBox item '{action.Name}' is not selectable.");
    }

    private static async Task SetActiveProviderByNameAsync(UiAutomationAction action)
    {
        if (string.IsNullOrWhiteSpace(action.Name))
            throw new InvalidOperationException("set-active-provider-by-name action requires a provider name.");

        await LocalApiClient.SetActiveProviderByNameAsync(action.Name);
    }

    private static AutomationElement FindTargetElement(UiAutomationAction action, ScreenshotRequest? defaultWindow)
    {
        var selector = BuildWindowSelector(action, defaultWindow);
        var window = NativeWindowFinder.GetWindow(selector);
        NativeWindowFinder.ActivateWindow(window.Handle);

        var root = AutomationElement.FromHandle(window.Handle)
            ?? throw new InvalidOperationException("Failed to inspect the target window for UI automation.");

        var condition = BuildElementCondition(action);
        var element = root.FindFirst(TreeScope.Descendants, condition);
        if (element != null)
            return element;

        throw new InvalidOperationException($"Failed to find UI element '{DescribeTarget(action)}' in window '{window.Title}'.");
    }

    private static AutomationElement FindInvokableElement(AutomationElement element)
    {
        AutomationElement? current = element;
        while (current != null)
        {
            if (current.TryGetCurrentPattern(InvokePattern.Pattern, out _) ||
                current.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out _) ||
                current.TryGetCurrentPattern(SelectionItemPattern.Pattern, out _))
            {
                return current;
            }

            current = TreeWalker.ControlViewWalker.GetParent(current);
        }

        return element;
    }

    private static Condition BuildElementCondition(UiAutomationAction action)
    {
        var conditions = new List<Condition>();

        if (!string.IsNullOrWhiteSpace(action.AutomationId))
            conditions.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, action.AutomationId));

        if (!string.IsNullOrWhiteSpace(action.Name))
            conditions.Add(new PropertyCondition(AutomationElement.NameProperty, action.Name));

        if (!string.IsNullOrWhiteSpace(action.ControlType))
            conditions.Add(new PropertyCondition(AutomationElement.ControlTypeProperty, ParseControlType(action.ControlType)));

        return conditions.Count switch
        {
            0 => throw new InvalidOperationException($"Automation action '{action.Kind}' requires at least one element selector."),
            1 => conditions[0],
            _ => new AndCondition(conditions.ToArray()),
        };
    }

    private static ControlType ParseControlType(string value)
        => Normalize(value) switch
        {
            "button" => ControlType.Button,
            "menuitem" => ControlType.MenuItem,
            "tabitem" => ControlType.TabItem,
            "text" => ControlType.Text,
            "combobox" => ControlType.ComboBox,
            "checkbox" => ControlType.CheckBox,
            _ => throw new InvalidOperationException($"Unsupported control type '{value}'."),
        };

    private static ScreenshotRequest BuildWindowSelector(UiAutomationAction action, ScreenshotRequest? defaultWindow)
        => new(
            OutputPath: defaultWindow?.OutputPath ?? string.Empty,
            ExactTitle: action.ExactTitle ?? defaultWindow?.ExactTitle,
            TitleContains: action.TitleContains ?? defaultWindow?.TitleContains,
            ProcessName: action.ProcessName ?? defaultWindow?.ProcessName,
            DelayMs: 0,
            Padding: defaultWindow?.Padding ?? 0,
            ActivateWindow: true,
            UseActiveWindow: false,
            Actions: null);

    private static async Task DelayIfNeededAsync(UiAutomationAction action)
    {
        if (action.DelayMs > 0)
            await Task.Delay(action.DelayMs);
    }

    private static string Normalize(string value)
        => value.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim()
            .ToLowerInvariant();

    private static string DescribeTarget(UiAutomationAction action)
        => action.AutomationId ?? action.Name ?? action.ControlType ?? "(unknown target)";
}
