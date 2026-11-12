using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Rally25rs.EFCoreLazyLoad.Tests.Data;

// Collections the generator can't implement, and a nullable backing field.
//  - `Parcels` is copied under a lock, so its getter is written by hand. It asks the entity's own ILazyLoader member
//    to load first. Declaring that member makes the generator use it instead of adding `__lazyLoader`.
//  - `Labels` is a generated get-only partial over a nullable `List<T>?` field that starts out null.
[LazyLoad]
public partial class Shipment
{
    private readonly Lock _sync = new();
    private readonly List<Parcel> _parcels = [];
    private List<Label>? _labels;

    public int Id { get; set; }
    public required string Reference { get; set; }

    [JsonIgnore]
    protected ILazyLoader? LazyLoader { get; set; }

    public IReadOnlyList<Parcel> Parcels
    {
        get
        {
            lock (_sync)
            {
                LazyLoader?.Load(this, nameof(Parcels));
                return _parcels.ToList().AsReadOnly();
            }
        }
    }

    public partial IEnumerable<Label> Labels { get; }

    public void AddParcel(Parcel parcel)
    {
        lock (_sync)
        {
            _parcels.Add(parcel);
        }
    }

    public void AddLabel(Label label)
        => (_labels ??= []).Add(label);
}
