using Ardalis.Specification;
using Rally25rs.EFCoreLazyLoad.Tests.Data;

namespace Rally25rs.EFCoreLazyLoad.Specification.Tests.Specs;

/// <summary>A customer by id, without any Include.</summary>
public sealed class CustomerByIdSpec : SingleResultSpecification<Customer>
{
    public CustomerByIdSpec(int id)
        => Query.Where(c => c.Id == id);
}

/// <summary>A customer by id, with its address and hobbies included.</summary>
public sealed class CustomerWithDetailsSpec : SingleResultSpecification<Customer>
{
    public CustomerWithDetailsSpec(int id)
        => Query
            .Where(c => c.Id == id)
            .Include(c => c.Address)
            .Include(c => c.Hobbies);
}

/// <summary>A customer by id, with only its address included: the hobbies Include is "forgotten".</summary>
public sealed class CustomerWithAddressSpec : SingleResultSpecification<Customer>
{
    public CustomerWithAddressSpec(int id)
        => Query
            .Where(c => c.Id == id)
            .Include(c => c.Address);
}

/// <summary>A customer by id, with everything included, using split queries.</summary>
public sealed class CustomerWithDetailsSplitSpec : SingleResultSpecification<Customer>
{
    public CustomerWithDetailsSplitSpec(int id)
        => Query
            .Where(c => c.Id == id)
            .Include(c => c.Address)
            .Include(c => c.Hobbies)
            .AsSplitQuery();
}

/// <summary>All customers, ordered by id, without any Include.</summary>
public sealed class CustomersSpec : Specification<Customer>
{
    public CustomersSpec()
        => Query.OrderBy(c => c.Id);
}

/// <summary>A customer by id, no-tracking, optionally with its address included.</summary>
public sealed class CustomerByIdNoTrackingSpec : SingleResultSpecification<Customer>
{
    public CustomerByIdNoTrackingSpec(int id, bool includeAddress = false, bool withIdentityResolution = false)
    {
        Query.Where(c => c.Id == id);

        if (includeAddress)
        {
            Query.Include(c => c.Address);
        }

        if (withIdentityResolution)
        {
            Query.AsNoTrackingWithIdentityResolution();
        }
        else
        {
            Query.AsNoTracking();
        }
    }
}

/// <summary>A customer's hobbies, each with its customer and the customer's address (Include + ThenInclude).</summary>
public sealed class HobbiesWithCustomerAndAddressSpec : Specification<Hobby>
{
    public HobbiesWithCustomerAndAddressSpec(int customerId)
        => Query
            .Where(h => h.CustomerId == customerId)
            .Include(h => h.Customer)
            .ThenInclude(c => c.Address);
}

/// <summary>An address with its customer and the customer's hobbies (ThenInclude of a collection).</summary>
public sealed class AddressWithCustomerAndHobbiesSpec : SingleResultSpecification<Address>
{
    public AddressWithCustomerAndHobbiesSpec(int customerId)
        => Query
            .Where(a => a.CustomerId == customerId)
            .Include(a => a.Customer)
            .ThenInclude(c => c.Hobbies);
}

/// <summary>A hobby with its customer, but not the customer's address: the ThenInclude is "forgotten".</summary>
public sealed class HobbiesWithCustomerSpec : Specification<Hobby>
{
    public HobbiesWithCustomerSpec(int customerId)
        => Query
            .Where(h => h.CustomerId == customerId)
            .OrderBy(h => h.Id)
            .Include(h => h.Customer);
}

public sealed record CustomerSummary(string Name, string City, int HobbyCount);

/// <summary>A projection that reads navigations inside <c>Select</c>; EF Core translates them to SQL joins.</summary>
public sealed class CustomerSummarySpec : SingleResultSpecification<Customer, CustomerSummary>
{
    public CustomerSummarySpec(int id)
        => Query
            .Where(c => c.Id == id)
            .Select(c => new CustomerSummary(c.Name, c.Address.City, c.Hobbies.Count));
}

/// <summary>A projection that returns an entity (the customer's address) rather than a DTO.</summary>
public sealed class AddressOfCustomerSpec : SingleResultSpecification<Customer, Address>
{
    public AddressOfCustomerSpec(int id)
        => Query
            .Where(c => c.Id == id)
            .Select(c => c.Address);
}
