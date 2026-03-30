const { getDefaultConfig } = require('expo/metro-config');
const { withNativeWind } = require('nativewind/metro');

// Apply NativeWind first so its resolveRequest is in place
const config = withNativeWind(getDefaultConfig(__dirname), { input: './global.css' });

// Chain our web stub AFTER NativeWind so we don't clobber its resolver
const upstreamResolveRequest = config.resolver?.resolveRequest;

config.resolver.resolveRequest = (context, moduleName, platform) => {
  // react-native-worklets has no web implementation — stub it out so
  // react-native-reanimated can load without crashing the browser bundle.
  if (platform === 'web' && moduleName === 'react-native-worklets') {
    return { type: 'empty' };
  }
  if (upstreamResolveRequest) {
    return upstreamResolveRequest(context, moduleName, platform);
  }
  return context.resolveRequest(context, moduleName, platform);
};

module.exports = config;
