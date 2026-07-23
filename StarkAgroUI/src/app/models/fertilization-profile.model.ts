// Perfil de adubação: dose de NPK por classe de biomassa, por cultura.
// classKey casa com as classes do NDVI (Solo Exposto … Alta).

export interface ZoneDose {
  classKey: string;
  nitrogenKgHa: number;
  phosphorusKgHa: number;
  potassiumKgHa: number;
}

export interface FertilizationProfile {
  id: number;
  culture: string;
  doses: ZoneDose[];
}

/** As 6 classes de biomassa, na mesma ordem/rótulo do NdviClassification do backend. */
export const NDVI_CLASSES: { key: string; label: string }[] = [
  { key: 'BareSoil', label: 'Solo Exposto' },
  { key: 'Low', label: 'Baixa' },
  { key: 'MediumLow', label: 'Média-Baixa' },
  { key: 'Medium', label: 'Média' },
  { key: 'MediumHigh', label: 'Média-Alta' },
  { key: 'High', label: 'Alta' }
];
