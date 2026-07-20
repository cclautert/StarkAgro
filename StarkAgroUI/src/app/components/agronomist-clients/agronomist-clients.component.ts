import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AgronomistService } from '../../services/agronomist.service';
import { AgronomistClient } from '../../models/agronomist.model';

@Component({
  selector: 'app-agronomist-clients',
  templateUrl: './agronomist-clients.component.html',
  styleUrl: './agronomist-clients.component.css',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule]
})
export class AgronomistClientsComponent implements OnInit {
  clients: AgronomistClient[] = [];
  loading = true;
  inviting = false;
  email = '';
  errorMessage = '';

  constructor(private agronomistService: AgronomistService) { }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.agronomistService.getClients().subscribe({
      next: clients => { this.clients = clients; this.loading = false; },
      error: () => this.loading = false
    });
  }

  invite(): void {
    if (!this.email.trim()) return;

    this.inviting = true;
    this.errorMessage = '';

    this.agronomistService.inviteClient(this.email.trim()).subscribe({
      next: () => {
        this.inviting = false;
        this.email = '';
        this.load();
      },
      error: err => {
        this.inviting = false;
        this.errorMessage = err?.error?.errors?.[0] ?? 'Não foi possível enviar o convite.';
      }
    });
  }

  revoke(client: AgronomistClient): void {
    const label = client.clientName || client.clientEmail;
    if (!confirm(`Encerrar o vínculo com ${label}? Você perde o acesso aos laudos dele.`)) return;

    this.agronomistService.revokeClient(client.id).subscribe(() => this.load());
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
