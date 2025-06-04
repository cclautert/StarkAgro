import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { PivotFormComponent } from './components/pivot-form/pivot-form.component';
import { UserFormComponent } from './components/user-form/user-form.component';
import { SensorFormComponent } from './components/sensor-form/sensor-form.component';
import { LoginComponent } from './components/login/login.component';
import { authGuard } from './guards/auth.guard';

const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: '', component: DashboardComponent, canActivate: [authGuard] },
  { path: 'pivot', component: PivotFormComponent, canActivate: [authGuard] },
  { path: 'user', component: UserFormComponent, canActivate: [authGuard] },
  { path: 'sensor', component: SensorFormComponent, canActivate: [authGuard] }
  // {
  //   path: '',
  //   component: DashboardComponent,
  //   children: [
  //     { path: '', component: DashboardComponent },
  //     { path: 'pivot', component: PivotFormComponent },
  //     { path: 'user', component: UserFormComponent },
  //     { path: 'sensor', component: SensorFormComponent }
  //   ]
  // }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
