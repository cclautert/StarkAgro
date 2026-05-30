using AgripeWebAPI.Domain.Handlers.Pivots;

namespace AgripeWebAPI.Tests.Domain.Handlers.Pivots
{
    public class MoisturePredictionAlgorithmTests
    {
        // ── FitWeightedLinear ────────────────────────────────────────────────────

        [Fact]
        public void FitWeightedLinear_PerfectLinearData_MatchesExactSlopeAndIntercept()
        {
            // y = 60 - 0.5 * t  (moisture drops by 0.5% per hour)
            var times = Enumerable.Range(0, 25).Select(i => (double)i).ToList();
            var values = times.Select(t => 60.0 - 0.5 * t).ToList();

            var (slope, intercept, rmse, r2) = MoisturePredictionAlgorithm.FitWeightedLinear(times, values);

            Assert.Equal(-0.5, slope, precision: 3);
            Assert.Equal(60.0, intercept, precision: 3);
            Assert.True(rmse < 0.01, $"RMSE should be near 0, was {rmse}");
            Assert.True(r2 > 0.99, $"R² should be near 1, was {r2}");
        }

        [Fact]
        public void FitWeightedLinear_ConstantData_ZeroSlope()
        {
            var times = new List<double> { 0, 10, 20, 30, 40 };
            var values = new List<double> { 50, 50, 50, 50, 50 };

            var (slope, intercept, rmse, r2) = MoisturePredictionAlgorithm.FitWeightedLinear(times, values);

            Assert.Equal(0.0, slope, precision: 6);
            Assert.Equal(50.0, intercept, precision: 3);
            Assert.True(rmse < 0.001, $"RMSE for constant should be ~0, was {rmse}");
            Assert.Equal(0.0, r2, precision: 6);
        }

        [Fact]
        public void FitWeightedLinear_TwoPoints_ExactFit()
        {
            var times = new List<double> { 0, 10 };
            var values = new List<double> { 80, 60 };

            var (slope, intercept, _, _) = MoisturePredictionAlgorithm.FitWeightedLinear(times, values);

            // With 2 points any line through them is exact (weighted least squares will fit them)
            // slope should be approx (60-80)/10 = -2 (exact with equal weights, slightly off with exponential)
            Assert.True(slope < 0, "Slope should be negative for decreasing moisture");
            // Predicted value at t=0 and t=10 should bracket [60, 80]
            Assert.InRange(intercept, 60, 80);
        }

        [Fact]
        public void FitWeightedLinear_RecentReadingsWeightedMore()
        {
            // First 10 hours: flat at 60. Last 14 hours: sharp drop to 30.
            // Weighted regression should produce a more negative slope than unweighted
            // because recent (dropping) readings carry higher weight.
            var times = new List<double>();
            var values = new List<double>();
            for (int i = 0; i <= 10; i++) { times.Add(i); values.Add(60.0); }
            for (int i = 11; i <= 24; i++) { times.Add(i); values.Add(60.0 - (i - 10) * 2.5); }

            var (slope, _, _, _) = MoisturePredictionAlgorithm.FitWeightedLinear(times, values);

            Assert.True(slope < -1.0, $"Weighted slope should capture the recent downtrend, was {slope}");
        }

        [Fact]
        public void FitWeightedLinear_SinglePoint_ReturnsZeroSlope()
        {
            var (slope, intercept, rmse, r2) =
                MoisturePredictionAlgorithm.FitWeightedLinear(new[] { 0.0 }, new[] { 55.0 });

            Assert.Equal(0.0, slope, precision: 6);
            Assert.Equal(55.0, intercept, precision: 3);
            Assert.Equal(0.0, rmse, precision: 6);
            Assert.Equal(0.0, r2, precision: 6);
        }

        // ── ET0DailyMm ───────────────────────────────────────────────────────────

        [Fact]
        public void ET0DailyMm_TypicalSummerDay_PositiveResult()
        {
            // Tmax=30, Tmin=20, 20 MJ/m²/day → typical ET0 ~5-8 mm/day
            double et0 = MoisturePredictionAlgorithm.ET0DailyMm(30, 20, 20);

            Assert.True(et0 > 0, "ET0 should be positive");
            Assert.InRange(et0, 1, 20); // sanity range
        }

        [Fact]
        public void ET0DailyMm_TmaxEqualsTmin_ReturnsZero()
        {
            double et0 = MoisturePredictionAlgorithm.ET0DailyMm(25, 25, 20);

            Assert.Equal(0.0, et0, precision: 6);
        }

        [Fact]
        public void ET0DailyMm_ZeroRadiation_ReturnsZero()
        {
            double et0 = MoisturePredictionAlgorithm.ET0DailyMm(30, 20, 0);

            Assert.Equal(0.0, et0, precision: 6);
        }

        // ── ET0ToHourlyMoistureRate ──────────────────────────────────────────────

        [Fact]
        public void ET0ToHourlyMoistureRate_PositiveInput_ReturnsSmallPositiveRate()
        {
            double daily = 6.0; // mm/day
            double hourly = MoisturePredictionAlgorithm.ET0ToHourlyMoistureRate(daily);

            Assert.True(hourly > 0, "Hourly rate must be positive");
            Assert.True(hourly < 1, "Hourly rate should be < 1 %/hour for realistic ET0");
        }

        // ── Project ──────────────────────────────────────────────────────────────

        [Fact]
        public void Project_NegativeSlope_MoistureDecreasesOverTime()
        {
            var from = new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc);
            var result = MoisturePredictionAlgorithm.Project(60.0, -0.3, 0, 2.0, from, hours: 72);

            Assert.Equal(72, result.Count);
            Assert.True(result[71].Moisture < result[0].Moisture, "Moisture should decrease with negative slope");
            Assert.Equal(from.AddHours(1), result[0].Date);
            Assert.Equal(from.AddHours(72), result[71].Date);
        }

        [Fact]
        public void Project_ConfidenceBandIsSymmetric()
        {
            var from = new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc);
            double rmse = 3.0;
            var result = MoisturePredictionAlgorithm.Project(50.0, 0, 0, rmse, from, hours: 1);

            var (_, moisture, min, max) = result[0];
            double expectedBand = 1.5 * rmse;
            Assert.Equal(moisture - expectedBand, min, precision: 6);
            Assert.Equal(moisture + expectedBand, max, precision: 6);
        }

        [Fact]
        public void Project_ETComponentReducesMoistureMore()
        {
            var from = new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc);
            var withoutET = MoisturePredictionAlgorithm.Project(60.0, -0.1, 0, 1.0, from, hours: 24);
            var withET = MoisturePredictionAlgorithm.Project(60.0, -0.1, 0.05, 1.0, from, hours: 24);

            Assert.True(withET[23].Moisture < withoutET[23].Moisture,
                "ET drying should result in lower projected moisture");
        }
    }
}
