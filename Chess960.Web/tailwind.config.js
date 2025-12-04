/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './**/*.{razor,html,cshtml}',
    '../Chess960.Web.Client/**/*.{razor,html,cshtml}'
  ],
  theme: {
    extend: {
      colors: {
        'chess-purple': {
          50: '#f5e6fa',
          100: '#eacdf5',
          200: '#d59bf0',
          300: '#c068eb',
          400: '#ab36e6',
          500: '#9400cd', // Base color
          600: '#7b00b6',
          700: '#67009a',
          800: '#5c008d',
          900: '#4f0079',
          950: '#240032',
        },
        'chess-dark': {
          900: '#160020',
          950: '#000000',
        }
      }
    },
  },
  plugins: [
    require('@tailwindcss/forms'),
  ],
}
