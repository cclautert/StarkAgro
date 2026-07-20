export const router = {
  replace: jest.fn(),
  push: jest.fn(),
  back: jest.fn(),
};

export const useRouter = jest.fn(() => router);

export const useLocalSearchParams = jest.fn(() => ({}));

export const useSegments = jest.fn(() => []);

export const useFocusEffect = jest.fn((cb: () => void) => cb());

export const Stack = { Screen: () => null };

export const Tabs = { Screen: () => null };

export const Link = ({ children }: { children: React.ReactNode }) => children;
