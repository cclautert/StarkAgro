import {
  AfterViewInit,
  Component,
  ElementRef,
  Inject,
  NgZone,
  OnDestroy,
  PLATFORM_ID,
  ViewChild,
  inject
} from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpParams } from '@angular/common/http';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { PivotLocation } from '../../models/pivot.model';

interface NominatimResult {
  lat: string;
  lon: string;
  display_name: string;
}

interface ElevationResponse {
  elevation: number[];
}

interface PivotLocationDialogData {
  initial: Partial<PivotLocation> | null;
}

const DEFAULT_LAT = -29.7;
const DEFAULT_LON = -53.7;
const DEFAULT_ZOOM = 13;

@Component({
  selector: 'app-pivot-location-map',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule],
  templateUrl: './pivot-location-map.component.html',
  styleUrls: ['./pivot-location-map.component.css']
})
export class PivotLocationMapComponent implements AfterViewInit, OnDestroy {

  @ViewChild('mapContainer', { static: false }) mapContainer!: ElementRef<HTMLDivElement>;

  private map: any | null = null;
  private marker: any | null = null;
  private leaflet: any | null = null;

  searchQuery = '';
  searchResults: NominatimResult[] = [];
  searching = false;

  latitude: number | null = null;
  longitude: number | null = null;
  altitude: number | null = null;
  locationAddress: string | null = null;

  loadingAltitude = false;
  loadingAddress = false;
  geolocationError: string | null = null;

  private readonly http = inject(HttpClient);
  private readonly zone = inject(NgZone);
  private readonly dialogRef = inject<MatDialogRef<PivotLocationMapComponent, PivotLocation | null>>(MatDialogRef);
  private readonly data = inject<PivotLocationDialogData>(MAT_DIALOG_DATA, { optional: true }) as PivotLocationDialogData | null;

  constructor(@Inject(PLATFORM_ID) private platformId: object) {}

  async ngAfterViewInit(): Promise<void> {
    if (!isPlatformBrowser(this.platformId)) return;

    // Lazy-load Leaflet only in the browser to keep SSR safe and the bundle small.
    // Leaflet ships as CommonJS, so `await import('leaflet')` returns a namespace
    // wrapper — unwrap `.default` to get the actual API.
    const leafletModule = await import('leaflet');
    const L: any = (leafletModule as any).default ?? leafletModule;
    this.leaflet = L;

    // Default Leaflet marker icons reference relative paths that break under bundlers.
    // Point them explicitly at the CDN-hosted images.
    const iconRetinaUrl = 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png';
    const iconUrl = 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png';
    const shadowUrl = 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png';

    const DefaultIcon = L.icon({
      iconRetinaUrl,
      iconUrl,
      shadowUrl,
      iconSize: [25, 41],
      iconAnchor: [12, 41],
      popupAnchor: [1, -34],
      tooltipAnchor: [16, -28],
      shadowSize: [41, 41]
    });
    (L.Marker.prototype as any).options.icon = DefaultIcon;

    const initial = this.data?.initial ?? null;
    const startLat = initial?.latitude ?? DEFAULT_LAT;
    const startLon = initial?.longitude ?? DEFAULT_LON;
    const hasInitial = initial?.latitude != null && initial?.longitude != null;

    this.map = L.map(this.mapContainer.nativeElement).setView([startLat, startLon], DEFAULT_ZOOM);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '&copy; OpenStreetMap contributors'
    }).addTo(this.map);

    if (hasInitial) {
      this.setMarker(startLat, startLon);
      this.latitude = startLat;
      this.longitude = startLon;
      this.altitude = initial?.altitude ?? null;
      this.locationAddress = initial?.locationAddress ?? null;
    }

    this.map.on('click', (event: any) => {
      const { lat, lng } = event.latlng;
      this.zone.run(() => this.handleLocationSelected(lat, lng));
    });

    // The MatDialog panel may still be sizing when ngAfterViewInit fires;
    // tell Leaflet to recompute once the dialog is fully open so tiles load.
    this.dialogRef.afterOpened().subscribe(() => {
      this.map?.invalidateSize();
    });
    setTimeout(() => this.map?.invalidateSize(), 0);
  }

  private setMarker(lat: number, lon: number): void {
    if (!this.map || !this.leaflet) return;
    const L = this.leaflet;

    if (this.marker) {
      this.marker.setLatLng([lat, lon]);
    } else {
      this.marker = L.marker([lat, lon], { draggable: true }).addTo(this.map);
      this.marker.on('dragend', () => {
        const pos = this.marker.getLatLng();
        this.zone.run(() => this.handleLocationSelected(pos.lat, pos.lng));
      });
    }
  }

  private async handleLocationSelected(lat: number, lon: number): Promise<void> {
    this.latitude = round(lat, 6);
    this.longitude = round(lon, 6);
    this.setMarker(this.latitude, this.longitude);

    await Promise.all([
      this.fetchAltitude(this.latitude, this.longitude),
      this.fetchAddress(this.latitude, this.longitude)
    ]);
  }

  private async fetchAltitude(lat: number, lon: number): Promise<void> {
    this.loadingAltitude = true;
    try {
      const params = new HttpParams()
        .set('latitude', lat.toString())
        .set('longitude', lon.toString());
      const resp = await firstValueFrom(
        this.http.get<ElevationResponse>('https://api.open-meteo.com/v1/elevation', { params })
      );
      this.altitude = resp?.elevation?.length ? round(resp.elevation[0], 1) : null;
    } catch {
      this.altitude = null;
    } finally {
      this.loadingAltitude = false;
    }
  }

  private async fetchAddress(lat: number, lon: number): Promise<void> {
    this.loadingAddress = true;
    try {
      const params = new HttpParams()
        .set('format', 'json')
        .set('lat', lat.toString())
        .set('lon', lon.toString());
      const resp = await firstValueFrom(
        this.http.get<NominatimResult>('https://nominatim.openstreetmap.org/reverse', { params })
      );
      this.locationAddress = resp?.display_name ?? null;
    } catch {
      this.locationAddress = null;
    } finally {
      this.loadingAddress = false;
    }
  }

  async runSearch(): Promise<void> {
    const term = this.searchQuery.trim();
    if (!term) {
      this.searchResults = [];
      return;
    }
    this.searching = true;
    try {
      const params = new HttpParams()
        .set('q', term)
        .set('format', 'json')
        .set('limit', '5');
      const results = await firstValueFrom(
        this.http.get<NominatimResult[]>('https://nominatim.openstreetmap.org/search', { params })
      );
      this.searchResults = results ?? [];
    } catch {
      this.searchResults = [];
    } finally {
      this.searching = false;
    }
  }

  selectSearchResult(result: NominatimResult): void {
    const lat = parseFloat(result.lat);
    const lon = parseFloat(result.lon);
    if (Number.isNaN(lat) || Number.isNaN(lon)) return;

    this.searchResults = [];
    this.searchQuery = result.display_name;
    if (this.map) this.map.setView([lat, lon], DEFAULT_ZOOM);
    this.handleLocationSelected(lat, lon);
  }

  useMyLocation(): void {
    if (!isPlatformBrowser(this.platformId) || !navigator.geolocation) {
      this.geolocationError = 'Geolocalização não suportada neste navegador.';
      return;
    }
    this.geolocationError = null;
    navigator.geolocation.getCurrentPosition(
      pos => {
        this.zone.run(() => {
          const { latitude, longitude } = pos.coords;
          if (this.map) this.map.setView([latitude, longitude], DEFAULT_ZOOM);
          this.handleLocationSelected(latitude, longitude);
        });
      },
      err => {
        this.zone.run(() => {
          this.geolocationError = `Não foi possível obter sua localização: ${err.message}`;
        });
      }
    );
  }

  confirm(): void {
    if (this.latitude == null || this.longitude == null) return;
    const result: PivotLocation = {
      latitude: this.latitude,
      longitude: this.longitude,
      altitude: this.altitude,
      locationAddress: this.locationAddress
    };
    this.dialogRef.close(result);
  }

  cancel(): void {
    this.dialogRef.close(null);
  }

  ngOnDestroy(): void {
    if (this.map) {
      this.map.remove();
      this.map = null;
    }
    this.marker = null;
    this.leaflet = null;
  }
}

function round(value: number, decimals: number): number {
  const f = Math.pow(10, decimals);
  return Math.round(value * f) / f;
}
