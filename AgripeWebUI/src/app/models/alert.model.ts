export type AlertType = 'MoistureLow' | 'AnomalyPersisted';

export interface UserAlert {
  id: string;
  title: string;
  pivotName: string;
  alertType: AlertType;
  createdAt: string;
  isRead: boolean;
}
