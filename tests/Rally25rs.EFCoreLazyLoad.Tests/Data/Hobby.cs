namespace Rally25rs.EFCoreLazyLoad.Tests.Data;

public class Hobby
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public required string Name { get; set; }

    public Customer? Customer { get; set; }
}
