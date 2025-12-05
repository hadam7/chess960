/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './**/*.{razor,html,cshtml}',
    '../Chess960.Web.Client/**/*.{razor,html,cshtml}'
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', 'sans-serif'],
        display: ['Outfit', 'sans-serif'],
      },
      colors: {
        'theme': {
          bg: 'rgb(var(--color-bg) / <alpha-value>)',
          surface: 'rgb(var(--color-surface) / <alpha-value>)',
          accent: 'rgb(var(--color-accent) / <alpha-value>)',
          text: 'rgb(var(--color-text) / <alpha-value>)',
          muted: 'rgb(var(--color-muted) / <alpha-value>)',
          glow: 'rgb(var(--color-glow) / <alpha-value>)',
        },
        'board': {
          white: '#f0fdf4',   // Mint 50
          black: '#064e3b',   // Emerald 900
          accent: 'rgb(var(--color-accent) / <alpha-value>)',
        }
      },
      borderRadius: {
        '4xl': '2rem',
        '5xl': '2.5rem',
      },
      boxShadow: {
        'theme-glow': '0 0 25px rgba(var(--color-glow), 0.4)',
      },
      animation: {
        'soft-up': 'softUp 0.8s ease-out forwards',
        'float-slow': 'floatSlow 6s ease-in-out infinite',
        'pulse-soft': 'pulseSoft 3s infinite',
      },
      keyframes: {
        softUp: {
          '0%': { transform: 'translateY(30px)', opacity: '0' },
          '100%': { transform: 'translateY(0)', opacity: '1' },
        },
        floatSlow: {
          '0%, 100%': { transform: 'translateY(0)' },
          '50%': { transform: 'translateY(-15px)' },
        },
        pulseSoft: {
          '0%, 100%': { opacity: '1' },
          '50%': { opacity: '0.7' },
        }
      },
      backgroundImage: {
        'theme-gradient': 'linear-gradient(to bottom right, rgb(var(--color-bg)), rgb(var(--gradient-end)))',
      }
    },
  },
  plugins: [
    require('@tailwindcss/forms'),
  ],
}
