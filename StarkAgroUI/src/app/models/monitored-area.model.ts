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

/**
 * Fatia da área num nível de biomassa. Rótulo, cor e faixa vêm do servidor de propósito: a
 * tabela de cores mora só em NdviClassification.cs, que também colore o PNG do overlay —
 * duplicá-la aqui faria a legenda divergir do mapa no primeiro ajuste de threshold.
 */
export interface NdviClassShare {
  key: string;
  label: string;
  color: string;
  minNdvi: number;
  maxNdvi: number;
  pixelCount: number;
  /** Percentual da área válida da passagem (0-100). */
  percent: number;
}

export interface NdviTrendPoint {
  readingId: number;
  acquisitionDate: string;
  ndviMean: number;
  ndviMin: number;
  ndviMax: number;
  /** Média de NDRE/NDMI. 0 em passagem buscada sem índices extras — o front esconde a série
   *  quando NENHUM ponto tem valor, para não desenhar uma linha reta em zero como se fosse dado. */
  ndreMean: number;
  ndmiMean: number;
  cloudCoveragePct: number;
  cloudRejected: boolean;
  /** Vazio em passagem nublada ou anterior à classificação — a tela esconde o painel. */
  classes?: NdviClassShare[];
  /** ReadingId do overlay quando há PNG; null → não desenhar overlay. */
  overlayReadingId?: number | null;
  /** [minLng, minLat, maxLng, maxLat] para posicionar o L.imageOverlay. */
  bbox?: number[] | null;
}

export interface NdviTrendResponse {
  areaId: number;
  points: NdviTrendPoint[];
}
