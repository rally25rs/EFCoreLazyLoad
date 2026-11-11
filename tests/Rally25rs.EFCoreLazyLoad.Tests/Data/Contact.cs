namespace Rally25rs.EFCoreLazyLoad.Tests.Data;

// Contact: no class-level settings. Its Supplier navigation never logs, whatever the context says.
[LazyLoad]
public partial class Contact
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public required string Email { get; set; }

    [LazyLoad(LogLazyLoads = false)]
    public partial Supplier Supplier { get; set; }
}
