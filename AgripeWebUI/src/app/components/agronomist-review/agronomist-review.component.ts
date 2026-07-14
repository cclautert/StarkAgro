import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { AgronomistService } from '../../services/agronomist.service';
import { PlantDiagnosis } from '../../models/plant-diagnosis.model';
import { renderMarkdown } from '../../utils/markdown';

@Component({
  selector: 'app-agronomist-review',
  templateUrl: './agronomist-review.component.html',
  styleUrl: './agronomist-review.component.css',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule]
})
export class AgronomistReviewComponent implements OnInit, OnDestroy {
  id!: number;
  diagnosis?: PlantDiagnosis;
  imageUrl?: string;

  loading = true;
  saving = false;
  errorMessage = '';
  aiExpanded = false;

  reportMarkdown = '';
  confirmedDisease = '';
  severity = '';
  prescription = '';

  constructor(
    private route: ActivatedRoute,
    private agronomistService: AgronomistService,
    private router: Router,
    private sanitizer: DomSanitizer
  ) { }

  ngOnInit(): void {
    this.id = Number(this.route.snapshot.paramMap.get('id'));
    this.load();
  }

  ngOnDestroy(): void {
    if (this.imageUrl) URL.revokeObjectURL(this.imageUrl);
  }

  get aiReportHtml(): SafeHtml | null {
    const report = this.diagnosis?.aiReportMarkdown;
    if (!report) return null;
    return this.sanitizer.bypassSecurityTrustHtml(renderMarkdown(report));
  }

  get isSigned(): boolean {
    return this.diagnosis?.status === 'Signed';
  }

  get canEdit(): boolean {
    return this.diagnosis?.status === 'InReview';
  }

  get needsClaim(): boolean {
    return this.diagnosis?.status === 'PendingReview';
  }

  downloadPdf(): void {
    this.agronomistService.getDiagnosisPdf(this.id).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `laudo-${this.id}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    });
  }

  claim(): void {
    this.saving = true;
    this.agronomistService.claim(this.id).subscribe({
      next: () => { this.saving = false; this.load(); },
      error: err => this.fail(err)
    });
  }

  saveDraft(): void {
    this.saving = true;
    this.errorMessage = '';

    this.agronomistService.saveDraft(this.id, this.payload()).subscribe({
      next: () => this.saving = false,
      error: err => this.fail(err)
    });
  }

  sign(): void {
    if (!this.reportMarkdown.trim()) {
      this.errorMessage = 'O laudo não pode ser assinado em branco.';
      return;
    }

    if (!confirm('Assinar este laudo? Depois de assinado ele não pode ser alterado.')) return;

    this.saving = true;
    this.errorMessage = '';

    this.agronomistService.sign(this.id, this.payload()).subscribe({
      next: () => this.router.navigate(['/agronomo/fila']),
      error: err => this.fail(err)
    });
  }

  reject(): void {
    const reason = prompt('Por que este laudo está sendo devolvido ao produtor?');
    if (!reason?.trim()) return;

    this.saving = true;
    this.agronomistService.reject(this.id, reason).subscribe({
      next: () => this.router.navigate(['/agronomo/fila']),
      error: err => this.fail(err)
    });
  }

  private payload() {
    return {
      reportMarkdown: this.reportMarkdown,
      confirmedDisease: this.confirmedDisease || null,
      severity: this.severity || null,
      prescription: this.prescription || null
    };
  }

  private load(): void {
    this.loading = true;

    this.agronomistService.getDiagnosis(this.id).subscribe({
      next: diagnosis => {
        this.diagnosis = diagnosis;
        this.loading = false;

        // A textarea já nasce preenchida com o texto da IA: o agrônomo EDITA, não redige do
        // zero. Essa economia de tempo é literalmente o que ele está comprando.
        this.reportMarkdown =
          diagnosis.agronomistReportMarkdown || diagnosis.aiReportMarkdown || '';
        this.confirmedDisease =
          diagnosis.confirmedDisease || diagnosis.diseases?.[0]?.name || '';
        this.severity = diagnosis.agronomistSeverity || '';
        this.prescription = diagnosis.prescription || '';

        this.loadImage();
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Laudo não encontrado.';
      }
    });
  }

  private loadImage(): void {
    if (this.imageUrl) return;

    this.agronomistService.getDiagnosisImage(this.id).subscribe({
      next: blob => this.imageUrl = URL.createObjectURL(blob),
      error: () => { /* a foto é acessório */ }
    });
  }

  private fail(err: any): void {
    this.saving = false;
    this.errorMessage = err?.error?.errors?.[0] ?? 'Não foi possível concluir a operação.';
  }
}
