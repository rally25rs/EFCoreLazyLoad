using Microsoft.EntityFrameworkCore;

namespace Rally25rs.EFCoreLazyLoad.Sample.Data;

// A plain EF Core context: nothing here is specific to Rally25rs.EFCoreLazyLoad.
// Lazy loading is switched on where the context is registered (see Program.cs).
public class BlogDbContext : DbContext
{
    public BlogDbContext(DbContextOptions<BlogDbContext> options)
        : base(options)
    {
    }

    // Lets a derived context (StrictBlogDbContext) pass its own options type through.
    protected BlogDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<Blog> Blogs => Set<Blog>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Post> Posts => Set<Post>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 1-to-1: Blog <-> Author (Blog holds the FK)
        modelBuilder.Entity<Blog>()
            .HasOne(b => b.Author)
            .WithOne(a => a.Blog)
            .HasForeignKey<Blog>(b => b.AuthorId);

        // 1-to-many: Blog -> Posts
        modelBuilder.Entity<Blog>()
            .HasMany(b => b.Posts)
            .WithOne(p => p.Blog)
            .HasForeignKey(p => p.BlogId);
    }
}

// The same model, registered a second time with different lazy-load options (Throw mode) so the sample can
// show both modes side by side. A real app would normally have just one registration.
public class StrictBlogDbContext(DbContextOptions<StrictBlogDbContext> options) : BlogDbContext(options);
