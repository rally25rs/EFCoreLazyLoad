using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Rally25rs.EFCoreLazyLoad.Generators;

// Pipeline outputs must be value-equatable (no ISymbol / SyntaxNode / Location) so the
// incremental driver can cache them between runs.

/// <summary>An immutable array with structural equality.</summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _items;

    public EquatableArray(ImmutableArray<T> items) => _items = items;

    public ImmutableArray<T> Items => _items.IsDefault ? ImmutableArray<T>.Empty : _items;

    public int Length => Items.Length;

    public bool Equals(EquatableArray<T> other) => Items.SequenceEqual(other.Items);

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in Items)
        {
            hash = unchecked((hash * 31) + (item?.GetHashCode() ?? 0));
        }

        return hash;
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal static class EquatableArray
{
    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> items)
        where T : IEquatable<T>
        => new(items.ToImmutableArray());
}

/// <summary>A serializable stand-in for <see cref="Location"/>.</summary>
internal sealed record LocationInfo(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, Span, LineSpan);

    public static LocationInfo? From(Location? location)
    {
        if (location is null || location.SourceTree is null)
        {
            return null;
        }

        return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
    }
}

/// <summary>A serializable stand-in for <see cref="Diagnostic"/>.</summary>
internal sealed record DiagnosticInfo(string Id, LocationInfo? Location, EquatableArray<string> MessageArgs)
{
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, params string[] args)
        => new(descriptor.Id, LocationInfo.From(location), args.ToEquatableArray());
}

/// <summary>A type declaration that encloses the entity (outermost first).</summary>
internal sealed record ContainingTypeModel(string Keyword, string NameWithTypeParameters);

internal sealed record AccessorModel(string Keyword, string Modifiers);

internal sealed record PropertyModel(
    string Modifiers,
    string Type,
    string Name,
    bool IsNullable,
    AccessorModel Getter,
    AccessorModel? Setter);

internal sealed record TypeModel(
    string HintName,
    string? Namespace,
    EquatableArray<ContainingTypeModel> ContainingTypes,
    string Keyword,
    string NameWithTypeParameters,
    // null when no loader field should be emitted (the type or a base type already has one).
    string? LoaderFieldAccessibility,
    string LoaderMemberName,
    EquatableArray<PropertyModel> Properties,
    EquatableArray<DiagnosticInfo> Diagnostics)
{
    /// <summary>False when diagnostics prevent emitting source for this type.</summary>
    public bool CanEmit { get; init; } = true;
}
