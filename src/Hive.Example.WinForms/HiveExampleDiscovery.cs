using System.Reflection;

namespace Hive.Example.WinForms;

internal static class HiveExampleDiscovery
{
    public static IReadOnlyList<IHiveExample> Discover(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var examples = new List<IHiveExample>();

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract ||
                type.IsInterface ||
                !typeof(IHiveExample).IsAssignableFrom(type) ||
                type.GetConstructor(Type.EmptyTypes) is null)
            {
                continue;
            }

            if (Activator.CreateInstance(type, nonPublic: true) is IHiveExample example)
            {
                ValidateExample(example, type);
                examples.Add(example);
            }
        }

        examples.Sort(static (left, right) =>
        {
            var order = left.Order.CompareTo(right.Order);
            if (order != 0)
                return order;

            var path = ComparePaths(left.NavigationPath, right.NavigationPath);
            return path != 0
                ? path
                : StringComparer.OrdinalIgnoreCase.Compare(left.Title, right.Title);
        });

        return examples.ToArray();
    }

    private static void ValidateExample(IHiveExample example, Type exampleType)
    {
        if (example.NavigationPath.Count == 0 ||
            example.NavigationPath.Any(static segment => string.IsNullOrWhiteSpace(segment)))
        {
            throw new InvalidOperationException(
                $"Example '{exampleType.FullName}' must define a non-empty navigation path with no empty segments.");
        }

        if (string.IsNullOrWhiteSpace(example.Title))
        {
            throw new InvalidOperationException(
                $"Example '{exampleType.FullName}' must define a non-empty Title.");
        }
    }

    private static int ComparePaths(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        var count = Math.Min(left.Count, right.Count);

        for (var index = 0; index < count; index++)
        {
            var comparison = StringComparer.OrdinalIgnoreCase.Compare(left[index], right[index]);
            if (comparison != 0)
                return comparison;
        }

        return left.Count.CompareTo(right.Count);
    }
}
