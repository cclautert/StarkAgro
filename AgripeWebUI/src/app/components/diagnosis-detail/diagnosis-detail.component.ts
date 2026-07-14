import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { DiagnosisService } from '../../services/diagnosis.service';
import {
  DiagnosisAuditEntry,
  DiagnosisHistory,
  PlantDiagnosis,
  PlantDiagnosisStatus
} from '../../models/plant-diagnosis.model';
import { renderMarkdown } from '../../utils/markdown';

@Component({
  selector: 'app-diagnosis-detail',
  templateUrl: './diagnosis-detail.component.html',
  styleUrl: './diagnosis-detail.component.css',
  standalone: true,
  imports: [CommonModule, RouterModule]
})
export class DiagnosisDetailComponent implements OnInit, OnDestroy {
  diagnosis?: PlantDiagnosis;
  imageUrl?: string;
  history?: DiagnosisHistory;
  audit: DiagnosisAuditEntry[] = [];
  loading = true;
  error = false;
  reprocessing = false;
  auditExpanded = false;

  id!: number;

  private pollTimer?: ReturnType<typeof setInterval>;

  constructor(
    private route: ActivatedRoute,
    private diagnosisService: DiagnosisService,
    private sanitizer: DomSanitizer
  ) { }

  ngOnInit(): void {
    this.id = Number(this.route.snapshot.paramMap.get('id'));
    this.load();
    this.loadImage();

    // Enquanto o laudo está sendo analisado, o produtor fica olhando a tela: 3s.
    this.pollTimer = setInterval(() => {
      if (this.diagnosis && this.isPending(this.diagnosis.status)) {
        this.load(true);
      }
    }, 3000);
  }

  ngOnDestroy(): void {
    if (this.pollTimer) clearInterval(this.pollTimer);
    if (this.imageUrl) URL.revokeObjectURL(this.imageUrl);
  }

  get reportHtml(): SafeHtml | null {
    const report = this.diagnosis?.aiReportMarkdown;
    if (!report) return null;
    return this.sanitizer.bypassSecurityTrustHtml(renderMarkdown(report));
  }

  isPending(status: PlantDiagnosisStatus): boolean {
    return status === 'Uploaded' || status === 'Processing';
  }

  statusLabel(status: PlantDiagnosisStatus): string {
    const labels: Record<PlantDiagnosisStatus, string> = {
      Uploaded: 'Na fila',
      Processing: 'Analisando a foto',
      PendingReview: 'Aguardando o agrônomo',
      InReview: 'Em revisão pelo agrônomo',
      AiCompleted: 'Pré-análise pronta',
      Signed: 'Assinado pelo agrônomo',
      Rejected: 'Foto rejeitada',
      Failed: 'Falhou'
    };
    return labels[status] ?? status;
  }

  /** Baixa o PDF do laudo. */
  downloadPdf(): void {
    this.diagnosisService.getPdf(this.id).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `laudo-${this.id}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    });
  }

  /** Reenfileira o laudo. A foto continua no servidor: não precisa fotografar de novo. */
  reprocess(): void {
    this.reprocessing = true;
    this.diagnosisService.reprocess(this.id).subscribe({
      next: () => {
        this.reprocessing = false;
        this.load();
      },
      error: () => this.reprocessing = false
    });
  }

  toggleAudit(): void {
    this.auditExpanded = !this.auditExpanded;

    if (this.auditExpanded && this.audit.length === 0) {
      this.diagnosisService.getAudit(this.id).subscribe(entries => this.audit = entries);
    }
  }

  actionLabel(action: string): string {
    const labels: Record<string, string> = {
      'created': 'Foto enviada',
      'claimed': 'Assumido',
      'processed:ai': 'Analisado pela IA',
      'processed:mock': 'Analisado',
      'rejected:low-confidence': 'Foto recusada na análise',
      'rejected:agronomist': 'Devolvido pelo agrônomo',
      'signed': 'Assinado',
      'failed': 'Falhou',
      'retry-scheduled': 'Nova tentativa agendada',
      'review-abandoned': 'Revisão devolvida à fila',
      'reprocess-requested': 'Reprocessamento solicitado'
    };
    return labels[action] ?? action;
  }

  private load(silent = false): void {
    if (!silent) this.loading = true;

    this.diagnosisService.getById(this.id).subscribe({
      next: diagnosis => {
        this.diagnosis = diagnosis;
        this.loading = false;
        this.error = false;

        if (diagnosis.pivotId && !this.history) {
          this.loadHistory(diagnosis.pivotId);
        }
      },
      error: () => {
        this.loading = false;
        this.error = true;
      }
    });
  }

  private loadHistory(pivotId: number): void {
    this.diagnosisService.getHistory(pivotId).subscribe({
      next: history => this.history = history,
      error: () => { /* histórico é acessório */ }
    });
  }

  private loadImage(): void {
    this.diagnosisService.getImage(this.id).subscribe({
      next: blob => this.imageUrl = URL.createObjectURL(blob),
      error: () => { /* a foto é acessório: o laudo continua legível sem ela */ }
    });
  }
}
