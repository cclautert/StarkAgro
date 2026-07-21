import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { AreaService } from '../../services/area.service';
import { AreaRequest, GeoCoordinate } from '../../models/monitored-area.model';
import { PivotLocationMapComponent } from '../pivot-location-map/pivot-location-map.component';
import { PivotLocation } from '../../models/pivot.model';

@Component({
  selector: 'app-area-form',
  templateUrl: './area-form.component.html',
  styleUrls: ['./area-form.component.css'],
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatSnackBarModule, MatDialogModule]
})
export class AreaFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private areaService = inject(AreaService);
  private snackBar = inject(MatSnackBar);
  private dialog = inject(MatDialog);

  form: FormGroup;
  areaId: number | null = null;
  isEditMode = false;
  saving = false;

  /** Anel preservado ao editar um polígono (desenho livre é follow-up com leaflet-geoman). */
  private existingPolygonRing: GeoCoordinate[] = [];

  constructor() {
    this.form = this.fb.group({
      name: ['', Validators.required],
      crop: [null as string | null],
      areaKind: ['Circle' as 'Circle' | 'Polygon'],
      centerLat: [null as number | null, [Validators.min(-90), Validators.max(90)]],
      centerLng: [null as number | null, [Validators.min(-180), Validators.max(180)]],
      radiusM: [250 as number | null, [Validators.min(10), Validators.max(20000)]],
      altitude: [null as number | null],
      locationAddress: [null as string | null],
      monitoringEnabled: [true]
    });
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEditMode = true;
      this.areaId = +idParam;
      this.areaService.get(this.areaId).subscribe(area => {
        this.existingPolygonRing = area.ring ?? [];
        this.form.patchValue({
          name: area.name,
          crop: area.crop ?? null,
          areaKind: area.areaKind,
          centerLat: area.centerLat ?? null,
          centerLng: area.centerLng ?? null,
          radiusM: area.radiusM ?? 250,
          altitude: area.altitude ?? null,
          locationAddress: area.locationAddress ?? null,
          monitoringEnabled: area.monitoringEnabled
        });
      });
    }
  }

  get areaKind(): 'Circle' | 'Polygon' { return this.form.get('areaKind')!.value; }
  get isPolygon(): boolean { return this.areaKind === 'Polygon'; }
  get centerLat(): number | null { return this.form.get('centerLat')!.value; }
  get centerLng(): number | null { return this.form.get('centerLng')!.value; }
  get altitude(): number | null { return this.form.get('altitude')!.value; }
  get locationAddress(): string | null { return this.form.get('locationAddress')!.value; }
  get hasCenter(): boolean { return this.centerLat !== null && this.centerLng !== null; }

  async openLocationMap(): Promise<void> {
    const initial: Partial<PivotLocation> | null = this.hasCenter
      ? { latitude: this.centerLat!, longitude: this.centerLng!, altitude: this.altitude, locationAddress: this.locationAddress }
      : null;

    const dialogRef = this.dialog.open<PivotLocationMapComponent, { initial: Partial<PivotLocation> | null }, PivotLocation | null>(
      PivotLocationMapComponent,
      { data: { initial }, panelClass: 'pivot-location-map-panel', autoFocus: false, maxWidth: '90vw' }
    );

    const result = await firstValueFrom(dialogRef.afterClosed());
    if (result) {
      this.form.patchValue({
        centerLat: result.latitude,
        centerLng: result.longitude,
        altitude: result.altitude,
        locationAddress: result.locationAddress
      });
      this.form.markAsDirty();
    }
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.value;
    let ring: GeoCoordinate[];

    if (this.isPolygon) {
      // Desenho livre de polígono é follow-up (leaflet-geoman); ao editar, o anel é preservado.
      if (this.existingPolygonRing.length < 3) {
        this.snackBar.open('Desenho de polígono ainda não disponível — use o tipo Círculo.', 'Fechar', { duration: 4000 });
        return;
      }
      ring = this.existingPolygonRing;
    } else {
      if (v.centerLat == null || v.centerLng == null || !v.radiusM) {
        this.snackBar.open('Defina o centro no mapa e o raio da área.', 'Fechar', { duration: 4000 });
        return;
      }
      ring = circleToRing(v.centerLat, v.centerLng, v.radiusM);
    }

    const body: AreaRequest = {
      name: v.name.trim(),
      crop: v.crop?.trim() || null,
      areaKind: v.areaKind,
      centerLat: v.centerLat,
      centerLng: v.centerLng,
      radiusM: v.radiusM,
      altitude: v.altitude,
      locationAddress: v.locationAddress,
      monitoringEnabled: v.monitoringEnabled,
      ring
    };

    this.saving = true;
    const op = this.isEditMode
      ? this.areaService.update(this.areaId!, body)
      : this.areaService.create(body);

    op.subscribe({
      next: () => {
        this.saving = false;
        this.snackBar.open(this.isEditMode ? 'Área atualizada!' : 'Área criada!', 'OK', { duration: 3000 });
        this.router.navigate(['/areas']);
      },
      error: () => {
        this.saving = false;
        this.snackBar.open('Não foi possível salvar a área.', 'Fechar', { duration: 4000 });
      }
    });
  }

  cancel(): void {
    this.router.navigate(['/areas']);
  }
}

/**
 * Aproxima um círculo (centro + raio em metros) a um anel de N vértices, em lat/lng.
 * O servidor fecha o anel e converte para [lng,lat]. Fonte clássica de bug: manter a
 * correção de longitude por cos(lat).
 */
export function circleToRing(lat: number, lng: number, radiusM: number, segments = 48): GeoCoordinate[] {
  const latRad = (lat * Math.PI) / 180;
  const dLat = radiusM / 111320;
  const dLng = radiusM / (111320 * Math.cos(latRad));
  const ring: GeoCoordinate[] = [];
  for (let i = 0; i < segments; i++) {
    const theta = (i / segments) * 2 * Math.PI;
    ring.push({
      lat: round(lat + dLat * Math.sin(theta), 6),
      lng: round(lng + dLng * Math.cos(theta), 6)
    });
  }
  return ring;
}

function round(value: number, decimals: number): number {
  const f = Math.pow(10, decimals);
  return Math.round(value * f) / f;
}
