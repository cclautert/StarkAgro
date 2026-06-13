import { Component, OnInit, OnDestroy, ViewChild } from '@angular/core';
import { ChartConfiguration } from 'chart.js';
import { ApiService } from '../../services/api.service';
import { Subscription, interval } from 'rxjs';
import { BaseChartDirective } from 'ng2-charts';
import { Router, ActivatedRoute } from '@angular/router';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { HttpErrorResponse } from '@angular/common/http';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Sensor } from '../../models/sensor.model';
import { SensorService } from '../../services/sensor.service';
import { TrendAnalysisService, TrendStats, ProjectionPoint } from '../../services/trend-analysis.service';
import { PivotService } from '../../services/pivot.service';
import { PivotForecast } from '../../models/pivot-forecast.model';
import { MoisturePrediction } from '../../models/moisture-prediction.model';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css'],
  standalone: false,
})
export class DashboardComponent implements OnInit, OnDestroy {

  @ViewChild(BaseChartDirective) chart?: BaseChartDirective;

  userId = 1;
  numberOfReads = 7;
  intervalSub!: Subscription;
  private breakpointSub!: Subscription;
  private paramSub!: Subscription;
  private readsSub: Subscription | null = null;
  sidebarOpened = true;
  isMobile = false;
  public pivoId: number | null = null;
  public quadranteNome: string | null = null;
  sensor: Sensor | undefined;
  sensors: Sensor[] | undefined;
  public selectedSensorId: number = 1;
  public quadrante: number | undefined;

  pivotName: string | null = null;
  limiteSuperior: number | null = null;
  limiteInferior: number | null = null;

  // ── Weather forecast state ────────────────────────────────────────────────
  forecast: PivotForecast | null = null;
  forecastLoading = false;
  private forecastSub: Subscription | null = null;

  // ── AI Insights state ─────────────────────────────────────────────────────
  aiInsights: string | null = null;
  aiInsightsLoading = false;
  aiGeneratedAt: Date | null = null;
  aiFromCache = false;
  aiError: string | null = null;
  aiExpanded = false;
  private aiSub: Subscription | null = null;

  // ── Trend-analysis overlay state ──────────────────────────────────────────
  showTrend = true;
  showMA = true;
  showProjection = true;
  trendStats: TrendStats | null = null;
  projectionPoints: ProjectionPoint[] = [];

  // ── Server-side moisture prediction ──────────────────────────────────────
  moisturePrediction: MoisturePrediction | null = null;
  predictionLoading = false;
  predictionError: string | null = null;
  private predictionSub: Subscription | null = null;

  // ── Anomaly source counts (edge vs cloud) ─────────────────────────────
  edgeAnomalyCount = 0;
  cloudAnomalyCount = 0;

  // ── LoRaWAN extra metrics ─────────────────────────────────────────────
  lastTemperature: number | null = null;
  lastBatteryVoltage: number | null = null;

  // Dataset index constants for the toggle handlers
  private readonly DS_TREND    = 4;
  private readonly DS_MA       = 5;
  private readonly DS_PROJ_MIN = 6;
  private readonly DS_PROJ_MAX = 7;
  private readonly DS_PROJ_MID = 8;

  public lineChartData: ChartConfiguration<'line'>['data'] = {
    datasets: [
      // [0] Sensor readings line
      {
        data: [],
        label: 'Leituras',
        borderColor: '#06b6d4',
        backgroundColor: 'transparent',
        fill: false,
        pointRadius: 3,
        tension: 0.3,
        order: 0,
        spanGaps: false
      },
      // [1] Limite Inferior — red zone fill downward
      {
        data: [],
        label: 'Limite Inferior',
        borderColor: '#F44336',
        borderDash: [5, 5],
        borderWidth: 1.5,
        pointRadius: 0,
        fill: 'start',
        backgroundColor: 'rgba(244,67,54,0.2)',
        tension: 0,
        order: 1,
        spanGaps: false
      },
      // [2] Green zone fills up to dataset[1]
      {
        data: [],
        label: '',
        borderWidth: 0,
        pointRadius: 0,
        fill: 1,
        backgroundColor: 'rgba(76,175,80,0.2)',
        tension: 0,
        order: 2,
        spanGaps: false
      },
      // [3] Limite Superior — blue zone fill upward
      {
        data: [],
        label: 'Limite Superior',
        borderColor: '#2196F3',
        borderDash: [5, 5],
        borderWidth: 1.5,
        pointRadius: 0,
        fill: 'end',
        backgroundColor: 'rgba(33,150,243,0.2)',
        tension: 0,
        order: 3,
        spanGaps: false
      },
      // [4] Trend line (linear regression) — yellow dashed
      {
        data: [],
        label: 'Tendencia (regressao linear)',
        borderColor: '#facc15',
        borderDash: [6, 3],
        borderWidth: 1.5,
        pointRadius: 0,
        fill: false,
        tension: 0,
        order: 4,
        spanGaps: false,
        hidden: false
      },
      // [5] Moving average — violet
      {
        data: [],
        label: 'Media Movel (3d)',
        borderColor: '#a78bfa',
        borderWidth: 1.5,
        pointRadius: 0,
        fill: false,
        tension: 0.3,
        order: 5,
        spanGaps: false,
        hidden: false
      },
      // [6] Projection floor (projMin) — transparent, used as fill base
      {
        data: [],
        label: '',
        borderColor: 'transparent',
        borderWidth: 0,
        pointRadius: 0,
        fill: false,
        tension: 0,
        order: 7,
        spanGaps: false,
        hidden: false
      },
      // [7] Projection ceiling (projMax) — fills down to dataset[6]
      {
        data: [],
        label: 'Projecao (faixa)',
        borderColor: 'transparent',
        borderWidth: 0,
        pointRadius: 0,
        fill: '-1',
        backgroundColor: 'rgba(251,146,60,0.18)',
        tension: 0,
        order: 8,
        spanGaps: false,
        hidden: false
      },
      // [8] Projection midline — orange dashed
      {
        data: [],
        label: 'Projecao (central)',
        borderColor: '#fb923c',
        borderDash: [4, 4],
        borderWidth: 2,
        pointRadius: 3,
        fill: false,
        tension: 0,
        order: 6,
        spanGaps: false,
        hidden: false
      }
    ],
    labels: []
  };

  public lineChartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      x: {
        title: { display: true, text: 'Data' },
        ticks: {
          maxRotation: 45,
          minRotation: 45
        }
      },
      y: { title: { display: true, text: '% Umidade' }, min: 0, max: 100 }
    },
    plugins: {
      annotation: {
        annotations: {}
      }
    } as any
  };

  constructor(
    private route: ActivatedRoute,
    private apiService: ApiService,
    private sensorService: SensorService,
    private router: Router,
    private breakpointObserver: BreakpointObserver,
    private trendAnalysisService: TrendAnalysisService,
    private pivotService: PivotService,
    private sanitizer: DomSanitizer
  ) {
    this.breakpointSub = this.breakpointObserver.observe([Breakpoints.Handset]).subscribe(result => {
      this.isMobile = result.matches;
    });
  }

  ngOnInit(): void {
    this.paramSub = this.route.paramMap.subscribe(params => {
      const id = params.get('pivoId');
      this.pivoId = id ? +id : null;
      this.quadranteNome = params.get('quadrante');

      if (!this.pivoId || !this.quadranteNome) return;

      switch (this.quadranteNome) {
        case 'TopLeft':    this.quadrante = 4; break;
        case 'TopRight':   this.quadrante = 1; break;
        case 'BottomLeft': this.quadrante = 3; break;
        case 'BottomRight':this.quadrante = 2; break;
      }

      if (!this.quadrante) return;

      // Capture into local consts so TypeScript narrowing holds inside async callbacks
      const pivoId = this.pivoId;
      const quadrante = this.quadrante;

      this.loadForecast(pivoId);
      this.loadMoisturePrediction(pivoId);

      this.apiService.getReadsByPivotId(pivoId, 1).subscribe(pivot => {
        this.pivotName = pivot.name ?? null;
        this.limiteSuperior = pivot.limiteSuperior ?? null;
        this.limiteInferior = pivot.limiteInferior ?? null;

        this.sensorService.getAllByPivotId(pivoId, quadrante).subscribe((sensors) => {
          this.sensors = sensors;
          if (!sensors || sensors.length === 0) {
            this.clearChart();
            return;
          }
          this.selectedSensorId = sensors[0].id;
          this.loadReads();
        });
      });
    });

    this.intervalSub = interval(60000).subscribe(() => this.loadReads());
  }

  onSensorChange(): void {
    this.loadReads();
  }

  ngOnDestroy(): void {
    this.intervalSub?.unsubscribe();
    this.breakpointSub?.unsubscribe();
    this.paramSub?.unsubscribe();
    this.readsSub?.unsubscribe();
    this.forecastSub?.unsubscribe();
    this.aiSub?.unsubscribe();
    this.predictionSub?.unsubscribe();
  }

  // ── Server-side moisture prediction ──────────────────────────────────────

  private loadMoisturePrediction(pivotId: number): void {
    this.predictionSub?.unsubscribe();
    this.predictionLoading = true;
    this.predictionError = null;
    this.predictionSub = this.pivotService.getMoisturePrediction(pivotId).subscribe({
      next: pred => {
        this.moisturePrediction = pred;
        this.predictionLoading = false;
        // If reads have already loaded, rebuild the chart with server prediction
        if (this.trendStats !== null) {
          this.loadReads();
        }
      },
      error: (err: HttpErrorResponse) => {
        this.moisturePrediction = null;
        this.predictionLoading = false;
        if (err.status === 422 || err.status === 400) {
          this.predictionError = 'Histórico insuficiente para predição (mínimo 24h de leituras).';
        } else if (err.status !== 404) {
          this.predictionError = 'Predição temporariamente indisponível.';
        }
        // Clear any stale annotation
        this.applyCriticalAnnotation(null);
        this.chart?.update();
      }
    });
  }

  /** Hours until critical moisture from now; null when EstimatedCriticalAt is absent. */
  get hoursUntilCritical(): number | null {
    if (!this.moisturePrediction?.estimatedCriticalAt) return null;
    const ms = new Date(this.moisturePrediction.estimatedCriticalAt).getTime() - Date.now();
    return ms > 0 ? Math.round(ms / 3600000) : 0;
  }

  /** Confidence as 0–100 integer percentage. */
  get predictionConfidencePct(): number {
    return Math.round((this.moisturePrediction?.confidence ?? 0) * 100);
  }

  /** True when the pivot has coordinates (derived from forecast response). */
  get predictionHasCoordinates(): boolean {
    return this.forecast?.hasCoordinates ?? false;
  }

  /**
   * Aggregates the 72h hourly prediction into up to 3 daily ProjectionPoints.
   * Returns an empty array when no server prediction is available.
   */
  private buildServerProjectionPoints(): ProjectionPoint[] {
    if (!this.moisturePrediction || !this.moisturePrediction.predictedValues.length) return [];

    const grouped = new Map<number, { mids: number[]; mins: number[]; maxs: number[] }>();
    const values = this.moisturePrediction.predictedValues;

    for (let i = 0; i < values.length; i++) {
      const dayBucket = Math.min(3, Math.ceil((i + 1) / 24));
      if (!grouped.has(dayBucket)) grouped.set(dayBucket, { mids: [], mins: [], maxs: [] });
      const g = grouped.get(dayBucket)!;
      g.mids.push(values[i].predictedMoisture);
      g.mins.push(values[i].confidenceMin);
      g.maxs.push(values[i].confidenceMax);
    }

    const avg = (a: number[]) =>
      parseFloat((a.reduce((s, v) => s + v, 0) / a.length).toFixed(1));

    return [1, 2, 3].map(d => {
      const g = grouped.get(d) ?? { mids: [0], mins: [0], maxs: [0] };
      return {
        date: `+${d}d`,
        projMin: avg(g.mins),
        projMax: avg(g.maxs),
        projMid: avg(g.mids)
      };
    });
  }

  /**
   * Returns the projection label ('+1d', '+2d', '+3d') where EstimatedCriticalAt falls,
   * or null when not applicable.
   */
  private criticalLabel(): string | null {
    if (!this.moisturePrediction?.estimatedCriticalAt) return null;
    const critTime = new Date(this.moisturePrediction.estimatedCriticalAt).getTime();
    const values = this.moisturePrediction.predictedValues;

    for (let i = 0; i < values.length; i++) {
      if (new Date(values[i].date).getTime() >= critTime) {
        const day = Math.min(3, Math.ceil((i + 1) / 24));
        return `+${day}d`;
      }
    }
    return '+1d';
  }

  /** Updates the Chart.js annotation plugin options for the critical vertical line. */
  private applyCriticalAnnotation(label: string | null): void {
    const annotationOpts = label === null
      ? { annotations: {} }
      : {
          annotations: {
            criticalLine: {
              type: 'line',
              scaleID: 'x',
              value: label,
              borderColor: '#ef4444',
              borderWidth: 2,
              borderDash: [6, 4],
              label: {
                display: true,
                content: 'Umidade crítica',
                position: 'start',
                color: '#ef4444',
                backgroundColor: 'rgba(239,68,68,0.08)',
                font: { size: 11, weight: 'bold' }
              }
            }
          }
        };

    // Store in bound options for chart re-initializations
    const boundOpts = this.lineChartOptions as any;
    if (!boundOpts.plugins) boundOpts.plugins = {};
    boundOpts.plugins.annotation = annotationOpts;

    // Also patch the live chart instance so that chart.update() picks up the change
    if (this.chart?.chart) {
      const liveOpts = this.chart.chart.options as any;
      if (!liveOpts.plugins) liveOpts.plugins = {};
      liveOpts.plugins.annotation = annotationOpts;
    }
  }

  // ── AI Insights ───────────────────────────────────────────────────────────

  loadAIInsights(): void {
    if (!this.pivoId) return;
    this.aiSub?.unsubscribe();
    this.aiInsightsLoading = true;
    this.aiError = null;
    this.aiExpanded = true;
    const pivotId = this.pivoId;
    this.aiSub = this.pivotService.getAIInsights(pivotId).subscribe({
      next: res => {
        this.aiInsights = res.insights;
        this.aiGeneratedAt = new Date(res.generatedAt);
        this.aiFromCache = res.fromCache;
        this.aiInsightsLoading = false;
      },
      error: (err: HttpErrorResponse) => {
        if (err.status === 503) {
          this.aiError = 'Serviço de IA temporariamente indisponível. Tente novamente em instantes.';
        } else {
          this.aiError = 'Não foi possível obter os insights. Verifique sua conexão e tente novamente.';
        }
        this.aiInsightsLoading = false;
      }
    });
  }

  toggleAiExpanded(): void {
    this.aiExpanded = !this.aiExpanded;
  }

  get aiInsightsHtml(): SafeHtml | null {
    if (!this.aiInsights) return null;
    return this.sanitizer.bypassSecurityTrustHtml(this.renderMarkdown(this.aiInsights));
  }

  private renderMarkdown(text: string): string {
    const escape = (s: string) =>
      s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

    const inline = (s: string) =>
      escape(s)
        .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
        .replace(/\*(.+?)\*/g, '<em>$1</em>')
        .replace(/`(.+?)`/g, '<code class="ai-code">$1</code>');

    const lines = text.split('\n');
    const out: string[] = [];
    let inList = false;

    for (const raw of lines) {
      const line = raw.trimEnd();

      if (!line) {
        if (inList) { out.push('</ul>'); inList = false; }
        out.push('<br>');
        continue;
      }

      if (line.startsWith('### ')) {
        if (inList) { out.push('</ul>'); inList = false; }
        out.push(`<h5 class="ai-h5">${inline(line.slice(4))}</h5>`);
        continue;
      }
      if (line.startsWith('## ')) {
        if (inList) { out.push('</ul>'); inList = false; }
        out.push(`<h4 class="ai-h4">${inline(line.slice(3))}</h4>`);
        continue;
      }
      if (line.startsWith('# ')) {
        if (inList) { out.push('</ul>'); inList = false; }
        out.push(`<h3 class="ai-h3">${inline(line.slice(2))}</h3>`);
        continue;
      }

      if (/^[-*] /.test(line)) {
        if (!inList) { out.push('<ul class="ai-ul">'); inList = true; }
        out.push(`<li>${inline(line.slice(2))}</li>`);
        continue;
      }

      if (inList) { out.push('</ul>'); inList = false; }
      out.push(`<p class="ai-p">${inline(line)}</p>`);
    }

    if (inList) out.push('</ul>');
    return out.join('');
  }

  // ── Forecast ──────────────────────────────────────────────────────────────

  private loadForecast(pivotId: number): void {
    this.forecastSub?.unsubscribe();
    this.forecastLoading = true;
    this.forecastSub = this.pivotService.getForecast(pivotId, 7).subscribe({
      next: f => {
        this.forecast = f;
        this.forecastLoading = false;
      },
      error: () => {
        this.forecast = null;
        this.forecastLoading = false;
      }
    });
  }

  goToPivotEdit(): void {
    if (this.pivoId !== null) {
      this.router.navigate(['/pivots/editar', this.pivoId]);
    }
  }

  // ── Chart ─────────────────────────────────────────────────────────────────

  loadReads(): void {
    this.readsSub?.unsubscribe();
    this.readsSub = this.apiService.getAllReadsBySensorId(
      this.selectedSensorId,
      this.quadrante!,
      this.numberOfReads
    ).subscribe(reads => {
      if (!reads || reads.length === 0) {
        this.trendStats = null;
        this.projectionPoints = [];
        this.edgeAnomalyCount = 0;
        this.cloudAnomalyCount = 0;
        this.lastTemperature = null;
        this.lastBatteryVoltage = null;
        this.clearChart();
        return;
      }

      const latestWithTemp = reads.find(r => r.temperature != null);
      const latestWithBatt = reads.find(r => r.batteryVoltage != null);
      this.lastTemperature = latestWithTemp?.temperature ?? null;
      this.lastBatteryVoltage = latestWithBatt?.batteryVoltage ?? null;

      const limInf = this.limiteInferior ?? 0;
      const limSup = this.limiteSuperior ?? 100;

      const { points, projection, stats } =
        this.trendAnalysisService.computeDailyData(reads, limInf, limSup);

      this.trendStats = stats;
      this.projectionPoints = projection;

      // Compute edge vs cloud breakdown for anomalous reads
      const anomalousReads = reads.filter(r => r.value < limInf || r.value > limSup);
      this.edgeAnomalyCount = anomalousReads.filter(r => r.isEdgeAnomaly === true).length;
      this.cloudAnomalyCount = anomalousReads.length - this.edgeAnomalyCount;

      // Use server-side prediction when available; fall back to client projection
      const serverProj = this.buildServerProjectionPoints();
      const useServerPred = serverProj.length > 0;
      const projToUse = useServerPred ? serverProj : projection;
      const midLabel  = useServerPred ? 'Previsão 72h'     : 'Projecao (central)';
      const bandLabel = useServerPred ? 'Banda de confiança' : 'Projecao (faixa)';

      const histLabels = points.map(p => p.date);
      const projLabels = projToUse.map(p => p.date);
      const allLabels  = [...histLabels, ...projLabels];
      const histLen    = histLabels.length;
      const totalLen   = allLabels.length;

      const histNulls = new Array<null>(histLen).fill(null);
      const projNulls = new Array<null>(projToUse.length).fill(null);

      this.lineChartData = {
        labels: allLabels,
        datasets: [
          // [DS_SENSOR=0] Sensor daily values — null in projection range
          {
            data: [...points.map(p => p.value), ...projNulls],
            label: 'Leituras',
            borderColor: '#06b6d4',
            backgroundColor: 'transparent',
            fill: false,
            pointRadius: 3,
            tension: 0.3,
            order: 0,
            spanGaps: false
          },
          // [DS_LIMIT_LOW=1] Limite Inferior flat line
          {
            data: new Array(totalLen).fill(limInf),
            label: 'Limite Inferior',
            borderColor: '#F44336',
            borderDash: [5, 5],
            borderWidth: 1.5,
            pointRadius: 0,
            fill: 'start',
            backgroundColor: 'rgba(244,67,54,0.2)',
            tension: 0,
            order: 1,
            spanGaps: false
          },
          // [DS_ZONE_MID=2] Green zone fills up to dataset[1]
          {
            data: new Array(totalLen).fill(limSup),
            label: '',
            borderWidth: 0,
            pointRadius: 0,
            fill: 1,
            backgroundColor: 'rgba(76,175,80,0.2)',
            tension: 0,
            order: 2,
            spanGaps: false
          },
          // [DS_LIMIT_HIGH=3] Limite Superior flat line
          {
            data: new Array(totalLen).fill(limSup),
            label: 'Limite Superior',
            borderColor: '#2196F3',
            borderDash: [5, 5],
            borderWidth: 1.5,
            pointRadius: 0,
            fill: 'end',
            backgroundColor: 'rgba(33,150,243,0.2)',
            tension: 0,
            order: 3,
            spanGaps: false
          },
          // [DS_TREND=4] Linear regression trend line
          {
            data: [...points.map(p => p.trend ?? null), ...projNulls],
            label: 'Tendencia (regressao linear)',
            borderColor: '#facc15',
            borderDash: [6, 3],
            borderWidth: 1.5,
            pointRadius: 0,
            fill: false,
            tension: 0,
            order: 4,
            spanGaps: false,
            hidden: !this.showTrend
          },
          // [DS_MA=5] 3-day moving average
          {
            data: [...points.map(p => p.movingAvg ?? null), ...projNulls],
            label: 'Media Movel (3d)',
            borderColor: '#a78bfa',
            borderWidth: 1.5,
            pointRadius: 0,
            fill: false,
            tension: 0.3,
            order: 5,
            spanGaps: false,
            hidden: !this.showMA
          },
          // [DS_PROJ_MIN=6] Projection/prediction floor
          {
            data: [...histNulls, ...projToUse.map(p => p.projMin)],
            label: '',
            borderColor: 'transparent',
            borderWidth: 0,
            pointRadius: 0,
            fill: false,
            tension: 0,
            order: 7,
            spanGaps: false,
            hidden: !this.showProjection
          },
          // [DS_PROJ_MAX=7] Projection/prediction ceiling — fills down to DS_PROJ_MIN
          {
            data: [...histNulls, ...projToUse.map(p => p.projMax)],
            label: bandLabel,
            borderColor: 'transparent',
            borderWidth: 0,
            pointRadius: 0,
            fill: '-1',
            backgroundColor: useServerPred ? 'rgba(251,146,60,0.25)' : 'rgba(251,146,60,0.18)',
            tension: 0,
            order: 8,
            spanGaps: false,
            hidden: !this.showProjection
          },
          // [DS_PROJ_MID=8] Projection/prediction midline
          {
            data: [...histNulls, ...projToUse.map(p => p.projMid)],
            label: midLabel,
            borderColor: useServerPred ? '#f97316' : '#fb923c',
            borderDash: [4, 4],
            borderWidth: 2,
            pointRadius: 3,
            fill: false,
            tension: 0,
            order: 6,
            spanGaps: false,
            hidden: !this.showProjection
          }
        ]
      };

      // Apply critical-line annotation
      this.applyCriticalAnnotation(useServerPred ? this.criticalLabel() : null);
      this.chart?.update();
    });
  }

  setDays(days: number): void {
    this.numberOfReads = days;
    this.loadReads();
  }

  onToggleTrend(): void {
    this.showTrend = !this.showTrend;
    if (this.lineChartData.datasets[this.DS_TREND]) {
      this.lineChartData.datasets[this.DS_TREND].hidden = !this.showTrend;
      this.chart?.update();
    }
  }

  onToggleMA(): void {
    this.showMA = !this.showMA;
    if (this.lineChartData.datasets[this.DS_MA]) {
      this.lineChartData.datasets[this.DS_MA].hidden = !this.showMA;
      this.chart?.update();
    }
  }

  onToggleProjection(): void {
    this.showProjection = !this.showProjection;
    [this.DS_PROJ_MIN, this.DS_PROJ_MAX, this.DS_PROJ_MID].forEach(idx => {
      if (this.lineChartData.datasets[idx]) {
        this.lineChartData.datasets[idx].hidden = !this.showProjection;
      }
    });
    this.chart?.update();
  }

  /** Returns the trend direction label and colour class based on the slope. */
  get trendDirection(): { label: string; cssClass: string } {
    if (!this.trendStats) return { label: 'Sem dados', cssClass: 'trend-badge-stable' };
    const s = this.trendStats.slope;
    if (Math.abs(s) < 0.3) return { label: 'Estavel', cssClass: 'trend-badge-stable' };
    if (s > 0) return { label: 'Subindo', cssClass: 'trend-badge-rising' };
    return { label: 'Caindo', cssClass: 'trend-badge-falling' };
  }

  /** Absolute value of slope formatted to one decimal. */
  get slopeAbs(): number {
    return this.trendStats ? Math.abs(this.trendStats.slope) : 0;
  }

  /** Left-border colour of the trend direction analysis tile. */
  get trendTileBorderColor(): string {
    if (!this.trendStats) return '#3b82f6';
    const s = this.trendStats.slope;
    if (Math.abs(s) < 0.3) return '#3b82f6';
    return s > 0 ? '#22c55e' : '#ef4444';
  }

  /** Label for the projection toggle — changes with server prediction. */
  get projectionToggleLabel(): string {
    return this.moisturePrediction ? 'Previsão 72h' : 'Projecao';
  }

  goHome(): void {
    this.router.navigate(['/home']);
  }

  goToConfig(): void {
    this.router.navigate(['/dashboard', this.pivoId, this.quadranteNome, 'config']);
  }

  logout(): void {
    localStorage.removeItem('token');
    this.router.navigate(['/login']);
  }

  private clearChart(): void {
    this.lineChartData.datasets.forEach(d => (d.data = []));
    this.lineChartData.labels = [];
    this.applyCriticalAnnotation(null);
    this.chart?.update();
  }
}
