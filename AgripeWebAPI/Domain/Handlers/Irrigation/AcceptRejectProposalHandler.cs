using AgripeWebAPI.Domain.Commands.Requests.Irrigation;
using AgripeWebAPI.Domain.Commands.Responses.Irrigation;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Irrigation
{
    public class AcceptRejectProposalHandler : IRequestHandler<AcceptRejectProposalRequest, AcceptRejectProposalResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public AcceptRejectProposalHandler(agpDBContext dbContext, ICurrentUserContext currentUser, INotifier notifier)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public async Task<AcceptRejectProposalResponse?> Handle(AcceptRejectProposalRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var normalizedAction = request.Action?.Trim().ToLowerInvariant();
            if (normalizedAction is not ("accept" or "reject"))
            {
                _notifier.Handle(new Notification("Action must be 'accept' or 'reject'."));
                return null;
            }

            var proposal = await _dbContext.IrrigationProposals
                .Find(p => p.Id == request.ProposalId && p.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (proposal is null)
            {
                _notifier.Handle(new Notification("IrrigationProposal not found."));
                return null;
            }

            if (proposal.Status != ProposalStatus.Pending)
            {
                _notifier.Handle(new Notification($"Proposal is already {proposal.Status.ToString().ToLower()}."));
                return null;
            }

            var newStatus = normalizedAction == "accept" ? ProposalStatus.Accepted : ProposalStatus.Rejected;
            var decidedAt = DateTime.UtcNow;

            var update = Builders<IrrigationProposal>.Update
                .Set(p => p.Status, newStatus)
                .Set(p => p.DecidedAt, decidedAt);

            await _dbContext.IrrigationProposals.UpdateOneAsync(
                p => p.Id == request.ProposalId && p.UserId == userId,
                update,
                cancellationToken: cancellationToken);

            return new AcceptRejectProposalResponse
            {
                ProposalId = proposal.Id,
                Status = newStatus.ToString().ToLower(),
                DecidedAt = decidedAt
            };
        }
    }
}
