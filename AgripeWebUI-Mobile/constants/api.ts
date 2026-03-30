import Constants from 'expo-constants';

export const API_BASE_URL: string =
  process.env.EXPO_PUBLIC_API_BASE_URL ??
  (Constants.expoConfig?.extra?.apiBaseUrl as string) ??
  '/api/v1/';

export const GOOGLE_CLIENT_ID: string =
  process.env.EXPO_PUBLIC_GOOGLE_CLIENT_ID ??
  (Constants.expoConfig?.extra?.googleClientId as string) ??
  '';

// Quadrant name → number mapping (consistent with backend)
export const QUADRANT_NAME_TO_NUMBER: Record<string, number> = {
  TopRight: 1,
  BottomRight: 2,
  BottomLeft: 3,
  TopLeft: 4,
};

export const QUADRANT_NUMBER_TO_NAME: Record<number, string> = {
  1: 'TopRight',
  2: 'BottomRight',
  3: 'BottomLeft',
  4: 'TopLeft',
};

export const QUADRANT_LABELS: Record<number, string> = {
  1: 'Quadrante 1',
  2: 'Quadrante 2',
  3: 'Quadrante 3',
  4: 'Quadrante 4',
};
