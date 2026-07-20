describe('QUADRANT_NAME_TO_NUMBER and QUADRANT_NUMBER_TO_NAME are inverses', () => {
  it('covers all four quadrants in both directions', () => {
    const {
      QUADRANT_NAME_TO_NUMBER,
      QUADRANT_NUMBER_TO_NAME,
    } = require('../../constants/api');

    const names = Object.keys(QUADRANT_NAME_TO_NUMBER);
    expect(names).toHaveLength(4);

    names.forEach((name) => {
      const num = QUADRANT_NAME_TO_NUMBER[name];
      expect(QUADRANT_NUMBER_TO_NAME[num]).toBe(name);
    });

    Object.keys(QUADRANT_NUMBER_TO_NAME).forEach((numStr) => {
      const num = parseInt(numStr);
      const name = QUADRANT_NUMBER_TO_NAME[num];
      expect(QUADRANT_NAME_TO_NUMBER[name]).toBe(num);
    });
  });
});

describe('QUADRANT_LABELS', () => {
  it('has a label for each of the four quadrants', () => {
    const { QUADRANT_LABELS } = require('../../constants/api');
    [1, 2, 3, 4].forEach((q) => {
      expect(QUADRANT_LABELS[q]).toBeTruthy();
    });
    expect(Object.keys(QUADRANT_LABELS)).toHaveLength(4);
  });
});

describe('API_BASE_URL and GOOGLE_CLIENT_ID', () => {
  it('API_BASE_URL resolves to mock value from expo-constants', () => {
    const { API_BASE_URL } = require('../../constants/api');
    expect(typeof API_BASE_URL).toBe('string');
    expect(API_BASE_URL.length).toBeGreaterThan(0);
  });

  it('GOOGLE_CLIENT_ID resolves to mock value from expo-constants', () => {
    const { GOOGLE_CLIENT_ID } = require('../../constants/api');
    expect(GOOGLE_CLIENT_ID).toBe('test-client-id');
  });
});
