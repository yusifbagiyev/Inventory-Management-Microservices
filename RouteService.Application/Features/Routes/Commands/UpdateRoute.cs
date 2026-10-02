using FluentValidation;
using MediatR;
using RouteService.Application.DTOs;
using RouteService.Application.Interfaces;
using RouteService.Domain.Exceptions;
using RouteService.Domain.Repositories;

namespace RouteService.Application.Features.Routes.Commands
{
    public class UpdateRoute
    {
        public record Command(int Id, UpdateRouteDto Dto) : IRequest;

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.Id).GreaterThan(0);
                RuleFor(x => x.Dto.Notes)
                    .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters");
            }
        }

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

                var dto = request.Dto;
                string? oldImageUrl = route.ImageUrl;
                string? newImageUrl = null;

                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                try
                {
                    if (!string.IsNullOrEmpty(dto.ToWorker) || !string.IsNullOrEmpty(dto.Notes))
                    {
                        route.UpdateExistingRoute(dto.ToWorker,dto.Notes);
                    }

                    if (dto.ImageFile != null && dto.ImageFile.Length > 0)
                    {
                        using var stream = dto.ImageFile.OpenReadStream();
                        newImageUrl = await _imageService.UploadImageAsync(
                            stream,
                            dto.ImageFile.FileName,
                            route.ProductSnapshot.InventoryCode);

                    }
                    route.UpdateImage(newImageUrl);

                    await _repository.UpdateAsync(route, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await _unitOfWork.CommitTransactionAsync(cancellationToken);

                    // Delete the old image only after the commit, so a failed update keeps it
                    if (!string.IsNullOrEmpty(oldImageUrl) && !string.IsNullOrEmpty(newImageUrl))
                    {
                        await _imageService.DeleteImageAsync(oldImageUrl);
                    }
                }
                catch
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);

                    if (!string.IsNullOrEmpty(newImageUrl))
                    {
                        await _imageService.DeleteImageAsync(newImageUrl);
                    }

                    throw;
                }
            }
        }
    }
}