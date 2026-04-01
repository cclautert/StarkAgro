import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PivotService } from '../../services/pivot.service';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

@Component({
  selector: 'app-pivot-config',
  templateUrl: './pivot-config.component.html',
  styleUrl: './pivot-config.component.css',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatSnackBarModule],
})
export class PivotConfigComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private pivotService = inject(PivotService);
  private snackBar = inject(MatSnackBar);

  configForm: FormGroup;
  pivoId: number | null = null;
  quadranteNome: string | null = null;

  constructor() {
    this.configForm = this.fb.group({
      limiteInferior: [null],
      limiteSuperior: [null],
    });
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('pivoId');
    this.pivoId = idParam ? +idParam : null;
    this.quadranteNome = this.route.snapshot.paramMap.get('quadrante');

    if (this.pivoId) {
      this.pivotService.getPivotById(this.pivoId).subscribe(pivot => {
        if (pivot) {
          this.configForm.patchValue({
            limiteInferior: pivot.limiteInferior ?? null,
            limiteSuperior: pivot.limiteSuperior ?? null,
          });
        }
      });
    }
  }

  onSubmit(): void {
    if (!this.pivoId) return;

    const { limiteInferior, limiteSuperior } = this.configForm.value;

    this.pivotService.updateLimits(this.pivoId, limiteInferior, limiteSuperior).subscribe({
      next: () => {
        this.snackBar.open('Configuração salva com sucesso!', 'OK', { duration: 3000 });
        this.voltar();
      },
      error: (err) => {
        console.error('Erro ao salvar configuração', err);
        this.snackBar.open('Erro ao salvar a configuração.', 'Fechar', { duration: 4000 });
      }
    });
  }

  voltar(): void {
    this.router.navigate(['/dashboard', this.pivoId, this.quadranteNome]);
  }
}
