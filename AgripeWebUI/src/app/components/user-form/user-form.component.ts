import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-user-form',
  templateUrl: './user-form.component.html',
  styleUrls: ['./user-form.component.css'],
  standalone: false
})
export class UserFormComponent {
  userForm: FormGroup;

  constructor(private fb: FormBuilder, private apiService: ApiService, private router: Router) {
    this.userForm = this.fb.group({
      name: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      active: [true]
    });
  }

  onSubmit(): void {
    if (this.userForm.valid) {
      this.apiService.addUser(this.userForm.value).subscribe({
        next: () => alert('User added successfully'),
        error: () => alert('Error adding user')
      });
    }
  };
  onCancel(): void {
    this.router.navigate(['/']);
  }
}
