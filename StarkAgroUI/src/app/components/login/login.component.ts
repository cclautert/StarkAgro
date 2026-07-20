import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { FormsModule } from '@angular/forms';
import { jwtDecode } from 'jwt-decode';
import { environment } from '../../../environments/environment';
import { MatSnackBar } from '@angular/material/snack-bar';

interface DecodedToken {
  id: number;
  name: string;
  email: string;
  isAdmin: boolean;
  isAgronomist: boolean;
}

/** Google OAuth 2.0 authorization endpoint */
const GOOGLE_AUTH_URL = 'https://accounts.google.com/o/oauth2/v2/auth';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css'],
  standalone: false
})
export class LoginComponent {
  email = '';
  password = '';

  get googleLoginEnabled(): boolean {
    return !!environment.googleClientId?.trim();
  }

  constructor(private fb: FormsModule, private apiService: ApiService, private router: Router, private snackBar: MatSnackBar) {}

  loginWithGoogle(): void {
    if (!this.googleLoginEnabled) return;
    const redirectUri = `${window.location.origin}/login/callback`;
    const params = new URLSearchParams({
      client_id: environment.googleClientId,
      redirect_uri: redirectUri,
      response_type: 'code',
      scope: 'email profile openid'
    });
    window.location.href = `${GOOGLE_AUTH_URL}?${params.toString()}`;
  }

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
            localStorage.setItem('isAdmin', (String(decodedToken.isAdmin) === 'true').toString());
            localStorage.setItem('isAgronomist', (String(decodedToken.isAgronomist) === 'true').toString());
            localStorage.setItem('isResellerManager', (String((decodedToken as any).isResellerManager) === 'true').toString());

            // 5. NAVEGUE PARA A HOME
            this.router.navigate(['/home']);

          } catch (error) {
            console.error("Erro ao decodificar o token:", error);
            this.snackBar.open('Token inválido ou corrompido.', 'Fechar', { duration: 4000 });
          }


          this.router.navigate(['/home']);
        } else {
          this.snackBar.open('Token inválido.', 'Fechar', { duration: 4000 });
        }
      },
      error: (err) => {
        if (err.status === 403) {
          this.snackBar.open('Contate o suporte técnico.', 'Fechar', { duration: 0 });
        } else if (err.status === 429) {
          this.snackBar.open('Muitas tentativas. Tente novamente em alguns minutos.', 'Fechar', { duration: 6000 });
        } else {
          this.snackBar.open('Usuário ou senha inválidos.', 'Fechar', { duration: 4000 });
        }
      }
    });
  }
}

