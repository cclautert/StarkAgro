import { Component, ElementRef, Inject, OnDestroy, OnInit, PLATFORM_ID, ViewChild } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ChartConfiguration } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { firstValueFrom } from 'rxjs';
import { AreaService } from '../../services/area.service';
import { MonitoredArea, NdviTrendPoint } from '../../models/monitored-area.model';
import { addBaseLayers, applyDefaultMarkerIcon } from '../../utils/leaflet-basemaps';

@Component({
  selector: 'app-area-detail',
  templateUrl: './area-detail.component.html',
  styleUrls: ['./area-detail.component.css'],
  standalone: true,
  imports: [CommonModule, RouterModule, BaseChartDirective]
})
export class AreaDetailComponent implements OnInit, OnDestroy {
  // O #mapContainer vive dentro do *ngIf="area && !error": quando o ngAfterViewInit roda, a
  // área ainda está vindo por HTTP e o div não existe — o ViewChild vinha undefined e o
  // L.map() estourava dentro de um método async, virando unhandled rejection silenciosa (mapa
  // em branco, sem nem os controles de zoom). O setter dispara no instante em que o div entra
  // no DOM. O #polygonMap do area-form não sofre disso porque usa [hidden], não *ngIf.
  private mapContainer?: ElementRef<HTMLDivElement>;

  @ViewChild('mapContainer')
  set mapContainerRef(ref: ElementRef<HTMLDivElement> | undefined) {
    this.mapContainer = ref;
    if (ref) void this.ensureMap();
  }

  id!: number;
  area?: MonitoredArea;
  points: NdviTrendPoint[] = [];
  latest?: NdviTrendPoint;
  loading = true;
  error = false;

  // Leaflet (carregado sob demanda no browser).
  private map: any | null = null;
  private leaflet: any | null = null;
  private mapReady = false;
  private mapInit: Promise<void> | null = null;
  private overlayUrl?: string;

  chartData: ChartConfiguration<'line'>['data'] = { labels: [], datasets: [] };
  // O padrão do Chart.js é texto #666 e grade quase preta — some sobre o painel escuro (#1a2540).
  chartOptions: ChartConfiguration<'line'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      y: {
        min: 0,
        max: 1,
        title: { display: true, text: 'NDVI', color: '#a0aec0' },
        ticks: { color: '#a0aec0' },
        grid: { color: '#2d3f5e' }
      },
      x: {
        ticks: { maxRotation: 0, autoSkip: true, color: '#a0aec0' },
        grid: { color: '#2d3f5e' }
      }
    },
    plugins: { legend: { display: true, labels: { color: '#e2e8f0' } } }
  };

  constructor(
    private route: ActivatedRoute,
    private areaService: AreaService,
    @Inject(PLATFORM_ID) private platformId: object
  ) { }

  ngOnInit(): void {
    this.id = Number(this.route.snapshot.paramMap.get('id'));
    this.load();
  }

  private load(): void {
    this.loading = true;
    this.error = false;

    this.areaService.get(this.id).subscribe({
      next: area => {
        this.area = area;
        this.renderGeometry();
      },
      error: () => { this.error = true; this.loading = false; }
    });

    this.areaService.trend(this.id).subscribe({
      next: trend => {
        this.points = trend.points ?? [];
        this.latest = [...this.points].reverse().find(p => !p.cloudRejected) ?? this.points[this.points.length - 1];
        this.buildChart();
        this.renderOverlay();
        this.loading = false;
      },
      error: () => { this.error = true; this.loading = false; }
    });
  }

  private buildChart(): void {
    const labels = this.points.map(p => this.shortDate(p.acquisitionDate));
    // Passagem nublada vira buraco honesto na série (null) — não uma queda de NDVI falsa.
    const mean = this.points.map(p => (p.cloudRejected ? null : round2(p.ndviMean)));

    this.chartData = {
      labels,
      datasets: [
        {
          data: mean,
          label: 'NDVI médio',
          borderColor: '#4ade80',
          backgroundColor: 'rgba(74,222,128,0.15)',
          fill: true,
          tension: 0.35,
          spanGaps: true,
          pointRadius: 4
        }
      ]
    };
  }

  /** Cria o mapa assim que o container existe no DOM. Idempotente. */
  private ensureMap(): Promise<void> {
    if (!isPlatformBrowser(this.platformId) || !this.mapContainer) return Promise.resolve();
    if (!this.mapInit) this.mapInit = this.initMap();
    return this.mapInit;
  }

  private async initMap(): Promise<void> {
    const leafletModule = await import('leaflet');
    const L: any = (leafletModule as any).default ?? leafletModule;
    this.leaflet = L;

    applyDefaultMarkerIcon(L);

    this.map = L.map(this.mapContainer!.nativeElement).setView([-29.7, -53.7], 13);
    addBaseLayers(L, this.map);

    this.mapReady = true;
    setTimeout(() => this.map?.invalidateSize(), 0);
    // A área e a tendência podem ter chegado antes do mapa existir — nesse caso os dois
    // renders abaixo bateram no guard de mapReady e não desenharam nada. Refaz agora.
    this.renderGeometry();
    this.renderOverlay();
  }

  /** Desenha o contorno do talhão a partir do anel (lat/lng) e ajusta o zoom. */
  private renderGeometry(): void {
    if (!this.mapReady || !this.area || !this.leaflet) return;
    const L = this.leaflet;
    const ring = (this.area.ring ?? []).map(c => [c.lat, c.lng]) as [number, number][];
    if (ring.length < 3) return;

    L.polygon(ring, { color: '#2e7d32', weight: 2, fillOpacity: 0.05 }).addTo(this.map);
    this.map.fitBounds(L.latLngBounds(ring), { padding: [20, 20] });
  }

  /** Sobrepõe o PNG NDVI da passagem mais nova que tem overlay, alinhado ao bbox. */
  private async renderOverlay(): Promise<void> {
    if (!this.mapReady || !this.leaflet || !this.points.length) return;
    const L = this.leaflet;

    const withOverlay = [...this.points].reverse().find(p => p.overlayReadingId != null && p.bbox && p.bbox.length === 4);
    if (!withOverlay) return;

    try {
      const blob = await firstValueFrom(this.areaService.overlay(this.id, withOverlay.overlayReadingId!));
      this.overlayUrl = URL.createObjectURL(blob);
      // bbox = [minLng, minLat, maxLng, maxLat] → Leaflet quer [[minLat,minLng],[maxLat,maxLng]].
      const [minLng, minLat, maxLng, maxLat] = withOverlay.bbox!;
      const bounds = L.latLngBounds([[minLat, minLng], [maxLat, maxLng]]);
      L.imageOverlay(this.overlayUrl, bounds, { opacity: 0.7 }).addTo(this.map);
    } catch {
      // Overlay é acessório — sem PNG, o mapa mostra só o contorno.
    }
  }

  shortDate(iso: string): string {
    const d = new Date(iso);
    return isNaN(d.getTime()) ? iso : d.toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' });
  }

  ndvi(value: number): string { return round2(value).toFixed(2); }

  ngOnDestroy(): void {
    if (this.map) { this.map.remove(); this.map = null; }
    this.leaflet = null;
    this.mapInit = null;
    this.mapReady = false;
    if (this.overlayUrl) URL.revokeObjectURL(this.overlayUrl);
  }
}

function round2(v: number): number { return Math.round(v * 100) / 100; }
