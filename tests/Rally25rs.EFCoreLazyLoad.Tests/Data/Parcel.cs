namespace Rally25rs.EFCoreLazyLoad.Tests.Data;

[LazyLoad]
public partial class Parcel
{
    public int Id { get; set; }
    public int ShipmentId { get; set; }
    public double WeightKg { get; set; }

    public partial Shipment Shipment { get; set; }
}
