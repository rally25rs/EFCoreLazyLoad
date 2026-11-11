using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Rally25rs.EFCoreLazyLoad.Internal;

/// <summary>
///     EF Core options extension that swaps EF's <see cref="ILazyLoaderFactory" /> for one producing
///     <see cref="LazyLoadLazyLoader" /> instances.
/// </summary>
/// <remarks>
///     The options it carries are read per context (through <see cref="IDbContextOptions" />) by
///     <see cref="LazyLoadLazyLoaderFactory" />, so they do not affect EF's internal service provider: contexts with
///     different <see cref="LazyLoadOptions" /> share one provider.
/// </remarks>
internal sealed class LazyLoadOptionsExtension(LazyLoadOptions options) : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    /// <summary>A private snapshot; never mutated after construction.</summary>
    public LazyLoadOptions Options { get; } = options;

    public DbContextOptionsExtensionInfo Info
        => _info ??= new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
        => services.Replace(ServiceDescriptor.Scoped<ILazyLoaderFactory, LazyLoadLazyLoaderFactory>());

    public void Validate(IDbContextOptions options)
    {
    }

    private sealed class ExtensionInfo(LazyLoadOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        private new LazyLoadOptionsExtension Extension
            => (LazyLoadOptionsExtension)base.Extension;

        public override bool IsDatabaseProvider
            => false;

        public override string LogFragment
            => $"using Rally25rs.EFCoreLazyLoad (Mode={Extension.Options.Mode}) ";

        // Options are read per context, so they never require a different internal service provider.
        public override int GetServiceProviderHashCode()
            => 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["Rally25rs.EFCoreLazyLoad"] = "1";
    }
}
