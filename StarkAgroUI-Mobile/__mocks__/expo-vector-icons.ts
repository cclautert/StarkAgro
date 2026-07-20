import React from 'react';
import { Text } from 'react-native';

const Icon = ({ name, ...rest }: { name: string; [key: string]: unknown }) =>
  React.createElement(Text, rest, name);

export const Ionicons = Icon;
export const MaterialIcons = Icon;
export const FontAwesome = Icon;
export const AntDesign = Icon;
export const Feather = Icon;
export const MaterialCommunityIcons = Icon;
export const Entypo = Icon;
export const FontAwesome5 = Icon;
export const Foundation = Icon;
export const EvilIcons = Icon;
export const Octicons = Icon;
export const SimpleLineIcons = Icon;
export const Zocial = Icon;
