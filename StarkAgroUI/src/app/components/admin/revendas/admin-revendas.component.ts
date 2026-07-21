import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AdminService } from '../../../services/admin.service';
import { Revenda, RevendaBilling } from '../../../models/revenda.model';
import { DiagnosisPlan } from '../../../models/diagnosis-plan.model';

@Component({
  selector: 'app-admin-revendas',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  templateUrl: './admin-revendas.component.html',
  styleUrls: ['./admin-revendas.component.css']
})
export class AdminRevendasComponent implements OnInit {
  private fb = inject(FormBuilder);
  private adminService = inject(AdminService);
  private snackBar = inject(MatSnackBar);

  revendas: Revenda[] = [];
  plans: DiagnosisPlan[] = [];
  isLoading = true;
  isSaving = false;
  editingId: number | null = null;

  assigningId: number | null = null;
  managerUserId: number | null = null;

  billingForId: number | null = null;
  billing: RevendaBilling | null = null;

  form!: FormGroup;

  ngOnInit(): void {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(120)]],
      cnpj: [''],
      contactEmail: [''],
      diagnosisPlanId: [null],
      diagnosisQuotaPerMonth: [null],
      active: [true]
    });
    this.adminService.getDiagnosisPlans().subscribe({
      next: (plans) => this.plans = plans,
      error: () => { /* sem planos: o seletor fica vazio */ }
    });
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.adminService.getRevendas().subscribe({
      next: (revendas) => { this.revendas = revendas; this.isLoading = false; },
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

  startEdit(r: Revenda): void {
    this.editingId = r.id;
    this.form.setValue({
      name: r.name,
      cnpj: r.cnpj ?? '',
      contactEmail: r.contactEmail ?? '',
      diagnosisPlanId: r.diagnosisPlanId ?? null,
      diagnosisQuotaPerMonth: r.diagnosisQuotaPerMonth ?? null,
      active: r.active
    });
  }

  cancelEdit(): void {
    this.editingId = null;
    this.form.reset({ name: '', cnpj: '', contactEmail: '', diagnosisPlanId: null, diagnosisQuotaPerMonth: null, active: true });
  }

  onSubmit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.isSaving = true;

    const v = this.form.value;
    const payload: Partial<Revenda> = {
      name: v.name,
      cnpj: v.cnpj || null,
      contactEmail: v.contactEmail || null,
      diagnosisPlanId: v.diagnosisPlanId != null && v.diagnosisPlanId !== '' ? Number(v.diagnosisPlanId) : null,
      diagnosisQuotaPerMonth: v.diagnosisQuotaPerMonth != null && v.diagnosisQuotaPerMonth !== '' ? Number(v.diagnosisQuotaPerMonth) : null,
      active: !!v.active
    };

    const req = this.editingId
      ? this.adminService.updateRevenda(this.editingId, payload)
      : this.adminService.createRevenda(payload);

    req.subscribe({
      next: () => {
        this.snackBar.open('Revenda salva.', 'OK', { duration: 3000 });
        this.isSaving = false;
        this.cancelEdit();
        this.load();
      },
      error: (err) => {
        this.snackBar.open(err?.error?.errors?.[0] ?? 'Erro ao salvar revenda.', 'Fechar', { duration: 4000 });
        this.isSaving = false;
      }
    });
  }

  startAssign(r: Revenda): void {
    this.assigningId = this.assigningId === r.id ? null : r.id;
    this.managerUserId = null;
  }

  assignManager(r: Revenda): void {
    if (!this.managerUserId) return;
    this.adminService.assignRevendaManager(r.id, Number(this.managerUserId)).subscribe({
      next: () => {
        this.snackBar.open('Gestor atribuído.', 'OK', { duration: 3000 });
        this.assigningId = null;
        this.managerUserId = null;
      },
      error: (err) => this.snackBar.open(err?.error?.errors?.[0] ?? 'Erro ao atribuir gestor.', 'Fechar', { duration: 4000 })
    });
  }

  toggleBilling(r: Revenda): void {
    if (this.billingForId === r.id) { this.billingForId = null; this.billing = null; return; }
    this.billingForId = r.id;
    this.billing = null;
    this.adminService.getRevendaBilling(r.id).subscribe({
      next: (b) => this.billing = b,
      error: () => this.snackBar.open('Erro ao carregar faturamento.', 'Fechar', { duration: 4000 })
    });
  }
}
