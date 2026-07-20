using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;

namespace StarkAgroAPI.Domain.Handlers.Pivots
{
    internal static class PivotLocationValidator
    {
        private const double MinLatitude = -90;
        private const double MaxLatitude = 90;
        private const double MinLongitude = -180;
        private const double MaxLongitude = 180;
        private const double MinAltitude = -500;
        private const double MaxAltitude = 9000;

        public static bool Validate(double? latitude, double? longitude, double? altitude, INotifier notifier)
        {
            var hasLat = latitude.HasValue;
            var hasLon = longitude.HasValue;

            if (hasLat ^ hasLon)
            {
                notifier.Handle(new Notification("Latitude and longitude must be provided together."));
                return false;
            }

            if (hasLat && (latitude!.Value < MinLatitude || latitude.Value > MaxLatitude))
            {
                notifier.Handle(new Notification($"Latitude must be between {MinLatitude} and {MaxLatitude}."));
                return false;
            }

            if (hasLon && (longitude!.Value < MinLongitude || longitude.Value > MaxLongitude))
            {
                notifier.Handle(new Notification($"Longitude must be between {MinLongitude} and {MaxLongitude}."));
                return false;
            }

            if (altitude.HasValue && (altitude.Value < MinAltitude || altitude.Value > MaxAltitude))
            {
                notifier.Handle(new Notification($"Altitude must be between {MinAltitude} and {MaxAltitude} meters."));
                return false;
            }

            return true;
        }
    }
}
