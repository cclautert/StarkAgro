import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AdminService } from '../../../services/admin.service';
import { Revenda, RevendaBilling } from '../../../models/revenda.model';
import { DiagnosisPlan } from '../../../models/diagnosis-plan.model';

/**
 * Lista de revendas. O cadastro vive em página própria (/admin/revendas/nova),
 * como em /admin/usuarios, /pivots e /sensores.
 */
@Component({
  selector: 'app-admin-revendas',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './admin-revendas.component.html',
  styleUrls: ['./admin-revendas.component.css']
})
export class AdminRevendasComponent implements OnInit {
  private adminService = inject(AdminService);
  private snackBar = inject(MatSnackBar);
  private router = inject(Router);

  revendas: Revenda[] = [];
  plans: DiagnosisPlan[] = [];
  isLoading = true;

  assigningId: number | null = null;
  managerEmail = '';
  isAssigning = false;

  billingForId: number | null = null;
  billing: RevendaBilling | null = null;

  ngOnInit(): void {
    this.adminService.getDiagnosisPlans().subscribe({
      next: plans => this.plans = plans,
      error: () => { /* sem planos: a lista mostra só o id do plano */ }
    });
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.adminService.getRevendas().subscribe({
      next: revendas => { this.revendas = revendas; this.isLoading = false; },
      error: () => {
        this.snackBar.open('Erro ao carregar revendas.', 'Fechar', { duration: 4000 });
        this.isLoading = false;
      }
    });
  }

  planName(id?: number | null): string {
    if (id == null) return 'Sem plano';
    return this.plans.find(p => p.id === id)?.name ?? `Plano #${id}`;
  }

  edit(r: Revenda): void {
    this.router.navigate(['/admin/revendas/editar', r.id]);
  }

  startAssign(r: Revenda): void {
    this.assigningId = this.assigningId === r.id ? null : r.id;
    this.managerEmail = '';
  }

  /** Gestor é identificado pelo e-mail — é o que o admin tem em mãos, não o id interno. */
  assignManager(r: Revenda): void {
    const email = this.managerEmail.trim();
    if (!email) return;

    this.isAssigning = true;
    this.adminService.assignRevendaManager(r.id, email).subscribe({
      next: () => {
        this.snackBar.open(`${email} agora é gestor de ${r.name}.`, 'OK', { duration: 4000 });
        this.isAssigning = false;
        this.assigningId = null;
        this.managerEmail = '';
      },
      error: err => {
        this.isAssigning = false;
        this.snackBar.open(
          err?.error?.errors?.[0] ?? 'Erro ao atribuir gestor.', 'Fechar', { duration: 5000 });
      }
    });
  }

  toggleBilling(r: Revenda): void {
    if (this.billingForId === r.id) { this.billingForId = null; this.billing = null; return; }
    this.billingForId = r.id;
    this.billing = null;
    this.adminService.getRevendaBilling(r.id).subscribe({
      next: b => this.billing = b,
      error: () => this.snackBar.open('Erro ao carregar faturamento.', 'Fechar', { duration: 4000 })
    });
  }
}
