using Rally25rs.EFCoreLazyLoad;

namespace Rally25rs.EFCoreLazyLoad.Specification.Sample.Data;

// [LazyLoad] + partial: the source generator implements the body-less partial navigations below.
// They are declared non-nullable because, with UseLazyLoad, reading one never silently returns null:
// it is either already loaded, lazy-loaded on first access (Load mode), or throws (Throw mode).

[LazyLoad]
public partial class Customer
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public partial List<Order> Orders { get; set; }
}

[LazyLoad]
public partial class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public required string Number { get; set; }

    public partial Customer Customer { get; set; }
    public partial List<OrderLine> Lines { get; set; }
}

[LazyLoad]
public partial class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }

    public partial Order Order { get; set; }
    public partial Product Product { get; set; }
}

[LazyLoad]
public partial class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
}
