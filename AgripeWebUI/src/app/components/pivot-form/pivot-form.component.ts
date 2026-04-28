import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { PivotService } from '../../services/pivot.service';
import { Pivot, PivotLocation } from '../../models/pivot.model';
import { PivotLocationMapComponent } from '../pivot-location-map/pivot-location-map.component';

@Component({
  selector: 'app-pivot-form',
  templateUrl: './pivot-form.component.html',
  styleUrl: './pivot-form.component.css',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatSnackBarModule, MatDialogModule],
})
export class PivotFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private pivotService = inject(PivotService);
  private snackBar = inject(MatSnackBar);
  private dialog = inject(MatDialog);

  pivotForm: FormGroup;
  pivotId: number | null = null;
  isEditMode = false;

  constructor() {
    this.pivotForm = this.fb.group({
      id: [null],
      name: ['', Validators.required],
      latitude: [null as number | null, [Validators.min(-90), Validators.max(90)]],
      longitude: [null as number | null, [Validators.min(-180), Validators.max(180)]],
      altitude: [null as number | null, [Validators.min(-500), Validators.max(9000)]],
      locationAddress: [null as string | null]
    });
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEditMode = true;
      this.pivotId = +idParam;
      this.pivotService.getPivotById(this.pivotId).subscribe(pivot => {
        if (pivot) {
          this.pivotForm.patchValue({
            id: pivot.id,
            name: pivot.name,
            latitude: pivot.latitude ?? null,
            longitude: pivot.longitude ?? null,
            altitude: pivot.altitude ?? null,
            locationAddress: pivot.locationAddress ?? null
          });
        }
      });
    }
  }

  get latitude(): number | null { return this.pivotForm.get('latitude')!.value; }
  get longitude(): number | null { return this.pivotForm.get('longitude')!.value; }
  get altitude(): number | null { return this.pivotForm.get('altitude')!.value; }
  get locationAddress(): string | null { return this.pivotForm.get('locationAddress')!.value; }

  get hasCoordinates(): boolean {
    return this.latitude !== null && this.longitude !== null;
  }

  async openLocationMap(): Promise<void> {
    const initial: Partial<PivotLocation> | null = this.hasCoordinates
      ? {
          latitude: this.latitude!,
          longitude: this.longitude!,
          altitude: this.altitude,
          locationAddress: this.locationAddress
        }
      : null;

    const dialogRef = this.dialog.open<PivotLocationMapComponent, { initial: Partial<PivotLocation> | null }, PivotLocation | null>(
      PivotLocationMapComponent,
      {
        data: { initial },
        panelClass: 'pivot-location-map-panel',
        autoFocus: false,
        maxWidth: '90vw'
      }
    );

    const result = await firstValueFrom(dialogRef.afterClosed());
    if (result) {
      this.pivotForm.patchValue({
        latitude: result.latitude,
        longitude: result.longitude,
        altitude: result.altitude,
        locationAddress: result.locationAddress
      });
      this.pivotForm.markAsDirty();
    }
  }

  clearLocation(): void {
    this.pivotForm.patchValue({
      latitude: null,
      longitude: null,
      altitude: null,
      locationAddress: null
    });
    this.pivotForm.markAsDirty();
  }

  onSubmit(): void {
    if (this.pivotForm.invalid) {
      this.pivotForm.markAllAsTouched();
      return;
    }

    const formValue = this.pivotForm.value as Pivot;

    if (this.isEditMode) {
      this.pivotService.updatePivot(formValue).subscribe({
        next: () => {
          this.snackBar.open('Pivot atualizado com sucesso!', 'OK', { duration: 3000 });
          this.router.navigate(['/pivots']);
        },
        error: (err) => {
          console.error('Erro ao atualizar pivot', err);
          this.snackBar.open('Erro ao atualizar o pivot.', 'Fechar', { duration: 4000 });
        }
      });
    } else {
      this.pivotService.addPivot(formValue).subscribe({
        next: () => {
          this.snackBar.open('Pivot criado com sucesso!', 'OK', { duration: 3000 });
          this.router.navigate(['/pivots']);
        },
        error: (err) => {
          console.error('Erro ao criar pivot', err);
          this.snackBar.open('Erro ao criar o pivot.', 'Fechar', { duration: 4000 });
        }
      });
    }
  }

  cancelar(): void {
    this.router.navigate(['/pivots']);
  }
}
