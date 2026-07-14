import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DiagnosisService } from '../../services/diagnosis.service';
import { AgronomistService } from '../../services/agronomist.service';
import { AgronomistInvite } from '../../models/agronomist.model';
import { PlantDiagnosisStatus, PlantDiagnosisSummary } from '../../models/plant-diagnosis.model';

interface DiagnosisCard extends PlantDiagnosisSummary {
  thumbnailUrl?: string;
}

@Component({
  selector: 'app-diagnosis-list',
  templateUrl: './diagnosis-list.component.html',
  styleUrl: './diagnosis-list.component.css',
  standalone: true,
  imports: [CommonModule, RouterModule]
})
export class DiagnosisListComponent implements OnInit, OnDestroy {
  diagnoses: DiagnosisCard[] = [];
  invites: AgronomistInvite[] = [];
  loading = true;
  error = false;

  /** URLs de blob criadas para as miniaturas — precisam ser revogadas ao sair. */
  private objectUrls: string[] = [];
  private pollTimer?: ReturnType<typeof setInterval>;

  constructor(
    private diagnosisService: DiagnosisService,
    private agronomistService: AgronomistService
  ) { }

  ngOnInit(): void {
    this.load();
    this.loadInvites();

    // Enquanto houver laudo em processamento, recarrega para o status andar sozinho na tela.
    this.pollTimer = setInterval(() => {
      if (this.diagnoses.some(d => this.isPending(d.status))) {
        this.load(true);
      }
    }, 5000);
  }

  ngOnDestroy(): void {
    if (this.pollTimer) clearInterval(this.pollTimer);
    this.revokeThumbnails();
  }

  load(silent = false): void {
    if (!silent) this.loading = true;

    this.diagnosisService.getAll().subscribe({
      next: items => {
        this.revokeThumbnails();
        this.diagnoses = items;
        this.loading = false;
        this.error = false;
        this.diagnoses.forEach(d => this.loadThumbnail(d));
      },
      error: () => {
        this.loading = false;
        this.error = true;
      }
    });
  }

  delete(id: number): void {
    if (!confirm('Tem certeza que deseja excluir este laudo?')) return;

    this.diagnosisService.delete(id).subscribe(() => this.load());
  }

  acceptInvite(invite: AgronomistInvite): void {
    this.agronomistService.acceptInvite(invite.id).subscribe(() => this.loadInvites());
  }

  declineInvite(invite: AgronomistInvite): void {
    this.agronomistService.declineInvite(invite.id).subscribe(() => this.loadInvites());
  }

  private loadInvites(): void {
    this.agronomistService.getMyInvites().subscribe({
      next: invites => this.invites = invites,
      error: () => { /* convite é acessório: a lista de laudos continua funcionando */ }
    });
  }

  isPending(status: PlantDiagnosisStatus): boolean {
    return status === 'Uploaded' || status === 'Processing';
  }

  statusLabel(status: PlantDiagnosisStatus): string {
    const labels: Record<PlantDiagnosisStatus, string> = {
      Uploaded: 'Na fila',
      Processing: 'Analisando',
      PendingReview: 'Aguardando agrônomo',
      InReview: 'Em revisão',
      AiCompleted: 'Pré-análise pronta',
      Signed: 'Assinado',
      Rejected: 'Foto rejeitada',
      Failed: 'Falhou'
    };
    return labels[status] ?? status;
  }

  private loadThumbnail(diagnosis: DiagnosisCard): void {
    this.diagnosisService.getImage(diagnosis.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        this.objectUrls.push(url);
        diagnosis.thumbnailUrl = url;
      },
      error: () => { /* miniatura é acessório: a lista continua utilizável sem ela */ }
    });
  }

  private revokeThumbnails(): void {
    this.objectUrls.forEach(url => URL.revokeObjectURL(url));
    this.objectUrls = [];
  }
}
