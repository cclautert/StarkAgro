import { Component, OnInit, OnDestroy, ViewChild } from '@angular/core';
import { ChartConfiguration } from 'chart.js';
import { ApiService } from '../../services/api.service';
import { Subscription, interval } from 'rxjs';
import { BaseChartDirective } from 'ng2-charts';
import { Router, ActivatedRoute } from '@angular/router';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Sensor } from '../../models/sensor.model';
import { SensorService } from '../../services/sensor.service';
@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css'],
  standalone: false,
})
export class DashboardComponent implements OnInit, OnDestroy {

  @ViewChild(BaseChartDirective) chart?: BaseChartDirective;

  userId = 1;
  numberOfReads = 15;
  intervalSub!: Subscription;
  sidebarOpened = true;
  isMobile = false;
  public pivoId: number | null = null;
  public quadranteNome: string | null = null;
  sensor: Sensor | undefined;
  sensors: Sensor[] | undefined;
  public selectedSensorId: number = 1;
  public quadrante: number | undefined;// 0 | 1 | 2 | 3 | 4 = 0;

  limiteSuperior: number | null = null;
  limiteInferior: number | null = null;

  public lineChartData: ChartConfiguration<'line'>['data'] = {
    datasets: [
      // [0] Linha da leitura do sensor
      {
        data: [],
        label: 'Sensor Values',
        borderColor: 'rgba(0,0,0,0.8)',
        backgroundColor: 'transparent',
        fill: false,
        pointRadius: 3,
        tension: 0.3,
        order: 0
      },
      // [1] Limite Inferior — zona VERMELHA abaixo
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
        order: 1
      },
      // [2] Zona VERDE entre limites (invisible border, fills to dataset[1])
      {
        data: [],
        label: '',
        borderWidth: 0,
        pointRadius: 0,
        fill: 1,
        backgroundColor: 'rgba(76,175,80,0.2)',
        tension: 0,
        order: 2
      },
      // [3] Limite Superior — zona AZUL acima
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
        order: 3
      }
    ],
    labels: []
  };

  public lineChartOptions: ChartConfiguration['options'] = {
    responsive: true,
    scales: {
      x: {
        title: { display: true, text: 'Data' },
        ticks: {
          callback: function(value, index, ticks) {
            const label = this.getLabelForValue(value as number);
            const date = new Date(label);
            return `${date.getDate().toString().padStart(2, '0')}/${(date.getMonth() + 1).toString().padStart(2, '0')} ${date.getHours().toString().padStart(2, '0')}:${(date.getMinutes()).toString().padStart(2, '0')}`;
          },
          maxRotation: 45,
          minRotation: 45
        }
      },
      y: { title: { display: true, text: 'Valor' } }
    }
  };

  constructor(
    private route: ActivatedRoute,
     private apiService: ApiService,
     private sensorService: SensorService,
     private router: Router,
     private breakpointObserver: BreakpointObserver) {
    this.breakpointObserver.observe([Breakpoints.Handset]).subscribe(result => {
      this.isMobile = result.matches;
    });
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const id = params.get('pivoId');
      this.pivoId = id ? +id : null;
      this.quadranteNome = params.get('quadrante');

      switch (this.quadranteNome) {
        case 'TopLeft':    this.quadrante = 4; break;
        case 'TopRight':   this.quadrante = 1; break;
        case 'BottomLeft': this.quadrante = 3; break;
        case 'BottomRight':this.quadrante = 2; break;
      }

      this.apiService.getReadsByPivotId(this.pivoId!, 1).subscribe(pivot => {
        this.limiteSuperior = pivot.limiteSuperior ?? null;
        this.limiteInferior = pivot.limiteInferior ?? null;

        this.sensorService.getAllByPivotId(this.pivoId!, this.quadrante!).subscribe((sensors) => {
          this.sensors = sensors;
          this.selectedSensorId = sensors?.length > 0 ? sensors[0].id : 1;
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
  }

  loadReads(): void {
    this.apiService.getAllReadsBySensorId(this.selectedSensorId, this.quadrante!, this.numberOfReads).subscribe(reads => {
      const n = reads.length;
      this.lineChartData.labels = reads.map(r => new Date(r.date).toLocaleString());
      this.lineChartData.datasets[0].data = reads.map(r => r.value);
      this.lineChartData.datasets[1].data = new Array(n).fill(this.limiteInferior);
      this.lineChartData.datasets[2].data = new Array(n).fill(this.limiteSuperior);
      this.lineChartData.datasets[3].data = new Array(n).fill(this.limiteSuperior);
      this.chart?.update();
    });
  }

  setDays(days: number): void {
    this.numberOfReads = days;
    this.loadReads();
  }

  goToConfig(): void {
    this.router.navigate(['/dashboard', this.pivoId, this.quadranteNome, 'config']);
  }

  logout(): void {
    // Aqui você pode limpar o token, se houver
    localStorage.removeItem('token');  // opcional

    // Redirecionar para a rota de login
    this.router.navigate(['/login']);
  }
}
