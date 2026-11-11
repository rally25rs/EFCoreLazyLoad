namespace Rally25rs.EFCoreLazyLoad.Internal;

internal static class TypeNames
{
    /// <summary>Short, readable type name: <c>Customer</c>, <c>Outer.Inner</c>, <c>Box&lt;Int32&gt;</c>.</summary>
    public static string Display(Type type)
    {
        var name = type.Name;
        if (type.IsGenericType)
        {
            var tick = name.IndexOf('`', StringComparison.Ordinal);
            if (tick >= 0)
            {
                name = name[..tick];
            }

            name += "<" + string.Join(", ", type.GetGenericArguments().Select(Display)) + ">";
        }

        return type.IsNested && !type.IsGenericParameter && type.DeclaringType is { } declaring
            ? Display(declaring) + "." + name
            : name;
    }
}
