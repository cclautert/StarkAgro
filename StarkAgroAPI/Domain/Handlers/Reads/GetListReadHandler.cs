using StarkAgroAPI.Domain.Commands.Requests.Reads;
using StarkAgroAPI.Domain.Commands.Responses.Pivots;
using StarkAgroAPI.Domain.Commands.Responses.Reads;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Interfaces;
using MediatR;
using MongoDB.Driver;
using System.Linq;

namespace StarkAgroAPI.Domain.Handlers.Sensors
{
    public class GetListReadHandler : IRequestHandler<GetListReadRequest, IAsyncEnumerable<GetReadResponse>>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public GetListReadHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<IAsyncEnumerable<GetReadResponse>> Handle(GetListReadRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required to list reads.");

            var startDate = DateTime.UtcNow.AddDays(-request.NumberOfReads);

            var reads = await _dbContext.ReadSensors
                .Find(x => x.UserId == userId && x.Date >= startDate)
                .SortBy(x => x.Date)
                .Project(x => new GetReadResponse
                {
                    Id = x.Id,
                    SensorId = x.SensorId,
                    Value = x.Humidity ?? 0,
                    Date = x.Date
                })
                .ToListAsync(cancellationToken);

            return ToAsyncEnumerable(reads, cancellationToken);
        }

        private static async IAsyncEnumerable<GetReadResponse> ToAsyncEnumerable(
            IEnumerable<GetReadResponse> reads,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var read in reads)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return read;
                await Task.CompletedTask;
            }
        }
    }
}
