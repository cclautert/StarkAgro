import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-pivot-form',
  templateUrl: './pivot-form.component.html',
  styleUrls: ['./pivot-form.component.css'],
  standalone: false
})
export class PivotFormComponent {
  pivotForm: FormGroup;

  constructor(private fb: FormBuilder, private apiService: ApiService) {
    this.pivotForm = this.fb.group({
      userId: ['', [Validators.required, Validators.min(1)]],
      name: ['', Validators.required]
    });
  }

  onSubmit(): void {
    if (this.pivotForm.valid) {
      this.apiService.addPivot(this.pivotForm.value).subscribe({
        next: () => alert('Pivot added successfully'),
        error: () => alert('Error adding pivot')
      });
    }
  }
}
