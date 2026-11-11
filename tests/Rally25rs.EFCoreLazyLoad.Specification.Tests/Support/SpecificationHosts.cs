using Ardalis.Specification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rally25rs.EFCoreLazyLoad.Tests.Data;
using Rally25rs.EFCoreLazyLoad.Tests.Support;

namespace Rally25rs.EFCoreLazyLoad.Specification.Tests.Support;

public static class SpecificationHosts
{
    /// <summary>
    ///     A host whose context is registered with <c>AddDbContext</c> and <c>UseLazyLoad(configure)</c>, plus
    ///     <see cref="IReadRepositoryBase{T}" /> and <see cref="IRepositoryBase{T}" /> as scoped
    ///     <see cref="TestRepository{T}" />s.
    /// </summary>
    public static TestHost CreateSpecificationHost(
        this TestDatabaseFixture db, Action<LazyLoadOptions>? configure = null)
        => db.CreateHost((services, useDatabase) =>
        {
            services.AddDbContext<TestDbContext>(o =>
            {
                useDatabase(o);
                o.UseLazyLoad(configure);
            });
            services.AddScoped(typeof(IReadRepositoryBase<>), typeof(TestRepository<>));
            services.AddScoped(typeof(IRepositoryBase<>), typeof(TestRepository<>));
        });

    /// <summary>
    ///     The read repository for <typeparamref name="T" /> in the host's default scope, so it shares
    ///     <see cref="TestHost.Context" />.
    /// </summary>
    public static IReadRepositoryBase<T> Repository<T>(this TestHost host)
        where T : class
        => new TestRepository<T>(host.Context);
}
