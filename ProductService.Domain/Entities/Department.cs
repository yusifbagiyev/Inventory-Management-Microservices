namespace ProductService.Domain.Entities
{
    public class Department
    {
        public int Id { get; set; }
        public string Name { get; private set; } = string.Empty;
        public string? DepartmentHead { get; private set; } = string.Empty;
        public string? Description { get; private set; } = string.Empty;
        public bool IsActive { get; private set; } = true;
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public ICollection<Product> Products { get; private set; } = [];

        // Only correct when Products is loaded
        public int WorkerCount => Products
            .Where(p=>!string.IsNullOrEmpty(p.Worker))
            .Select(p=>p.Worker)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        // For EF Core
        protected Department() { }
        public Department(string name, string? description,string? departmentHead, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Department name cannot be empty", nameof(name));
            Name = name;
            Description = description??string.Empty;
            DepartmentHead = departmentHead ?? string.Empty;
            IsActive = isActive;
            CreatedAt = DateTime.Now;
        }
        public void Update(string name, string? description,string? departmentHead, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Department name cannot be empty", nameof(name));

            // A blank description or head keeps the current value
            if(!string.IsNullOrWhiteSpace(description))
                Description=description;

            if (!string.IsNullOrWhiteSpace(departmentHead))
                DepartmentHead = departmentHead;

            Name = name;
            IsActive = isActive;
            UpdatedAt = DateTime.Now;
        }
        public void Activate()
        {
            IsActive = true;
            UpdatedAt = DateTime.Now;
        }
        public void Deactivate()
        {
            IsActive = false;
            UpdatedAt = DateTime.Now;
        }
    }
}
