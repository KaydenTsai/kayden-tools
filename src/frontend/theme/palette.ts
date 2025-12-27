/**
 * MUI 調色板定義
 * 根據模式（light/dark）生成對應的調色板
 */

import type { PaletteOptions } from '@mui/material/styles';
import { colors } from './colors';

/** Light 模式調色板 */
const lightPalette: PaletteOptions = {
  mode: 'light',
  primary: {
    main: colors.primary.light.main,
    light: colors.primary.light.light,
    dark: colors.primary.light.dark,
  },
  secondary: {
    main: colors.secondary.light.main,
    light: colors.secondary.light.light,
    dark: colors.secondary.light.dark,
  },
  error: {
    main: colors.error.main,
    light: colors.error.light,
    dark: colors.error.dark,
  },
  warning: {
    main: colors.warning.main,
    light: colors.warning.light,
    dark: colors.warning.dark,
  },
  info: {
    main: colors.info.main,
    light: colors.info.light,
    dark: colors.info.dark,
  },
  success: {
    main: colors.success.main,
    light: colors.success.light,
    dark: colors.success.dark,
  },
  background: {
    default: colors.grey[50],
    paper: '#fafafa',
  },
  text: {
    primary: colors.grey[900],
    secondary: colors.grey[600],
    disabled: colors.grey[400],
  },
  divider: colors.grey[300],  // 加深框線
  action: {
    hover: colors.grey[100],
    selected: colors.grey[200],
    disabledBackground: colors.grey[100],
  },
};

/** Dark 模式調色板 */
const darkPalette: PaletteOptions = {
  mode: 'dark',
  primary: {
    main: colors.primary.dark.main,
    light: colors.primary.dark.light,
    dark: colors.primary.dark.dark,
  },
  secondary: {
    main: colors.secondary.dark.main,
    light: colors.secondary.dark.light,
    dark: colors.secondary.dark.dark,
  },
  error: {
    main: colors.error.main,
    light: colors.error.light,
    dark: colors.error.dark,
  },
  warning: {
    main: colors.warning.main,
    light: colors.warning.light,
    dark: colors.warning.dark,
  },
  info: {
    main: colors.info.main,
    light: colors.info.light,
    dark: colors.info.dark,
  },
  success: {
    main: colors.success.main,
    light: colors.success.light,
    dark: colors.success.dark,
  },
  background: {
    default: '#121212',
    paper: '#1e1e1e',
  },
  text: {
    primary: '#e0e0e0',
    secondary: '#a0a0a0',
    disabled: '#6b6b6b',
  },
  divider: 'rgba(255, 255, 255, 0.12)',
  action: {
    hover: 'rgba(255, 255, 255, 0.08)',
    selected: 'rgba(255, 255, 255, 0.16)',
    disabledBackground: 'rgba(255, 255, 255, 0.05)',
  },
};

/** 根據模式取得調色板 */
export const getPalette = (mode: 'light' | 'dark'): PaletteOptions => {
  return mode === 'light' ? lightPalette : darkPalette;
};