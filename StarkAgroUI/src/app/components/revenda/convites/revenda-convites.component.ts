import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RevendaService } from '../../../services/revenda.service';
import { RevendaInvite } from '../../../models/revenda.model';

@Component({
  selector: 'app-revenda-convites',
  templateUrl: './revenda-convites.component.html',
  styleUrl: './revenda-convites.component.css',
  standalone: true,
  imports: [CommonModule]
})
export class RevendaConvitesComponent implements OnInit {
  invites: RevendaInvite[] = [];
  loading = true;
  acting = false;

  constructor(private revendaService: RevendaService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.revendaService.getMyInvites().subscribe({
      next: invites => { this.invites = invites; this.loading = false; },
      error: () => this.loading = false
    });
  }

  accept(invite: RevendaInvite): void {
    this.acting = true;
    this.revendaService.acceptInvite(invite.id).subscribe({
      next: () => { this.acting = false; this.load(); },
      error: () => { this.acting = false; this.load(); }
    });
  }

  decline(invite: RevendaInvite): void {
    this.acting = true;
    this.revendaService.declineInvite(invite.id).subscribe({
      next: () => { this.acting = false; this.load(); },
      error: () => { this.acting = false; this.load(); }
    });
  }

  roleLabel(role: string): string {
    const labels: Record<string, string> = { Manager: 'Gestor', Agronomist: 'Agrônomo', Client: 'Cliente' };
    return labels[role] ?? role;
  }
}
