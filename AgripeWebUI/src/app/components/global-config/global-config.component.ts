import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UserService } from '../../services/user.service';
import { WebPushService } from '../../services/web-push.service';
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
  webPush = inject(WebPushService);

  configForm: FormGroup;
  enablingPush = false;

  constructor() {
    this.configForm = this.fb.group({
      limiteInferior: [25, [Validators.required, Validators.min(0), Validators.max(100)]],
      limiteSuperior: [75, [Validators.required, Validators.min(0), Validators.max(100)]],
      rainThresholdMm: [null, [Validators.min(0)]],
      uplinkIntervalSeconds: [10800, [Validators.min(60)]],
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
            uplinkIntervalSeconds: user.uplinkIntervalSeconds ?? 10800,
          });
        }
      });
    }
  }

  onSubmit(): void {
    if (this.configForm.invalid) return;

    const { limiteInferior, limiteSuperior, rainThresholdMm, uplinkIntervalSeconds } = this.configForm.value;

    this.userService.updateLimits(limiteInferior, limiteSuperior, rainThresholdMm, uplinkIntervalSeconds).subscribe({
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

  async ativarNotificacoes(): Promise<void> {
    this.enablingPush = true;
    try {
      const result = await this.webPush.enableNotifications();
      switch (result) {
        case 'granted':
          this.snackBar.open('Notificações ativadas neste dispositivo!', 'OK', { duration: 3000 });
          break;
        case 'denied':
          this.snackBar.open('Permissão negada — reative nas configurações do navegador.', 'Fechar', { duration: 5000 });
          break;
        case 'unsupported':
          this.snackBar.open('Notificações não são suportadas neste navegador.', 'Fechar', { duration: 4000 });
          break;
        default:
          this.snackBar.open('Não foi possível ativar as notificações. Tente novamente.', 'Fechar', { duration: 4000 });
      }
    } finally {
      this.enablingPush = false;
    }
  }
}
