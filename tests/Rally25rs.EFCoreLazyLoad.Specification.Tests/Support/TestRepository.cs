using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Specification.Tests.Support;

/// <summary>The usual Ardalis.Specification repository: a <see cref="RepositoryBase{T}" /> over the app's context.</summary>
public sealed class TestRepository<T>(TestDbContext context) : RepositoryBase<T>(context), IReadRepositoryBase<T>
    where T : class;
