using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgripeWebAPI.Domain.Handlers.Pivots
{
    public class DeletePivotHandler : IRequestHandler<DeletePivotRequest, DeletePivotResponse>
    {
        private readonly agpDBContext _dbContext;
        public DeletePivotHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext;
        }
        public Task<DeletePivotResponse> Handle(DeletePivotRequest request, CancellationToken cancellationToken)
        {
            var pivot = _dbContext.Pivots.FirstOrDefault(p => p.Id == request.Id);
            _dbContext.Pivots.Remove(pivot);
            _dbContext.SaveChanges();
            return Task.FromResult(new DeletePivotResponse());
        }
    }
}
