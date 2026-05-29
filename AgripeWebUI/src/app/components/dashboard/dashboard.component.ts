import { Component, OnInit, OnDestroy, ViewChild } from '@angular/core';
import { ChartConfiguration } from 'chart.js';
import { ApiService } from '../../services/api.service';
import { Subscription, interval } from 'rxjs';
import { BaseChartDirective } from 'ng2-charts';
import { Router, ActivatedRoute } from '@angular/router';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Sensor } from '../../models/sensor.model';
import { SensorService } from '../../services/sensor.service';
import { TrendAnalysisService, TrendStats, ProjectionPoint } from '../../services/trend-analysis.service';
import { PivotService } from '../../services/pivot.service';
import { PivotForecast } from '../../models/pivot-forecast.model';

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
    }
  };

  constructor(
    private route: ActivatedRoute,
    private apiService: ApiService,
    private sensorService: SensorService,
    private router: Router,
    private breakpointObserver: BreakpointObserver,
    private trendAnalysisService: TrendAnalysisService,
    private pivotService: PivotService
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
  }

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
      error: () => {
        this.aiError = 'Assistente IA indisponível. Tente novamente em alguns minutos.';
        this.aiInsightsLoading = false;
      }
    });
  }

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
        this.clearChart();
        return;
      }

      // Compute trend analysis — requires limits to be set (they are, as this runs inside the
      // getReadsByPivotId callback chain).
      const limInf = this.limiteInferior ?? 0;
      const limSup = this.limiteSuperior ?? 100;

      const { points, projection, stats } =
        this.trendAnalysisService.computeDailyData(reads, limInf, limSup);

      this.trendStats = stats;
      this.projectionPoints = projection;

      // Build unified label array: historical day labels + projection labels
      const histLabels = points.map(p => p.date);
      const projLabels = projection.map(p => p.date);
      const allLabels = [...histLabels, ...projLabels];
      const histLen = histLabels.length;
      const totalLen = allLabels.length;

      // Null padding helpers
      const histNulls = new Array<null>(histLen).fill(null);
      const projNulls = new Array<null>(projection.length).fill(null);

      // Completely rebuild the datasets array on each load (avoids index-mutation bugs)
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
          // [DS_LIMIT_LOW=1] Limite Inferior flat line (full range for zone fills)
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
          // [DS_TREND=4] Linear regression trend line — null in projection range
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
          // [DS_MA=5] 3-day moving average — null in projection range
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
          // [DS_PROJ_MIN=6] Projection floor — null in historical range
          {
            data: [...histNulls, ...projection.map(p => p.projMin)],
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
          // [DS_PROJ_MAX=7] Projection ceiling fills down to DS_PROJ_MIN
          {
            data: [...histNulls, ...projection.map(p => p.projMax)],
            label: 'Projecao (faixa)',
            borderColor: 'transparent',
            borderWidth: 0,
            pointRadius: 0,
            fill: '-1',
            backgroundColor: 'rgba(251,146,60,0.18)',
            tension: 0,
            order: 8,
            spanGaps: false,
            hidden: !this.showProjection
          },
          // [DS_PROJ_MID=8] Projection central dashed line — null in historical range
          {
            data: [...histNulls, ...projection.map(p => p.projMid)],
            label: 'Projecao (central)',
            borderColor: '#fb923c',
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

  /** Absolute value of slope formatted to one decimal — used in the "Caindo" badge label. */
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
    this.chart?.update();
  }
}
