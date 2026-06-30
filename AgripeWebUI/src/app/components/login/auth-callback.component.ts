import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { jwtDecode } from 'jwt-decode';

interface DecodedToken {
  id: number;
  name: string;
  email: string;
  isAdmin: boolean;
}

@Component({
  selector: 'app-auth-callback',
  templateUrl: './auth-callback.component.html',
  styleUrls: ['./auth-callback.component.css'],
  standalone: false
})
export class AuthCallbackComponent implements OnInit {
  errorMessage = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private apiService: ApiService
  ) {}

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      const isBrowser = typeof window !== 'undefined' && typeof window.location !== 'undefined';

      // Em SSR/prerender não há window nem localStorage; apenas ignora o callback.
      if (!isBrowser) {
        return;
      }

      const code = params['code'];
      const error = params['error'];

      if (error) {
        this.errorMessage = error === 'access_denied' ? 'Login cancelado.' : `Erro: ${error}`;
        return;
      }

      if (!code) {
        this.errorMessage = 'Código de autorização não recebido.';
        return;
      }

      const redirectUri = window.location.origin + window.location.pathname;
      this.apiService.externalLogin('Google', code, redirectUri).subscribe({
        next: (response: { token: string }) => {
          const token = response?.token;
          if (token) {
            if (typeof localStorage !== 'undefined') {
              localStorage.setItem('token', token);
            }
            try {
              const decoded = jwtDecode(token) as DecodedToken;
              const userId = decoded.id;
              if (userId != null) {
                if (typeof localStorage !== 'undefined') {
                  localStorage.setItem('userId', String(userId));
                  localStorage.setItem('isAdmin', (decoded.isAdmin === true).toString());
                }
              }
            } catch {
              // optional: userId from token
            }
            this.router.navigate(['/home']);
          } else {
            this.errorMessage = 'Token inválido.';
          }
        },
        error: () => {
          this.errorMessage = 'Falha no login com Google. Tente novamente.';
        }
      });
    });
  }
}
