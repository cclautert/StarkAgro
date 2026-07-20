import { Routes } from '@angular/router';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { PivotFormComponent } from './components/pivot-form/pivot-form.component';
import { UserFormComponent } from './components/user-form/user-form.component';
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
import { AdminPlansComponent } from './components/admin/plans/admin-plans.component';
import { DiagnosisListComponent } from './components/diagnosis-list/diagnosis-list.component';
import { DiagnosisNewComponent } from './components/diagnosis-new/diagnosis-new.component';
import { DiagnosisDetailComponent } from './components/diagnosis-detail/diagnosis-detail.component';
import { AgronomistGuard } from './guards/agronomist.guard';
import { AgronomistQueueComponent } from './components/agronomist-queue/agronomist-queue.component';
import { AgronomistReviewComponent } from './components/agronomist-review/agronomist-review.component';
import { AgronomistClientsComponent } from './components/agronomist-clients/agronomist-clients.component';
import { AgronomistBillingComponent } from './components/agronomist/billing/agronomist-billing.component';

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
      { path: 'sensores/novo', loadComponent: () => import('./components/sensor-form/sensor-form.component').then(m => m.SensorFormComponent), canActivate: [AuthGuard] },
      { path: 'sensores/editar/:id', loadComponent: () => import('./components/sensor-form/sensor-form.component').then(m => m.SensorFormComponent), canActivate: [AuthGuard] },
      { path: 'diagnosticos', component: DiagnosisListComponent, canActivate: [AuthGuard] },
      { path: 'diagnosticos/novo', component: DiagnosisNewComponent, canActivate: [AuthGuard] },
      { path: 'diagnosticos/:id', component: DiagnosisDetailComponent, canActivate: [AuthGuard] },

      // Área do agrônomo (apenas quem tem o papel)
      { path: 'agronomo/fila', component: AgronomistQueueComponent, canActivate: [AuthGuard, AgronomistGuard] },
      { path: 'agronomo/laudo/:id', component: AgronomistReviewComponent, canActivate: [AuthGuard, AgronomistGuard] },
      { path: 'agronomo/clientes', component: AgronomistClientsComponent, canActivate: [AuthGuard, AgronomistGuard] },
      { path: 'agronomo/faturamento', component: AgronomistBillingComponent, canActivate: [AuthGuard, AgronomistGuard] },

      // Rotas de administração (apenas super usuário)
      { path: 'admin/usuarios', component: AdminUserListComponent, canActivate: [AuthGuard, AdminGuard] },
      { path: 'admin/usuarios/novo', component: AdminUserFormComponent, canActivate: [AuthGuard, AdminGuard] },
      { path: 'admin/usuarios/editar/:id', component: AdminUserFormComponent, canActivate: [AuthGuard, AdminGuard] },
      { path: 'admin/ia', component: AdminAiSettingsComponent, canActivate: [AuthGuard, AdminGuard] },
      { path: 'admin/planos', component: AdminPlansComponent, canActivate: [AuthGuard, AdminGuard] },

      // Default child route inside layout
      { path: '', redirectTo: 'home', pathMatch: 'full' }
    ]
  },

  // Global redirects
  { path: '**', redirectTo: 'login' }
];
