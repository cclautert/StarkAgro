import { Tabs, useRouter } from 'expo-router';
import { TouchableOpacity, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useAuthStore } from '../../stores/authStore';
import { Colors } from '../../constants/colors';
import { useIdleTimeout } from '../../hooks/useIdleTimeout';

type IoniconsName = React.ComponentProps<typeof Ionicons>['name'];

function TabIcon({ name, color }: { name: IoniconsName; color: string }) {
  return <Ionicons name={name} size={24} color={color} />;
}

export default function AppLayout() {
  const router = useRouter();
  const { logout } = useAuthStore();
  const { resetActivity } = useIdleTimeout();

  const handleLogout = async () => {
    await logout();
    router.replace('/(auth)/login');
  };

  return (
    <View style={{ flex: 1 }} onTouchStart={resetActivity}>
    <Tabs
      screenOptions={{
        headerShown: false,
        tabBarStyle: {
          backgroundColor: Colors.tabBarBg,
          borderTopColor: Colors.tabBarBorder,
          borderTopWidth: 1,
        },
        tabBarActiveTintColor: Colors.tabBarActive,
        tabBarInactiveTintColor: Colors.tabBarInactive,
      }}
    >
      <Tabs.Screen
        name="home"
        options={{
          title: 'Início',
          tabBarIcon: ({ color }) => <TabIcon name="home-outline" color={color} />,
        }}
      />
      <Tabs.Screen
        name="pivots/index"
        options={{
          title: 'Pivôs',
          tabBarIcon: ({ color }) => <TabIcon name="git-branch-outline" color={color} />,
        }}
      />
      <Tabs.Screen
        name="sensors/index"
        options={{
          title: 'Sensores',
          tabBarIcon: ({ color }) => <TabIcon name="radio-outline" color={color} />,
        }}
      />
      <Tabs.Screen
        name="settings"
        options={{
          title: 'Config.',
          tabBarIcon: ({ color }) => <TabIcon name="settings-outline" color={color} />,
        }}
      />
      <Tabs.Screen
        name="logout"
        options={{
          title: 'Sair',
          tabBarIcon: ({ color }) => <TabIcon name="log-out-outline" color={color} />,
          tabBarButton: ({ onPress: _onPress, ...props }) => (
            <TouchableOpacity {...(props as object)} onPress={handleLogout} />
          ),
        }}
      />
      {/* Hidden screens (no tab) */}
      <Tabs.Screen name="dashboard/[pivotId]/[quadrant]" options={{ href: null }} />
      <Tabs.Screen name="pivots/new" options={{ href: null }} />
      <Tabs.Screen name="pivots/[id]/edit" options={{ href: null }} />
      <Tabs.Screen name="sensors/new" options={{ href: null }} />
      <Tabs.Screen name="sensors/[id]/edit" options={{ href: null }} />
    </Tabs>
    </View>
  );
}
