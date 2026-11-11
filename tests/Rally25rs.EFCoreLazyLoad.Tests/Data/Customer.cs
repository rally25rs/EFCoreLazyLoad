namespace Rally25rs.EFCoreLazyLoad.Tests.Data;

[LazyLoad]
public partial class Customer
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }

    // Navigations are non-nullable partial properties: the generator implements them so that, under
    // UseLazyLoad, they are loaded (or blocked) on first access instead of silently staying null.
    public partial Address Address { get; set; }
    public partial List<Hobby> Hobbies { get; set; }
}
