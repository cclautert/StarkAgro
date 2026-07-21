// NDVI — áreas monitoradas (talhões) e a série de índice de vegetação do Sentinel-2.

export interface GeoCoordinate {
  lat: number;
  lng: number;
}

export type AreaKind = 'Circle' | 'Polygon';

export interface MonitoredArea {
  id: number;
  name: string;
  crop?: string | null;
  areaKind: AreaKind;
  centerLat?: number | null;
  centerLng?: number | null;
  radiusM?: number | null;
  altitude?: number | null;
  locationAddress?: string | null;
  monitoringEnabled: boolean;
  status: string;
  ring: GeoCoordinate[];
  lastFetchAt?: string | null;
  lastAcquisitionDate?: string | null;
  createdAt: string;
  updatedAt: string;
}

/** Corpo de criação/edição. O servidor fecha o anel e converte para [lng,lat]. */
export interface AreaRequest {
  name: string;
  crop?: string | null;
  areaKind: AreaKind;
  centerLat?: number | null;
  centerLng?: number | null;
  radiusM?: number | null;
  altitude?: number | null;
  locationAddress?: string | null;
  monitoringEnabled: boolean;
  ring: GeoCoordinate[];
}

export interface NdviTrendPoint {
  readingId: number;
  acquisitionDate: string;
  ndviMean: number;
  ndviMin: number;
  ndviMax: number;
  cloudCoveragePct: number;
  cloudRejected: boolean;
  /** ReadingId do overlay quando há PNG; null → não desenhar overlay. */
  overlayReadingId?: number | null;
  /** [minLng, minLat, maxLng, maxLat] para posicionar o L.imageOverlay. */
  bbox?: number[] | null;
}

export interface NdviTrendResponse {
  areaId: number;
  points: NdviTrendPoint[];
}
