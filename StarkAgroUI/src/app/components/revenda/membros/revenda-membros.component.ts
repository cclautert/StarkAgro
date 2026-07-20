import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { RevendaService } from '../../../services/revenda.service';
import { RevendaMember } from '../../../models/revenda.model';

@Component({
  selector: 'app-revenda-membros',
  templateUrl: './revenda-membros.component.html',
  styleUrl: './revenda-membros.component.css',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule]
})
export class RevendaMembrosComponent implements OnInit {
  members: RevendaMember[] = [];
  loading = true;
  inviting = false;
  email = '';
  role = 'Client';
  errorMessage = '';

  constructor(private revendaService: RevendaService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.revendaService.getMembers().subscribe({
      next: members => { this.members = members; this.loading = false; },
      error: () => this.loading = false
    });
  }

  invite(): void {
    if (!this.email.trim()) return;
    this.inviting = true;
    this.errorMessage = '';

    this.revendaService.invite(this.email.trim(), this.role).subscribe({
      next: () => { this.inviting = false; this.email = ''; this.load(); },
      error: err => {
        this.inviting = false;
        this.errorMessage = err?.error?.errors?.[0] ?? 'Não foi possível enviar o convite.';
      }
    });
  }

  revoke(member: RevendaMember): void {
    const label = member.memberName || member.memberEmail;
    if (!confirm(`Encerrar o vínculo com ${label}?`)) return;
    this.revendaService.revokeMember(member.id).subscribe(() => this.load());
  }

  roleLabel(role: string): string {
    const labels: Record<string, string> = {
      Manager: 'Gestor',
      Agronomist: 'Agrônomo',
      Client: 'Cliente'
    };
    return labels[role] ?? role;
  }

  statusLabel(status: string): string {
    const labels: Record<string, string> = {
      Pending: 'Convite enviado',
      Active: 'Ativo',
      Declined: 'Recusado',
      Revoked: 'Encerrado',
      Expired: 'Convite expirado'
    };
    return labels[status] ?? status;
  }
}
