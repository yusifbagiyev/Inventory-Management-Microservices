using MediatR;
using Microsoft.Extensions.Logging;
using RouteService.Application.Events;
using RouteService.Application.Interfaces;
using RouteService.Domain.Exceptions;
using RouteService.Domain.Repositories;

namespace RouteService.Application.Features.Routes.Commands
{
    public class CompleteRoute
    {
        public record Command(int Id) : IRequest;

        public class Handler : IRequestHandler<Command>
        {
            private readonly IInventoryRouteRepository _repository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IMessagePublisher _messagePublisher;
            private readonly ILogger<Handler> _logger;

            public Handler(
                IInventoryRouteRepository repository, 
                IUnitOfWork unitOfWork, 
                IMessagePublisher messagePublisher,
                ILogger<Handler> logger)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
                _messagePublisher = messagePublisher;
                _logger = logger;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var route = await _repository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new RouteException($"Route with ID {request.Id} not found");

                if (route.IsCompleted)
                    throw new RouteException("Route is already completed");

                route.Complete();

                await _repository.UpdateAsync(route, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // The photo goes as bytes because ProductService can't read this service's files
                byte[]? imageData = null;
                string? imageFileName = null;
                
                if(!string.IsNullOrEmpty(route.ImageUrl))
                {
                    try
                    {
                        var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                        var imagePath = Path.Combine(webRootPath, route.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                        if (File.Exists(imagePath))
                        {
                            imageData = await File.ReadAllBytesAsync(imagePath, cancellationToken);
                            imageFileName = Path.GetFileName(imagePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to read image data for route {RouteId}", route.Id);
                    }
                }

                var transferEvent = new ProductTransferredEvent
                {
                    ProductId = route.ProductSnapshot.ProductId,
                    ToDepartmentId = route.ToDepartmentId,
                    ToWorker = route.ToWorker,
                    ImageData = imageData,
                    ImageFileName = imageFileName,
                    TransferredAt = DateTime.Now
                };

                await _messagePublisher.PublishAsync(transferEvent, "product.transferred", cancellationToken);

                var completedEvent = new RouteCompletedEvent
                {
                    RouteId = route.Id,
                    ProductId = route.ProductSnapshot.ProductId,
                    InventoryCode = route.ProductSnapshot.InventoryCode,
                    Model = route.ProductSnapshot.Model,
                    Vendor = route.ProductSnapshot.Vendor,
                    CategoryName = route.ProductSnapshot.CategoryName,
                    FromDepartmentId = route.FromDepartmentId ?? 0,
                    FromDepartmentName = route.FromDepartmentName ?? "",
                    FromWorker = route.FromWorker,
                    ToWorker = route.ToWorker ?? "",
                    ToDepartmentId = route.ToDepartmentId,
                    ToDepartmentName = route.ToDepartmentName,
                    Notes = route.Notes,
                    ImageUrl = route.ImageUrl,
                    ImageData = imageData,
                    ImageFileName=imageFileName,
                    CompletedAt = DateTime.Now
                };

                await _messagePublisher.PublishAsync(completedEvent, "route.completed", cancellationToken);
            }
        }
    }
}