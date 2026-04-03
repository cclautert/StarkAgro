/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./app/**/*.{ts,tsx}', './components/**/*.{ts,tsx}'],
  presets: [require('nativewind/preset')],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        background: '#0A1628',
        card: '#112240',
        cardBorder: '#1E3A5F',
        primary: '#4FC3F7',
        success: '#22C55E',
        warning: '#FBBF24',
        danger: '#EF4444',
        textPrimary: '#F1F5F9',
        textSecondary: '#94A3B8',
      },
    },
  },
  plugins: [],
};
