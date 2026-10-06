import { Text, View } from 'react-native';

import { Font } from '@/shared/theme/fonts';

/** Placeholder. The screen ticket fills this in. */
export default function SettingsScreen() {
  return (
    <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center' }}>
      <Text style={{ fontFamily: Font.textBold }}>Settings</Text>
    </View>
  );
}
