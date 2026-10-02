using FluentValidation;
using MediatR;
using ProductService.Application.Events;
using ProductService.Application.Interfaces;
using SharedServices.Exceptions;
using ProductService.Domain.Repositories;
using ProductService.Application.DTOs;

namespace ProductService.Application.Features.Products.Commands
{
    public class UpdateProductInventoryCode
    {
        public record Command(int Id, int InventoryCode) : IRequest;
        public class Validator:AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.InventoryCode)
                    .GreaterThan(0).WithMessage("Inventory code must be greater than 0")
                    .LessThan(9999).WithMessage("Inventory code must be less than 9999");
            }
        }
        public class Handler : IRequestHandler<Command>
        {
            private readonly IProductRepository _productRepository;
            private readonly IDepartmentRepository _departmentRepository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IMessagePublisher _messagePublisher;
            public Handler(IProductRepository productRepository,
                IUnitOfWork unitOfWork,
                IMessagePublisher messagePublisher,
                IDepartmentRepository departmentRepository)
            {
                _productRepository = productRepository;
                _unitOfWork = unitOfWork;
                _messagePublisher = messagePublisher;
                _departmentRepository = departmentRepository;
            }
            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
                if (product == null)
                {
                    throw new NotFoundException($"Product with ID {request.Id} not found");
                }

                string changes = string.Empty;

                if (product.InventoryCode != request.InventoryCode)
                    changes = $"Inventory code changed from {product.InventoryCode} to {request.InventoryCode}";

                product.ChangeInventoryCode(request.InventoryCode);
                await _productRepository.UpdateAsync(product, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                
                var departmentName = await _departmentRepository.GetByIdAsync(product.DepartmentId, cancellationToken)
                    ?? throw new ArgumentException($"Department with ID {product.DepartmentId} not found");

                if (!string.IsNullOrEmpty(changes))
                {
                    var eventMessage = new ProductUpdatedEvent
                    {
                        Product=new ProductDto
                        {
                            Id = product.Id,
                            InventoryCode = product.InventoryCode,
                            CategoryId = product.CategoryId,
                            DepartmentId = product.DepartmentId,
                            DepartmentName = departmentName.Name,
                            Worker = product.Worker,
                        },
                        Changes = changes,
                        UpdatedAt = DateTime.Now
                    };
                    await _messagePublisher.PublishAsync(eventMessage,"product.updated", cancellationToken);
                }
            }
        }
    }
}
