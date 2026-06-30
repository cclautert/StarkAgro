import { Routes } from '@angular/router';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { PivotFormComponent } from './components/pivot-form/pivot-form.component';
import { UserFormComponent } from './components/user-form/user-form.component';
import { SensorFormComponent } from './components/sensor-form/sensor-form.component';
import { LoginComponent } from './components/login/login.component';
import { AuthCallbackComponent } from './components/login/auth-callback.component';
import { AuthGuard } from './guards/auth.guard';
import { AdminGuard } from './guards/admin.guard';
import { PivotListComponent } from './components/pivot-list/pivot-list.component';
import { SensorListComponent } from './components/sensor-list/sensor-list.component';
import { LayoutComponent } from './components/layout/layout.component';
import { HomeComponent } from './components/home/home.component';
import { IrrigationDashboardComponent } from './components/irrigation-dashboard/irrigation-dashboard.component';
import { PivotConfigComponent } from './components/pivot-config/pivot-config.component';
import { GlobalConfigComponent } from './components/global-config/global-config.component';
import { AdminUserListComponent } from './components/admin/user-list/admin-user-list.component';
import { AdminUserFormComponent } from './components/admin/user-form/admin-user-form.component';
import { AdminAiSettingsComponent } from './components/admin/ai-settings/admin-ai-settings.component';

export const routes: Routes = [
  // Login routes outside the main layout
  { path: 'login', component: LoginComponent },
  { path: 'login/callback', component: AuthCallbackComponent },

  // Routes wrapped by the main layout (with left menu)
  {
    path: '',
    component: LayoutComponent,
    canActivate: [AuthGuard],
    children: [
      { path: 'home', component: HomeComponent, canActivate: [AuthGuard] },
      { path: 'dashboard/:pivoId/:quadrante', component: DashboardComponent, canActivate: [AuthGuard] },
      { path: 'dashboard/:pivoId/:quadrante/config', component: PivotConfigComponent, canActivate: [AuthGuard] },
      { path: 'config', component: GlobalConfigComponent, canActivate: [AuthGuard] },
      { path: 'irrigation-dashboard', component: IrrigationDashboardComponent, canActivate: [AuthGuard] },
      { path: 'user', component: UserFormComponent, canActivate: [AuthGuard] },
      { path: 'pivots', component: PivotListComponent, canActivate: [AuthGuard] },
      { path: 'pivots/novo', component: PivotFormComponent, canActivate: [AuthGuard] },
      { path: 'pivots/editar/:id', component: PivotFormComponent, canActivate: [AuthGuard] },
      { path: 'sensores', component: SensorListComponent, canActivate: [AuthGuard] },
      { path: 'sensores/novo', component: SensorFormComponent, canActivate: [AuthGuard] },
      { path: 'sensores/editar/:id', component: SensorFormComponent, canActivate: [AuthGuard] },

      // Rotas de administração (apenas super usuário)
      { path: 'admin/usuarios', component: AdminUserListComponent, canActivate: [AuthGuard, AdminGuard] },
      { path: 'admin/usuarios/novo', component: AdminUserFormComponent, canActivate: [AuthGuard, AdminGuard] },
      { path: 'admin/usuarios/editar/:id', component: AdminUserFormComponent, canActivate: [AuthGuard, AdminGuard] },
      { path: 'admin/ia', component: AdminAiSettingsComponent, canActivate: [AuthGuard, AdminGuard] },

      // Default child route inside layout
      { path: '', redirectTo: 'home', pathMatch: 'full' }
    ]
  },

  // Global redirects
  { path: '**', redirectTo: 'login' }
];
