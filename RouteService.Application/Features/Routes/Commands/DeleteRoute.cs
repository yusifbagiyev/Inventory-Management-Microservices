using MediatR;
using RouteService.Application.Interfaces;
using RouteService.Domain.Exceptions;
using RouteService.Domain.Repositories;

namespace RouteService.Application.Features.Routes.Commands
{
    public class DeleteRoute
    {
        public record Command(int Id) : IRequest;

        public class Handler : IRequestHandler<Command>
        {
            private readonly IInventoryRouteRepository _repository;
            private readonly IImageService _imageService;
            private readonly IUnitOfWork _unitOfWork;

            public Handler(
                IInventoryRouteRepository repository,
                IImageService imageService,
                IUnitOfWork unitOfWork)
            {
                _repository = repository;
                _imageService = imageService;
                _unitOfWork = unitOfWork;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var route = await _repository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new RouteException($"Route with ID {request.Id} not found");

                if (route.IsCompleted)
                    throw new RouteException("Cannot delete completed route. Completed routes are part of the audit trail.");

                if (!string.IsNullOrEmpty(route.ImageUrl))
                {
                    await _imageService.DeleteImageAsync(route.ImageUrl);
                }

                await _repository.DeleteAsync(route, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
