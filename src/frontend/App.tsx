import { RouterProvider } from 'react-router-dom';
import { ThemeProvider, CssBaseline } from '@mui/material';
import { router } from './router';
import { useThemeStore } from '@/stores/themeStore';
import { lightTheme, darkTheme } from '@/theme';

function App() {
  const resolvedMode = useThemeStore((state) => state.resolvedMode);
  const theme = resolvedMode === 'light' ? lightTheme : darkTheme;

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <RouterProvider router={router} />
    </ThemeProvider>
  );
}

export default App;
