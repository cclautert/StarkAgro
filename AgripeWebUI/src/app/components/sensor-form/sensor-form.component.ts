
import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { SensorService } from '../../services/sensor.service';
import { PivotService } from '../../services/pivot.service';
import { Sensor } from '../../models/sensor.model';
import { Pivot } from '../../models/pivot.model';
import { forkJoin } from 'rxjs';

@Component({
  selector: 'app-sensor-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './sensor-form.component.html',
  styleUrl: './sensor-form.component.css' // O CSS pode ser o mesmo
})
export class SensorFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private sensorService = inject(SensorService);
  private pivotService = inject(PivotService);

  sensorForm: FormGroup;
  isEditMode = false;
  private sensorId: number | undefined;

  // Lista para popular o <select> no HTML
  pivotsDisponiveis: Pivot[] = [];

  constructor() {
    // Definindo o novo formulário com os campos 'quadrante' e 'code'
    this.sensorForm = this.fb.group({
      quadrante: [null, [Validators.required, Validators.min(1)]],
      code: ['', Validators.required]
    });
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEditMode = true;
      this.sensorId = +idParam;
      this.carregarDadosParaEdicao();
    } else {
      this.isEditMode = false;
      // Para um novo sensor, só precisa carregar a lista de pivôs
      this.carregarPivots();
    }

    // Atualiza a definição do formulário para incluir o campo 'pivot'
    this.sensorForm = this.fb.group({
      // O valor inicial de um objeto deve ser null
      id: [null],
      pivot: [null, Validators.required],
      code: ['', Validators.required],
      quadrante: [null, [Validators.required, Validators.min(1)]]
    });

    // A lógica para obter o ID da rota permanece a mesma
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEditMode = true;
      this.sensorId = +id;
      this.sensorService.getSensorById(this.sensorId).subscribe(sensor => {
        // Preenche o formulário com os dados do sensor buscado
        this.sensorForm.patchValue(sensor);
      });
    }
  }

  onSubmit(): void {
    if (this.sensorForm.invalid) {
      this.sensorForm.markAllAsTouched(); // Marca todos os campos como "tocados" para exibir os erros
      return;
    }

     // O valor do formulário já corresponde à interface Sensor!
    const formValue: Sensor = this.sensorForm.value;

    if (this.isEditMode) {
      // Para edição, enviamos o formulário para o método de atualização
      this.sensorService.updateSensor(formValue).subscribe({
        next: () => {
          alert('Sensor atualizado com sucesso!');
          this.router.navigate(['/sensores']);
        },
        error: (err) => {
            console.error('Erro ao atualizar sensor', err);
            alert('Erro ao atualizar o sensor.');
        }
      });
    } else {
      // Para criação, enviamos para o método de criação
      this.sensorService.addSensor(formValue).subscribe({
        next: () => {
          alert('Sensor criado com sucesso!');
          this.router.navigate(['/sensores']);
        },
        error: (err) => {
            console.error('Erro ao criar sensor', err);
            alert('Erro ao criar o sensor.');
        }
      });
    }
  }

  carregarDadosParaEdicao(): void {
    const pivots$ = this.pivotService.getPivots();
    const sensor$ = this.sensorService.getSensorById(this.sensorId!);

    // forkJoin espera as duas chamadas terminarem
    forkJoin([pivots$, sensor$]).subscribe({
      next: ([pivots, sensor]) => {
        // 1. Preenche a lista de pivôs para o dropdown
        this.pivotsDisponiveis = pivots;

        // 2. AGORA, com a lista já carregada, preenche o formulário
        this.sensorForm.patchValue({
          pivot: sensor.pivot, // O objeto pivot inteiro
          code: sensor.code,
          quadrante: sensor.quadrante
        });
      },
      error: err => {
        console.error('Erro ao carregar dados para edição', err);
        // Tratar erro, talvez redirecionar o usuário
      }
    });
  }

  cancelar(): void {
    this.router.navigate(['/sensores']);
  }

  carregarPivots(): void {
    this.pivotService.getPivots().subscribe(pivots => {
      this.pivotsDisponiveis = pivots.filter(Boolean);
    });
  }

  // Função de comparação para o [compareWith] no <select>
  // Compara os objetos para saber qual selecionar no modo de edição
  comparePivots(p1: Pivot, p2: Pivot): boolean {
    return p1 && p2 ? p1.name === p2.name : p1 === p2;
  }

  // Getters para facilitar o acesso aos controles no template
  get pivot() { return this.sensorForm.get('pivot'); }
  get code() { return this.sensorForm.get('code'); }
  get quadrante() { return this.sensorForm.get('quadrante'); }
}
