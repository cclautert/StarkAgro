import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UserService } from '../../services/user.service';

@Component({
  selector: 'app-user-form',
  templateUrl: './user-form.component.html',
  styleUrls: ['./user-form.component.css'],
  standalone: false
})
export class UserFormComponent {
  userForm: FormGroup;
  userId: number | undefined;

  constructor(
    private fb: FormBuilder,
    private router: Router,
    private userService: UserService,
  ) {
    this.userForm = this.fb.group({
      name: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', Validators.required],
      active: [true]
    });
  }

   ngOnInit(): void {
    // Verifica se há um 'id' na URL para determinar se é modo de edição
    const idParam = localStorage.getItem('userId');
    if (idParam) {
      this.userId = +idParam; // O '+' converte string para número
      this.userService.getUserById(this.userId).subscribe(user => {
        if (user) {
          // Preenche o formulário com os dados existentes
          this.userForm.patchValue(user);
        }
      });
    }
  }

  onSubmit(): void {
    if (this.userForm.valid) {
      this.userService.updateUser(this.userForm.value).subscribe({
        next: () => alert('User added successfully'),
        error: () => alert('Error adding user')
      });
    }
  };
  onCancel(): void {
    this.router.navigate(['/dashboard']);
  }
}

