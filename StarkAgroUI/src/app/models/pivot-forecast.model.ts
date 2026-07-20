import { WeatherForecast } from './irrigation-trend.model';

export interface PivotForecast {
  pivotId: number;
  pivotName: string | null;
  latitude: number | null;
  longitude: number | null;
  days: number;
  hasCoordinates: boolean;
  forecast: WeatherForecast | null;
  message: string | null;
}
