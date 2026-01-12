/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{vue,js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        stake: {
          dark: '#0f212e',
          card: '#1a2c38',
          hover: '#213743',
          text: '#b1bad3',
          blue: '#1475e1',
          green: '#00e701',
        }
      },
      fontFamily: {
        sans: ['Inter', 'sans-serif'],
      }
    },
  },
  plugins: [],
}