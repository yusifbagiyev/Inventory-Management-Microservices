namespace RouteService.Domain.ValueObjects
{
    /// <summary>The product as it was before an update, which becomes the from side of the update route.</summary>
    public record ExistingProduct
    (
        int ProductId,
        int InventoryCode,
        int? CategoryId,
        string? CategoryName,
        int? DepartmentId,
        string? DepartmentName,
        string? Worker,
        string? Description,
        bool? IsActive,
        bool? IsNewItem,
        bool? IsWorking
    );
}