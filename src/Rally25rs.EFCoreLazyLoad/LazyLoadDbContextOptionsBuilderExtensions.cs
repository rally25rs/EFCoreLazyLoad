using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rally25rs.EFCoreLazyLoad.Internal;

namespace Rally25rs.EFCoreLazyLoad;

/// <summary>
///     Extension methods on <see cref="DbContextOptionsBuilder" /> for enabling Rally25rs.EFCoreLazyLoad.
/// </summary>
public static class LazyLoadDbContextOptionsBuilderExtensions
{
    /// <summary>
    ///     Enables Rally25rs.EFCoreLazyLoad for this context, so navigations marked with <see cref="LazyLoadAttribute" />
    ///     are lazy loaded, logged, or blocked according to <see cref="LazyLoadOptions" />.
    /// </summary>
    /// <remarks>
    ///     Can be called before or after the database provider is configured. Calling it again updates the
    ///     previously configured options: <paramref name="configure" /> receives the options as they were left by the
    ///     previous call.
    /// </remarks>
    /// <param name="optionsBuilder">The builder being used to configure the context.</param>
    /// <param name="configure">An optional action to configure the <see cref="LazyLoadOptions" />.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public static DbContextOptionsBuilder UseLazyLoad(
        this DbContextOptionsBuilder optionsBuilder,
        Action<LazyLoadOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        var options = GetCurrentOptions(optionsBuilder);
        configure?.Invoke(options);
        return SetOptions(optionsBuilder, options);
    }

    /// <summary>
    ///     Enables Rally25rs.EFCoreLazyLoad for this context, so navigations marked with <see cref="LazyLoadAttribute" />
    ///     are lazy loaded, logged, or blocked according to <see cref="LazyLoadOptions" />.
    /// </summary>
    /// <remarks>
    ///     Can be called before or after the database provider is configured. Calling it again updates the
    ///     previously configured options: <paramref name="configure" /> receives the options as they were left by the
    ///     previous call.
    /// </remarks>
    /// <typeparam name="TContext">The type of context being configured.</typeparam>
    /// <param name="optionsBuilder">The builder being used to configure the context.</param>
    /// <param name="configure">An optional action to configure the <see cref="LazyLoadOptions" />.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public static DbContextOptionsBuilder<TContext> UseLazyLoad<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        Action<LazyLoadOptions>? configure = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseLazyLoad((DbContextOptionsBuilder)optionsBuilder, configure);

    /// <summary>
    ///     Enables Rally25rs.EFCoreLazyLoad for this context using the <see cref="LazyLoadOptions" /> registered with
    ///     <see cref="LazyLoadServiceCollectionExtensions.AddEFCoreLazyLoad" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Use this from the <c>AddDbContext((serviceProvider, options) => ...)</c> overload, or when building
    ///         options yourself for a context created with <c>new</c>. If no <see cref="LazyLoadOptions" /> are
    ///         registered in <paramref name="serviceProvider" />, previously configured options (or the defaults) are
    ///         used. Registered options replace any options configured by an earlier call.
    ///     </para>
    ///     <para>
    ///         When <see cref="LazyLoadOptions.LoggerFactory" /> is not set, the <see cref="ILoggerFactory" /> from
    ///         <paramref name="serviceProvider" /> is used, so log entries reach the application's logging even for
    ///         contexts that EF Core doesn't resolve from it.
    ///     </para>
    /// </remarks>
    /// <param name="optionsBuilder">The builder being used to configure the context.</param>
    /// <param name="serviceProvider">The application service provider.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public static DbContextOptionsBuilder UseLazyLoad(
        this DbContextOptionsBuilder optionsBuilder,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var registered = serviceProvider.GetService<IOptions<LazyLoadOptions>>()?.Value;
        var options = registered?.Clone() ?? GetCurrentOptions(optionsBuilder);
        options.LoggerFactory ??= serviceProvider.GetService<ILoggerFactory>();
        return SetOptions(optionsBuilder, options);
    }

    /// <summary>
    ///     Enables Rally25rs.EFCoreLazyLoad for this context using the <see cref="LazyLoadOptions" /> registered with
    ///     <see cref="LazyLoadServiceCollectionExtensions.AddEFCoreLazyLoad" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Use this from the <c>AddDbContext((serviceProvider, options) => ...)</c> overload, or when building
    ///         options yourself for a context created with <c>new</c>. If no <see cref="LazyLoadOptions" /> are
    ///         registered in <paramref name="serviceProvider" />, previously configured options (or the defaults) are
    ///         used. Registered options replace any options configured by an earlier call.
    ///     </para>
    ///     <para>
    ///         When <see cref="LazyLoadOptions.LoggerFactory" /> is not set, the <see cref="ILoggerFactory" /> from
    ///         <paramref name="serviceProvider" /> is used, so log entries reach the application's logging even for
    ///         contexts that EF Core doesn't resolve from it.
    ///     </para>
    /// </remarks>
    /// <typeparam name="TContext">The type of context being configured.</typeparam>
    /// <param name="optionsBuilder">The builder being used to configure the context.</param>
    /// <param name="serviceProvider">The application service provider.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public static DbContextOptionsBuilder<TContext> UseLazyLoad<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        IServiceProvider serviceProvider)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseLazyLoad((DbContextOptionsBuilder)optionsBuilder, serviceProvider);

    private static LazyLoadOptions GetCurrentOptions(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.Options.FindExtension<LazyLoadOptionsExtension>()?.Options.Clone() ?? new LazyLoadOptions();

    private static DbContextOptionsBuilder SetOptions(DbContextOptionsBuilder optionsBuilder, LazyLoadOptions options)
    {
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(new LazyLoadOptionsExtension(options));
        return optionsBuilder;
    }
}
