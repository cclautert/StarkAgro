import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PivotService } from '../../services/pivot.service';
import { Pivot } from '../../models/pivot.model';

@Component({
  selector: 'app-pivot-form',
  templateUrl: './pivot-form.component.html',
  styleUrl: './pivot-form.component.css', // O CSS pode ser o mesmo
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
})
export class PivotFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private pivotService = inject(PivotService);

  pivotForm: FormGroup;
  pivotId: number | null = null;
  isEditMode = false;

  constructor() {
    // Definindo o novo formulário com os campos 'quadrante' e 'code'
    this.pivotForm = this.fb.group({
      id: [null], // O 'id' pode ser null para novos pivôs
      name: ['', Validators.required]
    });
  }

  ngOnInit(): void {
    // Verifica se há um 'id' na URL para determinar se é modo de edição
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEditMode = true;
      this.pivotId = +idParam; // O '+' converte string para número
      this.pivotService.getPivotById(this.pivotId).subscribe(pivot => {
        if (pivot) {
          // Preenche o formulário com os dados existentes
          this.pivotForm.patchValue(pivot);
        }
      });
    }
  }

  onSubmit(): void {
    if (this.pivotForm.invalid) {
          this.pivotForm.markAllAsTouched(); // Marca todos os campos como "tocados" para exibir os erros
          return;
        }

        const formValue: Pivot = this.pivotForm.value;

        if (this.isEditMode) {
          // Para edição, enviamos o formulário para o método de atualização
          this.pivotService.updatePivot(formValue).subscribe({
            next: () => {
              alert('Pivot atualizado com sucesso!');
              this.router.navigate(['/pivots']);
            },
            error: (err) => {
                console.error('Erro ao atualizar pivot', err);
                alert('Erro ao atualizar o pivot.');
            }
          });
        } else {
          // Para criação, enviamos para o método de criação
          this.pivotService.addPivot(formValue).subscribe({
            next: () => {
              alert('Pivot criado com sucesso!');
              this.router.navigate(['/pivots']);
            },
            error: (err) => {
                console.error('Erro ao criar pivot', err);
                alert('Erro ao criar o pivot.');
            }
          });
        }
  }

  cancelar(): void {
    this.router.navigate(['/pivots']);
  }
}
