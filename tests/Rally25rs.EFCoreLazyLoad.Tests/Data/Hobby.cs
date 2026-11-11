namespace Rally25rs.EFCoreLazyLoad.Tests.Data;

[LazyLoad]
public partial class Hobby
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public required string Name { get; set; }

    public partial Customer Customer { get; set; }
}
