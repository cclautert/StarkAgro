import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AdminService } from '../../../services/admin.service';
import { AdminUser } from '../../../models/admin-user.model';

@Component({
  selector: 'app-admin-user-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './admin-user-list.component.html',
  styleUrls: ['./admin-user-list.component.css']
})
export class AdminUserListComponent implements OnInit {
  private adminService = inject(AdminService);
  private router = inject(Router);
  private snackBar = inject(MatSnackBar);

  users: AdminUser[] = [];
  isLoading = true;

  ngOnInit(): void {
    this.carregarUsuarios();
  }

  carregarUsuarios(): void {
    this.isLoading = true;
    this.adminService.getAllUsers().subscribe({
      next: (users) => {
        this.users = users;
        this.isLoading = false;
      },
      error: () => {
        this.snackBar.open('Erro ao carregar usuários.', 'Fechar', { duration: 4000 });
        this.isLoading = false;
      }
    });
  }

  editarUsuario(id: number): void {
    this.router.navigate(['/admin/usuarios/editar', id]);
  }

  toggleActive(user: AdminUser): void {
    const acao = user.active ? 'desativar' : 'ativar';
    if (!confirm(`Deseja ${acao} o usuário "${user.name}"?`)) return;

    this.adminService.toggleActive(user.id, !user.active).subscribe({
      next: (updated) => {
        user.active = updated.active;
        this.snackBar.open(`Usuário ${user.active ? 'ativado' : 'desativado'}.`, 'OK', { duration: 3000 });
      },
      error: () => this.snackBar.open('Erro ao alterar status.', 'Fechar', { duration: 4000 })
    });
  }

  excluirUsuario(user: AdminUser): void {
    if (!confirm(`Excluir permanentemente o usuário "${user.name}"? Esta ação não pode ser desfeita.`)) return;

    this.adminService.deleteUser(user.id).subscribe({
      next: () => {
        this.users = this.users.filter(u => u.id !== user.id);
        this.snackBar.open('Usuário excluído.', 'OK', { duration: 3000 });
      },
      error: () => this.snackBar.open('Erro ao excluir usuário.', 'Fechar', { duration: 4000 })
    });
  }
}
