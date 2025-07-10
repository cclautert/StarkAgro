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

const routes: Routes = [
  // Rota de login fora do layout
  { path: 'login', component: LoginComponent },

  // Rota com layout (rotas protegidas)
  {
    path: '',
    component: LayoutComponent,
    children: [
      { path: 'home', component: HomeComponent, canActivate: [AuthGuard] },
      { path: 'dashboard/:pivoId/:quadrante', component: DashboardComponent, canActivate: [AuthGuard] },
      { path: 'user', component: UserFormComponent, canActivate: [AuthGuard] },
      { path: 'pivots', component: PivotListComponent, canActivate: [AuthGuard] },
      { path: 'pivots/novo', component: PivotFormComponent, canActivate: [AuthGuard] },
      { path: 'pivots/editar/:id', component: PivotFormComponent, canActivate: [AuthGuard] },
      { path: 'sensores', component: SensorListComponent, canActivate: [AuthGuard] },
      { path: 'sensores/novo', component: SensorFormComponent, canActivate: [AuthGuard] },
      { path: 'sensores/editar/:id', component: SensorFormComponent, canActivate: [AuthGuard] },
      { path: 'login', component: LoginComponent },

      // Redirecionamentos
      { path: '', redirectTo: 'login', pathMatch: 'full' },
      { path: '**', redirectTo: 'login' }, // fallback
    ]
  },
];
@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
