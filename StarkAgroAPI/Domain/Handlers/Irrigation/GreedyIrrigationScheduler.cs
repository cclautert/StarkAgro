using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;

namespace StarkAgroAPI.Domain.Handlers.Irrigation
{
    /// <summary>
    /// Pure greedy scheduling algorithm — no I/O, fully unit-testable.
    ///
    /// Assigns sequential, non-overlapping irrigation windows starting from <paramref name="horizonStart"/>
    /// and capped at <paramref name="horizonEnd"/>. A pivot's window is skipped when the 48-h forecast
    /// total precipitation for that pivot's coordinates meets or exceeds the rain threshold.
    ///
    /// estimatedMm = (maxFlowLitersPerHour * durationHours) / (defaultCoverageAreaM2 / 1000)
    /// where defaultCoverageAreaM2 = π × 500² ≈ 785 398 m² (typical 500-m-arm center pivot).
    /// </summary>
    public static class GreedyIrrigationScheduler
    {
        // π × 500² m² — default center-pivot coverage area assumption
        private const double DefaultCoverageAreaM2 = 785_398.0;

        public record PivotInput(
            int PivotId,
            double? Latitude,
            double? Longitude,
            WeatherForecast? Forecast48h);

        public record ScheduleWindow(
            int PivotId,
            DateTime StartUtc,
            DateTime EndUtc,
            double EstimatedMm);

        /// <summary>
        /// Builds the greedy schedule.
        /// </summary>
        /// <param name="pivots">Pivots to schedule (in priority order).</param>
        /// <param name="maxFlowLitersPerHour">Total source flow rate; one pivot irrigates at a time.</param>
        /// <param name="durationMinutes">Typical irrigation duration per pivot.</param>
        /// <param name="rainThresholdMm">Skip pivot if forecasted rain &gt;= this value.</param>
        /// <param name="horizonStart">Schedule start (typically next UTC hour).</param>
        /// <param name="horizonEnd">Schedule end (typically horizonStart + 48 h).</param>
        public static List<ScheduleWindow> Build(
            IReadOnlyList<PivotInput> pivots,
            double maxFlowLitersPerHour,
            int durationMinutes,
            double rainThresholdMm,
            DateTime horizonStart,
            DateTime horizonEnd)
        {
            if (pivots is null || pivots.Count == 0)
                return new List<ScheduleWindow>();

            var windows = new List<ScheduleWindow>();
            var cursor = horizonStart;
            var duration = TimeSpan.FromMinutes(durationMinutes);
            double estimatedMm = ComputeEstimatedMm(maxFlowLitersPerHour, durationMinutes);

            foreach (var pivot in pivots)
            {
                var windowEnd = cursor + duration;
                if (windowEnd > horizonEnd)
                    break;

                if (ShouldSkipForRain(pivot.Forecast48h, rainThresholdMm))
                    continue;

                windows.Add(new ScheduleWindow(pivot.PivotId, cursor, windowEnd, estimatedMm));
                cursor = windowEnd;
            }

            return windows;
        }

        /// <summary>Returns true when forecasted precipitation meets or exceeds the threshold.</summary>
        public static bool ShouldSkipForRain(WeatherForecast? forecast, double rainThresholdMm)
        {
            if (forecast is null || !forecast.IsAvailable)
                return false;

            return forecast.TotalPrecipitationMm >= rainThresholdMm;
        }

        /// <summary>
        /// Computes estimated irrigation depth in mm.
        /// Assumes the entire flow irrigates a single pivot with <see cref="DefaultCoverageAreaM2"/>.
        /// </summary>
        public static double ComputeEstimatedMm(double maxFlowLitersPerHour, int durationMinutes)
        {
            var volumeLiters = maxFlowLitersPerHour * (durationMinutes / 60.0);
            // 1 liter / m² = 1 mm; divide volume (liters) by area (m²)
            return Math.Round(volumeLiters / DefaultCoverageAreaM2, 3);
        }
    }
}
