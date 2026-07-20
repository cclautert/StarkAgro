import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AgronomistService } from '../../../services/agronomist.service';
import { AgronomistBilling } from '../../../models/agronomist-billing.model';

@Component({
  selector: 'app-agronomist-billing',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './agronomist-billing.component.html',
  styleUrls: ['./agronomist-billing.component.css']
})
export class AgronomistBillingComponent implements OnInit {
  private agronomistService = inject(AgronomistService);

  billing: AgronomistBilling | null = null;
  isLoading = true;
  hasError = false;

  ngOnInit(): void {
    this.agronomistService.getBilling().subscribe({
      next: (b) => { this.billing = b; this.isLoading = false; },
      error: () => { this.hasError = true; this.isLoading = false; }
    });
  }
}
