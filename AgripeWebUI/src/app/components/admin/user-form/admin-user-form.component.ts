import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AdminService } from '../../../services/admin.service';

@Component({
  selector: 'app-admin-user-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './admin-user-form.component.html',
  styleUrls: ['./admin-user-form.component.css']
})
export class AdminUserFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private adminService = inject(AdminService);
  private snackBar = inject(MatSnackBar);

  form!: FormGroup;
  isEditMode = false;
  userId: number | null = null;
  isLoading = false;
  isSaving = false;

  get changePassword() { return this.form.get('changePassword'); }
  get name() { return this.form.get('name'); }
  get email() { return this.form.get('email'); }
  get password() { return this.form.get('password'); }

  ngOnInit(): void {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(2)]],
      email: ['', [Validators.required, Validators.email]],
      active: [true],
      isAdmin: [false],
      isAgronomist: [false],
      agronomistCrea: [''],
      diagnosisQuotaPerMonth: [null],
      changePassword: [false],
      password: ['', [Validators.required, Validators.minLength(8)]]
    });

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEditMode = true;
      this.userId = +idParam;
      this.password?.clearValidators();
      this.password?.updateValueAndValidity();
      this.carregarUsuario(this.userId);

      this.changePassword?.valueChanges.subscribe((enable: boolean) => {
        if (enable) {
          this.password?.setValidators([Validators.required, Validators.minLength(8)]);
        } else {
          this.password?.clearValidators();
          this.password?.setValue('');
        }
        this.password?.updateValueAndValidity();
      });
    }
  }

  carregarUsuario(id: number): void {
    this.isLoading = true;
    this.adminService.getAllUsers().subscribe({
      next: (users) => {
        const user = users.find(u => u.id === id);
        if (user) {
          this.form.patchValue({
            name: user.name,
            email: user.email,
            active: user.active,
            isAdmin: user.isAdmin,
            isAgronomist: user.isAgronomist ?? false,
            agronomistCrea: user.agronomistCrea ?? '',
            diagnosisQuotaPerMonth: user.diagnosisQuotaPerMonth ?? null
          });
        } else {
          this.snackBar.open('Usuário não encontrado.', 'Fechar', { duration: 4000 });
          this.voltar();
        }
        this.isLoading = false;
      },
      error: () => {
        this.snackBar.open('Erro ao carregar usuário.', 'Fechar', { duration: 4000 });
        this.isLoading = false;
      }
    });
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSaving = true;
    const { name, email, active, isAdmin, isAgronomist, agronomistCrea, diagnosisQuotaPerMonth, changePassword, password } = this.form.value;

    if (this.isEditMode && this.userId) {
      const payload: any = {
        name, email, active, isAdmin, isAgronomist,
        agronomistCrea: agronomistCrea || null,
        diagnosisQuotaPerMonth: diagnosisQuotaPerMonth === '' ? null : diagnosisQuotaPerMonth
      };
      if (changePassword && password) payload.password = password;

      this.adminService.updateUser(this.userId, payload).subscribe({
        next: () => {
          this.snackBar.open('Usuário atualizado.', 'OK', { duration: 3000 });
          this.voltar();
        },
        error: () => {
          this.snackBar.open('Erro ao atualizar usuário.', 'Fechar', { duration: 4000 });
          this.isSaving = false;
        }
      });
    } else {
      this.adminService.createUser({ name, email, active, isAdmin, password }).subscribe({
        next: () => {
          this.snackBar.open('Usuário criado.', 'OK', { duration: 3000 });
          this.voltar();
        },
        error: () => {
          this.snackBar.open('Erro ao criar usuário.', 'Fechar', { duration: 4000 });
          this.isSaving = false;
        }
      });
    }
  }

  voltar(): void {
    this.router.navigate(['/admin/usuarios']);
  }
}
