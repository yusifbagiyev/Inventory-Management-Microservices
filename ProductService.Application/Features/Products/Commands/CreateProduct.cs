using AutoMapper;
using FluentValidation;
using MediatR;
using ProductService.Application.DTOs;
using ProductService.Application.Events;
using ProductService.Application.Interfaces;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Products.Commands
{
    public class CreateProduct
    {
        public record Command(CreateProductDto ProductDto) : IRequest<ProductDto>;
        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.ProductDto.InventoryCode)
               .GreaterThan(0).WithMessage("Inventory code must be greater than 0")
               .LessThan(10000).WithMessage("Inventory code must be lass than 10000");

                RuleFor(x => x.ProductDto.CategoryId)
                    .GreaterThan(0).WithMessage("Valid category is required");

                RuleFor(x => x.ProductDto.DepartmentId)
                    .GreaterThan(0).WithMessage("Valid department is required");
            }
        }
        public class CreateProductCommandHandler : IRequestHandler<Command, ProductDto>
        {
            private readonly IProductRepository _productRepository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IMapper _mapper;
            private readonly IImageService _imageService;
            private readonly ITransactionService _transactionService;
            private readonly IMessagePublisher _messagePublisher;

            public CreateProductCommandHandler(
                IProductRepository productRepository,
                IUnitOfWork unitOfWork,
                IMapper mapper,
                IImageService ımageService,
                ITransactionService transactionService,
                IMessagePublisher messagePublisher)
            {
                _productRepository = productRepository;
                _unitOfWork = unitOfWork;
                _mapper = mapper;
                _imageService = ımageService;
                _transactionService = transactionService;
                _messagePublisher = messagePublisher;
            }

            public async Task<ProductDto> Handle(Command request, CancellationToken cancellationToken)
            {
                var dto = request.ProductDto;

                var imageUrl = string.Empty;

                return await _transactionService.ExecuteAsync(
                async () =>
                {
                    if (dto.ImageFile != null && dto.ImageFile.Length > 0)
                    {
                        using var stream = dto.ImageFile.OpenReadStream();
                        imageUrl = await _imageService.UploadImageAsync(stream, dto.ImageFile.FileName, dto.InventoryCode);
                    }
                    var newProduct = new Product(
                        dto.InventoryCode,
                        dto.Model,
                        dto.Vendor,
                        dto.CategoryId,
                        dto.DepartmentId,
                        dto.Worker,
                        imageUrl,
                        dto.Description,
                        dto.IsActive,
                        dto.IsWorking,
                        dto.IsNewItem);

                    await _productRepository.AddAsync(newProduct, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    var createdProduct = await _productRepository.GetByIdAsync(newProduct.Id, cancellationToken);

                    var productCreatedEvent = new ProductCreatedEvent
                    {
                        ProductId = createdProduct!.Id,
                        InventoryCode = createdProduct.InventoryCode,
                        Model = createdProduct.Model,
                        Vendor = createdProduct.Vendor,
                        CategoryName = createdProduct.Category!.Name,
                        DepartmentId = createdProduct.DepartmentId,
                        DepartmentName = createdProduct.Department!.Name,
                        Worker = createdProduct.Worker ?? string.Empty,
                        Description=createdProduct.Description,
                        IsWorking = createdProduct.IsWorking,
                        ImageUrl = createdProduct.ImageUrl,
                        CreatedAt = createdProduct.CreatedAt,
                        IsNewItem = createdProduct.IsNewItem,
                        ImageData= null,
                        ImageFileName= null
                    };

                    // Consumers get the bytes so they don't have to fetch the file from this service
                    if(dto.ImageFile != null && dto.ImageFile.Length > 0)
                    {
                        using var ms= new MemoryStream();
                        await dto.ImageFile.CopyToAsync(ms);
                        productCreatedEvent.ImageData =ms.ToArray();
                        productCreatedEvent.ImageFileName = dto.ImageFile.FileName;
                    }

                    await _messagePublisher.PublishAsync(productCreatedEvent, "product.created", cancellationToken);

                    return _mapper.Map<ProductDto>(createdProduct);
                },
                async () =>
                {
                    // Don't leave an orphaned file behind when the insert fails
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        await _imageService.DeleteImageAsync(imageUrl);
                    }
                });
            }
        }
    }
}