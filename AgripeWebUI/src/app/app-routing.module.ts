import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { PivotFormComponent } from './components/pivot-form/pivot-form.component';
import { UserFormComponent } from './components/user-form/user-form.component';
import { SensorFormComponent } from './components/sensor-form/sensor-form.component';
import { LoginComponent } from './components/login/login.component';
import { AuthGuard} from './guards/auth.guard';

const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'dashboard', component: DashboardComponent, canActivate: [AuthGuard] },
  { path: 'pivot', component: PivotFormComponent, canActivate: [AuthGuard] },
  { path: 'user', component: UserFormComponent, canActivate: [AuthGuard] },
  { path: 'sensor', component: SensorFormComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: '**', redirectTo: 'login' } // rota fallback
];


@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
