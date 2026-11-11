namespace Rally25rs.EFCoreLazyLoad.Tests.Data;

[LazyLoad]
public partial class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public required string ProductName { get; set; }
    public int Quantity { get; set; }

    public partial Order Order { get; set; }
}
