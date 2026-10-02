using System.Security.Claims;
using ApprovalService.Application.DTOs;
using ApprovalService.Application.Features.Commands;
using ApprovalService.Application.Features.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedServices.DTOs;

namespace ApprovalService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ApprovalRequestsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public ApprovalRequestsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<ActionResult<ApprovalRequestDto>> Create(CreateApprovalRequestDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var userName = User.Identity?.Name ?? "Unknown";

            var result = await _mediator.Send(new CreateApprovalRequest.Command(dto, userId, userName));
            return Ok(result);
        }


        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<PagedResultDto<ApprovalRequestDto>>> GetPending(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _mediator.Send(new GetPendingRequests.Query(pageNumber, pageSize));
            return Ok(result);
        }


        [HttpGet("my-requests")]
        public async Task<ActionResult<IEnumerable<ApprovalRequestDto>>> GetMyRequests()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var result = await _mediator.Send(new GetUserRequests.Query(userId));
            return Ok(result);
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<ApprovalRequestDto>> GetRequestById(int Id)
        {
            var approvalRequest = await _mediator.Send(new GetRequestById.Query(Id));
            if (approvalRequest == null) return NotFound();
            return Ok(approvalRequest);
        }


        [HttpPost("{id}/approve")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var userName = User.Identity?.Name ?? "Unknown";

            await _mediator.Send(new ApproveRequest.Command(id, userId, userName));
            return NoContent();
        }


        [HttpPost("{id}/reject")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reject(int id, RejectRequestDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var userName = User.Identity?.Name ?? "Unknown";

            await _mediator.Send(new RejectRequest.Command(id, userId, userName, dto.Reason));
            return NoContent();
        }


        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<ApprovalRequestDto>>> GetAllRequests()
        {
            var result = await _mediator.Send(new GetAllRequests.Query());
            return Ok(result);
        }


        [HttpDelete("{id}/cancel")]
        public async Task<IActionResult> CancelRequestIt(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var request = await _mediator.Send(new GetRequestById.Query(id));
            if (request == null)
                return NotFound();

            if (request.RequestedById != userId)
                return Forbid("You can only cancel your own requests");

            if (request.Status != "Pending")
                return BadRequest("Only pending requests can be cancelled");

            await _mediator.Send(new CancelRequest.Command(id));
            return NoContent();
        }
    }
}