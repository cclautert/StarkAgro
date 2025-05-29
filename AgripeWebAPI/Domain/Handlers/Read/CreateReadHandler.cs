using AgripeWebAPI.Domain.Commands.Requests.Read;
using AgripeWebAPI.Domain.Commands.Responses.Read;
using MediatR;

namespace AgripeWebAPI.Domain.Handlers.Read
{
    public class CreateReadHandler : IRequestHandler<CreateReadRequest, CreateReadResponse>
    {
        public CreateReadHandler()
        {
            
        }

        public Task<CreateReadResponse> Handle(CreateReadRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new CreateReadResponse());
        }
    }
}
