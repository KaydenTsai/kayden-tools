/**
 * MUI 元件樣式覆蓋
 * 集中管理所有元件的預設樣式
 */

import type { Components, Theme } from '@mui/material/styles';

export const componentOverrides: Components<Theme> = {
  MuiButton: {
    styleOverrides: {
      root: {
        textTransform: 'none',
        fontWeight: 500,
      },
    },
  },

  MuiCard: {
    styleOverrides: {
      root: ({ theme }) => ({
        boxShadow:
          theme.palette.mode === 'light'
            ? '0 2px 4px rgba(0,0,0,0.1), 0 1px 2px rgba(0,0,0,0.08)'
            : '0 1px 3px rgba(0,0,0,0.4)',
      }),
    },
  },

  MuiPaper: {
    styleOverrides: {
      root: ({ theme }) => ({
        backgroundImage: 'none', // 移除 dark mode 的預設 overlay
        ...(theme.palette.mode === 'light' && {
          boxShadow: '0 1px 3px rgba(0,0,0,0.12), 0 1px 2px rgba(0,0,0,0.06)',
        }),
      }),
    },
  },

  MuiTextField: {
    defaultProps: {
      size: 'small',
    },
  },

  MuiListItemButton: {
    styleOverrides: {
      root: ({ theme }) => ({
        borderRadius: theme.shape.borderRadius,
        '&.Mui-selected': {
          backgroundColor: theme.palette.action.selected,
        },
      }),
    },
  },

  MuiChip: {
    styleOverrides: {
      root: {
        fontWeight: 500,
      },
    },
  },

  MuiAlert: {
    styleOverrides: {
      root: {
        borderRadius: 8,
      },
    },
  },
};