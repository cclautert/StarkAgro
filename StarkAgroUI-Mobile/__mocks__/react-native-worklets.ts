// Minimal mock for react-native-worklets in Jest
module.exports = {
  createWorkletRuntime: jest.fn(),
  runOnRuntime: jest.fn(),
  makeShareableCloneRecursive: jest.fn((v: unknown) => v),
};
