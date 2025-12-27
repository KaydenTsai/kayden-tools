import { useCallback, useMemo, useState } from 'react';
import { Alert, Box, Chip, IconButton, Paper, Snackbar, Stack, TextField, Tooltip, Typography, } from '@mui/material';
import { Clear as ClearIcon, ContentCopy as CopyIcon, } from '@mui/icons-material';
import { ToolPageLayout } from "../../../components/ui/ToolPageLayout";

interface DecodedJwt {
  header: Record<string, unknown>;
  payload: Record<string, unknown>;
  signature: string;
}

function decodeBase64Url(str: string): string {
  // 將 Base64URL 轉換為標準 Base64
  let base64 = str.replace(/-/g, '+').replace(/_/g, '/');
  // 補齊 padding
  const padding = base64.length % 4;
  if (padding) {
    base64 += '='.repeat(4 - padding);
  }
  return atob(base64);
}

function formatTimestamp(timestamp: number): string {
  const date = new Date(timestamp * 1000);
  return date.toLocaleString('zh-TW', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  });
}

export function JwtDecoderPage() {
  const [input, setInput] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [snackbarOpen, setSnackbarOpen] = useState(false);
  const [snackbarMessage, setSnackbarMessage] = useState('');

  const decoded = useMemo<DecodedJwt | null>(() => {
    if (!input.trim()) return null;

    try {
      const parts = input.trim().split('.');
      if (parts.length !== 3) {
        throw new Error('JWT 應該包含三個部分（header.payload.signature）');
      }

      const [headerB64, payloadB64, signature] = parts;

      const header = JSON.parse(decodeBase64Url(headerB64));
      const payload = JSON.parse(decodeBase64Url(payloadB64));

      setError(null);
      return { header, payload, signature };
    } catch (e) {
      setError(e instanceof Error ? e.message : '無效的 JWT 格式');
      return null;
    }
  }, [input]);

  const tokenStatus = useMemo(() => {
    if (!decoded?.payload) return null;

    const now = Math.floor(Date.now() / 1000);
    const exp = decoded.payload.exp as number | undefined;
    const iat = decoded.payload.iat as number | undefined;
    const nbf = decoded.payload.nbf as number | undefined;

    if (exp && exp < now) {
      return { status: 'expired', message: '已過期', color: 'error' as const };
    }
    if (nbf && nbf > now) {
      return { status: 'not_active', message: '尚未生效', color: 'warning' as const };
    }
    if (exp) {
      const remaining = exp - now;
      const hours = Math.floor(remaining / 3600);
      const minutes = Math.floor((remaining % 3600) / 60);
      return {
        status: 'valid',
        message: `有效（剩餘 ${hours}h ${minutes}m）`,
        color: 'success' as const,
      };
    }
    if (iat) {
      return { status: 'no_exp', message: '無過期時間', color: 'info' as const };
    }
    return null;
  }, [decoded]);

  const handleCopy = useCallback(async (content: string, label: string) => {
    await navigator.clipboard.writeText(content);
    setSnackbarMessage(`已複製 ${label}`);
    setSnackbarOpen(true);
  }, []);

  const handleClear = useCallback(() => {
    setInput('');
    setError(null);
  }, []);

  const renderJsonSection = (
    title: string,
    data: Record<string, unknown>,
    copyLabel: string
  ) => (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', mb: 1.5 }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 600, flex: 1 }}>
          {title}
        </Typography>
        <Tooltip title="複製">
          <IconButton
            size="small"
            onClick={() => handleCopy(JSON.stringify(data, null, 2), copyLabel)}
          >
            <CopyIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Box>
      <Box
        component="pre"
        sx={{
          m: 0,
          p: 2,
          bgcolor: 'action.disabledBackground',
          borderRadius: 1,
          overflow: 'auto',
          fontSize: '0.875rem',
          fontFamily: 'monospace',
        }}
      >
        {JSON.stringify(data, null, 2)}
      </Box>
    </Paper>
  );

  return (
    <ToolPageLayout
      title="JWT Decoder"
      description="解析 JWT Token，顯示 Header、Payload 和過期時間"
    >
      <Stack spacing={3}>
        <Box>
          <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600, flex: 1 }}>
              JWT Token
            </Typography>
            <Tooltip title="清除">
              <IconButton onClick={handleClear} size="small">
                <ClearIcon />
              </IconButton>
            </Tooltip>
          </Box>
          <TextField
            fullWidth
            multiline
            minRows={3}
            maxRows={6}
            value={input}
            onChange={(e) => setInput(e.target.value)}
            placeholder="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c"
            sx={{
              '& .MuiInputBase-root': {
                fontFamily: 'monospace',
                fontSize: '0.875rem',
              },
            }}
          />
        </Box>

        {error && (
          <Alert severity="error" variant="outlined">
            {error}
          </Alert>
        )}

        {decoded && (
          <Stack spacing={2}>
            {tokenStatus && (
              <Alert severity={tokenStatus.color} variant="outlined">
                Token 狀態：{tokenStatus.message}
                {typeof decoded.payload.exp === 'number' && (
                  <Typography variant="body2" sx={{ mt: 0.5 }}>
                    過期時間：{formatTimestamp(decoded.payload.exp)}
                  </Typography>
                )}
              </Alert>
            )}

            {renderJsonSection('Header', decoded.header, 'Header')}

            <Paper variant="outlined" sx={{ p: 2 }}>
              <Box sx={{ display: 'flex', alignItems: 'center', mb: 1.5 }}>
                <Typography variant="subtitle2" sx={{ fontWeight: 600, flex: 1 }}>
                  Payload
                </Typography>
                <Tooltip title="複製">
                  <IconButton
                    size="small"
                    onClick={() =>
                      handleCopy(JSON.stringify(decoded.payload, null, 2), 'Payload')
                    }
                  >
                    <CopyIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              </Box>

              <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mb: 2 }}>
                {decoded.payload.sub != null && (
                  <Chip
                    label={`sub: ${String(decoded.payload.sub)}`}
                    size="small"
                    variant="outlined"
                  />
                )}
                {decoded.payload.iss != null && (
                  <Chip
                    label={`iss: ${String(decoded.payload.iss)}`}
                    size="small"
                    variant="outlined"
                  />
                )}
                {decoded.payload.aud != null && (
                  <Chip
                    label={`aud: ${String(decoded.payload.aud)}`}
                    size="small"
                    variant="outlined"
                  />
                )}
              </Box>

              <Box
                component="pre"
                sx={{
                  m: 0,
                  p: 2,
                  bgcolor: 'action.disabledBackground',
                  borderRadius: 1,
                  overflow: 'auto',
                  fontSize: '0.875rem',
                  fontFamily: 'monospace',
                }}
              >
                {JSON.stringify(decoded.payload, null, 2)}
              </Box>
            </Paper>

            <Paper variant="outlined" sx={{ p: 2 }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1.5 }}>
                Signature
              </Typography>
              <Typography
                variant="body2"
                sx={{
                  fontFamily: 'monospace',
                  wordBreak: 'break-all',
                  color: 'text.secondary',
                }}
              >
                {decoded.signature}
              </Typography>
            </Paper>
          </Stack>
        )}
      </Stack>

      <Snackbar
        open={snackbarOpen}
        autoHideDuration={2000}
        onClose={() => setSnackbarOpen(false)}
        message={snackbarMessage}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      />
    </ToolPageLayout>
  );
}
