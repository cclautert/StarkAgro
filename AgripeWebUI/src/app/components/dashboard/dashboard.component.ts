import { Component, OnInit, OnDestroy, ViewChild } from '@angular/core';
import { ChartConfiguration } from 'chart.js';
import { ApiService } from '../../services/api.service';
import { Subscription, interval } from 'rxjs';
import { BaseChartDirective } from 'ng2-charts';
import { Router } from '@angular/router';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
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
      x: { title: { display: true, text: 'Data' } },
      y: { title: { display: true, text: 'Valor' } }
    }
  };

  constructor(private apiService: ApiService, private router: Router, private breakpointObserver: BreakpointObserver) {
    this.breakpointObserver.observe([Breakpoints.Handset]).subscribe(result => {
      this.isMobile = result.matches;
    });
  }

  ngOnInit(): void {
    this.loadReads();
    this.intervalSub = interval(60000).subscribe(() => this.loadReads()); // A cada 60 segundos
  }

  ngOnDestroy(): void {
    this.intervalSub?.unsubscribe();
  }

  loadReads(): void {
    this.apiService.getReads(this.userId, this.numberOfReads).subscribe(reads => {
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
