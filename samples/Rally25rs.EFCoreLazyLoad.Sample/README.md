# Rally25rs.EFCoreLazyLoad sample

A small console app showing basic use of Rally25rs.EFCoreLazyLoad with the .NET Generic Host, Microsoft
dependency injection, and a SQLite database.

## Run it

From the repository root:

```shell
dotnet run --project samples/Rally25rs.EFCoreLazyLoad.Sample
```

The app recreates and seeds `blog.db` in its output folder on every run, so it can be run repeatedly.

## What's in it

- `Data/`: a `Blog` with one `Author` (1-to-1) and many `Post`s (1-to-many). Each entity is a `[LazyLoad] partial`
  class with non-nullable `partial` navigation properties. `BlogDbContext` is a plain EF Core context.
  Two entities override the context's options with attribute settings:
  - `Post` is `[LazyLoad(Mode = LazyLoadMode.Throw)]`, so an unloaded `post.Blog` always throws;
  - `Author.Blog` is `[LazyLoad(LogLazyLoads = false)]`, so it is lazy-loaded without a warning.
- `Program.cs`: registers the context two ways:
  - `BlogDbContext` with `AddDbContext` + `UseLazyLoad(lazy => ...)` in `Load` mode with logging (the main path);
  - `StrictBlogDbContext` with `AddEFCoreLazyLoad(lazy => ...)` + `UseLazyLoad(serviceProvider)` in `Throw` mode.

## Expected output

Console logging shows the SQL EF Core runs and the warnings from the `Rally25rs.EFCoreLazyLoad` category.

1. **Query with Include**: one query loads the blog, author and posts. No lazy loading, no warnings.
2. **Load mode, no Include**: the blog is queried alone. Reading `blog.Author` and `blog.Posts` logs a warning
   such as `Lazy-loading navigation 'Blog.Author'` and runs one extra query each. Reading `blog.Author` again
   doesn't query or log.
3. **Throw mode, no Include**: reading `blog.Author` logs `Blocked lazy-load of navigation 'Blog.Author'` and
   throws a `NavigationNotLoadedException` (without querying), whose message suggests adding
   `.Include(e => e.Author)`.
4. **Per-entity overrides**, in the `Load`-mode context: reading an unloaded `post.Blog` throws
   `NavigationNotLoadedException` because of `Post`'s attribute. Reading `author.Blog` runs one extra query and logs
   nothing, because of the property's attribute.
