using Rally25rs.EFCoreLazyLoad;

namespace Rally25rs.EFCoreLazyLoad.Sample.Data;

// [LazyLoad] + partial: the source generator implements the body-less partial navigations below.
// They are declared non-nullable because, with UseLazyLoad, reading one never silently returns null:
// it is either already loaded, lazy-loaded on first access (Load mode), or throws (Throw mode).
[LazyLoad]
public partial class Blog
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public int AuthorId { get; set; }

    public partial Author Author { get; set; }      // one-to-one
    public partial List<Post> Posts { get; set; }   // one-to-many
}
