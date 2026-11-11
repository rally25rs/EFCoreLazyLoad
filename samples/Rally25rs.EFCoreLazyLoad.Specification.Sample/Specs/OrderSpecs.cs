using Ardalis.Specification;
using Rally25rs.EFCoreLazyLoad.Specification.Sample.Data;

namespace Rally25rs.EFCoreLazyLoad.Specification.Sample.Specs;

/// <summary>An order with everything the order page shows: its customer, and each line's product.</summary>
public sealed class OrderWithDetailsSpec : SingleResultSpecification<Order>
{
    public OrderWithDetailsSpec(string number)
        => Query
            .Where(o => o.Number == number)
            .Include(o => o.Customer)
            .Include(o => o.Lines)
            .ThenInclude(l => l.Product);
}

/// <summary>
///     An order with its lines, but no <c>.ThenInclude(l => l.Product)</c> and no customer: the kind of spec that is
///     easy to write, and easy to reuse somewhere that needs more than it loads.
/// </summary>
public sealed class OrderWithLinesSpec : SingleResultSpecification<Order>
{
    public OrderWithLinesSpec(string number)
        => Query
            .Where(o => o.Number == number)
            .Include(o => o.Lines);
}

public sealed record OrderSummary(string Number, string CustomerName, int LineCount, int TotalQuantity);

/// <summary>
///     A projection. Navigations read inside Select are translated to SQL, so no entity is loaded and there is
///     nothing to lazy-load: no Includes needed.
/// </summary>
public sealed class OrderSummariesSpec : Specification<Order, OrderSummary>
{
    public OrderSummariesSpec()
        => Query
            .AsNoTracking()
            .OrderBy(o => o.Number)
            .Select(o => new OrderSummary(o.Number, o.Customer.Name, o.Lines.Count, o.Lines.Sum(l => l.Quantity)));
}
