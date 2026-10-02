namespace ProductService.Domain.Entities
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public bool IsActive { get; private set; } = true;
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }

        public ICollection<Product> Products { get; private set; } = [];

        // For EF Core
        protected Category() { }

        public Category(string name, string? description,bool isActive)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Category name cannot be empty", nameof(name));

            Name = name;
            Description = description ?? string.Empty ;
            IsActive = isActive;
            CreatedAt = DateTime.Now;
        }

        public void Update(string name, string? description,bool isActive)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Category name cannot be empty", nameof(name));

            if (!string.IsNullOrWhiteSpace(description))
                Description = description;
            Name = name;
            IsActive= isActive;
            UpdatedAt = DateTime.Now;
        }
    }
}