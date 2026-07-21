/**
 * Camadas base compartilhadas dos mapas Leaflet (áreas NDVI e pivôs).
 *
 * O padrão é o satélite: em zona rural o mapa de ruas é quase vazio e não dá
 * referência nenhuma para reconhecer o contorno do talhão.
 *
 * O Leaflet é carregado sob demanda em cada componente (`await import('leaflet')`),
 * então estas funções recebem o namespace `L` como parâmetro em vez de importá-lo —
 * importar aqui puxaria o Leaflet para o bundle inicial e quebraria o SSR.
 */

const OSM_URL = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
const OSM_ATTRIBUTION = '&copy; OpenStreetMap contributors';

/** Atenção à ordem `{z}/{y}/{x}` — a Esri inverte y/x em relação ao OSM. */
const ESRI_IMAGERY_URL =
  'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}';
const ESRI_ATTRIBUTION =
  'Tiles &copy; Esri — Source: Esri, Maxar, Earthstar Geographics, and the GIS User Community';

/**
 * Os ícones default do Leaflet apontam para caminhos relativos que quebram sob
 * bundler — daí o redirecionamento explícito para as imagens do CDN.
 */
export function applyDefaultMarkerIcon(L: any): void {
  L.Marker.prototype.options.icon = L.icon({
    iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
    iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
    shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
    iconSize: [25, 41],
    iconAnchor: [12, 41],
    popupAnchor: [1, -34],
    tooltipAnchor: [16, -28],
    shadowSize: [41, 41]
  });
}

/**
 * Adiciona Satélite + Ruas ao mapa e o seletor de camadas.
 *
 * O seletor fica em `topright` porque o geoman (desenho de polígono) ocupa o
 * `topleft`. `maxNativeZoom: 18` no satélite: em área rural a Esri nem sempre tem
 * tile em z19 e o mapa ficaria cinza no zoom máximo — assim o Leaflet só amplia
 * o tile de z18.
 */
export function addBaseLayers(L: any, map: any, initial: 'satellite' | 'streets' = 'satellite'): void {
  const streets = L.tileLayer(OSM_URL, { maxZoom: 19, attribution: OSM_ATTRIBUTION });
  const satellite = L.tileLayer(ESRI_IMAGERY_URL, {
    maxZoom: 19,
    maxNativeZoom: 18,
    attribution: ESRI_ATTRIBUTION
  });

  (initial === 'satellite' ? satellite : streets).addTo(map);

  L.control
    .layers({ 'Satélite': satellite, 'Ruas': streets }, {}, { position: 'topright' })
    .addTo(map);
}
