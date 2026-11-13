namespace LoyalAnimal.Shared;

public class ShopOrderFulfillment
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int ShopId { get; set; }

    public string Status { get; set; } = "Pending";

    public string TrackingNumber { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
