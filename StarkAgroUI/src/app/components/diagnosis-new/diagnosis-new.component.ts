import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { HttpEventType } from '@angular/common/http';
import { DiagnosisService } from '../../services/diagnosis.service';
import { PivotService } from '../../services/pivot.service';
import { CultureService } from '../../services/culture.service';
import { Pivot } from '../../models/pivot.model';

/** Foto de celular vem com 2–8 MB; reduzir antes de subir corta upload, custo de IA e latência. */
const MAX_DIMENSION = 1600;
const JPEG_QUALITY = 0.8;

@Component({
  selector: 'app-diagnosis-new',
  templateUrl: './diagnosis-new.component.html',
  styleUrl: './diagnosis-new.component.css',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule]
})
export class DiagnosisNewComponent implements OnInit, OnDestroy {
  pivots: Pivot[] = [];

  pivotId: number | null = null;
  cropName = '';
  notes = '';

  /** Culturas do seletor (lista gerida pelo admin). Opcional aqui — dica pro classificador. */
  cultures: string[] = [];
  get cropOptions(): string[] {
    return this.cropName && !this.cultures.includes(this.cropName) ? [this.cropName, ...this.cultures] : this.cultures;
  }

  previewUrl?: string;
  uploading = false;
  progress = 0;
  errorMessage = '';

  private file?: File;

  constructor(
    private diagnosisService: DiagnosisService,
    private pivotService: PivotService,
    private cultureService: CultureService,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.pivotService.getPivots().subscribe({
      next: pivots => this.pivots = pivots,
      error: () => { /* o pivô é opcional: sem a lista, o laudo ainda pode ser enviado */ }
    });
    this.cultureService.list().subscribe({ next: c => this.cultures = c, error: () => {} });
  }

  ngOnDestroy(): void {
    this.clearPreview();
  }

  async onFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const selected = input.files?.[0];
    if (!selected) return;

    this.errorMessage = '';

    if (!['image/jpeg', 'image/png', 'image/webp'].includes(selected.type)) {
      this.errorMessage = 'Formato não suportado. Envie uma foto JPEG, PNG ou WebP.';
      return;
    }

    try {
      this.file = await this.downscale(selected);
    } catch {
      // Se o navegador não conseguir redimensionar, sobe o original — a API valida o tamanho.
      this.file = selected;
    }

    this.clearPreview();
    this.previewUrl = URL.createObjectURL(this.file);
  }

  submit(): void {
    if (!this.file || this.uploading) return;

    const formData = new FormData();
    formData.append('image', this.file, this.file.name);
    if (this.pivotId) formData.append('pivotId', this.pivotId.toString());
    if (this.cropName) formData.append('cropName', this.cropName);
    if (this.notes) formData.append('notes', this.notes);

    this.uploading = true;
    this.progress = 0;
    this.errorMessage = '';

    this.diagnosisService.create(formData).subscribe({
      next: event => {
        if (event.type === HttpEventType.UploadProgress && event.total) {
          this.progress = Math.round((event.loaded / event.total) * 100);
        } else if (event.type === HttpEventType.Response) {
          this.router.navigate(['/diagnosticos']);
        }
      },
      error: err => {
        this.uploading = false;
        this.errorMessage = err?.status === 413
          ? 'A foto é grande demais para o servidor.'
          : err?.error?.errors?.[0] ?? 'Não foi possível enviar a foto. Tente novamente.';
      }
    });
  }

  /** Redimensiona via canvas mantendo a proporção. */
  private downscale(file: File): Promise<File> {
    return new Promise((resolve, reject) => {
      const image = new Image();
      const sourceUrl = URL.createObjectURL(file);

      image.onload = () => {
        const scale = Math.min(1, MAX_DIMENSION / Math.max(image.width, image.height));

        if (scale === 1 && file.type === 'image/jpeg') {
          URL.revokeObjectURL(sourceUrl);
          resolve(file);
          return;
        }

        const canvas = document.createElement('canvas');
        canvas.width = Math.round(image.width * scale);
        canvas.height = Math.round(image.height * scale);

        const context = canvas.getContext('2d');
        if (!context) {
          URL.revokeObjectURL(sourceUrl);
          reject(new Error('Canvas indisponível'));
          return;
        }

        context.drawImage(image, 0, 0, canvas.width, canvas.height);
        URL.revokeObjectURL(sourceUrl);

        canvas.toBlob(blob => {
          if (!blob) {
            reject(new Error('Falha ao redimensionar'));
            return;
          }
          const name = file.name.replace(/\.[^.]+$/, '') + '.jpg';
          resolve(new File([blob], name, { type: 'image/jpeg' }));
        }, 'image/jpeg', JPEG_QUALITY);
      };

      image.onerror = () => {
        URL.revokeObjectURL(sourceUrl);
        reject(new Error('Imagem inválida'));
      };

      image.src = sourceUrl;
    });
  }

  private clearPreview(): void {
    if (this.previewUrl) {
      URL.revokeObjectURL(this.previewUrl);
      this.previewUrl = undefined;
    }
  }
}
