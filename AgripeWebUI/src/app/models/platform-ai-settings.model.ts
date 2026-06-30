export interface PlatformAiSettings {
  openAiKey?: string | null;
  openAiModel?: string | null;
  anthropicKey?: string | null;
  anthropicModel?: string | null;
  geminiKey?: string | null;
  geminiModel?: string | null;
  activeProvider: 'openai' | 'anthropic' | 'gemini';
}
