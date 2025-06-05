import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css'],
  standalone: false
})
export class LoginComponent {
  email = '';
  password = '';

  constructor(private fb: FormsModule, private apiService: ApiService, private router: Router) {}

  /**
   * Authenticates the user using the provided email and password.
   * If authentication is successful, stores a token in local storage and navigates to the dashboard.
   * If authentication fails, displays an error alert.
   */

  login() {
    this.apiService.login(this.email, this.password).subscribe({
      next: (response: any) => {
        const token = response?.token;
        if (token) {
          localStorage.setItem('token', token);
          this.router.navigate(['/dashboard']);
        } else {
          alert('Token inválido');
        }
      },
      error: (err) => {
        alert('Usuário ou senha inválidos');
      }
    });
  }
}
