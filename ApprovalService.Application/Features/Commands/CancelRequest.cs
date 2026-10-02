using ApprovalService.Application.DTOs;
using ApprovalService.Application.Interfaces;
using ApprovalService.Domain.Enums;
using ApprovalService.Domain.Repositories;
using MediatR;

namespace ApprovalService.Application.Features.Commands
{
    public class CancelRequest
    {
        public record Command(int RequestId) : IRequest;    

        public class Handler : IRequestHandler<Command>
        {
            private readonly IApprovalRequestRepository _repository;
            private readonly IMessagePublisher _messagePublisher;
            private readonly IUnitOfWork _unitOfWork;

            public Handler(
                IApprovalRequestRepository repository,
                IMessagePublisher messagePublisher,
                IUnitOfWork unitOfWork)
            {
                _repository = repository;
                _messagePublisher = messagePublisher;
                _unitOfWork = unitOfWork;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var approvalRequest = await _repository.GetByIdAsync(request.RequestId, cancellationToken)
                    ?? throw new InvalidOperationException($"Request {request.RequestId} not found");

                if (approvalRequest.Status != ApprovalStatus.Pending)
                    throw new InvalidOperationException("Only pending requests can be cancelled");

                // Publish while the request still exists, it is deleted right after.
                var cancelEvent = new ApprovalRequestCancelledEvent
                {
                    RequestId = approvalRequest.Id,
                    RequestType = approvalRequest.RequestType,
                    RequestedById = approvalRequest.RequestedById,
                    CancelledAt = DateTime.Now
                };

                await _messagePublisher.PublishAsync(cancelEvent, "approval.request.cancelled", cancellationToken);

                await _repository.DeleteAsync(approvalRequest, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}