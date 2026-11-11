using Microsoft.EntityFrameworkCore;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Tests;

public class CustomerLoadingTests(TestDatabaseFixture db) : IClassFixture<TestDatabaseFixture>
{
    [Fact]
    public async Task Load_WithInclude_PopulatesRelations()
    {
        await using var context = db.CreateContext();

        var customer = await context.Customers
            .Include(c => c.Address)
            .Include(c => c.Hobbies)
            .SingleAsync(c => c.Id == TestDatabaseFixture.SeededCustomerId, TestContext.Current.CancellationToken);

        Assert.NotNull(customer.Address);
        Assert.Equal("Springfield", customer.Address.City);
        Assert.NotNull(customer.Hobbies);
        Assert.Equal(3, customer.Hobbies.Count);
    }

    [Fact]
    public async Task Load_WithoutInclude_LeavesRelationsNull()
    {
        await using var context = db.CreateContext();

        var customer = await context.Customers
            .SingleAsync(c => c.Id == TestDatabaseFixture.SeededCustomerId, TestContext.Current.CancellationToken);

        Assert.Null(customer.Address);
        Assert.Null(customer.Hobbies);
    }
}
