import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, FormsModule } from '@angular/forms';

// Interface para definir a estrutura dos dados de um quadrante
interface QuadrantColors {
  topLeft: string;
  topRight: string;
  bottomLeft: string;
  bottomRight:string;
}

// Interface para os dados do Pivô
interface PivotData {
  id: number;
  name: string;
  colors: QuadrantColors;
}

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
  pivots: PivotData[] = [
    {
      id: 1,
      name: 'Pivô de Vendas 2023',
      colors: {
        topLeft: '#4CAF50',    // Verde
        topRight: '#2196F3',   // Azul
        bottomLeft: '#FFC107', // Amarelo
        bottomRight: '#F44336' // Vermelho
      }
    },
    {
      id: 2,
      name: 'Pivô de Marketing Q4',
      colors: {
        topLeft: '#9C27B0',    // Roxo
        topRight: '#00BCD4',   // Ciano
        bottomLeft: '#E91E63', // Rosa
        bottomRight: '#795548' // Marrom
      }
    },
    {
      id: 3,
      name: 'Pivô de Suporte ao Cliente',
      colors: {
        topLeft: '#607D8B',    // Cinza Azulado
        topRight: '#607D8B',    // Cinza Azulado
        bottomLeft: '#4CAF50', // Verde
        bottomRight: '#FF9800' // Laranja
      }
    }
  ];

  // ID do pivô atualmente selecionado no dropdown
  public selectedPivotId: number = 1;

  // Objeto que armazena as cores do pivô selecionado para o binding no template
  public currentColors: QuadrantColors | undefined;

  constructor() { }

  ngOnInit(): void {
    // Ao iniciar o componente, carregamos as cores do primeiro pivô da lista
    this.updateCircleColors();
  }

  // Método chamado quando o usuário muda a seleção no dropdown
  onPivotChange(): void {
    this.updateCircleColors();
  }

  private updateCircleColors(): void {
    // Encontra os dados do pivô selecionado na lista 'pivots'
    const selectedPivot = this.pivots.find(p => p.id === this.selectedPivotId);

    // Atualiza as cores que serão usadas no SVG
    if (selectedPivot) {
      this.currentColors = selectedPivot.colors;
    }
  }
}
