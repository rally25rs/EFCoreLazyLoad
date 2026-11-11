using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;

namespace Rally25rs.EFCoreLazyLoad.Specification.Sample.Data;

// The usual Ardalis.Specification repository. Nothing here is specific to Rally25rs.EFCoreLazyLoad: entities
// returned by any specification get lazy loading (or Throw mode) from the context's UseLazyLoad options.
public class Repository<T>(ShopDbContext db) : RepositoryBase<T>(db), IReadRepositoryBase<T>, IRepositoryBase<T>
    where T : class;
