import { Tabs } from 'expo-router';
import { useEffect } from 'react';
import { View } from 'react-native';

import { ChangeMarkersProvider, useChangeMarkers } from '@/features/timetable/change-markers-context';
import { BottomNav, type NavTabKey } from '@/shared/components/bottom-nav';
import { PushPrompt } from '@/shared/push/push-prompt';
import { NotesOutboxSync } from '@/shared/sync/notes-outbox';

/** Today is the group's index route, so it is "/" rather than "/today". */
const ROUTE_FOR: Record<NavTabKey, string> = {
  today: 'index',
  timetable: 'timetable',
  changes: 'changes',
  settings: 'settings',
};

export default function TabsLayout() {
  return (
    <ChangeMarkersProvider>
      <View style={{ flex: 1 }}>
        <PushPrompt />
                <NotesOutboxSync />
        <Tabs
          screenOptions={{ headerShown: false }}
          tabBar={({ state, navigation }) => {
            const current = state.routes[state.index].name;
            const active = (Object.keys(ROUTE_FOR) as NavTabKey[]).find(
              (k) => ROUTE_FOR[k] === current,
            );
            return (
              <NavBar
                active={active ?? 'today'}
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
    </ChangeMarkersProvider>
  );
}

/**
 * The bottom nav, with the number of unseen changes on the Changes tab (#72). Opening that tab is
 * seeing the changes, so it counts them all as seen.
 */
function NavBar({ active, onSelect }: { active: NavTabKey; onSelect: (key: NavTabKey) => void }) {
  const { unseenCount, markAllSeen } = useChangeMarkers();
  // Also covers reaching Changes by a notification tap, and a change arriving while it is open.
  useEffect(() => {
    if (active === 'changes' && unseenCount > 0) markAllSeen();
  }, [active, unseenCount, markAllSeen]);
  return (
    <BottomNav
      active={active}
      changesCount={unseenCount}
      onSelect={(key) => {
        if (key === 'changes') markAllSeen();
        onSelect(key);
      }}
    />
  );
}
