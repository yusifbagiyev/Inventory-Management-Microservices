using System.Text.Json;
using ApprovalService.Application.DTOs;
using ApprovalService.Application.Events;
using ApprovalService.Application.Interfaces;
using ApprovalService.Domain.Entities;
using ApprovalService.Domain.Repositories;
using AutoMapper;
using MediatR;
using SharedServices.DTOs;

namespace ApprovalService.Application.Features.Commands
{
    public class CreateApprovalRequest
    {
        public record Command(CreateApprovalRequestDto Dto, int UserId, string UserName) : IRequest<ApprovalRequestDto>;
        
        public class Handler : IRequestHandler<Command, ApprovalRequestDto>
        {
            private readonly IApprovalRequestRepository _repository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IMessagePublisher _messagePublisher;
            private readonly IMapper _mapper;
            
            public Handler(
                IApprovalRequestRepository repository,
                IUnitOfWork unitOfWork,
                IMessagePublisher messagePublisher,
                IMapper mapper)
            {
                _repository= repository;
                _unitOfWork= unitOfWork;
                _messagePublisher= messagePublisher;
                _mapper= mapper;
            }

            public async Task<ApprovalRequestDto> Handle(Command request, CancellationToken cancellationToken)
            {
                var actionDataJson = JsonSerializer.Serialize(request.Dto.ActionData);

                var approvalRequest = new ApprovalRequest(
                    request.Dto.RequestType,
                    request.Dto.EntityType,
                    request.Dto.EntityId,
                    actionDataJson,
                    request.UserId,
                    request.UserName);

                await _repository.AddAsync(approvalRequest, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var evt = new ApprovalRequestCreatedEvent
                {
                    RequestId = approvalRequest.Id,
                    RequestType = approvalRequest.RequestType,
                    RequestedById = approvalRequest.RequestedById,
                    RequestedByName = approvalRequest.RequestedByName,
                    CreatedAt = approvalRequest.CreatedAt
                };

                await _messagePublisher.PublishAsync(evt, "approval.request.created", cancellationToken);

                return _mapper.Map<ApprovalRequestDto>(approvalRequest);
            }
        }
    }
}