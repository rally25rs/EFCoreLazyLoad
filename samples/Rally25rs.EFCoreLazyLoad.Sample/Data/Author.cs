using Rally25rs.EFCoreLazyLoad;

namespace Rally25rs.EFCoreLazyLoad.Sample.Data;

[LazyLoad]
public partial class Author
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public partial Blog Blog { get; set; }
}
