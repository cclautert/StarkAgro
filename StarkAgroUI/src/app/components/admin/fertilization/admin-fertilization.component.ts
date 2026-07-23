import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../services/admin.service';
import { FertilizationProfile, ZoneDose, NDVI_CLASSES } from '../../../models/fertilization-profile.model';

@Component({
  selector: 'app-admin-fertilization',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin-fertilization.component.html',
  styleUrls: ['./admin-fertilization.component.css']
})
export class AdminFertilizationComponent implements OnInit {
  readonly classes = NDVI_CLASSES;

  profiles: FertilizationProfile[] = [];
  loading = true;
  saving = false;
  error = '';

  // Edição inline: null = nenhum form aberto; id undefined = novo.
  editing: { id?: number; culture: string; doses: ZoneDose[] } | null = null;

  constructor(private admin: AdminService) {}

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading = true;
    this.admin.getFertilizationProfiles().subscribe({
      next: p => { this.profiles = p; this.loading = false; },
      error: () => { this.error = 'Falha ao carregar perfis.'; this.loading = false; }
    });
  }

  classLabel(key: string): string {
    return this.classes.find(c => c.key === key)?.label ?? key;
  }

  /** Novo perfil: semeia as 6 classes ZERADAS — estrutura, não doses inventadas. */
  novo(): void {
    this.editing = {
      culture: '',
      doses: this.classes.map(c => ({ classKey: c.key, nitrogenKgHa: 0, phosphorusKgHa: 0, potassiumKgHa: 0 }))
    };
    this.error = '';
  }

  editar(p: FertilizationProfile): void {
    // Garante uma linha por classe, preservando os valores já salvos.
    const doses = this.classes.map(c =>
      p.doses.find(d => d.classKey === c.key) ?? { classKey: c.key, nitrogenKgHa: 0, phosphorusKgHa: 0, potassiumKgHa: 0 });
    this.editing = { id: p.id, culture: p.culture, doses: doses.map(d => ({ ...d })) };
    this.error = '';
  }

  cancelar(): void {
    this.editing = null;
  }

  salvar(): void {
    if (!this.editing || !this.editing.culture.trim()) {
      this.error = 'Informe a cultura.';
      return;
    }
    this.saving = true;
    this.error = '';
    const body = { culture: this.editing.culture.trim(), doses: this.editing.doses };
    const req = this.editing.id
      ? this.admin.updateFertilizationProfile(this.editing.id, body)
      : this.admin.createFertilizationProfile(body);

    req.subscribe({
      next: () => { this.saving = false; this.editing = null; this.load(); },
      error: () => { this.saving = false; this.error = 'Falha ao salvar.'; }
    });
  }

  excluir(p: FertilizationProfile): void {
    this.admin.deleteFertilizationProfile(p.id).subscribe({
      next: () => this.load(),
      error: () => { this.error = 'Falha ao excluir.'; }
    });
  }
}
