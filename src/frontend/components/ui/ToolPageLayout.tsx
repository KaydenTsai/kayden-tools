import type { ReactNode } from 'react';
import { Box, Typography, Paper } from '@mui/material';

interface ToolPageLayoutProps {
  title: string;
  description?: string;
  children: ReactNode;
  disablePaperWrapper?: boolean;
}

export function ToolPageLayout({ title, description, children, disablePaperWrapper = false }: ToolPageLayoutProps) {
  return (
    <Box>
      <Box sx={{ mb: { xs: 2, md: 3 } }}>
        <Typography
          variant="h4"
          component="h1"
          sx={{
            fontWeight: 700,
            mb: 0.5,
            fontSize: { xs: '1.5rem', md: '2.125rem' },
          }}
        >
          {title}
        </Typography>
        {description && (
          <Typography variant="body2" color="text.secondary">
            {description}
          </Typography>
        )}
      </Box>
      {disablePaperWrapper ? (
        children
      ) : (
        <Paper
          sx={{
            p: { xs: 2, md: 3 },
            borderRadius: { xs: 2, md: 3 },
          }}
        >
          {children}
        </Paper>
      )}
    </Box>
  );
}
