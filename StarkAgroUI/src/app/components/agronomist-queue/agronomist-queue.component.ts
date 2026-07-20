import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { AgronomistService } from '../../services/agronomist.service';
import { AgronomistQueueItem } from '../../models/agronomist.model';

interface QueueCard extends AgronomistQueueItem {
  thumbnailUrl?: string;
}

@Component({
  selector: 'app-agronomist-queue',
  templateUrl: './agronomist-queue.component.html',
  styleUrl: './agronomist-queue.component.css',
  standalone: true,
  imports: [CommonModule, RouterModule]
})
export class AgronomistQueueComponent implements OnInit, OnDestroy {
  items: QueueCard[] = [];
  loading = true;
  error = false;

  private objectUrls: string[] = [];

  constructor(
    private agronomistService: AgronomistService,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.load();
  }

  ngOnDestroy(): void {
    this.objectUrls.forEach(url => URL.revokeObjectURL(url));
  }

  load(): void {
    this.loading = true;

    this.agronomistService.getQueue().subscribe({
      next: items => {
        this.items = items;
        this.loading = false;
        this.error = false;
        this.items.forEach(item => this.loadThumbnail(item));
      },
      error: () => {
        this.loading = false;
        this.error = true;
      }
    });
  }

  open(item: QueueCard): void {
    this.router.navigate(['/agronomo/laudo', item.id]);
  }

  private loadThumbnail(item: QueueCard): void {
    this.agronomistService.getDiagnosisImage(item.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        this.objectUrls.push(url);
        item.thumbnailUrl = url;
      },
      error: () => { /* miniatura é acessório */ }
    });
  }
}
