import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { Observable } from 'rxjs';
import { Sensor } from '../../models/sensor.model';
import { SensorService } from '../../services/sensor.service';

@Component({
  selector: 'app-sensor-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './sensor-list.component.html',
  styleUrl: './sensor-list.component.css'
})
export class SensorListComponent implements OnInit {
  private sensorService = inject(SensorService);
  private router = inject(Router);

  // Usamos o pipe 'async' no template para gerenciar a inscrição
  sensores$!: Observable<Sensor[]>;

  ngOnInit(): void {
    this.carregarSensores();
  }

  carregarSensores(): void {
    this.sensores$ = this.sensorService.getSensores();
  }

  editarSensor(id: number): void {
    this.router.navigate(['/sensores/editar', id]);
  }

  excluirSensor(id: number): void {
    if (confirm('Tem certeza que deseja excluir este sensor?')) {
      this.sensorService.deleteSensor(id).subscribe({
        next: () => {
          alert('Sensor excluído com sucesso!');
          // Recarrega a lista para refletir a exclusão
          this.carregarSensores();
        },
        error: (err) => console.error('Erro ao excluir sensor', err)
      });
    }
  }
}
