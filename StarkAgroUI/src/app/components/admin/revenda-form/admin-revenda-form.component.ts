import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AdminService } from '../../../services/admin.service';
import { Revenda } from '../../../models/revenda.model';
import { DiagnosisPlan } from '../../../models/diagnosis-plan.model';

/**
 * Cadastro/edição de revenda em página própria — mesmo formato de /admin/usuarios,
 * /pivots e /sensores. Antes o formulário ficava aberto no topo da lista.
 */
@Component({
  selector: 'app-admin-revenda-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './admin-revenda-form.component.html',
  styleUrls: ['./admin-revenda-form.component.css']
})
export class AdminRevendaFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private adminService = inject(AdminService);
  private snackBar = inject(MatSnackBar);

  form!: FormGroup;
  plans: DiagnosisPlan[] = [];
  revendaId: number | null = null;
  isEditMode = false;
  isLoading = false;
  isSaving = false;

  get isNew(): boolean { return !this.isEditMode; }

  ngOnInit(): void {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(120)]],
      cnpj: [''],
      contactEmail: ['', [Validators.email]],
      diagnosisPlanId: [null],
      diagnosisQuotaPerMonth: [null],
      active: [true]
    });

    this.adminService.getDiagnosisPlans().subscribe({
      next: plans => this.plans = plans,
      error: () => { /* sem planos: o seletor fica só com "Sem plano" */ }
    });

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEditMode = true;
      this.revendaId = +id;
      this.load(this.revendaId);
    }
  }

  private load(id: number): void {
    this.isLoading = true;
    this.adminService.getRevendas().subscribe({
      next: revendas => {
        const r = revendas.find(x => x.id === id);
        this.isLoading = false;
        if (!r) {
          this.snackBar.open('Revenda não encontrada.', 'Fechar', { duration: 4000 });
          this.router.navigate(['/admin/revendas']);
          return;
        }
        this.form.patchValue({
          name: r.name,
          cnpj: r.cnpj ?? '',
          contactEmail: r.contactEmail ?? '',
          diagnosisPlanId: r.diagnosisPlanId ?? null,
          diagnosisQuotaPerMonth: r.diagnosisQuotaPerMonth ?? null,
          active: r.active
        });
      },
      error: () => {
        this.isLoading = false;
        this.snackBar.open('Erro ao carregar a revenda.', 'Fechar', { duration: 4000 });
      }
    });
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

    const req = this.isEditMode
      ? this.adminService.updateRevenda(this.revendaId!, payload)
      : this.adminService.createRevenda(payload);

    req.subscribe({
      next: () => {
        this.snackBar.open('Revenda salva.', 'OK', { duration: 3000 });
        this.router.navigate(['/admin/revendas']);
      },
      error: err => {
        this.snackBar.open(err?.error?.errors?.[0] ?? 'Erro ao salvar revenda.', 'Fechar', { duration: 4000 });
        this.isSaving = false;
      }
    });
  }

  cancel(): void {
    this.router.navigate(['/admin/revendas']);
  }
}
