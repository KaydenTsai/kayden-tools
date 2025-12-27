/**
 * MUI 主題設定
 * 整合顏色、調色板、元件樣式
 */

import { createTheme, type Theme } from '@mui/material/styles';
import { getPalette } from './palette';
import { componentOverrides } from './components';

// 匯出顏色常量供元件使用
export { colors } from './colors';

/** 共用的主題設定 */
const baseTheme = {
  typography: {
    fontFamily: [
      'Inter',
      '-apple-system',
      'BlinkMacSystemFont',
      '"Segoe UI"',
      'Roboto',
      '"Helvetica Neue"',
      'Arial',
      'sans-serif',
    ].join(','),
    h1: {
      fontSize: '2.5rem',
      fontWeight: 700,
    },
    h2: {
      fontSize: '2rem',
      fontWeight: 600,
    },
    h3: {
      fontSize: '1.5rem',
      fontWeight: 600,
    },
  },
  shape: {
    borderRadius: 12,
  },
};

/** 根據模式取得主題 */
export const getTheme = (mode: 'light' | 'dark'): Theme => {
  return createTheme({
    ...baseTheme,
    palette: getPalette(mode),
    components: componentOverrides,
  });
};

// 向後相容：保留原有的 lightTheme 和 darkTheme 匯出
export const lightTheme = getTheme('light');
export const darkTheme = getTheme('dark');
