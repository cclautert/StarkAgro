namespace AgripeWebAPI.Domain.Handlers.Pivots
{
    /// <summary>
    /// Weighted linear regression and ET0 helpers for moisture prediction.
    /// Exposed as public to allow direct unit testing of the algorithm without MongoDB dependencies.
    /// </summary>
    public static class MoisturePredictionAlgorithm
    {
        // Exponential decay: recent readings carry more weight.
        private const double DefaultDecayLambda = 0.05;

        // Approximation: 1 mm of ET0 reduces soil moisture by this many percentage points.
        // Assumes ~200 mm effective root-zone depth and typical field capacity/wilting margins.
        private const double EtToMoistureFactorPerHour = 0.3 / 24.0;

        /// <summary>
        /// Fits a weighted linear regression y = intercept + slope * t to the provided data.
        /// Weights are exponential: w[i] = exp(-lambda * (maxT - t[i])), so the most
        /// recent readings influence the slope the most.
        /// Returns (slope, intercept, rmse, r2). R² = 0 when all values are identical.
        /// </summary>
        public static (double Slope, double Intercept, double Rmse, double R2) FitWeightedLinear(
            IReadOnlyList<double> times,
            IReadOnlyList<double> values,
            double decayLambda = DefaultDecayLambda)
        {
            if (times.Count != values.Count || times.Count < 2)
                return (0, values.Count > 0 ? values[0] : 0, 0, 0);

            double maxT = times[times.Count - 1];

            double sumW = 0, sumWt = 0, sumWy = 0, sumWtt = 0, sumWty = 0;
            var weights = new double[times.Count];

            for (int i = 0; i < times.Count; i++)
            {
                double w = Math.Exp(-decayLambda * (maxT - times[i]));
                weights[i] = w;
                sumW += w;
                sumWt += w * times[i];
                sumWy += w * values[i];
                sumWtt += w * times[i] * times[i];
                sumWty += w * times[i] * values[i];
            }

            double denom = sumW * sumWtt - sumWt * sumWt;
            double slope, intercept;

            if (Math.Abs(denom) < 1e-12)
            {
                slope = 0;
                intercept = sumWy / sumW;
            }
            else
            {
                slope = (sumW * sumWty - sumWt * sumWy) / denom;
                intercept = (sumWy - slope * sumWt) / sumW;
            }

            // Weighted RMSE and R²
            double sumWRes = 0, sumWTot = 0;
            double yMean = sumWy / sumW;

            for (int i = 0; i < times.Count; i++)
            {
                double pred = intercept + slope * times[i];
                double res = values[i] - pred;
                sumWRes += weights[i] * res * res;
                double dev = values[i] - yMean;
                sumWTot += weights[i] * dev * dev;
            }

            double rmse = Math.Sqrt(sumWRes / sumW);
            double r2 = sumWTot < 1e-12 ? 0.0 : Math.Max(0.0, 1.0 - sumWRes / sumWTot);

            return (slope, intercept, rmse, r2);
        }

        /// <summary>
        /// Hargreaves ET0 simplified formula.
        /// Returns estimated daily ET0 in mm/day.
        /// Ra (shortwave_radiation_sum from Open-Meteo in MJ/m²/day) is used as proxy for
        /// extraterrestrial radiation; divide by latent heat of vaporization (2.45 MJ/kg = 2.45 mm/MJ) to get mm equivalent.
        /// </summary>
        public static double ET0DailyMm(double tMax, double tMin, double shortwaveRadiationMJm2)
        {
            if (tMax <= tMin || shortwaveRadiationMJm2 <= 0) return 0;
            double tMean = (tMax + tMin) / 2.0;
            double raMmDay = shortwaveRadiationMJm2 / 2.45;
            return 0.0023 * (tMean + 17.8) * Math.Sqrt(tMax - tMin) * raMmDay;
        }

        /// <summary>
        /// Converts daily ET0 (mm/day) to an hourly moisture percentage decrease rate.
        /// </summary>
        public static double ET0ToHourlyMoistureRate(double et0DailyMm)
            => et0DailyMm * EtToMoistureFactorPerHour;

        /// <summary>
        /// Projects hourly moisture values for the next <paramref name="hours"/> hours.
        /// The effective slope combines the historical trend and the ET drying rate.
        /// </summary>
        public static List<(DateTime Date, double Moisture, double Min, double Max)> Project(
            double lastMoisture,
            double histSlope,
            double etHourlyRate,
            double rmse,
            DateTime fromUtc,
            int hours = 72)
        {
            double effectiveSlope = histSlope - etHourlyRate;
            double halfBand = 1.5 * rmse;

            var result = new List<(DateTime, double, double, double)>(hours);
            for (int h = 1; h <= hours; h++)
            {
                double predicted = lastMoisture + effectiveSlope * h;
                result.Add((fromUtc.AddHours(h), predicted, predicted - halfBand, predicted + halfBand));
            }
            return result;
        }
    }
}
