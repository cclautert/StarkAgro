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

  onSubmit() {
    if (this.apiService.login(this.email, this.password)) {
      localStorage.setItem('token', 'usuario-logado');
      this.router.navigate(['/']);
    } else {
      alert('Usuário ou senha inválidos');
    }
  }
}
