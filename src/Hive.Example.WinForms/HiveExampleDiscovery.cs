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
                examples.Add(example);
        }

        examples.Sort(static (left, right) =>
        {
            var order = left.Order.CompareTo(right.Order);
            if (order != 0)
                return order;

            var category = StringComparer.OrdinalIgnoreCase.Compare(
                left.Category,
                right.Category);

            if (category != 0)
                return category;

            var subcategory = StringComparer.OrdinalIgnoreCase.Compare(
                left.Subcategory,
                right.Subcategory);

            return subcategory != 0
                ? subcategory
                : StringComparer.OrdinalIgnoreCase.Compare(
                    left.Title,
                    right.Title);
        });

        return examples.ToArray();
    }
}
