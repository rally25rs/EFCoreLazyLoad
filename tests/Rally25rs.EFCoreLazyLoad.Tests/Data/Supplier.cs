namespace Rally25rs.EFCoreLazyLoad.Tests.Data;

// Entities that override the context's LazyLoadOptions with [LazyLoad(...)] settings.
// Supplier: class-level Throw, except Products, whose property-level attribute switches it back to Load.
[LazyLoad(Mode = LazyLoadMode.Throw)]
public partial class Supplier
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public partial Contact Contact { get; set; }

    [LazyLoad(Mode = LazyLoadMode.Load)]
    public partial List<Product> Products { get; set; }
}
