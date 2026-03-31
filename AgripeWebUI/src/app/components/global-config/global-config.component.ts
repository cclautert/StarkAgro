import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UserService } from '../../services/user.service';

@Component({
  selector: 'app-global-config',
  templateUrl: './global-config.component.html',
  styleUrl: './global-config.component.css',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
})
export class GlobalConfigComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private userService = inject(UserService);

  configForm: FormGroup;

  constructor() {
    this.configForm = this.fb.group({
      limiteInferior: [25, [Validators.required, Validators.min(0), Validators.max(100)]],
      limiteSuperior: [75, [Validators.required, Validators.min(0), Validators.max(100)]],
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
          });
        }
      });
    }
  }

  onSubmit(): void {
    if (this.configForm.invalid) return;

    const { limiteInferior, limiteSuperior } = this.configForm.value;

    this.userService.updateLimits(limiteInferior, limiteSuperior).subscribe({
      next: () => {
        alert('Configuração salva com sucesso!');
        this.router.navigate(['/home']);
      },
      error: (err) => {
        console.error('Erro ao salvar configuração', err);
        alert('Erro ao salvar a configuração.');
      }
    });
  }

  cancelar(): void {
    this.router.navigate(['/home']);
  }
}
