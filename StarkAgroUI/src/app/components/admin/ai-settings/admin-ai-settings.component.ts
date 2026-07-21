import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AdminService } from '../../../services/admin.service';
import { PlatformAiSettings } from '../../../models/platform-ai-settings.model';

@Component({
  selector: 'app-admin-ai-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './admin-ai-settings.component.html',
  styleUrls: ['./admin-ai-settings.component.css']
})
export class AdminAiSettingsComponent implements OnInit {
  private fb = inject(FormBuilder);
  private adminService = inject(AdminService);
  private snackBar = inject(MatSnackBar);

  form!: FormGroup;
  isLoading = true;
  isSaving = false;

  /** Gasto de IA no mês corrente, em centavos (só leitura, vem do backend). */
  currentMonthAiCostCents = 0;

  /** Gasto de NDVI (PU) no mês corrente, em centavos (só leitura). */
  currentMonthNdviCostCents = 0;

  showGeminiKey = false;
  showOpenAiKey = false;
  showAnthropicKey = false;
  showCropHealthKey = false;
  showCdseSecret = false;

  geminiModels = ['gemini-1.5-flash', 'gemini-1.5-pro', 'gemini-2.0-flash'];
  openAiModels = ['gpt-4o', 'gpt-4o-mini', 'gpt-4-turbo', 'o3-mini'];
  anthropicModels = ['claude-sonnet-4-6', 'claude-opus-4-8', 'claude-haiku-4-5-20251001'];

  ngOnInit(): void {
    this.form = this.fb.group({
      activeProvider: ['gemini', Validators.required],
      geminiKey: [''],
      geminiModel: ['gemini-1.5-flash'],
      openAiKey: [''],
      openAiModel: ['gpt-4o'],
      anthropicKey: [''],
      anthropicModel: ['claude-sonnet-4-6'],
      cropHealthKey: [''],
      cropHealthEnabled: [false],
      defaultDiagnosisQuotaPerMonth: [0],
      cropHealthCostCents: [3, [Validators.min(0)]],
      cdseClientId: [''],
      cdseClientSecret: [''],
      sentinel2Enabled: [false],
      ndviCostCents: [1, [Validators.min(0)]],
      ndviMonthlyBudgetCents: [0, [Validators.min(0)]],
      ndviMaxAreasPerUser: [0, [Validators.min(0)]]
    });

    this.adminService.getAiSettings().subscribe({
      next: (settings: PlatformAiSettings) => {
        this.form.patchValue({
          activeProvider: settings.activeProvider ?? 'gemini',
          geminiKey: settings.geminiKey ?? '',
          geminiModel: settings.geminiModel ?? 'gemini-1.5-flash',
          openAiKey: settings.openAiKey ?? '',
          openAiModel: settings.openAiModel ?? 'gpt-4o',
          anthropicKey: settings.anthropicKey ?? '',
          anthropicModel: settings.anthropicModel ?? 'claude-sonnet-4-6',
          cropHealthKey: settings.cropHealthKey ?? '',
          cropHealthEnabled: settings.cropHealthEnabled ?? false,
          defaultDiagnosisQuotaPerMonth: settings.defaultDiagnosisQuotaPerMonth ?? 0,
          cropHealthCostCents: settings.cropHealthCostCents ?? 3,
          cdseClientId: settings.cdseClientId ?? '',
          cdseClientSecret: settings.cdseClientSecret ?? '',
          sentinel2Enabled: settings.sentinel2Enabled ?? false,
          ndviCostCents: settings.ndviCostCents ?? 1,
          ndviMonthlyBudgetCents: settings.ndviMonthlyBudgetCents ?? 0,
          ndviMaxAreasPerUser: settings.ndviMaxAreasPerUser ?? 0
        });
        this.currentMonthAiCostCents = settings.currentMonthAiCostCents ?? 0;
        this.currentMonthNdviCostCents = settings.currentMonthNdviCostCents ?? 0;
        this.isLoading = false;
      },
      error: () => {
        this.snackBar.open('Erro ao carregar configurações de IA.', 'Fechar', { duration: 4000 });
        this.isLoading = false;
      }
    });
  }

  onSubmit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.isSaving = true;

    const payload: PlatformAiSettings = {
      activeProvider: this.form.value.activeProvider,
      geminiKey: this.form.value.geminiKey || null,
      geminiModel: this.form.value.geminiModel || null,
      openAiKey: this.form.value.openAiKey || null,
      openAiModel: this.form.value.openAiModel || null,
      anthropicKey: this.form.value.anthropicKey || null,
      anthropicModel: this.form.value.anthropicModel || null,
      cropHealthKey: this.form.value.cropHealthKey || null,
      cropHealthEnabled: !!this.form.value.cropHealthEnabled,
      defaultDiagnosisQuotaPerMonth: Number(this.form.value.defaultDiagnosisQuotaPerMonth) || 0,
      cropHealthCostCents: Number(this.form.value.cropHealthCostCents) || 0,
      cdseClientId: this.form.value.cdseClientId || null,
      cdseClientSecret: this.form.value.cdseClientSecret || null,
      sentinel2Enabled: !!this.form.value.sentinel2Enabled,
      ndviCostCents: Number(this.form.value.ndviCostCents) || 0,
      ndviMonthlyBudgetCents: Number(this.form.value.ndviMonthlyBudgetCents) || 0,
      ndviMaxAreasPerUser: Number(this.form.value.ndviMaxAreasPerUser) || 0
    };

    this.adminService.updateAiSettings(payload).subscribe({
      next: () => {
        this.snackBar.open('Configurações salvas.', 'OK', { duration: 3000 });
        this.isSaving = false;
      },
      error: () => {
        this.snackBar.open('Erro ao salvar configurações.', 'Fechar', { duration: 4000 });
        this.isSaving = false;
      }
    });
  }
}
