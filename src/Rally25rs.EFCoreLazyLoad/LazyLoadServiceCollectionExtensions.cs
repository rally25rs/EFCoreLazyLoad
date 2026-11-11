using Microsoft.Extensions.DependencyInjection;

namespace Rally25rs.EFCoreLazyLoad;

/// <summary>
///     Extension methods on <see cref="IServiceCollection" /> for registering Rally25rs.EFCoreLazyLoad options.
/// </summary>
public static class LazyLoadServiceCollectionExtensions
{
    /// <summary>
    ///     Registers <see cref="LazyLoadOptions" /> with the options pattern, so contexts configured with
    ///     <see cref="LazyLoadDbContextOptionsBuilderExtensions.UseLazyLoad(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder, IServiceProvider)" />
    ///     pick them up.
    /// </summary>
    /// <remarks>
    ///     May be called more than once; each <paramref name="configure" /> action is applied in order. The options
    ///     can also be bound from configuration with <c>services.Configure&lt;LazyLoadOptions&gt;(...)</c>.
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">An optional action to configure the <see cref="LazyLoadOptions" />.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddEFCoreLazyLoad(
        this IServiceCollection services,
        Action<LazyLoadOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = services.AddOptions<LazyLoadOptions>();
        if (configure is not null)
        {
            builder.Configure(configure);
        }

        return services;
    }
}
