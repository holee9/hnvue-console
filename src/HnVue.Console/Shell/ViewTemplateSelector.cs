using System.Windows;
using System.Windows.Controls;

namespace HnVue.Console.Shell;

/// <summary>
/// DataTemplateSelector for routing view names to corresponding View templates.
/// SPEC-UI-001 Navigation Infrastructure.
/// </summary>
public class ViewTemplateSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not string viewName)
            return base.SelectTemplate(item, container);

        // Find the DataTemplate by key in the Window's resources
        if (container is FrameworkElement element && element.TryFindResource($"{viewName}Template") is DataTemplate template)
        {
            return template;
        }

        // Fallback to empty template (or could show a "View not found" message)
        return base.SelectTemplate(item, container);
    }
}
