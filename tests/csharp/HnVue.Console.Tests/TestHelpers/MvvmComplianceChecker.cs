using Moq;
using Moq.Protected;
using System.Reflection;
using System.Windows.Data;

namespace HnVue.Console.Tests.TestHelpers;

/// <summary>
/// MVVM compliance test helper.
/// SPEC-UI-001: FR-UI-00 MVVM architecture validation.
/// </summary>
public static class MvvmComplianceChecker
{
    /// <summary>
    /// ViewModels must not reference System.Windows types directly.
    /// Only data binding and value converters should interact with WPF types.
    /// </summary>
    private static readonly HashSet<string> ProhibitedNamespaces = new(StringComparer.Ordinal)
    {
        "System.Windows",
        "System.Windows.Controls",
        "System.Windows.Data",
        "System.Windows.Input",
        "System.Windows.Media",
        "System.Windows.Shapes",
        "Microsoft.Xaml.Behaviors"
    };

    /// <summary>
    /// Allowed exceptions (interfaces and types that ViewModels may use).
    /// WriteableBitmap is permitted as it is necessary for WPF image display in acquisition and review contexts.
    /// </summary>
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.Ordinal)
    {
        "System.Windows.Input.ICommand",
        "System.ComponentModel.INotifyPropertyChanged",
        "System.ComponentModel.IEditableObject",
        "System.ComponentModel.IDataErrorInfo",
        "System.ComponentModel.INotifyDataErrorInfo",
        // WriteableBitmap is required for high-performance preview rendering in AcquisitionViewModel
        // and ImageReviewViewModel (direct pixel manipulation needed for DICOM image display).
        "System.Windows.Media.Imaging.WriteableBitmap"
    };

    /// <summary>
    /// Checks if a ViewModel type complies with MVVM rules.
    /// </summary>
    /// <param name="viewModelType">The ViewModel type to check.</param>
    /// <returns>True if compliant, false otherwise with violation details.</returns>
    public static (bool IsCompliant, IList<string> Violations) CheckCompliance(Type viewModelType)
    {
        var violations = new List<string>();

        // Skip if not a ViewModel (ends with ViewModel)
        if (!viewModelType.Name.EndsWith("ViewModel") && viewModelType.Name != "ViewModelBase")
        {
            return (true, violations);
        }

        // Check base type
        if (viewModelType.BaseType != null)
        {
            CheckTypeForViolations(viewModelType.BaseType, violations);
        }

        // Check interfaces
        foreach (var iface in viewModelType.GetInterfaces())
        {
            if (!AllowedTypes.Contains(iface.FullName ?? ""))
            {
                var ns = iface.Namespace ?? "";
                if (ProhibitedNamespaces.Any(p => ns.StartsWith(p, StringComparison.Ordinal)))
                {
                    violations.Add($"ViewModel {viewModelType.Name} implements prohibited interface: {iface.FullName}");
                }
            }
        }

        // Check properties
        foreach (var prop in viewModelType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            CheckTypeForViolations(prop.PropertyType, violations, $"{viewModelType.Name}.{prop.Name}");
        }

        // Check methods
        foreach (var method in viewModelType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (method.IsSpecialName) continue; // Skip properties, events, etc.

            CheckTypeForViolations(method.ReturnType, violations, $"{viewModelType.Name}.{method.Name}()");

            foreach (var param in method.GetParameters())
            {
                CheckTypeForViolations(param.ParameterType, violations, $"{viewModelType.Name}.{method.Name}() parameter '{param.Name}'");
            }
        }

        // Check fields
        foreach (var field in viewModelType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            CheckTypeForViolations(field.FieldType, violations, $"{viewModelType.Name}.{field.Name} field");
        }

        return (violations.Count == 0, violations);
    }

    /// <summary>
    /// Checks all ViewModels in an assembly for MVVM compliance.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>Compliance report with all violations.</returns>
    public static MvvmComplianceReport ScanAssembly(Assembly assembly)
    {
        var viewModelTypes = assembly.GetTypes()
            .Where(t => t.Name.EndsWith("ViewModel") || t.Name == "ViewModelBase")
            .Where(t => t.IsClass && !t.IsAbstract)
            .ToList();

        var allViolations = new List<string>();
        var compliantCount = 0;

        foreach (var type in viewModelTypes)
        {
            var (isCompliant, violations) = CheckCompliance(type);
            if (isCompliant)
            {
                compliantCount++;
            }
            else
            {
                allViolations.Add($"=== {type.Name} ===");
                allViolations.AddRange(violations);
            }
        }

        return new MvvmComplianceReport
        {
            TotalViewModels = viewModelTypes.Count,
            CompliantCount = compliantCount,
            Violations = allViolations
        };
    }

    private static void CheckTypeForViolations(Type type, IList<string> violations, string? context = null)
    {
        var fullName = type.FullName ?? "";

        // Skip allowed types and primitives
        if (AllowedTypes.Contains(fullName) ||
            type.IsPrimitive ||
            type == typeof(void) ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            fullName == "System.Threading.CancellationToken" ||
            fullName == "System.IAsyncResult" ||
            fullName == "System.IAsyncResult" ||
            fullName.StartsWith("System.Threading.Tasks", StringComparison.Ordinal) ||
            fullName.StartsWith("System.Collections", StringComparison.Ordinal) ||
            fullName.StartsWith("System.Collections.Generic", StringComparison.Ordinal) ||
            fullName.StartsWith("System.Linq", StringComparison.Ordinal))
        {
            return;
        }

        // Check for prohibited namespaces
        var ns = type.Namespace ?? "";
        if (ProhibitedNamespaces.Any(p => ns.StartsWith(p, StringComparison.Ordinal)))
        {
            var contextStr = string.IsNullOrEmpty(context) ? "" : $" in {context}";
            violations.Add($"  Prohibited type '{fullName}'{contextStr}");
        }

        // Check generic type arguments
        if (type.IsGenericType)
        {
            foreach (var typeArg in type.GetGenericArguments())
            {
                CheckTypeForViolations(typeArg, violations, context);
            }
        }

        // Check element type for arrays
        if (type.IsArray)
        {
            CheckTypeForViolations(type.GetElementType()!, violations, context);
        }
    }
}

/// <summary>
/// MVVM compliance scan result.
/// </summary>
public record MvvmComplianceReport
{
    public required int TotalViewModels { get; init; }
    public required int CompliantCount { get; init; }
    public required IList<string> Violations { get; init; }

    public bool IsFullyCompliant => Violations.Count == 0;
    public double CompliancePercentage => TotalViewModels > 0 ? (double)CompliantCount / TotalViewModels * 100 : 100;

    public string GetSummary()
    {
        if (IsFullyCompliant)
        {
            return $"✓ All {TotalViewModels} ViewModels are MVVM compliant";
        }

        return $"✗ {CompliantCount}/{TotalViewModels} ViewModels compliant ({CompliancePercentage:F1}%)";
    }
}
