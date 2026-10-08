import { Tabs } from 'expo-router';
import { View } from 'react-native';

import { BottomNav, type NavTabKey } from '@/shared/components/bottom-nav';
import { PushPrompt } from '@/shared/push/push-prompt';

/** Today is the group's index route, so it is "/" rather than "/today". */
const ROUTE_FOR: Record<NavTabKey, string> = {
  today: 'index',
  timetable: 'timetable',
  changes: 'changes',
  settings: 'settings',
};

export default function TabsLayout() {
  return (
    <View style={{ flex: 1 }}>
      <PushPrompt />
      <Tabs
        screenOptions={{ headerShown: false }}
        tabBar={({ state, navigation }) => {
          const current = state.routes[state.index].name;
          const active = (Object.keys(ROUTE_FOR) as NavTabKey[]).find(
            (k) => ROUTE_FOR[k] === current,
          );
          return (
            <BottomNav
              active={active ?? 'today'}
              // Wired to the unseen-change count by #72.
              changesCount={0}
              onSelect={(key) => navigation.navigate(ROUTE_FOR[key])}
            />
          );
        }}
      >
        <Tabs.Screen name="index" />
        <Tabs.Screen name="timetable" />
        <Tabs.Screen name="changes" />
        <Tabs.Screen name="settings" />
      </Tabs>
    </View>
  );
}
