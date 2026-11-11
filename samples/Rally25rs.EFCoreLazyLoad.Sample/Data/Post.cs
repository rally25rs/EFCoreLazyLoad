using Rally25rs.EFCoreLazyLoad;

namespace Rally25rs.EFCoreLazyLoad.Sample.Data;

[LazyLoad]
public partial class Post
{
    public int Id { get; set; }
    public int BlogId { get; set; }
    public required string Title { get; set; }

    public partial Blog Blog { get; set; }
}
