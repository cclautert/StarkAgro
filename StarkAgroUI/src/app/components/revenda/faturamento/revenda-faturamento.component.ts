import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RevendaService } from '../../../services/revenda.service';
import { RevendaBilling } from '../../../models/revenda.model';

@Component({
  selector: 'app-revenda-faturamento',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './revenda-faturamento.component.html',
  styleUrls: ['./revenda-faturamento.component.css']
})
export class RevendaFaturamentoComponent implements OnInit {
  private revendaService = inject(RevendaService);

  billing: RevendaBilling | null = null;
  isLoading = true;
  hasError = false;

  ngOnInit(): void {
    this.revendaService.getBilling().subscribe({
      next: (b) => { this.billing = b; this.isLoading = false; },
      error: () => { this.hasError = true; this.isLoading = false; }
    });
  }
}
