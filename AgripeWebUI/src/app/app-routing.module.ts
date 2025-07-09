import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { PivotFormComponent } from './components/pivot-form/pivot-form.component';
import { UserFormComponent } from './components/user-form/user-form.component';
import { SensorFormComponent } from './components/sensor-form/sensor-form.component';
import { LoginComponent } from './components/login/login.component';
import { AuthGuard} from './guards/auth.guard';
import { PivotListComponent } from './components/pivot-list/pivot-list.component';
import { SensorListComponent } from './components/sensor-list/sensor-list.component';
import { LayoutComponent } from './components/layout/layout.component';
import { HomeComponent } from './components/home/home.component';

// const routes: Routes = [
//   { path: 'login', component: LoginComponent },
//   { path: 'dashboard', component: DashboardComponent, canActivate: [AuthGuard] },
//   { path: 'user', component: UserFormComponent, canActivate: [AuthGuard] },

//   // Rota para listar todos os Pivots (READ)
//   { path: 'pivots', component: PivotListComponent, canActivate: [AuthGuard] },
//   // Rota para criar um novo Pivot (CREATE)
//   { path: 'pivots/novo', component: PivotFormComponent, canActivate: [AuthGuard] },
//   // Rota para editar um Pivot existente (UPDATE)
//   { path: 'pivots/editar/:id', component: PivotFormComponent, canActivate: [AuthGuard] },

//   // Rota principal da lista de sensores
//   { path: 'sensores', component: SensorListComponent, canActivate: [AuthGuard] },
//   // Rota para criar um novo sensor
//   { path: 'sensores/novo', component: SensorFormComponent, canActivate: [AuthGuard] },
//   // Rota para editar um sensor existente (usa um parâmetro 'id')
//   { path: 'sensores/editar/:id', component: SensorFormComponent, canActivate: [AuthGuard] },

//   // Opcional: Redirecionar rotas não encontradas
//   { path: '', redirectTo: 'login', pathMatch: 'full' },
//   { path: '**', redirectTo: 'login' }, // rota fallback
// ];

const routes: Routes = [
  {
    path: '',
    component: LayoutComponent,
    children: [
      { path: 'home', component: HomeComponent, canActivate: [AuthGuard] },
      { path: 'dashboard/:pivoId/:quadrante', component: DashboardComponent, canActivate: [AuthGuard] },
      { path: 'user', component: UserFormComponent, canActivate: [AuthGuard] },

      // Rota para listar todos os Pivots (READ)
      { path: 'pivots', component: PivotListComponent, canActivate: [AuthGuard] },
      // Rota para criar um novo Pivot (CREATE)
      { path: 'pivots/novo', component: PivotFormComponent, canActivate: [AuthGuard] },
      // Rota para editar um Pivot existente (UPDATE)
      { path: 'pivots/editar/:id', component: PivotFormComponent, canActivate: [AuthGuard] },

      // Rota principal da lista de sensores
      { path: 'sensores', component: SensorListComponent, canActivate: [AuthGuard] },
      // Rota para criar um novo sensor
      { path: 'sensores/novo', component: SensorFormComponent, canActivate: [AuthGuard] },
      // Rota para editar um sensor existente (usa um parâmetro 'id')
      { path: 'sensores/editar/:id', component: SensorFormComponent, canActivate: [AuthGuard] },

      { path: 'login', component: LoginComponent }, //Corrigir
      { path: '', redirectTo: 'login', pathMatch: 'full' },
      { path: '**', redirectTo: 'login' }, // rota fallback
    ]
  },
  //{ path: 'login', component: LoginComponent },
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
