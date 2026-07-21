import { AfterViewInit, Component, ElementRef, Inject, NgZone, OnDestroy, OnInit, PLATFORM_ID, ViewChild, inject } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { AreaService } from '../../services/area.service';
import { AreaRequest, GeoCoordinate } from '../../models/monitored-area.model';
import { PivotLocationMapComponent } from '../pivot-location-map/pivot-location-map.component';
import { PivotLocation } from '../../models/pivot.model';
import { addBaseLayers, applyDefaultMarkerIcon } from '../../utils/leaflet-basemaps';

@Component({
  selector: 'app-area-form',
  templateUrl: './area-form.component.html',
  styleUrls: ['./area-form.component.css'],
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatSnackBarModule, MatDialogModule]
})
export class AreaFormComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('polygonMap', { static: false }) polygonMap!: ElementRef<HTMLDivElement>;

  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private areaService = inject(AreaService);
  private snackBar = inject(MatSnackBar);
  private dialog = inject(MatDialog);
  private zone = inject(NgZone);

  form: FormGroup;
  areaId: number | null = null;
  isEditMode = false;
  saving = false;
  geolocating = false;

  /** Anel do polígono desenhado (ou carregado na edição). */
  polygonRing: GeoCoordinate[] = [];

  // Leaflet + geoman (carregados sob demanda no browser).
  private map: any | null = null;
  private leaflet: any | null = null;
  private drawnLayer: any | null = null;
  private gpsMarker: any | null = null;
  private mapReady: Promise<void> | null = null;
  private viewReady = false;

  constructor(@Inject(PLATFORM_ID) private platformId: object) {
    this.form = this.fb.group({
      name: ['', Validators.required],
      crop: [null as string | null],
      areaKind: ['Circle' as 'Circle' | 'Polygon'],
      centerLat: [null as number | null, [Validators.min(-90), Validators.max(90)]],
      centerLng: [null as number | null, [Validators.min(-180), Validators.max(180)]],
      radiusM: [250 as number | null, [Validators.min(10), Validators.max(20000)]],
      altitude: [null as number | null],
      locationAddress: [null as string | null],
      monitoringEnabled: [true]
    });
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEditMode = true;
      this.areaId = +idParam;
      this.areaService.get(this.areaId).subscribe(area => {
        this.polygonRing = area.ring ?? [];
        this.form.patchValue({
          name: area.name,
          crop: area.crop ?? null,
          areaKind: area.areaKind,
          centerLat: area.centerLat ?? null,
          centerLng: area.centerLng ?? null,
          radiusM: area.radiusM ?? 250,
          altitude: area.altitude ?? null,
          locationAddress: area.locationAddress ?? null,
          monitoringEnabled: area.monitoringEnabled
        });
        if (area.areaKind === 'Polygon') this.ensurePolygonMap();
      });
    }
  }

  ngAfterViewInit(): void {
    this.viewReady = true;
    if (this.isPolygon) this.ensurePolygonMap();
  }

  get areaKind(): 'Circle' | 'Polygon' { return this.form.get('areaKind')!.value; }
  get isPolygon(): boolean { return this.areaKind === 'Polygon'; }
  get centerLat(): number | null { return this.form.get('centerLat')!.value; }
  get centerLng(): number | null { return this.form.get('centerLng')!.value; }
  get altitude(): number | null { return this.form.get('altitude')!.value; }
  get locationAddress(): string | null { return this.form.get('locationAddress')!.value; }
  get hasCenter(): boolean { return this.centerLat !== null && this.centerLng !== null; }
  get polygonVertexCount(): number { return this.polygonRing.length; }

  onKindChange(): void {
    if (this.isPolygon) this.ensurePolygonMap();
  }

  async openLocationMap(): Promise<void> {
    const initial: Partial<PivotLocation> | null = this.hasCenter
      ? { latitude: this.centerLat!, longitude: this.centerLng!, altitude: this.altitude, locationAddress: this.locationAddress }
      : null;

    const dialogRef = this.dialog.open<PivotLocationMapComponent, { initial: Partial<PivotLocation> | null }, PivotLocation | null>(
      PivotLocationMapComponent,
      { data: { initial }, panelClass: 'pivot-location-map-panel', autoFocus: false, maxWidth: '90vw' }
    );

    const result = await firstValueFrom(dialogRef.afterClosed());
    if (result) {
      this.form.patchValue({
        centerLat: result.latitude,
        centerLng: result.longitude,
        altitude: result.altitude,
        locationAddress: result.locationAddress
      });
      this.form.markAsDirty();
    }
  }

  /**
   * Centraliza o mapa de desenho onde o GPS do aparelho aponta — sem isto o
   * produtor cai no centro default (RS) e precisa navegar até o talhão na mão.
   */
  async useMyLocation(): Promise<void> {
    if (!isPlatformBrowser(this.platformId) || !navigator.geolocation) {
      this.snackBar.open('Geolocalização não suportada neste navegador.', 'Fechar', { duration: 4000 });
      return;
    }

    await this.ensurePolygonMap();
    if (!this.map) return;

    this.geolocating = true;
    navigator.geolocation.getCurrentPosition(
      pos => this.zone.run(() => {
        this.geolocating = false;
        const { latitude, longitude } = pos.coords;
        this.map.setView([latitude, longitude], GPS_ZOOM);
        if (this.gpsMarker) this.map.removeLayer(this.gpsMarker);
        this.gpsMarker = this.leaflet
          .circleMarker([latitude, longitude], { radius: 6, color: '#1565c0', fillColor: '#1565c0', fillOpacity: 0.9 })
          .addTo(this.map)
          .bindTooltip('Você está aqui');
      }),
      err => this.zone.run(() => {
        this.geolocating = false;
        this.snackBar.open(`Não foi possível obter sua localização: ${err.message}`, 'Fechar', { duration: 5000 });
      }),
      { enableHighAccuracy: true, timeout: 15000 }
    );
  }

  /** Inicia (ou revalida) o mapa de desenho de polígono. Idempotente e aguardável. */
  private ensurePolygonMap(): Promise<void> {
    if (!isPlatformBrowser(this.platformId) || !this.viewReady) return Promise.resolve();
    if (this.mapReady) {
      setTimeout(() => this.map?.invalidateSize(), 0);
      return this.mapReady;
    }
    this.mapReady = this.initPolygonMap();
    return this.mapReady;
  }

  private async initPolygonMap(): Promise<void> {
    const leafletModule = await import('leaflet');
    const L: any = (leafletModule as any).default ?? leafletModule;
    // Geoman aumenta os protótipos do mesmo módulo Leaflet (draw/edit/remove de polígono).
    await import('@geoman-io/leaflet-geoman-free');
    this.leaflet = L;

    applyDefaultMarkerIcon(L);

    const hasRing = this.polygonRing.length >= 3;
    const start: [number, number] = hasRing
      ? [this.polygonRing[0].lat, this.polygonRing[0].lng]
      : [-29.7, -53.7];

    this.map = L.map(this.polygonMap.nativeElement).setView(start, 13);
    addBaseLayers(L, this.map);

    this.map.pm.addControls({
      position: 'topleft',
      drawPolygon: true,
      editMode: true,
      dragMode: false,
      removalMode: true,
      drawMarker: false,
      drawCircle: false,
      drawCircleMarker: false,
      drawPolyline: false,
      drawRectangle: false,
      drawText: false,
      cutPolygon: false,
      rotateMode: false
    });
    this.map.pm.setLang('pt_br');

    // Desenho novo: só um polígono por área — o anterior é descartado.
    this.map.on('pm:create', (e: any) => {
      if (this.drawnLayer && this.drawnLayer !== e.layer) this.map.removeLayer(this.drawnLayer);
      this.drawnLayer = e.layer;
      this.captureRing();
      e.layer.on('pm:edit', () => this.captureRing());
    });

    if (hasRing) {
      const latlngs = this.polygonRing.map(c => [c.lat, c.lng]) as [number, number][];
      this.drawnLayer = L.polygon(latlngs, { color: '#2e7d32' }).addTo(this.map);
      this.drawnLayer.on('pm:edit', () => this.captureRing());
      this.map.fitBounds(L.latLngBounds(latlngs), { padding: [20, 20] });
    }

    setTimeout(() => this.map?.invalidateSize(), 0);
  }

  private captureRing(): void {
    if (!this.drawnLayer) return;
    const latlngs = this.drawnLayer.getLatLngs();
    const outer = Array.isArray(latlngs[0]) ? latlngs[0] : latlngs; // polígono → [ [LatLng...] ]
    this.polygonRing = outer.map((p: any) => ({ lat: round(p.lat, 6), lng: round(p.lng, 6) }));
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.value;
    let ring: GeoCoordinate[];

    if (this.isPolygon) {
      if (this.polygonRing.length < 3) {
        this.snackBar.open('Desenhe o polígono no mapa (mínimo 3 vértices).', 'Fechar', { duration: 4000 });
        return;
      }
      ring = this.polygonRing;
    } else {
      if (v.centerLat == null || v.centerLng == null || !v.radiusM) {
        this.snackBar.open('Defina o centro no mapa e o raio da área.', 'Fechar', { duration: 4000 });
        return;
      }
      ring = circleToRing(v.centerLat, v.centerLng, v.radiusM);
    }

    const body: AreaRequest = {
      name: v.name.trim(),
      crop: v.crop?.trim() || null,
      areaKind: v.areaKind,
      centerLat: this.isPolygon ? null : v.centerLat,
      centerLng: this.isPolygon ? null : v.centerLng,
      radiusM: this.isPolygon ? null : v.radiusM,
      altitude: v.altitude,
      locationAddress: v.locationAddress,
      monitoringEnabled: v.monitoringEnabled,
      ring
    };

    this.saving = true;
    const op = this.isEditMode
      ? this.areaService.update(this.areaId!, body)
      : this.areaService.create(body);

    op.subscribe({
      next: () => {
        this.saving = false;
        this.snackBar.open(this.isEditMode ? 'Área atualizada!' : 'Área criada!', 'OK', { duration: 3000 });
        this.router.navigate(['/areas']);
      },
      error: () => {
        this.saving = false;
        this.snackBar.open('Não foi possível salvar a área.', 'Fechar', { duration: 4000 });
      }
    });
  }

  cancel(): void {
    this.router.navigate(['/areas']);
  }

  ngOnDestroy(): void {
    if (this.map) { this.map.remove(); this.map = null; }
    this.leaflet = null;
    this.drawnLayer = null;
    this.gpsMarker = null;
  }
}

/** Zoom ao centralizar pelo GPS — perto o bastante para o talhão caber na tela. */
const GPS_ZOOM = 16;

/**
 * Aproxima um círculo (centro + raio em metros) a um anel de N vértices, em lat/lng.
 * O servidor fecha o anel e converte para [lng,lat]. Fonte clássica de bug: manter a
 * correção de longitude por cos(lat).
 */
export function circleToRing(lat: number, lng: number, radiusM: number, segments = 48): GeoCoordinate[] {
  const latRad = (lat * Math.PI) / 180;
  const dLat = radiusM / 111320;
  const dLng = radiusM / (111320 * Math.cos(latRad));
  const ring: GeoCoordinate[] = [];
  for (let i = 0; i < segments; i++) {
    const theta = (i / segments) * 2 * Math.PI;
    ring.push({
      lat: round(lat + dLat * Math.sin(theta), 6),
      lng: round(lng + dLng * Math.cos(theta), 6)
    });
  }
  return ring;
}

function round(value: number, decimals: number): number {
  const f = Math.pow(10, decimals);
  return Math.round(value * f) / f;
}
