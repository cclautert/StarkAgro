import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-sensor-form',
  templateUrl: './sensor-form.component.html',
  styleUrls: ['./sensor-form.component.css'],
  standalone: false
})
export class SensorFormComponent {
  sensorForm: FormGroup;

  constructor(private fb: FormBuilder, private apiService: ApiService) {
    this.sensorForm = this.fb.group({
      pivotId: ['', Validators.required],
      userId: ['', [Validators.required, Validators.min(1)]],
      quadrante: ['', [Validators.required, Validators.min(1)]],
      code: ['', Validators.required]
    });
  }

  onSubmit(): void {
    if (this.sensorForm.valid) {
      this.apiService.addSensor(this.sensorForm.value).subscribe({
        next: () => alert('Sensor added successfully'),
        error: () => alert('Error adding sensor')
      });
    }
  }
}
