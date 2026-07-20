export interface DailyForecast {
  date: string;
  precipitationMm: number;
  probabilityPercent: number | null;
}

export interface WeatherForecast {
  totalPrecipitationMm: number;
  source: string;
  isAvailable: boolean;
  probabilityOfPrecipitation: number | null;
  dailyForecasts: DailyForecast[];
}

export interface IrrigationTrend {
  pivotId: number;
  pivotName: string | null;
  latitude: number | null;
  longitude: number | null;
  limiteInferior: number | null;
  limiteSuperior: number | null;
  currentAverage: number | null;
  needsIrrigation: boolean;
  irrigationPostponed: boolean;
  postponeReason: string | null;
  weatherForecast: WeatherForecast | null;
}
