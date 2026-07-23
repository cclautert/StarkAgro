export type AlertType = 'MoistureLow' | 'AnomalyPersisted' | 'AgronomistInvite' | 'RevendaInvite' | 'FireHotspot' | 'FrostRisk' | 'HeatRisk';

export interface UserAlert {
  id: string;
  title: string;
  pivotName: string;
  alertType: AlertType;
  createdAt: string;
  isRead: boolean;
}
