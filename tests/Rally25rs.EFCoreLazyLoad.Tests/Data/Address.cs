namespace Rally25rs.EFCoreLazyLoad.Tests.Data;

[LazyLoad]
public partial class Address
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public required string Street { get; set; }
    public required string City { get; set; }
    public required string PostalCode { get; set; }

    public partial Customer Customer { get; set; }
}
