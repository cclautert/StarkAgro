using AgripeWebAPI.Domain.Handlers.Irrigation;
using AgripeWebAPI.Models;

namespace AgripeWebAPI.Tests.Domain.Handlers.Irrigation
{
    public class GreedyIrrigationSchedulerTests
    {
        private static readonly DateTime HorizonStart = new(2025, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime HorizonEnd = HorizonStart.AddHours(48);

        // --- ShouldSkipForRain ---

        [Fact]
        public void ShouldSkipForRain_NullForecast_ReturnsFalse()
        {
            Assert.False(GreedyIrrigationScheduler.ShouldSkipForRain(null, 10.0));
        }

        [Fact]
        public void ShouldSkipForRain_UnavailableForecast_ReturnsFalse()
        {
            var forecast = WeatherForecast.Unavailable("OpenMeteo");
            Assert.False(GreedyIrrigationScheduler.ShouldSkipForRain(forecast, 10.0));
        }

        [Fact]
        public void ShouldSkipForRain_PrecipBelowThreshold_ReturnsFalse()
        {
            var forecast = new WeatherForecast { TotalPrecipitationMm = 5.0, IsAvailable = true };
            Assert.False(GreedyIrrigationScheduler.ShouldSkipForRain(forecast, 10.0));
        }

        [Fact]
        public void ShouldSkipForRain_PrecipAtThreshold_ReturnsTrue()
        {
            var forecast = new WeatherForecast { TotalPrecipitationMm = 10.0, IsAvailable = true };
            Assert.True(GreedyIrrigationScheduler.ShouldSkipForRain(forecast, 10.0));
        }

        [Fact]
        public void ShouldSkipForRain_PrecipAboveThreshold_ReturnsTrue()
        {
            var forecast = new WeatherForecast { TotalPrecipitationMm = 25.0, IsAvailable = true };
            Assert.True(GreedyIrrigationScheduler.ShouldSkipForRain(forecast, 10.0));
        }

        // --- ComputeEstimatedMm ---

        [Fact]
        public void ComputeEstimatedMm_PositiveFlow_ReturnsExpectedValue()
        {
            var mm = GreedyIrrigationScheduler.ComputeEstimatedMm(5000.0, 60);
            // 5000 L/h * 1 h / 785398 m² = 0.00636...
            Assert.True(mm > 0, "estimatedMm should be positive");
            Assert.Equal(Math.Round(5000.0 / 785398.0, 3), mm);
        }

        [Fact]
        public void ComputeEstimatedMm_DoubleDuration_DoublesResult()
        {
            var mm120 = GreedyIrrigationScheduler.ComputeEstimatedMm(5000.0, 120);
            var expected = Math.Round(5000.0 * 2.0 / 785398.0, 3);
            Assert.Equal(expected, mm120);
        }

        // --- Build: empty input ---

        [Fact]
        public void Build_EmptyPivots_ReturnsEmptyList()
        {
            var result = GreedyIrrigationScheduler.Build(
                new List<GreedyIrrigationScheduler.PivotInput>(),
                5000, 60, 10.0, HorizonStart, HorizonEnd);

            Assert.Empty(result);
        }

        // --- Build: basic sequential schedule ---

        [Fact]
        public void Build_ThreePivots_NoRain_ReturnsThreeWindows()
        {
            var pivots = new List<GreedyIrrigationScheduler.PivotInput>
            {
                new(1, -15.5, -47.0, null),
                new(2, -15.6, -47.1, null),
                new(3, -15.7, -47.2, null)
            };

            var result = GreedyIrrigationScheduler.Build(pivots, 5000, 60, 10.0, HorizonStart, HorizonEnd);

            Assert.Equal(3, result.Count);
            Assert.Equal(1, result[0].PivotId);
            Assert.Equal(HorizonStart, result[0].StartUtc);
            Assert.Equal(HorizonStart.AddHours(1), result[0].EndUtc);

            Assert.Equal(2, result[1].PivotId);
            Assert.Equal(HorizonStart.AddHours(1), result[1].StartUtc);
            Assert.Equal(HorizonStart.AddHours(2), result[1].EndUtc);

            Assert.Equal(3, result[2].PivotId);
            Assert.Equal(HorizonStart.AddHours(2), result[2].StartUtc);
            Assert.Equal(HorizonStart.AddHours(3), result[2].EndUtc);
        }

        [Fact]
        public void Build_Windows_AreNonOverlapping()
        {
            var pivots = Enumerable.Range(1, 5)
                .Select(i => new GreedyIrrigationScheduler.PivotInput(i, null, null, null))
                .ToList();

            var result = GreedyIrrigationScheduler.Build(pivots, 5000, 90, 10.0, HorizonStart, HorizonEnd);

            for (int i = 1; i < result.Count; i++)
            {
                Assert.True(result[i].StartUtc >= result[i - 1].EndUtc,
                    $"Window {i} overlaps with window {i - 1}");
            }
        }

        // --- Build: rain filtering ---

        [Fact]
        public void Build_PivotWithHighRain_IsSkipped()
        {
            var heavyRain = new WeatherForecast { TotalPrecipitationMm = 20.0, IsAvailable = true };

            var pivots = new List<GreedyIrrigationScheduler.PivotInput>
            {
                new(1, null, null, null),
                new(2, -15.6, -47.1, heavyRain),  // should be skipped
                new(3, null, null, null)
            };

            var result = GreedyIrrigationScheduler.Build(pivots, 5000, 60, 10.0, HorizonStart, HorizonEnd);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, w => w.PivotId == 1);
            Assert.Contains(result, w => w.PivotId == 3);
            Assert.DoesNotContain(result, w => w.PivotId == 2);
        }

        [Fact]
        public void Build_AllPivotsWithHighRain_ReturnsEmptyList()
        {
            var heavyRain = new WeatherForecast { TotalPrecipitationMm = 15.0, IsAvailable = true };

            var pivots = new List<GreedyIrrigationScheduler.PivotInput>
            {
                new(1, null, null, heavyRain),
                new(2, null, null, heavyRain)
            };

            var result = GreedyIrrigationScheduler.Build(pivots, 5000, 60, 10.0, HorizonStart, HorizonEnd);

            Assert.Empty(result);
        }

        [Fact]
        public void Build_PivotWithUnavailableForecast_IsNotSkipped()
        {
            var unavailable = WeatherForecast.Unavailable("OpenMeteo");

            var pivots = new List<GreedyIrrigationScheduler.PivotInput>
            {
                new(1, -15.5, -47.0, unavailable)
            };

            var result = GreedyIrrigationScheduler.Build(pivots, 5000, 60, 10.0, HorizonStart, HorizonEnd);

            Assert.Single(result);
        }

        // --- Build: horizon capping ---

        [Fact]
        public void Build_WindowExceedsHorizon_IsNotIncluded()
        {
            var nearEnd = HorizonEnd.AddMinutes(-30);
            var pivots = new List<GreedyIrrigationScheduler.PivotInput>
            {
                new(1, null, null, null),
                new(2, null, null, null) // this window would start at nearEnd + 60 min, exceeding horizon
            };

            var tightHorizonEnd = HorizonStart.AddMinutes(90); // only 1.5 h available
            var result = GreedyIrrigationScheduler.Build(pivots, 5000, 60, 10.0, HorizonStart, tightHorizonEnd);

            Assert.Single(result); // only pivot 1 fits
        }

        // --- Build: estimatedMm consistency ---

        [Fact]
        public void Build_AllWindowsHaveSameEstimatedMm()
        {
            var pivots = Enumerable.Range(1, 3)
                .Select(i => new GreedyIrrigationScheduler.PivotInput(i, null, null, null))
                .ToList();

            var result = GreedyIrrigationScheduler.Build(pivots, 5000, 60, 10.0, HorizonStart, HorizonEnd);

            var expected = GreedyIrrigationScheduler.ComputeEstimatedMm(5000, 60);
            Assert.All(result, w => Assert.Equal(expected, w.EstimatedMm));
        }
    }
}
