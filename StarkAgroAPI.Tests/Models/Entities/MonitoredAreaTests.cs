using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Ndvi;

namespace StarkAgroAPI.Tests.Models.Entities
{
    public class MonitoredAreaTests
    {
        [Fact]
        public void Defaults_ArePolygonIdleAndEnabled()
        {
            var area = new MonitoredArea();

            Assert.Equal(MonitoredAreaKind.Polygon, area.AreaKind);
            Assert.Equal(MonitoredAreaStatus.Idle, area.Status);
            Assert.True(area.MonitoringEnabled);
        }

        [Fact]
        public void AllFields_RoundTrip()
        {
            MonitoredAreaGeometry.TryBuild(new List<GeoCoordinate>
            {
                new() { Lat = -23.0, Lng = -47.0 },
                new() { Lat = -23.0, Lng = -46.99 },
                new() { Lat = -22.99, Lng = -46.99 }
            }, out var geo, out _);
            var now = DateTime.UtcNow;

            var area = new MonitoredArea
            {
                Id = 1, UserId = 42, Name = "Talhão", Crop = "Soja", AreaKind = MonitoredAreaKind.Circle,
                CenterLat = -23.0, CenterLng = -47.0, RadiusM = 200, Altitude = 550, LocationAddress = "Fazenda X",
                Geometry = geo, MonitoringEnabled = false,
                NextFetchAt = now, LastFetchAt = now, LastAcquisitionDate = "2026-07-20",
                Status = MonitoredAreaStatus.Queued, ProcessingStartedAt = now, WorkerId = "w1",
                RetryCount = 2, NextAttemptAt = now, FailureReason = "boom", CreatedAt = now, UpdatedAt = now
            };

            Assert.Equal(42, area.UserId);
            Assert.Equal(200, area.RadiusM);
            Assert.False(area.MonitoringEnabled);
            Assert.Equal("2026-07-20", area.LastAcquisitionDate);
            Assert.Equal(2, area.RetryCount);
            Assert.Equal("boom", area.FailureReason);
            Assert.Equal(MonitoredAreaStatus.Queued, area.Status);
        }
    }
}
