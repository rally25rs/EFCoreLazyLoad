using Rally25rs.EFCoreLazyLoad;

namespace Rally25rs.EFCoreLazyLoad.Sample.Data;

[LazyLoad]
public partial class Author
{
    public int Id { get; set; }
    public required string Name { get; set; }

    // Settings can also go on a single navigation. Lazy-loading an author's blog is expected, so it is never
    // logged, even when the context logs lazy loads. Settings that aren't set (Mode here) follow the context.
    [LazyLoad(LogLazyLoads = false)]
    public partial Blog Blog { get; set; }
}
