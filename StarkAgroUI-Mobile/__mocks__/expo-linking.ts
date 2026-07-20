export const useURL = jest.fn(() => null as string | null);

export const parse = jest.fn((_url: string) => ({
  scheme: 'agripeweb',
  hostname: 'callback',
  path: '',
  queryParams: {} as Record<string, string>,
}));

export const createURL = jest.fn((path: string) => `agripeweb://${path}`);
