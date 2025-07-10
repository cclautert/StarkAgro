import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { Pivot } from '../../models/pivot.model';
import { PivotService } from '../../services/pivot.service';
import { ApiService } from '../../services/api.service';
import { Quadrante } from '../../models/quadrante.model';
import { Router } from '@angular/router';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrl: './home.component.css',
  standalone: false
})
export class HomeComponent implements OnInit {
  private fb = inject(FormBuilder);
  // Lista de pivôs disponíveis para seleção.
  // Cada pivô tem um conjunto de cores para os quadrantes.
   pivot: Pivot | undefined;
   pivots: Pivot[] | undefined;

  // ID do pivô atualmente selecionado no dropdown
  public selectedPivotId: number = 1;

  // Objeto que armazena as cores do pivô selecionado para o binding no template
  public currentColors: Quadrante | undefined;

  constructor(
    private pivotService: PivotService,
    private apiService: ApiService,
   private router: Router) { }

  ngOnInit(): void {
    this.pivotService.getPivots().subscribe(pivots => {
      this.pivots = pivots;
      this.selectedPivotId = this.pivots && this.pivots.length > 0 ? this.pivots[0].id : 1;

      // Ao iniciar o componente, carregamos as cores do primeiro pivô da lista
      this.updateCircleColors();
    });
  }

  // Método chamado quando o usuário muda a seleção no dropdown
  onPivotChange(): void {
    this.updateCircleColors();
  }

  private updateCircleColors(): void {

    this.apiService.getReadsByPivotId(this.selectedPivotId, 1).subscribe(pivot => {
      this.pivot = pivot;

      // Encontra os dados do pivô selecionado na lista 'pivots'
      const selectedPivot = this.pivot;

      // Atualiza as cores que serão usadas no SVG
      if (selectedPivot) {
        this.currentColors = selectedPivot.quadrante;
      }
    });
  }

  // NOVO MÉTODO: Chamado quando um quadrante é clicado
  public onQuadrantClick2(quadranteName: 'TopLeft' | 'TopRight' | 'BottomLeft' | 'BottomRight'): void {
    if (!this.pivot?.quadrante) {
      console.error('Dados do quadrante não disponíveis.');
      return;
    }

    let color: string | undefined | null;
    let average: number | undefined | null;

    // Usamos um switch para encontrar os dados corretos com base no nome
    switch (quadranteName) {
      case 'TopLeft':
        color = this.pivot.quadrante.topLeft;
        average = this.pivot.quadrante.topLeftAvg;
        break;
      case 'TopRight':
        color = this.pivot.quadrante.topRight;
        average = this.pivot.quadrante.topRightAvg;
        break;
      case 'BottomLeft':
        color = this.pivot.quadrante.bottomLeft;
        average = this.pivot.quadrante.bottomLeftAvg;
        break;
      case 'BottomRight':
        color = this.pivot.quadrante.bottomRight;
        average = this.pivot.quadrante.bottomRightAvg;
        break;
    }

    const avgDisplay = average !== null && average !== undefined ? average.toFixed(2) : 'N/A';

    // Exibe os dados. Você pode usar um alert, um console.log, ou abrir um modal.
    console.log(`Quadrante Clicado: ${quadranteName}`);
    console.log(`Cor: ${color}`);
    console.log(`Média: ${avgDisplay}`);

    alert(`Quadrante: ${quadranteName}\nCor: ${color}\nMédia: ${avgDisplay}`);
  }

  public onQuadrantClick(quadranteName: 'TopLeft' | 'TopRight' | 'BottomLeft' | 'BottomRight'): void {
    if (!this.selectedPivotId) {
      console.error('ID do Pivô não está selecionado, não é possível navegar.');
      return;
    }

    // Navega para a rota do dashboard, passando o ID do pivô e o nome do quadrante
    this.router.navigate(['/dashboard', this.selectedPivotId, quadranteName]);
  }
}
