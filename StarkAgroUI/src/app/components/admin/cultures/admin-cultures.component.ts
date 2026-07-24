import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../services/admin.service';
import { Culture } from '../../../models/culture.model';

@Component({
  selector: 'app-admin-cultures',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin-cultures.component.html',
  styleUrls: ['./admin-cultures.component.css']
})
export class AdminCulturesComponent implements OnInit {
  cultures: Culture[] = [];
  loading = true;
  saving = false;
  error = '';

  newName = '';
  // Renomeação inline: id da cultura em edição (null = nenhuma).
  editingId: number | null = null;
  editingName = '';

  constructor(private admin: AdminService) {}

  ngOnInit(): void { this.load(); }

  private load(): void {
    this.loading = true;
    this.admin.getCultures().subscribe({
      next: c => { this.cultures = c; this.loading = false; },
      error: () => { this.error = 'Falha ao carregar culturas.'; this.loading = false; }
    });
  }

  adicionar(): void {
    const name = this.newName.trim();
    if (!name) { this.error = 'Informe o nome da cultura.'; return; }
    this.saving = true;
    this.error = '';
    this.admin.createCulture(name).subscribe({
      next: () => { this.saving = false; this.newName = ''; this.load(); },
      error: err => { this.saving = false; this.error = err?.error?.errors?.[0] ?? 'Falha ao adicionar.'; }
    });
  }

  editar(c: Culture): void {
    this.editingId = c.id;
    this.editingName = c.name;
    this.error = '';
  }

  cancelar(): void {
    this.editingId = null;
    this.editingName = '';
  }

  salvar(c: Culture): void {
    const name = this.editingName.trim();
    if (!name) { this.error = 'Informe o nome da cultura.'; return; }
    this.saving = true;
    this.error = '';
    this.admin.renameCulture(c.id, name).subscribe({
      next: () => { this.saving = false; this.editingId = null; this.load(); },
      error: err => { this.saving = false; this.error = err?.error?.errors?.[0] ?? 'Falha ao renomear.'; }
    });
  }

  excluir(c: Culture): void {
    this.admin.deleteCulture(c.id).subscribe({
      next: () => this.load(),
      error: () => { this.error = 'Falha ao excluir.'; }
    });
  }
}
