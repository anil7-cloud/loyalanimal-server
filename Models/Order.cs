namespace LoyalAnimal.Shared;

public class Order
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = "Pending";

    public string ShippingFullName { get; set; } = string.Empty;

    public string ShippingPhone { get; set; } = string.Empty;

    public string ShippingAddress { get; set; } = string.Empty;

    public string ShippingCity { get; set; } = string.Empty;

    public string ShippingDistrict { get; set; } = string.Empty;

    public string PaymentStatus { get; set; } = "Unpaid";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
