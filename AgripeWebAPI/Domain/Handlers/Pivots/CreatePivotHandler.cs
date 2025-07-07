using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MediatR;

namespace AgripeWebAPI.Domain.Handlers.Pivots
{
    public class CreatePivotHandler : IRequestHandler<CreatePivotRequest, CreatePivotResponse>
    {
        private readonly agpDBContext _dbContext;

        public CreatePivotHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<CreatePivotResponse> Handle(CreatePivotRequest request, CancellationToken cancellationToken)
        {
            if (!request.UserId.HasValue)
            {
                throw new ArgumentNullException(nameof(request.UserId), "UserId cannot be null.");
            }

            var pivot = new Pivot
            {
                Name = request.Name,
                UserId = request.UserId.Value
            };

            _dbContext.Pivots.Add(pivot);
            _dbContext.SaveChanges(); // EF Core salva e preenche o Id automaticamente

            return Task.FromResult(new CreatePivotResponse
            {
                Id = pivot.Id // <-- Aqui o Id já estará preenchido
            });

        }
    }
}
