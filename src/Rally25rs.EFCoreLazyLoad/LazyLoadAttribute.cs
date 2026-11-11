namespace Rally25rs.EFCoreLazyLoad;

/// <summary>
///     Marks an entity class, or individual navigation properties of it, for lazy loading.
///     The Rally25rs.EFCoreLazyLoad source generator emits the implementation of each marked
///     <c>partial</c> property so that its getter asks EF Core's <c>ILazyLoader</c> to load the navigation
///     on first access.
/// </summary>
/// <remarks>
///     <para>
///         On a class, every <c>partial</c> property declaration that has no implementation part gets a lazy-loading
///         getter. On a property, only that property does. In both cases the class must be <c>partial</c>.
///     </para>
///     <para>
///         Only reference-type properties (reference and collection navigations) can be lazy loaded.
///     </para>
///     <para>
///         How a lazy load behaves (load, log, or throw) is configured with
///         <see cref="LazyLoadDbContextOptionsBuilderExtensions.UseLazyLoad(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder, System.Action{LazyLoadOptions}?)" />.
///         Without <c>UseLazyLoad</c>, marked navigations get EF Core's standard lazy-loading behavior.
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, Inherited = false)]
public sealed class LazyLoadAttribute : Attribute
{
}
