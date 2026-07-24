using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Domain.Commands.Responses.Admin;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Admin
{
    /// <summary>
    /// Lista de nomes de culturas, ordenada — pública para qualquer usuário autenticado (produtor
    /// cadastra área/diagnóstico). Os três seletores de cultura leem daqui.
    /// </summary>
    public class GetCulturesHandler : IRequestHandler<GetCulturesRequest, List<string>>
    {
        private readonly agpDBContext _dbContext;
        public GetCulturesHandler(agpDBContext dbContext) => _dbContext = dbContext;

        public async Task<List<string>> Handle(GetCulturesRequest request, CancellationToken cancellationToken)
        {
            var cultures = await _dbContext.Cultures.Find(_ => true).ToListAsync(cancellationToken);
            return cultures
                .Select(c => c.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public class GetAdminCulturesHandler : IRequestHandler<GetAdminCulturesRequest, List<CultureResponse>>
    {
        private readonly agpDBContext _dbContext;
        public GetAdminCulturesHandler(agpDBContext dbContext) => _dbContext = dbContext;

        public async Task<List<CultureResponse>> Handle(GetAdminCulturesRequest request, CancellationToken cancellationToken)
        {
            var cultures = await _dbContext.Cultures.Find(_ => true).ToListAsync(cancellationToken);
            return cultures
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(c => new CultureResponse { Id = c.Id, Name = c.Name })
                .ToList();
        }
    }

    public class CreateCultureHandler : IRequestHandler<CreateCultureRequest, CultureResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly INotifier _notifier;

        public CreateCultureHandler(agpDBContext dbContext, INotifier notifier)
        {
            _dbContext = dbContext;
            _notifier = notifier;
        }

        public async Task<CultureResponse?> Handle(CreateCultureRequest request, CancellationToken cancellationToken)
        {
            var name = request.Name.Trim();
            var now = DateTime.UtcNow;
            var culture = new Culture
            {
                Id = await _dbContext.GetNextIdAsync(nameof(Culture), cancellationToken),
                Name = name,
                CreatedAt = now,
                UpdatedAt = now
            };

            try
            {
                await _dbContext.Cultures.InsertOneAsync(culture, null, cancellationToken);
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                // Índice único case-insensitive: já existe uma cultura com esse nome.
                _notifier.Handle(new Notification($"A cultura \"{name}\" já existe."));
                return null;
            }

            return new CultureResponse { Id = culture.Id, Name = culture.Name };
        }
    }

    public class UpdateCultureHandler : IRequestHandler<UpdateCultureRequest, CultureResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly INotifier _notifier;

        public UpdateCultureHandler(agpDBContext dbContext, INotifier notifier)
        {
            _dbContext = dbContext;
            _notifier = notifier;
        }

        public async Task<CultureResponse?> Handle(UpdateCultureRequest request, CancellationToken cancellationToken)
        {
            var existing = await _dbContext.Cultures.Find(c => c.Id == request.Id).FirstOrDefaultAsync(cancellationToken);
            if (existing is null)
            {
                _notifier.Handle(new Notification("Cultura não encontrada."));
                return null;
            }

            var name = request.Name.Trim();
            try
            {
                await _dbContext.Cultures.UpdateOneAsync(
                    c => c.Id == request.Id,
                    Builders<Culture>.Update.Set(c => c.Name, name).Set(c => c.UpdatedAt, DateTime.UtcNow),
                    null, cancellationToken);
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                _notifier.Handle(new Notification($"A cultura \"{name}\" já existe."));
                return null;
            }

            return new CultureResponse { Id = request.Id, Name = name };
        }
    }

    public class DeleteCultureHandler : IRequestHandler<DeleteCultureRequest, bool>
    {
        private readonly agpDBContext _dbContext;
        public DeleteCultureHandler(agpDBContext dbContext) => _dbContext = dbContext;

        public async Task<bool> Handle(DeleteCultureRequest request, CancellationToken cancellationToken)
        {
            // Delete permitido mesmo em uso: o seletor do front inclui o valor salvo mesmo fora da
            // lista, então nenhuma área/perfil perde a cultura.
            await _dbContext.Cultures.DeleteOneAsync(c => c.Id == request.Id, cancellationToken);
            return true;
        }
    }
}
