using Microsoft.Extensions.Configuration;
using ProductService.Application.Interfaces;

namespace ProductService.Application.Services
{
    /// <summary>Stores product photos on disk in one folder per inventory code.</summary>
    public class ImageService : IImageService
    {
        private readonly string _imagePath;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png"};

        public ImageService(IConfiguration configuration)
        {
            _imagePath = configuration.GetSection("ImageSettings:Path").Value ?? "wwwroot/images/products";
            Directory.CreateDirectory(_imagePath);
        }

        public async Task<string> UploadImageAsync(Stream imageStream, string fileName,int inventoryCode)
        {
            if (!IsValidImage(fileName))
                throw new ArgumentException("Invalid image format");

            if (imageStream.Length > 5 * 1024 * 1024)
                throw new ArgumentException("Image size exceeds 5MB limit");

            var inventoryFolder=Path.Combine(_imagePath,inventoryCode.ToString());
            Directory.CreateDirectory(inventoryFolder);

            var uniqueFileName = $"{DateTime.Now.Ticks}{Path.GetExtension(fileName)}";
            var filePath = Path.Combine(inventoryFolder, uniqueFileName);

            using var fileStream = new FileStream(filePath, FileMode.Create);
            await imageStream.CopyToAsync(fileStream);

            return $"/images/products/{inventoryCode}/{uniqueFileName}";
        }

        public Task DeleteImageAsync(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
                return Task.CompletedTask;
            
            var segments= imageUrl.Split('/');
            if (segments.Length>=2)
            {
                var inventoryCode = segments[^2];
                var fileName = segments[^1];
                var filePath = Path.Combine(_imagePath, inventoryCode,fileName);

                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            return Task.CompletedTask;
        }

        public Task DeleteInventoryFolderAsync(int inventoryCode)
        {
            var folderPath = Path.Combine(_imagePath, inventoryCode.ToString());

            if (Directory.Exists(folderPath))
                Directory.Delete(folderPath, true);

            return Task.CompletedTask;
        }

        public bool IsValidImage(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return _allowedExtensions.Contains(extension);
        }
    }
}
