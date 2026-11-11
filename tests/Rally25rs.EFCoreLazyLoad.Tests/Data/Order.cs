namespace Rally25rs.EFCoreLazyLoad.Tests.Data;

// A DDD-style aggregate: the collection is a private list exposed read-only, and only changed through a domain
// method. The navigation is a get-only partial property; the generator finds the `_lines` backing field (EF
// Core's naming convention), triggers the lazy load, and returns `_lines.AsReadOnly()`.
[LazyLoad]
public partial class Order
{
    private readonly List<OrderLine> _lines = [];

    public int Id { get; set; }
    public required string Reference { get; set; }

    public partial IEnumerable<OrderLine> Lines { get; }

    public void AddLine(string productName, int quantity)
    {
        // Reads the collection through the property, so the invariant is checked against the loaded lines (the
        // property lazy-loads them, or throws in Throw mode) rather than against an empty, unloaded list.
        if (Lines.Any(l => l.ProductName == productName))
        {
            throw new InvalidOperationException($"Order '{Reference}' already has a line for '{productName}'.");
        }

        _lines.Add(new OrderLine { ProductName = productName, Quantity = quantity });
    }
}
