
import { Component, OnDestroy, OnInit, ViewChild, ElementRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatIconModule } from '@angular/material/icon';
import { SensorService } from '../../services/sensor.service';
import { PivotService } from '../../services/pivot.service';
import { Sensor } from '../../models/sensor.model';
import { Pivot } from '../../models/pivot.model';
import { forkJoin } from 'rxjs';
import { BrowserMultiFormatReader, IScannerControls } from '@zxing/browser';
import { BarcodeFormat, DecodeHintType } from '@zxing/library';

@Component({
  selector: 'app-sensor-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatSnackBarModule, MatIconModule],
  templateUrl: './sensor-form.component.html',
  styleUrl: './sensor-form.component.css' // O CSS pode ser o mesmo
})
export class SensorFormComponent implements OnInit, OnDestroy {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private sensorService = inject(SensorService);
  private pivotService = inject(PivotService);
  private snackBar = inject(MatSnackBar);

  @ViewChild('scannerVideo') scannerVideo?: ElementRef<HTMLVideoElement>;

  isScanning = false;
  isSyncing = false;

  private codeReader?: BrowserMultiFormatReader;
  private scannerControls?: IScannerControls;

  sensorForm: FormGroup;
  isEditMode = false;
  private sensorId: number | undefined;

  pivotsDisponiveis: Pivot[] = [];

  constructor() {
    this.sensorForm = this.fb.group({
      id: [null],
      name: ['', Validators.required],
      pivot: [null, Validators.required],
      code: ['', [Validators.required]],
      quadrante: [null, [Validators.required]],
      uplinkIntervalSeconds: [10800]
    });
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEditMode = true;
      this.sensorId = +idParam;
      this.carregarDadosParaEdicao();
    } else {
      this.isEditMode = false;
      this.carregarPivots();
    }
  }

  onSubmit(): void {
    if (this.sensorForm.invalid) {
      this.sensorForm.markAllAsTouched(); // Marca todos os campos como "tocados" para exibir os erros
      return;
    }

     // O valor do formulário já corresponde à interface Sensor!
    const formValue: Sensor = this.sensorForm.value;

    if (this.isEditMode) {
      // Para edição, enviamos o formulário para o método de atualização
      this.sensorService.updateSensor(formValue).subscribe({
        next: () => {
          this.snackBar.open('Sensor atualizado com sucesso!', 'OK', { duration: 3000 });
          this.router.navigate(['/sensores']);
        },
        error: (err) => {
            console.error('Erro ao atualizar sensor', err);
            this.snackBar.open('Erro ao atualizar o sensor.', 'Fechar', { duration: 4000 });
        }
      });
    } else {
      // Para criação, enviamos para o método de criação
      this.sensorService.addSensor(formValue).subscribe({
        next: () => {
          this.snackBar.open('Sensor criado com sucesso!', 'OK', { duration: 3000 });
          this.router.navigate(['/sensores']);
        },
        error: (err) => {
            console.error('Erro ao criar sensor', err);
            this.snackBar.open('Erro ao criar o sensor.', 'Fechar', { duration: 4000 });
        }
      });
    }
  }

  carregarDadosParaEdicao(): void {
    const pivots$ = this.pivotService.getPivots();
    const sensor$ = this.sensorService.getSensorById(this.sensorId!);

    // forkJoin espera as duas chamadas terminarem
    forkJoin([pivots$, sensor$]).subscribe({
      next: ([pivots, sensor]) => {
        // 1. Preenche a lista de pivôs para o dropdown
        this.pivotsDisponiveis = pivots;

        // 2. AGORA, com a lista já carregada, preenche o formulário
        this.sensorForm.patchValue({
          name: sensor.name,
          pivot: sensor.pivot,
          code: sensor.code,
          quadrante: sensor.quadrante,
          uplinkIntervalSeconds: sensor.uplinkIntervalSeconds ?? 10800
        });
      },
      error: err => {
        console.error('Erro ao carregar dados para edição', err);
        // Tratar erro, talvez redirecionar o usuário
      }
    });
  }

  cancelar(): void {
    this.router.navigate(['/sensores']);
  }

  carregarPivots(): void {
    this.pivotService.getPivots().subscribe(pivots => {
      this.pivotsDisponiveis = pivots.filter(Boolean);
    });
  }

  // Função de comparação para o [compareWith] no <select>
  // Compara os objetos para saber qual selecionar no modo de edição
  comparePivots(p1: Pivot, p2: Pivot): boolean {
    return p1 && p2 ? p1.name === p2.name : p1 === p2;
  }

  syncDownlink(): void {
    if (!this.sensorId || this.isSyncing) return;
    this.isSyncing = true;
    this.sensorService.syncDownlink(this.sensorId).subscribe({
      next: (res) => this.snackBar.open(res.message, 'OK', { duration: 5000 }),
      error: (err) => {
        this.isSyncing = false;
        this.snackBar.open(err?.error?.message ?? 'Erro ao sincronizar downlink.', 'Fechar', { duration: 5000 });
      },
      complete: () => { this.isSyncing = false; }
    });
  }

  // Getters para facilitar o acesso aos controles no template
  get name() { return this.sensorForm.get('name'); }
  get pivot() { return this.sensorForm.get('pivot'); }
  get code() { return this.sensorForm.get('code'); }
  get quadrante() { return this.sensorForm.get('quadrante'); }
  get uplinkIntervalSeconds() { return this.sensorForm.get('uplinkIntervalSeconds'); }

  /**
   * Abre a câmera e lê QR Code ou código de barras 1D (EAN, Code128, Code39, etc.)
   * usando ZXing-js, que decodifica no próprio navegador — funciona no Android e no
   * Safari do iOS, onde a API nativa BarcodeDetector não existe.
   */
  async scanQrCode(): Promise<void> {
    if (this.isScanning) return;

    if (!navigator.mediaDevices?.getUserMedia) {
      this.snackBar.open('Câmera não disponível neste navegador.', 'Fechar', { duration: 4000 });
      return;
    }

    // O <video> fica sempre no DOM, então o ViewChild já está resolvido no clique.
    const videoEl = this.scannerVideo?.nativeElement;
    if (!videoEl) {
      this.snackBar.open('Não foi possível iniciar o leitor.', 'Fechar', { duration: 4000 });
      return;
    }

    this.isScanning = true;

    const hints = new Map<DecodeHintType, unknown>();
    hints.set(DecodeHintType.POSSIBLE_FORMATS, [
      BarcodeFormat.QR_CODE,
      BarcodeFormat.EAN_13,
      BarcodeFormat.EAN_8,
      BarcodeFormat.CODE_128,
      BarcodeFormat.CODE_39,
      BarcodeFormat.CODE_93,
      BarcodeFormat.ITF,
      BarcodeFormat.UPC_A,
      BarcodeFormat.UPC_E,
      BarcodeFormat.CODABAR,
      BarcodeFormat.DATA_MATRIX
    ]);
    this.codeReader = new BrowserMultiFormatReader(hints);

    try {
      this.scannerControls = await this.codeReader.decodeFromConstraints(
        { video: { facingMode: { ideal: 'environment' } } },
        videoEl,
        (result, err) => {
          if (result) {
            this.sensorForm.patchValue({ code: result.getText() });
            this.sensorForm.get('code')?.markAsDirty();
            this.snackBar.open('Código lido com sucesso!', 'OK', { duration: 3000 });
            this.stopScan();
          }
          // Erros de "não encontrado neste frame" são normais; ignoramos para seguir tentando.
        }
      );
    } catch (err: any) {
      const msg = err?.name === 'NotAllowedError'
        ? 'Permissão de câmera negada.'
        : (err?.message ?? 'Erro ao acessar a câmera.');
      this.snackBar.open(msg, 'Fechar', { duration: 4000 });
      this.stopScan();
    }
  }

  /** Encerra a leitura e libera a câmera. */
  stopScan(): void {
    this.scannerControls?.stop();
    this.scannerControls = undefined;
    this.codeReader = undefined;
    this.isScanning = false;
  }

  ngOnDestroy(): void {
    this.stopScan();
  }
}
