import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AdminService } from '../../../services/admin.service';
import { DiagnosisPlan } from '../../../models/diagnosis-plan.model';

@Component({
  selector: 'app-admin-plans',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './admin-plans.component.html',
  styleUrls: ['./admin-plans.component.css']
})
export class AdminPlansComponent implements OnInit {
  private fb = inject(FormBuilder);
  private adminService = inject(AdminService);
  private snackBar = inject(MatSnackBar);

  plans: DiagnosisPlan[] = [];
  isLoading = true;
  isSaving = false;
  editingId: number | null = null;

  form!: FormGroup;

  ngOnInit(): void {
    // Preços digitados em reais; o backend guarda em centavos inteiros (dinheiro sem float).
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(80)]],
      monthlyPrice: [0, [Validators.required, Validators.min(0)]],
      includedReportsPerMonth: [0, [Validators.required, Validators.min(0)]],
      overagePrice: [0, [Validators.required, Validators.min(0)]],
      active: [true]
    });
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.adminService.getDiagnosisPlans().subscribe({
      next: (plans) => { this.plans = plans; this.isLoading = false; },
      error: () => {
        this.snackBar.open('Erro ao carregar planos.', 'Fechar', { duration: 4000 });
        this.isLoading = false;
      }
    });
  }

  startEdit(plan: DiagnosisPlan): void {
    this.editingId = plan.id;
    this.form.setValue({
      name: plan.name,
      monthlyPrice: plan.monthlyPriceCents / 100,
      includedReportsPerMonth: plan.includedReportsPerMonth,
      overagePrice: plan.overagePriceCents / 100,
      active: plan.active
    });
  }

  cancelEdit(): void {
    this.editingId = null;
    this.form.reset({ name: '', monthlyPrice: 0, includedReportsPerMonth: 0, overagePrice: 0, active: true });
  }

  onSubmit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.isSaving = true;

    const v = this.form.value;
    const payload: Partial<DiagnosisPlan> = {
      name: v.name,
      // Math.round evita o clássico 99.99 * 100 = 9998.999...
      monthlyPriceCents: Math.round(Number(v.monthlyPrice) * 100),
      includedReportsPerMonth: Number(v.includedReportsPerMonth) || 0,
      overagePriceCents: Math.round(Number(v.overagePrice) * 100),
      active: !!v.active
    };

    const req = this.editingId
      ? this.adminService.updateDiagnosisPlan(this.editingId, payload)
      : this.adminService.createDiagnosisPlan(payload);

    req.subscribe({
      next: () => {
        this.snackBar.open('Plano salvo.', 'OK', { duration: 3000 });
        this.isSaving = false;
        this.cancelEdit();
        this.load();
      },
      error: () => {
        this.snackBar.open('Erro ao salvar plano.', 'Fechar', { duration: 4000 });
        this.isSaving = false;
      }
    });
  }

  remove(plan: DiagnosisPlan): void {
    this.adminService.deleteDiagnosisPlan(plan.id).subscribe({
      next: () => { this.snackBar.open('Plano removido.', 'OK', { duration: 3000 }); this.load(); },
      error: () => this.snackBar.open(
        'Não foi possível remover — o plano pode estar em uso. Desative-o.', 'Fechar', { duration: 5000 })
    });
  }
}
