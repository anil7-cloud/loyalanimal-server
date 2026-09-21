namespace LoyalAnimal.Shared;

public class Product
{
    public int Id { get; set; }

    // Ürünün hangi petshopa ait olduğu
    public int ShopId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int Stock { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
