import { useState, useCallback } from 'react';
import {
  Box,
  Button,
  TextField,
  Typography,
  Alert,
  Stack,
  IconButton,
  Tooltip,
  Snackbar,
  ToggleButton,
  ToggleButtonGroup,
} from '@mui/material';
import {
  ContentCopy as CopyIcon,
  Clear as ClearIcon,
  SwapVert as SwapIcon,
} from '@mui/icons-material';
import { ToolPageLayout } from "../../../components/ui/ToolPageLayout";

type Mode = 'encode' | 'decode';

export function Base64Page() {
  const [input, setInput] = useState('');
  const [output, setOutput] = useState('');
  const [mode, setMode] = useState<Mode>('encode');
  const [error, setError] = useState<string | null>(null);
  const [snackbarOpen, setSnackbarOpen] = useState(false);

  const handleConvert = useCallback(() => {
    if (!input.trim()) {
      setOutput('');
      setError(null);
      return;
    }

    try {
      if (mode === 'encode') {
        // 使用 TextEncoder 處理 UTF-8
        const encoder = new TextEncoder();
        const data = encoder.encode(input);
        const binary = Array.from(data)
          .map((byte) => String.fromCharCode(byte))
          .join('');
        setOutput(btoa(binary));
      } else {
        // 解碼
        const binary = atob(input);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) {
          bytes[i] = binary.charCodeAt(i);
        }
        const decoder = new TextDecoder();
        setOutput(decoder.decode(bytes));
      }
      setError(null);
    } catch (e) {
      setError(
        mode === 'decode'
          ? '無效的 Base64 字串'
          : '編碼失敗：' + (e instanceof Error ? e.message : '未知錯誤')
      );
      setOutput('');
    }
  }, [input, mode]);

  const handleModeChange = useCallback(
    (_: React.MouseEvent<HTMLElement>, newMode: Mode | null) => {
      if (newMode) {
        setMode(newMode);
        setInput('');
        setOutput('');
        setError(null);
      }
    },
    []
  );

  const handleSwap = useCallback(() => {
    if (output) {
      setInput(output);
      setOutput('');
      setMode(mode === 'encode' ? 'decode' : 'encode');
      setError(null);
    }
  }, [output, mode]);

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

  return (
    <ToolPageLayout
      title="Base64 編解碼"
      description="文字的 Base64 編碼與解碼，支援 UTF-8"
    >
      <Stack spacing={3}>
        <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center' }}>
          <ToggleButtonGroup
            value={mode}
            exclusive
            onChange={handleModeChange}
            size="small"
          >
            <ToggleButton value="encode">編碼 (Encode)</ToggleButton>
            <ToggleButton value="decode">解碼 (Decode)</ToggleButton>
          </ToggleButtonGroup>

          <Button
            variant="contained"
            onClick={handleConvert}
            sx={{ minWidth: 100 }}
          >
            {mode === 'encode' ? '編碼' : '解碼'}
          </Button>

          <Tooltip title="交換輸入輸出">
            <span>
              <IconButton onClick={handleSwap} disabled={!output} size="small">
                <SwapIcon />
              </IconButton>
            </span>
          </Tooltip>

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
              {mode === 'encode' ? '原始文字' : 'Base64 字串'}
            </Typography>
            <TextField
              multiline
              fullWidth
              minRows={8}
              maxRows={16}
              value={input}
              onChange={(e) => {
                setInput(e.target.value);
                setError(null);
              }}
              placeholder={
                mode === 'encode' ? '輸入要編碼的文字...' : '輸入 Base64 字串...'
              }
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
                {mode === 'encode' ? 'Base64 結果' : '解碼結果'}
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
              maxRows={16}
              value={output}
              InputProps={{ readOnly: true }}
              placeholder="結果將顯示在這裡"
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
