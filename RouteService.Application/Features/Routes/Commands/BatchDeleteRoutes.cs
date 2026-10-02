using FluentValidation;
using MediatR;
using RouteService.Application.DTOs;
using RouteService.Application.Interfaces;
using RouteService.Domain.Repositories;

namespace RouteService.Application.Features.Routes.Commands
{
    public class BatchDeleteRoutes
    {
        public record Command(BatchDeleteDto Dto) : IRequest<BatchDeleteResultDto>;

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.Dto.RouteIds)
                    .NotEmpty().WithMessage("At least one route ID must be provided")
                    .Must(x => x.Count <= 100).WithMessage("Cannot delete more than 100 routes at once");
            }
        }

        public class Handler : IRequestHandler<Command, BatchDeleteResultDto>
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

            public async Task<BatchDeleteResultDto> Handle(Command request, CancellationToken cancellationToken)
            {
                var result = new BatchDeleteResultDto();

                foreach (var routeId in request.Dto.RouteIds)
                {
                    try
                    {
                        var route = await _repository.GetByIdAsync(routeId, cancellationToken);
                        if (route == null)
                        {
                            result.Failed.Add(new DeleteFailureDto
                            {
                                RouteId = routeId,
                                Reason = "Route not found"
                            });
                            continue;
                        }

                        if (route.IsCompleted)
                        {
                            result.Failed.Add(new DeleteFailureDto
                            {
                                RouteId = routeId,
                                Reason = "Cannot delete completed route"
                            });
                            continue;
                        }

                        if (!string.IsNullOrEmpty(route.ImageUrl))
                        {
                            await _imageService.DeleteImageAsync(route.ImageUrl);
                        }

                        await _repository.DeleteAsync(route, cancellationToken);
                        result.SuccessfulIds.Add(routeId);
                    }
                    catch (Exception ex)
                    {
                        result.Failed.Add(new DeleteFailureDto
                        {
                            RouteId = routeId,
                            Reason = ex.Message
                        });
                    }
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return result;
            }
        }
    }

    public class BatchDeleteResultDto
    {
        public List<int> SuccessfulIds { get; set; } = new();
        public List<DeleteFailureDto> Failed { get; set; } = new();
        public int TotalProcessed => SuccessfulIds.Count + Failed.Count;
        public int TotalSuccessful => SuccessfulIds.Count;
        public int TotalFailed => Failed.Count;
    }

    public class DeleteFailureDto
    {
        public int RouteId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
