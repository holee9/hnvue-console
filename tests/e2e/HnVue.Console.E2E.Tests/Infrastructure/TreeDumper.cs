using FlaUI.Core.AutomationElements;
using System.Text;

namespace HnVue.Console.E2E.Tests;

/// <summary>
/// Dumps UIAutomation element tree as a diagnostic string.
/// Automatically called by TestBase on test failure to identify missing elements
/// and diagnose XAML rendering issues without needing a live debugger.
/// </summary>
public static class TreeDumper
{
    /// <summary>
    /// Dumps the UIAutomation element tree rooted at <paramref name="root"/> up to <paramref name="maxDepth"/> levels.
    /// </summary>
    public static string Dump(AutomationElement? root, int maxDepth = 4)
    {
        if (root == null) return "(null root element)";
        var sb = new StringBuilder();
        sb.AppendLine("=== UIAutomation Tree Dump ===");
        DumpElement(root, sb, 0, maxDepth);
        return sb.ToString();
    }

    private static void DumpElement(AutomationElement el, StringBuilder sb, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;
        var indent = new string(' ', depth * 2);
        try
        {
            var id = el.AutomationId ?? "";
            var name = el.Name ?? "";
            var type = el.ControlType.ToString();
            sb.AppendLine($"{indent}[{type}] id='{id}' name='{name}'");

            var children = el.FindAllChildren();
            foreach (var child in children)
                DumpElement(child, sb, depth + 1, maxDepth);
        }
        catch
        {
            sb.AppendLine($"{indent}(error reading element)");
        }
    }
}
