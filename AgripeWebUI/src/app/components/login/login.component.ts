import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { FormsModule } from '@angular/forms';
import { jwtDecode } from 'jwt-decode'; // 1. IMPORTE A BIBLIOTECA

interface DecodedToken {
  id: number;
  name: string;
  email: string;
}

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

  login() {
    this.apiService.login(this.email, this.password).subscribe({
      next: (response: any) => {
        const token = response?.token;
        if (token) {
          localStorage.setItem('token', token);

          try {
            // 3. DECODIFIQUE O TOKEN PARA EXTRAIR OS DADOS
            const decodedToken: DecodedToken = jwtDecode(token);

            // 4. PEGUE O ID E SALVE-O NO LOCALSTORAGE
            // IMPORTANTE: O nome do campo de ID pode variar!
            // Pode ser 'sub', 'userId', 'id', 'nameid', etc.
            // Verifique com seu back-end qual é o nome correto da "claim".
            const userId = decodedToken.id;
            localStorage.setItem('userId', userId.toString());

            // 5. NAVEGUE PARA A HOME
            this.router.navigate(['/home']);

          } catch (error) {
            console.error("Erro ao decodificar o token:", error);
            alert('Token inválido ou corrompido.');
          }


          this.router.navigate(['/home']);
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

