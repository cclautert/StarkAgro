import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { DiagnosisService } from '../../services/diagnosis.service';
import { PlantDiagnosis, PlantDiagnosisStatus } from '../../models/plant-diagnosis.model';
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
  loading = true;
  error = false;

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

  private load(silent = false): void {
    if (!silent) this.loading = true;

    this.diagnosisService.getById(this.id).subscribe({
      next: diagnosis => {
        this.diagnosis = diagnosis;
        this.loading = false;
        this.error = false;
      },
      error: () => {
        this.loading = false;
        this.error = true;
      }
    });
  }

  private loadImage(): void {
    this.diagnosisService.getImage(this.id).subscribe({
      next: blob => this.imageUrl = URL.createObjectURL(blob),
      error: () => { /* a foto é acessório: o laudo continua legível sem ela */ }
    });
  }
}
