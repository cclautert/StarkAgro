import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { forkJoin } from 'rxjs';
import { Sensor } from '../../models/sensor.model';
import { SensorTelemetry } from '../../models/sensor-telemetry.model';
import { SensorService } from '../../services/sensor.service';
import { ApiService } from '../../services/api.service';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatIconModule } from '@angular/material/icon';

interface DeviceGroup {
  deviceEui: string;
  quadrante: number;
  pivotName: string;
  pivotId: number;
  hSensor: Sensor | null;
  tSensor: Sensor | null;
  bSensor: Sensor | null;
  telemetry: SensorTelemetry | null;
}

const LORA_SUFFIX = /_[HTBhtb]$/;

@Component({
  selector: 'app-sensor-list',
  standalone: true,
  imports: [CommonModule, RouterModule, MatSnackBarModule, MatIconModule],
  templateUrl: './sensor-list.component.html',
  styleUrl: './sensor-list.component.css'
})
export class SensorListComponent implements OnInit {
  private sensorService = inject(SensorService);
  private apiService = inject(ApiService);
  private router = inject(Router);
  private snackBar = inject(MatSnackBar);

  isLoading = true;
  devices: DeviceGroup[] = [];
  legacySensors: Sensor[] = [];

  ngOnInit(): void {
    this.carregarSensores();
  }

  carregarSensores(): void {
    this.isLoading = true;
    this.sensorService.getSensores().subscribe({
      next: sensors => this.buildView(sensors),
      error: () => { this.isLoading = false; }
    });
  }

  private buildView(sensors: Sensor[]): void {
    const loraSensors = sensors.filter(s => LORA_SUFFIX.test(s.code ?? ''));
    this.legacySensors = sensors.filter(s => !LORA_SUFFIX.test(s.code ?? ''));

    const groupMap = new Map<string, Sensor[]>();
    for (const s of loraSensors) {
      const key = s.code.slice(0, -2).toUpperCase();
      if (!groupMap.has(key)) groupMap.set(key, []);
      groupMap.get(key)!.push(s);
    }

    this.devices = [...groupMap.entries()].map(([devEui, list]) => {
      const first = list[0];
      return {
        deviceEui: devEui,
        quadrante: first.quadrante,
        pivotName: first.pivot?.name ?? '',
        pivotId: first.pivot?.id ?? 0,
        hSensor: list.find(s => /[_][Hh]$/.test(s.code)) ?? null,
        tSensor: list.find(s => /[_][Tt]$/.test(s.code)) ?? null,
        bSensor: list.find(s => /[_][Bb]$/.test(s.code)) ?? null,
        telemetry: null
      };
    });

    const uniquePivotIds = [...new Set(this.devices.map(d => d.pivotId))].filter(id => id > 0);
    if (uniquePivotIds.length === 0) {
      this.isLoading = false;
      return;
    }

    forkJoin(uniquePivotIds.map(pid => this.apiService.getSensorTelemetry(pid))).subscribe({
      next: results => {
        const telemetryMap = new Map<string, SensorTelemetry>();
        results.forEach((telemetries, idx) => {
          const pivotId = uniquePivotIds[idx];
          telemetries.forEach(t => telemetryMap.set(`${pivotId}_${t.quadrante}`, t));
        });
        this.devices.forEach(d => {
          d.telemetry = telemetryMap.get(`${d.pivotId}_${d.quadrante}`) ?? null;
        });
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; }
    });
  }

  editarSensor(id: number): void {
    this.router.navigate(['/sensores/editar', id]);
  }

  excluirSensor(id: number): void {
    if (confirm('Tem certeza que deseja excluir este sensor?')) {
      this.sensorService.deleteSensor(id).subscribe({
        next: () => {
          this.snackBar.open('Sensor excluído com sucesso!', 'OK', { duration: 3000 });
          this.carregarSensores();
        },
        error: (err) => console.error('Erro ao excluir sensor', err)
      });
    }
  }

  batteryIcon(pct: number | null): string {
    if (pct === null) return 'battery_unknown';
    if (pct >= 95) return 'battery_full';
    if (pct >= 75) return 'battery_6_bar';
    if (pct >= 55) return 'battery_4_bar';
    if (pct >= 35) return 'battery_3_bar';
    if (pct >= 15) return 'battery_1_bar';
    return 'battery_alert';
  }
}
