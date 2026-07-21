using StarkAgroAPI.Domain.Commands.Requests.Ndvi;
using StarkAgroAPI.Domain.Commands.Responses.Ndvi;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services.Ndvi;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Ndvi
{
    internal static class MonitoredAreaMapper
    {
        public static MonitoredAreaResponse ToResponse(MonitoredArea a) => new()
        {
            Id = a.Id,
            Name = a.Name,
            Crop = a.Crop,
            AreaKind = a.AreaKind,
            CenterLat = a.CenterLat,
            CenterLng = a.CenterLng,
            RadiusM = a.RadiusM,
            Altitude = a.Altitude,
            LocationAddress = a.LocationAddress,
            MonitoringEnabled = a.MonitoringEnabled,
            Status = a.Status,
            Ring = MonitoredAreaGeometry.ToRing(a.Geometry),
            LastFetchAt = a.LastFetchAt,
            LastAcquisitionDate = a.LastAcquisitionDate,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        };

        public static bool IsValidKind(string kind) =>
            kind == MonitoredAreaKind.Circle || kind == MonitoredAreaKind.Polygon;
    }

    public class ListMonitoredAreasHandler : IRequestHandler<ListMonitoredAreasRequest, List<MonitoredAreaResponse>>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public ListMonitoredAreasHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
        }

        public async Task<List<MonitoredAreaResponse>> Handle(ListMonitoredAreasRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
            var areas = await _dbContext.MonitoredAreas.Find(a => a.UserId == userId).ToListAsync(cancellationToken);
            return areas.Select(MonitoredAreaMapper.ToResponse).ToList();
        }
    }

    public class GetMonitoredAreaHandler : IRequestHandler<GetMonitoredAreaRequest, MonitoredAreaResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public GetMonitoredAreaHandler(agpDBContext dbContext, ICurrentUserContext currentUser, INotifier notifier)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
            _notifier = notifier;
        }

        public async Task<MonitoredAreaResponse?> Handle(GetMonitoredAreaRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
            var area = await _dbContext.MonitoredAreas
                .Find(a => a.Id == request.Id && a.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
            if (area is null)
            {
                _notifier.Handle(new Notification("Área não encontrada."));
                return null;
            }
            return MonitoredAreaMapper.ToResponse(area);
        }
    }

    public class CreateMonitoredAreaHandler : IRequestHandler<CreateMonitoredAreaRequest, MonitoredAreaResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public CreateMonitoredAreaHandler(agpDBContext dbContext, ICurrentUserContext currentUser, INotifier notifier)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
            _notifier = notifier;
        }

        public async Task<MonitoredAreaResponse?> Handle(CreateMonitoredAreaRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("Authenticated user is required.");

            if (!MonitoredAreaMapper.IsValidKind(request.AreaKind))
            {
                _notifier.Handle(new Notification("Tipo de área inválido. Use Circle ou Polygon."));
                return null;
            }

            if (!MonitoredAreaGeometry.TryBuild(request.Ring, out var geometry, out var error))
            {
                _notifier.Handle(new Notification(error!));
                return null;
            }

            var now = DateTime.UtcNow;
            var area = new MonitoredArea
            {
                Id = await _dbContext.GetNextIdAsync(nameof(MonitoredArea), cancellationToken),
                UserId = userId,
                Name = request.Name.Trim(),
                Crop = string.IsNullOrWhiteSpace(request.Crop) ? null : request.Crop.Trim(),
                AreaKind = request.AreaKind,
                CenterLat = request.CenterLat,
                CenterLng = request.CenterLng,
                RadiusM = request.RadiusM,
                Altitude = request.Altitude,
                LocationAddress = request.LocationAddress,
                MonitoringEnabled = request.MonitoringEnabled,
                Geometry = geometry,
                Status = MonitoredAreaStatus.Idle,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _dbContext.MonitoredAreas.InsertOneAsync(area, null, cancellationToken);
            return MonitoredAreaMapper.ToResponse(area);
        }
    }

    public class EditMonitoredAreaHandler : IRequestHandler<EditMonitoredAreaRequest, MonitoredAreaResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public EditMonitoredAreaHandler(agpDBContext dbContext, ICurrentUserContext currentUser, INotifier notifier)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
            _notifier = notifier;
        }

        public async Task<MonitoredAreaResponse?> Handle(EditMonitoredAreaRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("Authenticated user is required.");

            var area = await _dbContext.MonitoredAreas
                .Find(a => a.Id == request.Id && a.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
            if (area is null)
            {
                _notifier.Handle(new Notification("Área não encontrada."));
                return null;
            }

            if (!MonitoredAreaMapper.IsValidKind(request.AreaKind))
            {
                _notifier.Handle(new Notification("Tipo de área inválido. Use Circle ou Polygon."));
                return null;
            }

            if (!MonitoredAreaGeometry.TryBuild(request.Ring, out var geometry, out var error))
            {
                _notifier.Handle(new Notification(error!));
                return null;
            }

            area.Name = request.Name.Trim();
            area.Crop = string.IsNullOrWhiteSpace(request.Crop) ? null : request.Crop.Trim();
            area.AreaKind = request.AreaKind;
            area.CenterLat = request.CenterLat;
            area.CenterLng = request.CenterLng;
            area.RadiusM = request.RadiusM;
            area.Altitude = request.Altitude;
            area.LocationAddress = request.LocationAddress;
            area.MonitoringEnabled = request.MonitoringEnabled;
            area.Geometry = geometry;
            area.UpdatedAt = DateTime.UtcNow;

            await _dbContext.MonitoredAreas.ReplaceOneAsync(
                a => a.Id == area.Id && a.UserId == userId, area, new ReplaceOptions(), cancellationToken);

            return MonitoredAreaMapper.ToResponse(area);
        }
    }

    public class DeleteMonitoredAreaHandler : IRequestHandler<DeleteMonitoredAreaRequest, bool>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly INotifier _notifier;

        public DeleteMonitoredAreaHandler(agpDBContext dbContext, ICurrentUserContext currentUser, INotifier notifier)
        {
            _dbContext = dbContext;
            _currentUser = currentUser;
            _notifier = notifier;
        }

        public async Task<bool> Handle(DeleteMonitoredAreaRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? throw new InvalidOperationException("Authenticated user is required.");

            var result = await _dbContext.MonitoredAreas.DeleteOneAsync(
                a => a.Id == request.Id && a.UserId == userId, cancellationToken);

            if (result.DeletedCount == 0)
            {
                _notifier.Handle(new Notification("Área não encontrada."));
                return false;
            }
            return true;
        }
    }
}
