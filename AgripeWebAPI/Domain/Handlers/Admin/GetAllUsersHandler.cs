using AgripeWebAPI.Domain.Commands.Requests.Admin;
using AgripeWebAPI.Domain.Commands.Responses.Admin;
using AgripeWebAPI.Models;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Admin
{
    public class GetAllUsersHandler : IRequestHandler<GetAllUsersRequest, List<AdminUserResponse>>
    {
        private readonly agpDBContext _dbContext;

        public GetAllUsersHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<List<AdminUserResponse>> Handle(GetAllUsersRequest request, CancellationToken cancellationToken)
        {
            var users = await _dbContext.Users.Find(_ => true).ToListAsync(cancellationToken);

            return users.Select(u => new AdminUserResponse
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Active = u.Active,
                IsAdmin = u.IsAdmin,
                IsAgronomist = u.IsAgronomist,
                AgronomistCrea = u.AgronomistCrea,
                DiagnosisQuotaPerMonth = u.DiagnosisQuotaPerMonth,
                LimiteInferior = u.LimiteInferior,
                LimiteSuperior = u.LimiteSuperior,
                RainThresholdMm = u.RainThresholdMm,
                UplinkIntervalSeconds = u.UplinkIntervalSeconds
            }).ToList();
        }
    }
}
