using Rally25rs.EFCoreLazyLoad;

namespace Rally25rs.EFCoreLazyLoad.Sample.Data;

// [LazyLoad(...)] settings override the context's options for this entity. Here, reading an unloaded
// Post.Blog always throws, even in a context configured for Load mode: code that works with posts is
// expected to Include their blog.
[LazyLoad(Mode = LazyLoadMode.Throw)]
public partial class Post
{
    public int Id { get; set; }
    public int BlogId { get; set; }
    public required string Title { get; set; }

    public partial Blog Blog { get; set; }
}
