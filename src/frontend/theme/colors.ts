/**
 * 應用程式顏色常量定義
 * 所有顏色的單一來源，方便維護與擴充主題
 */

export const colors = {
  // 主色系
  primary: {
    light: {
      main: '#2563eb',
      light: '#3b82f6',
      dark: '#1d4ed8',
    },
    dark: {
      main: 'rgba(0, 229, 255, 0.6)',
      light: 'rgba(0, 229, 255, 0.8)',
      dark: 'rgba(0, 229, 255, 0.4)',
    },
    // 共用色階
    50: '#eff6ff',
    100: '#dbeafe',
  },

  // 輔助色
  secondary: {
    light: {
      main: '#7c3aed',
      light: '#8b5cf6',
      dark: '#6d28d9',
    },
    dark: {
      main: '#a78bfa',
      light: '#c4b5fd',
      dark: '#8b5cf6',
    },
  },

  // 語義色
  error: {
    main: '#ef4444',
    light: '#fca5a5',
    dark: '#dc2626',
    50: '#fef2f2',
  },

  success: {
    main: '#22c55e',
    light: '#86efac',
    dark: '#16a34a',
    50: '#f0fdf4',
  },

  warning: {
    main: '#f59e0b',
    light: '#fcd34d',
    dark: '#d97706',
    50: '#fffbeb',
  },

  info: {
    main: '#3b82f6',
    light: '#93c5fd',
    dark: '#2563eb',
    50: '#eff6ff',
  },

  // 基礎色
  common: {
    white: '#ffffff',
    black: '#000000',
  },

  // 灰階 (Tailwind Slate)
  grey: {
    50: '#f8fafc',
    100: '#f1f5f9',
    200: '#e2e8f0',
    300: '#cbd5e1',
    400: '#94a3b8',
    500: '#64748b',
    600: '#475569',
    700: '#334155',
    800: '#1e293b',
    900: '#0f172a',
  },

  // 業務相關顏色
  category: {
    dev: '#3b82f6',
    daily: '#f97316',
  },
} as const;

export type Colors = typeof colors;