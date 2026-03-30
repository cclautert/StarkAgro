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

  public lineChartData: ChartConfiguration<'line'>['data'] = {
    datasets: [
      {
        data: [],
        label: 'Sensor Values',
        borderColor: 'blue',
        backgroundColor: 'rgba(0,0,255,0.1)',
        fill: true,
        pointRadius: 3,
        tension: 0.3
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

      this.sensorService.getAllByPivotId(this.pivoId!, this.quadrante!).subscribe((sensors) => {
        this.sensors = sensors;
        this.selectedSensorId = sensors?.length > 0 ? sensors[0].id : 1;
        this.loadReads();
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
      this.lineChartData.labels = reads.map(r => new Date(r.date).toLocaleString());
      this.lineChartData.datasets[0].data = reads.map(r => r.value);
      this.chart?.update();
    });
  }

  setDays(days: number): void {
    this.numberOfReads = days;
    this.loadReads();
  }

  logout(): void {
    // Aqui você pode limpar o token, se houver
    localStorage.removeItem('token');  // opcional

    // Redirecionar para a rota de login
    this.router.navigate(['/login']);
  }
}
