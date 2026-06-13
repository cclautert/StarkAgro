import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UserService } from '../../services/user.service';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

@Component({
  selector: 'app-global-config',
  templateUrl: './global-config.component.html',
  styleUrl: './global-config.component.css',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatSnackBarModule],
})
export class GlobalConfigComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private userService = inject(UserService);
  private snackBar = inject(MatSnackBar);

  configForm: FormGroup;

  showApiKey = false;

  constructor() {
    this.configForm = this.fb.group({
      limiteInferior: [25, [Validators.required, Validators.min(0), Validators.max(100)]],
      limiteSuperior: [75, [Validators.required, Validators.min(0), Validators.max(100)]],
      rainThresholdMm: [null, [Validators.min(0)]],
      geminiApiKey: [null],
    });
  }

  ngOnInit(): void {
    const idParam = localStorage.getItem('userId');
    if (idParam) {
      this.userService.getUserById(+idParam).subscribe(user => {
        if (user) {
          this.configForm.patchValue({
            limiteInferior: user.limiteInferior ?? 25,
            limiteSuperior: user.limiteSuperior ?? 75,
            rainThresholdMm: user.rainThresholdMm ?? null,
            geminiApiKey: user.geminiApiKey ?? null,
          });
        }
      });
    }
  }

  onSubmit(): void {
    if (this.configForm.invalid) return;

    const { limiteInferior, limiteSuperior, rainThresholdMm, geminiApiKey } = this.configForm.value;

    this.userService.updateLimits(limiteInferior, limiteSuperior, rainThresholdMm, geminiApiKey).subscribe({
      next: () => {
        this.snackBar.open('Configuração salva com sucesso!', 'OK', { duration: 3000 });
        this.router.navigate(['/home']);
      },
      error: (err) => {
        console.error('Erro ao salvar configuração', err);
        this.snackBar.open('Erro ao salvar a configuração.', 'Fechar', { duration: 4000 });
      }
    });
  }

  cancelar(): void {
    this.router.navigate(['/home']);
  }
}
