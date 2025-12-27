import { useState, useCallback } from 'react';
import {
  Box,
  Button,
  ButtonGroup,
  TextField,
  Typography,
  Alert,
  Stack,
  IconButton,
  Tooltip,
  Snackbar,
} from '@mui/material';
import {
  ContentCopy as CopyIcon,
  Clear as ClearIcon,
  UnfoldMore as ExpandIcon,
  UnfoldLess as CompressIcon,
} from '@mui/icons-material';
import { ToolPageLayout } from "../../../components/ui/ToolPageLayout";

export function JsonFormatterPage() {
  const [input, setInput] = useState('');
  const [output, setOutput] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [indentSize, setIndentSize] = useState(2);
  const [snackbarOpen, setSnackbarOpen] = useState(false);

  const formatJson = useCallback(() => {
    if (!input.trim()) {
      setOutput('');
      setError(null);
      return;
    }

    try {
      const parsed = JSON.parse(input);
      setOutput(JSON.stringify(parsed, null, indentSize));
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : '無效的 JSON 格式');
      setOutput('');
    }
  }, [input, indentSize]);

  const minifyJson = useCallback(() => {
    if (!input.trim()) {
      setOutput('');
      setError(null);
      return;
    }

    try {
      const parsed = JSON.parse(input);
      setOutput(JSON.stringify(parsed));
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : '無效的 JSON 格式');
      setOutput('');
    }
  }, [input]);

  const handleCopy = useCallback(async () => {
    if (output) {
      await navigator.clipboard.writeText(output);
      setSnackbarOpen(true);
    }
  }, [output]);

  const handleClear = useCallback(() => {
    setInput('');
    setOutput('');
    setError(null);
  }, []);

  const handleInputChange = useCallback((e: React.ChangeEvent<HTMLTextAreaElement>) => {
    setInput(e.target.value);
    setError(null);
  }, []);

  return (
    <ToolPageLayout
      title="JSON Formatter"
      description="格式化、驗證、壓縮 JSON 資料"
    >
      <Stack spacing={3}>
        <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center' }}>
          <ButtonGroup variant="contained" size="small">
            <Button onClick={formatJson} startIcon={<ExpandIcon />}>
              格式化
            </Button>
            <Button onClick={minifyJson} startIcon={<CompressIcon />}>
              壓縮
            </Button>
          </ButtonGroup>

          <ButtonGroup variant="outlined" size="small">
            {[2, 4].map((size) => (
              <Button
                key={size}
                onClick={() => setIndentSize(size)}
                variant={indentSize === size ? 'contained' : 'outlined'}
              >
                {size} 空格
              </Button>
            ))}
          </ButtonGroup>

          <Box sx={{ flex: 1 }} />

          <Tooltip title="清除">
            <IconButton onClick={handleClear} size="small">
              <ClearIcon />
            </IconButton>
          </Tooltip>
        </Box>

        {error && (
          <Alert severity="error" variant="outlined">
            {error}
          </Alert>
        )}

        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' },
            gap: 2,
          }}
        >
          <Box>
            <Typography variant="subtitle2" sx={{ mb: 1, fontWeight: 600 }}>
              輸入
            </Typography>
            <TextField
              multiline
              fullWidth
              minRows={8}
              maxRows={20}
              value={input}
              onChange={handleInputChange}
              placeholder='{"key": "value"}'
              sx={{
                '& .MuiInputBase-root': {
                  fontFamily: 'monospace',
                  fontSize: '0.875rem',
                },
              }}
            />
          </Box>

          <Box>
            <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 600, flex: 1 }}>
                輸出
              </Typography>
              {output && (
                <Tooltip title="複製">
                  <IconButton size="small" onClick={handleCopy}>
                    <CopyIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              )}
            </Box>
            <TextField
              multiline
              fullWidth
              minRows={8}
              maxRows={20}
              value={output}
              InputProps={{ readOnly: true }}
              placeholder="格式化結果將顯示在這裡"
              sx={{
                '& .MuiInputBase-root': {
                  fontFamily: 'monospace',
                  fontSize: '0.875rem',
                  bgcolor: 'action.disabledBackground',
                },
              }}
            />
          </Box>
        </Box>
      </Stack>

      <Snackbar
        open={snackbarOpen}
        autoHideDuration={2000}
        onClose={() => setSnackbarOpen(false)}
        message="已複製到剪貼簿"
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      />
    </ToolPageLayout>
  );
}
