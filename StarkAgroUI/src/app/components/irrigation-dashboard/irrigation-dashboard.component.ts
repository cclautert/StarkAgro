import { Component, OnInit, OnDestroy, ViewChild, HostListener } from '@angular/core';
import { ChartConfiguration } from 'chart.js';
import { forkJoin, interval, of, Subscription } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { BaseChartDirective } from 'ng2-charts';
import { Pivot } from '../../models/pivot.model';
import { ReadEntry } from '../../models/quadrante.model';
import { IrrigationTrend } from '../../models/irrigation-trend.model';
import { Anomaly } from '../../models/anomaly.model';
import { SensorTelemetry } from '../../models/sensor-telemetry.model';
import { ApiService } from '../../services/api.service';
import { PivotService } from '../../services/pivot.service';

interface QuadrantInfo {
  label: string;
  value: number | null;
  temperature: number | null;
  batteryPercent: number | null;
  batteryIcon: string;
  statusLabel: string;
  badgeClass: string;
  valueClass: string;
}

interface AlertInfo {
  title: string;
  type: 'alert-low' | 'alert-high';
}

@Component({
  selector: 'app-irrigation-dashboard',
  templateUrl: './irrigation-dashboard.component.html',
  styleUrls: ['./irrigation-dashboard.component.css'],
  standalone: false,
})
export class IrrigationDashboardComponent implements OnInit, OnDestroy {

  @ViewChild(BaseChartDirective) chart?: BaseChartDirective;

  pivots: Pivot[] = [];
  selectedPivotId: number = 0;
  numberOfDays: number = 7;
  quadrants: QuadrantInfo[] = [];
  alerts: AlertInfo[] = [];
  irrigationTrend: IrrigationTrend | null = null;
  anomalyCount: number = 0;
  anomalies: Anomaly[] = [];
  anomalyModalOpen: boolean = false;
  chartFullscreen: boolean = false;
  private intervalSub!: Subscription;

  toggleChartFullscreen(): void {
    this.chartFullscreen = !this.chartFullscreen;
    setTimeout(() => this.chart?.chart?.resize(), 50);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.chartFullscreen) this.chartFullscreen = false;
  }

  public chartData: ChartConfiguration<'line'>['data'] = {
    datasets: [],
    labels: []
  };

  public chartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      x: {
        ticks: { color: '#a0aec0' },
        grid: { color: 'rgba(255,255,255,0.08)' }
      },
      y: {
        suggestedMin: 0,
        suggestedMax: 100,
        ticks: { color: '#a0aec0', stepSize: 10 },
        grid: { color: 'rgba(255,255,255,0.08)' }
      }
    },
    plugins: {
      legend: {
        labels: { color: '#e2e8f0', usePointStyle: true }
      }
    }
  };

  private readonly QUADRANT_COLORS = ['#f97316', '#22c55e', '#3b82f6', '#ef4444'];
  private readonly QUADRANT_LABELS = ['Quadrante 1', 'Quadrante 2', 'Quadrante 3', 'Quadrante 4'];

  constructor(
    private pivotService: PivotService,
    private apiService: ApiService
  ) {}

  ngOnInit(): void {
    this.pivotService.getPivots().subscribe(pivots => {
      this.pivots = pivots;
      if (pivots.length > 0) {
        this.selectedPivotId = pivots[0].id;
        this.loadDashboard();
      }
    });

    this.intervalSub = interval(30000).subscribe(() => this.loadDashboard());
  }

  ngOnDestroy(): void {
    this.intervalSub?.unsubscribe();
  }

  get selectedPivotName(): string {
    return this.pivots.find(p => p.id === this.selectedPivotId)?.name ?? '';
  }

  onPivotChange(): void {
    this.loadDashboard();
  }

  setDays(days: number): void {
    this.numberOfDays = days;
    this.loadDashboard();
  }

  openAnomalyModal(): void {
    this.anomalyModalOpen = true;
  }

  closeAnomalyModal(): void {
    this.anomalyModalOpen = false;
  }

  loadDashboard(): void {
    this.apiService.getIrrigationTrend(this.selectedPivotId, this.numberOfDays).subscribe({
      next: trend => this.irrigationTrend = trend,
      error: () => this.irrigationTrend = null
    });

    this.pivotService.getAnomalies(this.selectedPivotId).subscribe({
      next: anomalies => {
        this.anomalies = anomalies.filter(a => !a.acknowledged);
        this.anomalyCount = this.anomalies.length;
      },
      error: () => {
        this.anomalies = [];
        this.anomalyCount = 0;
      }
    });

    forkJoin([
      this.apiService.getReadsByPivotId(this.selectedPivotId, this.numberOfDays),
      this.apiService.getSensorTelemetry(this.selectedPivotId).pipe(catchError(() => of([] as SensorTelemetry[])))
    ]).subscribe({
      next: ([pivot, telemetry]) => {
        const q = pivot.quadrante;
        const limInf = pivot.limiteInferior ?? 25;
        const limSup = pivot.limiteSuperior ?? 75;

        const telemetryMap = new Map<number, SensorTelemetry>(telemetry.map(t => [t.quadrante, t]));

        const rawValues = [
          q?.topRightAvg ?? null,    // Quadrante 1
          q?.bottomRightAvg ?? null, // Quadrante 2
          q?.bottomLeftAvg ?? null,  // Quadrante 3
          q?.topLeftAvg ?? null      // Quadrante 4
        ];

        this.quadrants = rawValues.map((val, i) => {
          const quadNum = i + 1;
          const t = telemetryMap.get(quadNum);
          return this.buildQuadrantInfo(
            this.QUADRANT_LABELS[i], val,
            t?.temperature ?? null,
            t?.batteryPercent ?? null,
            limInf, limSup
          );
        });
        this.alerts = this.buildAlerts(limInf, limSup);

        const readGroups: ReadEntry[][] = [
          q?.topRightReads ?? [],    // Quadrante 1
          q?.bottomRightReads ?? [], // Quadrante 2
          q?.bottomLeftReads ?? [],  // Quadrante 3
          q?.topLeftReads ?? []      // Quadrante 4
        ];
        this.buildChart(readGroups, limInf, limSup);
      }
    });
  }

  private buildQuadrantInfo(
    label: string,
    value: number | null,
    temperature: number | null,
    batteryPercent: number | null,
    limInf: number, limSup: number
  ): QuadrantInfo {
    const batteryIcon = this.batteryIconFromPercent(batteryPercent);
    if (value === null) {
      return { label, value: null, temperature, batteryPercent, batteryIcon, statusLabel: 'Sem dados', badgeClass: 'badge-gray', valueClass: 'value-gray' };
    }
    if (value < limInf) {
      return { label, value, temperature, batteryPercent, batteryIcon, statusLabel: 'Alerta: Umidade Baixa!', badgeClass: 'badge-orange', valueClass: 'value-orange' };
    }
    const midPoint = (limInf + limSup) / 2;
    if (value < midPoint) {
      return { label, value, temperature, batteryPercent, batteryIcon, statusLabel: 'Normal', badgeClass: 'badge-green', valueClass: 'value-green' };
    }
    if (value <= limSup) {
      return { label, value, temperature, batteryPercent, batteryIcon, statusLabel: 'Ótimo', badgeClass: 'badge-blue', valueClass: 'value-blue' };
    }
    return { label, value, temperature, batteryPercent, batteryIcon, statusLabel: 'Alerta: Umidade Alta!', badgeClass: 'badge-red', valueClass: 'value-red' };
  }

  private batteryIconFromPercent(pct: number | null): string {
    if (pct === null) return 'battery_unknown';
    if (pct >= 95) return 'battery_full';
    if (pct >= 75) return 'battery_6_bar';
    if (pct >= 55) return 'battery_4_bar';
    if (pct >= 35) return 'battery_3_bar';
    if (pct >= 15) return 'battery_1_bar';
    return 'battery_alert';
  }

  private buildAlerts(limInf: number, limSup: number): AlertInfo[] {
    const alerts: AlertInfo[] = [];
    this.quadrants.forEach((q) => {
      if (q.value !== null && q.value < limInf) {
        alerts.push({ title: `Umidade Baixa no ${q.label}!`, type: 'alert-low' });
      } else if (q.value !== null && q.value > limSup) {
        alerts.push({ title: `Umidade Alta no ${q.label}!`, type: 'alert-high' });
      }
    });
    return alerts;
  }

  private buildChart(readGroups: ReadEntry[][], limInf: number, limSup: number): void {
    const daySet = new Set<string>();
    readGroups.forEach(reads => {
      reads.forEach(r => daySet.add(this.toDayLabel(new Date(r.date))));
    });
    const labels = Array.from(daySet).sort((a, b) => {
      const [da, ma] = a.split('/').map(Number);
      const [db, mb] = b.split('/').map(Number);
      return ma !== mb ? ma - mb : da - db;
    });

    const quadrantDatasets = readGroups.map((reads, i) => {
      const byDay = new Map<string, number[]>();
      reads.forEach(r => {
        const key = this.toDayLabel(new Date(r.date));
        if (!byDay.has(key)) byDay.set(key, []);
        byDay.get(key)!.push(Number(r.value));
      });
      const data = labels.map(lbl => {
        const vals = byDay.get(lbl);
        return vals && vals.length > 0 ? vals[vals.length - 1] : null;
      });
      return {
        data,
        label: this.QUADRANT_LABELS[i],
        borderColor: this.QUADRANT_COLORS[i],
        backgroundColor: 'transparent',
        fill: false,
        tension: 0.4,
        pointRadius: 4,
        borderWidth: 2,
        spanGaps: true
      };
    });

    const upperRef = {
      data: labels.map(() => limSup),
      label: `Limite Superior ${limSup}%`,
      borderColor: '#2196F3',
      backgroundColor: 'transparent',
      borderDash: [6, 4],
      fill: false,
      pointRadius: 0,
      borderWidth: 2,
      tension: 0
    };
    const lowerRef = {
      data: labels.map(() => limInf),
      label: `Limite Inferior ${limInf}%`,
      borderColor: '#F44336',
      backgroundColor: 'transparent',
      borderDash: [6, 4],
      fill: false,
      pointRadius: 0,
      borderWidth: 2,
      tension: 0
    };

    this.chartData = {
      labels,
      datasets: [...quadrantDatasets, upperRef, lowerRef] as ChartConfiguration<'line'>['data']['datasets']
    };
    this.chart?.update();
  }

  private toDayLabel(date: Date): string {
    return `${date.getDate().toString().padStart(2, '0')}/${(date.getMonth() + 1).toString().padStart(2, '0')}`;
  }
}
