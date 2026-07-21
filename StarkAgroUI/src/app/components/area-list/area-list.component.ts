import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { AreaService } from '../../services/area.service';
import { MonitoredArea } from '../../models/monitored-area.model';

@Component({
  selector: 'app-area-list',
  templateUrl: './area-list.component.html',
  styleUrls: ['./area-list.component.css'],
  standalone: true,
  imports: [CommonModule, RouterModule]
})
export class AreaListComponent implements OnInit {
  areas: MonitoredArea[] = [];
  loading = true;
  error = false;

  constructor(private areaService: AreaService, private router: Router) { }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = false;
    this.areaService.list().subscribe({
      next: areas => { this.areas = areas; this.loading = false; },
      error: () => { this.error = true; this.loading = false; }
    });
  }

  detail(id: number): void {
    this.router.navigate(['/areas', id]);
  }

  edit(id: number): void {
    this.router.navigate(['/areas/editar', id]);
  }

  remove(area: MonitoredArea): void {
    if (!confirm(`Excluir a área "${area.name}"? As leituras de NDVI serão perdidas.`)) return;
    this.areaService.delete(area.id).subscribe({
      next: () => this.load(),
      error: () => this.load()
    });
  }

  kindLabel(kind: string): string {
    return kind === 'Circle' ? 'Círculo' : 'Polígono';
  }
}
