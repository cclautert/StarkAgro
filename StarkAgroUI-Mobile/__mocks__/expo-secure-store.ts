const store = new Map<string, string>();

export const getItemAsync = jest.fn(async (key: string): Promise<string | null> => {
  return store.get(key) ?? null;
});

export const setItemAsync = jest.fn(async (key: string, value: string): Promise<void> => {
  store.set(key, value);
});

export const deleteItemAsync = jest.fn(async (key: string): Promise<void> => {
  store.delete(key);
});

export const __reset = () => store.clear();
