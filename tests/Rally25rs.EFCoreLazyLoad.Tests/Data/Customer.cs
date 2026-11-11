namespace Rally25rs.EFCoreLazyLoad.Tests.Data;

public class Customer
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }

    // Relations are intentionally nullable: they stay null unless explicitly loaded.
    public Address? Address { get; set; }
    public List<Hobby>? Hobbies { get; set; }
}
