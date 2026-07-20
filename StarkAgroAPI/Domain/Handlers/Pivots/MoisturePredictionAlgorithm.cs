namespace StarkAgroAPI.Domain.Handlers.Pivots
{
    /// <summary>
    /// Weighted linear regression and ET0 helpers for moisture prediction.
    /// Exposed as public to allow direct unit testing of the algorithm without MongoDB dependencies.
    /// </summary>
    public static class MoisturePredictionAlgorithm
    {
        // Exponential decay: recent readings carry more weight.
        private const double DefaultDecayLambda = 0.05;

        // Weight applied to relative RMSE (rmse / scale) when computing stability confidence.
        // Higher sensitivity means a smaller relative noise ratio is enough to erode
        // confidence away from 1.0. Tunable — no field data yet to calibrate against.
        private const double FlatSignalNoiseSensitivity = 20.0;

        // Approximation: 1 mm of ET0 reduces soil moisture by this many percentage points.
        // Assumes ~200 mm effective root-zone depth and typical field capacity/wilting margins.
        private const double EtToMoistureFactorPerHour = 0.3 / 24.0;

        /// <summary>
        /// Fits a weighted linear regression y = intercept + slope * t to the provided data.
        /// Weights are exponential: w[i] = exp(-lambda * (maxT - t[i])), so the most
        /// recent readings influence the slope the most.
        /// Returns (slope, intercept, rmse, r2). r2 is the higher of two confidence signals:
        /// (a) the classic R² — how much of the signal's variance the trend line explains,
        /// which degenerates toward 0 for a flat/stable signal since there's ~no variance to
        /// explain — and (b) a stability score based on how tight the residuals are relative to
        /// the signal's own scale. A flat, low-noise reading scores high on (b) even when (a)
        /// collapses; a genuinely noisy signal (flat or trending) scores low on both.
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

            // Trend fit quality: how much of the signal's own variance a straight line explains.
            // Degenerates toward 0 for a flat/stable signal, since there's ~no variance for any
            // line to "explain" — that alone would make a stable, low-noise signal look unreliable.
            double r2Trend = sumWTot < 1e-12 ? 0.0 : Math.Max(0.0, 1.0 - sumWRes / sumWTot);

            // Stability confidence: how tight the residuals are relative to the reading's own
            // scale, independent of the trend's variance. A flat, low-noise signal scores high
            // here even when r2Trend collapses; a noisy signal (flat or trending) scores low on both.
            double scale = Math.Max(Math.Abs(yMean), 1.0);
            double stabilityConfidence = Math.Clamp(1.0 - (rmse / scale) * FlatSignalNoiseSensitivity, 0.0, 1.0);

            double r2 = Math.Max(r2Trend, stabilityConfidence);

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
        /// Converts daily forecast precipitation (mm) to an hourly moisture percentage gain rate,
        /// using the same mm→% infiltration approximation as <see cref="ET0ToHourlyMoistureRate"/>.
        /// </summary>
        public static double RainToHourlyMoistureRate(double precipitationMm)
            => precipitationMm * EtToMoistureFactorPerHour;

        /// <summary>
        /// Projects hourly moisture values for the next <paramref name="hours"/> hours.
        /// The effective slope combines the historical trend with a per-day net drying rate
        /// (ET0 drying minus rain gain). <paramref name="dailyNetDryingRate"/> has one entry
        /// per projected day (index 0 = first day); the last entry is reused for any day beyond
        /// the list's length. An empty list means no ET/rain component (rate 0 throughout).
        /// </summary>
        public static List<(DateTime Date, double Moisture, double Min, double Max)> Project(
            double lastMoisture,
            double histSlope,
            IReadOnlyList<double> dailyNetDryingRate,
            double rmse,
            DateTime fromUtc,
            int hours = 72)
        {
            double halfBand = 1.5 * rmse;

            var result = new List<(DateTime, double, double, double)>(hours);
            double cumulativeDrying = 0;
            for (int h = 1; h <= hours; h++)
            {
                if (dailyNetDryingRate.Count > 0)
                {
                    int dayIndex = Math.Min((h - 1) / 24, dailyNetDryingRate.Count - 1);
                    cumulativeDrying += dailyNetDryingRate[dayIndex];
                }

                double predicted = lastMoisture + histSlope * h - cumulativeDrying;
                result.Add((fromUtc.AddHours(h), predicted, predicted - halfBand, predicted + halfBand));
            }
            return result;
        }
    }
}
