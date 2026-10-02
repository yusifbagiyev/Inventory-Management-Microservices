namespace RouteService.Domain.ValueObjects
{
    /// <summary>The product's details as they were when the route was recorded.</summary>
    public class ProductSnapshot
    {
        public int ProductId { get; private set; }
        public int InventoryCode { get; private set; }
        public string Model { get; private set; }
        public string Vendor { get; private set; }
        public string CategoryName { get; private set; }
        public bool IsWorking { get; private set; }

        public ProductSnapshot(int productId, int inventoryCode, string model,
            string vendor, string categoryName, bool isWorking)
        {
            ProductId = productId;
            InventoryCode = inventoryCode;
            Model = model;
            Vendor = vendor;
            CategoryName = categoryName;
            IsWorking = isWorking;
        }
    }
}