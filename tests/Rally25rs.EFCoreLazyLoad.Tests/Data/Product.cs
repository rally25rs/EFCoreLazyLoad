using Microsoft.Extensions.Logging;

namespace Rally25rs.EFCoreLazyLoad.Tests.Data;

// Product: always logs its lazy loads, at Information, without a stack trace. Mode is not set, so it follows
// the context.
[LazyLoad(LogLazyLoads = true, LogLevel = LogLevel.Information, LogStackTrace = false)]
public partial class Product
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public required string Name { get; set; }

    public partial Supplier Supplier { get; set; }
}
