using Ardalis.Specification;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Specification.Tests.Specs;

/// <summary>A supplier by id, without any Include, optionally no-tracking.</summary>
public sealed class SupplierByIdSpec : SingleResultSpecification<Supplier>
{
    public SupplierByIdSpec(int id, bool noTracking = false)
    {
        Query.Where(s => s.Id == id);

        if (noTracking)
        {
            Query.AsNoTracking();
        }
    }
}

/// <summary>A supplier by id with its contact included.</summary>
public sealed class SupplierWithContactSpec : SingleResultSpecification<Supplier>
{
    public SupplierWithContactSpec(int id)
        => Query
            .Where(s => s.Id == id)
            .Include(s => s.Contact);
}

/// <summary>All products, without any Include.</summary>
public sealed class ProductsSpec : Specification<Product>
{
    public ProductsSpec()
        => Query.OrderBy(p => p.Id);
}

/// <summary>All contacts, without any Include.</summary>
public sealed class ContactsSpec : Specification<Contact>
{
    public ContactsSpec()
        => Query.OrderBy(c => c.Id);
}
