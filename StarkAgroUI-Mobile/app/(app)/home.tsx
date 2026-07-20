import React from 'react';
import { useSettingsStore } from '../../stores/settingsStore';
import { NewDashboard } from '../../components/dashboard/NewDashboard';
import { OldDashboard } from '../../components/dashboard/OldDashboard';

export default function HomeScreen() {
  const { useNewDashboard } = useSettingsStore();
  return useNewDashboard ? <NewDashboard /> : <OldDashboard />;
}
