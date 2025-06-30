// import { Component } from '@angular/core';
// import { FormBuilder, FormGroup, Validators } from '@angular/forms';
// import { ApiService } from '../../services/api.service';

// @Component({
//   selector: 'app-sensor-form',
//   templateUrl: './sensor-form.component.html',
//   styleUrls: ['./sensor-form.component.css'],
//   standalone: false
// })
// export class SensorFormComponent {
//   sensorForm: FormGroup;

//   constructor(private fb: FormBuilder, private apiService: ApiService) {
//     this.sensorForm = this.fb.group({
//       pivotId: ['', Validators.required],
//       userId: ['', [Validators.required, Validators.min(1)]],
//       quadrante: ['', [Validators.required, Validators.min(1)]],
//       code: ['', Validators.required]
//     });
//   }

//   onSubmit(): void {
//     if (this.sensorForm.valid) {
//       this.apiService.addSensor(this.sensorForm.value).subscribe({
//         next: () => alert('Sensor added successfully'),
//         error: () => alert('Error adding sensor')
//       });
//     }
//   }
// }
import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { SensorService } from '../../services/sensor.service';
import { Sensor } from '../../models/sensor.model';

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

  sensorForm: FormGroup;
  sensorId: number | null = null;
  isEditMode = false;

  constructor() {
    // Definindo o novo formulário com os campos 'quadrante' e 'code'
    this.sensorForm = this.fb.group({
      quadrante: [null, [Validators.required, Validators.min(1)]],
      code: ['', Validators.required]
    });
  }

  ngOnInit(): void {
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

  cancelar(): void {
    this.router.navigate(['/sensores']);
  }

  // Funções helper para acessar controles no template de forma mais limpa
  get quadrante() { return this.sensorForm.get('quadrante'); }
  get code() { return this.sensorForm.get('code'); }
}
