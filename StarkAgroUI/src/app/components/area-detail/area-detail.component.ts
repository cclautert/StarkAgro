import { Component, ElementRef, Inject, OnDestroy, OnInit, PLATFORM_ID, ViewChild } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ChartConfiguration } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { firstValueFrom } from 'rxjs';
import { AreaService } from '../../services/area.service';
import { MonitoredArea, NdviClassShare, NdviTrendPoint, Sentinel1TrendPoint } from '../../models/monitored-area.model';
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

  /** Classes do PNG que está no mapa — é a legenda. Ver renderOverlay() para o porquê. */
  legendClasses: NdviClassShare[] = [];
  /** true quando alguma passagem já veio classificada; senão o painel de composição some. */
  hasClasses = false;

  // Índice exibido no gráfico de tendência. NDVI sempre existe; NDRE/NDMI só aparecem no seletor
  // quando alguma passagem foi buscada com índices extras (senão seriam uma linha reta em zero).
  readonly indices = [
    { key: 'ndvi' as const, label: 'NDVI', hint: 'Vigor geral da vegetação. Satura em dossel denso.' },
    { key: 'ndre' as const, label: 'NDRE', hint: 'Red-edge: clorofila e nitrogênio. Não satura onde o NDVI para.' },
    { key: 'ndmi' as const, label: 'NDMI', hint: 'Umidade do dossel: estresse hídrico antes do sintoma visível.' },
    { key: 'rvi' as const, label: 'RVI (radar)', hint: 'Radar (Sentinel-1): atravessa nuvem. Existe justamente nas datas em que o NDVI tem buraco — não é um "NDVI que funciona na chuva", é outra medida.' }
  ];
  selectedIndex: 'ndvi' | 'ndre' | 'ndmi' | 'rvi' = 'ndvi';

  /** Série de radar (S1) — datas próprias, separada dos pontos de NDVI. */
  radar: Sentinel1TrendPoint[] = [];

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

  // Composição por nível: área empilhada 100%. A média sozinha esconde o que importa — um
  // talhão metade ótimo e metade solo exposto tem a mesma média de um talhão todo medíocre.
  classChartData: ChartConfiguration<'line'>['data'] = { labels: [], datasets: [] };
  classChartOptions: ChartConfiguration<'line'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: 'index', intersect: false },
    scales: {
      y: {
        stacked: true,
        min: 0,
        max: 100,
        title: { display: true, text: '% da área', color: '#a0aec0' },
        ticks: { color: '#a0aec0', callback: v => `${v}%` },
        grid: { color: '#2d3f5e' }
      },
      x: {
        ticks: { maxRotation: 0, autoSkip: true, color: '#a0aec0' },
        grid: { color: '#2d3f5e' }
      }
    },
    plugins: {
      // reverse: a legenda lista Alta primeiro, na mesma ordem em que as faixas aparecem
      // empilhadas no gráfico (Alta no topo).
      legend: { display: true, reverse: true, labels: { color: '#e2e8f0', boxWidth: 12 } },
      tooltip: {
        callbacks: {
          label: ctx => `${ctx.dataset.label}: ${(ctx.parsed.y ?? 0).toFixed(1)}%`
        }
      }
    }
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
        this.radar = trend.radar ?? [];
        this.latest = [...this.points].reverse().find(p => !p.cloudRejected) ?? this.points[this.points.length - 1];
        this.buildChart();
        this.buildClassChart();
        this.renderOverlay();
        this.loading = false;
      },
      error: () => { this.error = true; this.loading = false; }
    });
  }

  get currentIndexHint(): string {
    return this.indices.find(i => i.key === this.selectedIndex)?.hint ?? '';
  }

  /** Troca do índice no seletor: só reconstrói a série de tendência (composição/mapa não mudam). */
  selectIndex(key: 'ndvi' | 'ndre' | 'ndmi' | 'rvi'): void {
    this.selectedIndex = key;
    this.buildChart();
  }

  /** Opção disponível no seletor: NDVI sempre; NDRE/NDMI se houver valor; RVI se houver série S1. */
  hasIndex(key: 'ndvi' | 'ndre' | 'ndmi' | 'rvi'): boolean {
    if (key === 'ndvi') return true;
    if (key === 'rvi') return this.radar.length > 0;
    const pick = key === 'ndre' ? (p: NdviTrendPoint) => p.ndreMean : (p: NdviTrendPoint) => p.ndmiMean;
    return this.points.some(p => !p.cloudRejected && pick(p) !== 0);
  }

  private buildChart(): void {
    const meta = this.indices.find(i => i.key === this.selectedIndex)!;

    // RVI é uma série SEPARADA (radar tem datas próprias, cadência diferente do NDVI).
    if (this.selectedIndex === 'rvi') {
      this.setYAxis(false, meta.label);
      this.chartData = {
        labels: this.radar.map(p => this.shortDate(p.acquisitionDate)),
        datasets: [{
          data: this.radar.map(p => round2(p.rviMean)),
          label: 'RVI (radar)',
          borderColor: '#a78bfa',
          backgroundColor: 'rgba(167,139,250,0.15)',
          fill: true, tension: 0.35, spanGaps: true, pointRadius: 4
        }]
      };
      return;
    }

    const labels = this.points.map(p => this.shortDate(p.acquisitionDate));
    const value = (p: NdviTrendPoint) =>
      this.selectedIndex === 'ndre' ? p.ndreMean : this.selectedIndex === 'ndmi' ? p.ndmiMean : p.ndviMean;
    // Passagem nublada vira buraco honesto na série (null) — não uma queda falsa.
    const series = this.points.map(p => (p.cloudRejected ? null : round2(value(p))));

    // NDVI vive em [0,1]; os demais podem sair disso — só fixa o eixo no NDVI.
    this.setYAxis(this.selectedIndex === 'ndvi', meta.label);

    this.chartData = {
      labels,
      datasets: [
        {
          data: series,
          label: `${meta.label} médio`,
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

  private setYAxis(fixedZeroToOne: boolean, title: string): void {
    if (!this.chartOptions?.scales?.['y']) return;
    const y = this.chartOptions.scales['y'] as Record<string, unknown>;
    y['min'] = fixedZeroToOne ? 0 : undefined;
    y['max'] = fixedZeroToOne ? 1 : undefined;
    (y['title'] as { text: string }).text = title;
  }

  /**
   * Área empilhada 100%: um dataset por classe, na ordem em que o servidor manda (menor
   * biomassa primeiro), então Solo Exposto fica na base e Alta no topo. As cores vêm do
   * payload — nunca hardcoded aqui, senão divergem do PNG.
   */
  private buildClassChart(): void {
    // A referência de classes é a passagem mais recente que tem classificação COM pixel válido:
    // passagens antigas (pré-feature) vêm sem lista, e nubladas podem vir com tudo zerado.
    const reference = [...this.points].reverse().find(p =>
      !p.cloudRejected && p.classes?.length && p.classes.some(c => c.pixelCount > 0))?.classes;
    this.hasClasses = !!reference;
    if (!reference) {
      this.classChartData = { labels: [], datasets: [] };
      return;
    }

    this.classChartData = {
      labels: this.points.map(p => this.shortDate(p.acquisitionDate)),
      datasets: reference.map((cls, i) => ({
        label: cls.label,
        // Passagem sem classificação vira buraco honesto, igual ao gráfico de média — não
        // uma faixa que despenca a zero e sugere que o talhão virou solo exposto.
        // Nublada conta como sem classificação mesmo que venha com as classes zeradas:
        // 0% em todas as faixas desenha um vale que parece perda de vigor e não é.
        data: this.points.map(p => {
          if (p.cloudRejected) return null;
          const share = p.classes?.find(c => c.key === cls.key);
          const total = (p.classes ?? []).reduce((sum, c) => sum + c.pixelCount, 0);
          return share && total > 0 ? round2(share.percent) : null;
        }),
        borderColor: cls.color,
        backgroundColor: cls.color,
        // Empilhado: a primeira faixa preenche até a base, as demais até a faixa de baixo.
        fill: i === 0 ? 'origin' : '-1',
        borderWidth: 1,
        tension: 0.25,
        spanGaps: true,
        pointRadius: 2
      }))
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

    // A passagem com overlay é a que tem GeoTIFF de zonas disponível para download.
    this.zoneReadingId = withOverlay.overlayReadingId ?? undefined;

    try {
      const blob = await firstValueFrom(this.areaService.overlay(this.id, withOverlay.overlayReadingId!));
      this.overlayUrl = URL.createObjectURL(blob);
      // bbox = [minLng, minLat, maxLng, maxLat] → Leaflet quer [[minLat,minLng],[maxLat,maxLng]].
      const [minLng, minLat, maxLng, maxLat] = withOverlay.bbox!;
      const bounds = L.latLngBounds([[minLat, minLng], [maxLat, maxLng]]);
      L.imageOverlay(this.overlayUrl, bounds, { opacity: 0.7 }).addTo(this.map);

      // A legenda descreve ESTE PNG, então sai das classes desta passagem. PNG gravado antes
      // da classificação foi colorido com o ramp antigo e vem sem `classes` — nesse caso a
      // legenda não aparece, em vez de anunciar cortes que a imagem não usou.
      this.legendClasses = [...(withOverlay.classes ?? [])].reverse();
    } catch {
      // Overlay é acessório — sem PNG, o mapa mostra só o contorno.
    }
  }

  // Reading do overlay mais recente — o mesmo que tem GeoTIFF de zonas disponível.
  zoneReadingId?: number;
  downloadingZones = false;
  zonesError = false;

  /** Baixa o GeoTIFF de zonas (gerado sob demanda no servidor) como arquivo. */
  async downloadZones(): Promise<void> {
    if (!this.zoneReadingId || this.downloadingZones) return;
    this.downloadingZones = true;
    this.zonesError = false;
    try {
      const blob = await firstValueFrom(this.areaService.zones(this.id, this.zoneReadingId));
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `zonas-${this.id}-${this.zoneReadingId}.tiff`;
      a.click();
      URL.revokeObjectURL(url);
    } catch {
      this.zonesError = true; // servidor pode devolver 404 se a geração falhar (kill-switch, etc.)
    } finally {
      this.downloadingZones = false;
    }
  }

  /** Nível que ocupa a maior fatia da passagem exibida — a leitura rápida do card. */
  get dominantClass(): NdviClassShare | undefined {
    const classes = this.latest?.classes;
    if (!classes?.length) return undefined;
    return classes.reduce((a, b) => (b.percent > a.percent ? b : a));
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
