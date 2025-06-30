import { Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { Pivot } from '../../models/pivot.model';
import { PivotService } from '../../services/pivot.service';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-pivot-list',
  templateUrl: './pivot-list.component.html',
  styleUrl: './pivot-list.component.css',
  standalone: true,
  imports: [CommonModule, RouterModule]
})
export class PivotListComponent implements OnInit {
  pivots$!: Observable<Pivot[]>;
  constructor(private pivotService: PivotService, private router: Router) { }

  ngOnInit(): void {
    this.loadPivots();
  }

  loadPivots(): void {
    this.pivots$ = this.pivotService.getPivots();
  }

  updatePivot(id: number): void {
    this.router.navigate(['/pivots/editar', id]);
  }

  deletePivot(id: number): void {
    if (confirm('Tem certeza que deseja excluir este item?')) {
      this.pivotService.deletePivot(id).subscribe(() => {
        // Recarrega a lista para refletir a exclusão
        this.loadPivots();
      });
    }
  }
}
