import { useCallback, useEffect, useState } from 'react';
import {
    Box,
    Button,
    IconButton,
    Paper,
    Snackbar,
    Stack,
    TextField,
    ToggleButton,
    ToggleButtonGroup,
    Tooltip,
    Typography,
} from '@mui/material';
import { ContentCopy as CopyIcon, Refresh as RefreshIcon, } from '@mui/icons-material';
import { ToolPageLayout } from "../../../components/ui/ToolPageLayout";

type Unit = 'seconds' | 'milliseconds';

export function TimestampPage() {
  const [currentTimestamp, setCurrentTimestamp] = useState(Math.floor(Date.now() / 1000));
  const [timestampInput, setTimestampInput] = useState('');
  const [dateInput, setDateInput] = useState('');
  const [unit, setUnit] = useState<Unit>('seconds');
  const [snackbarOpen, setSnackbarOpen] = useState(false);

  // 更新當前時間戳
  useEffect(() => {
    const interval = setInterval(() => {
      setCurrentTimestamp(Math.floor(Date.now() / 1000));
    }, 1000);
    return () => clearInterval(interval);
  }, []);

  const timestampToDate = useCallback(
    (ts: string): string => {
      if (!ts.trim()) return '';
      const num = parseInt(ts, 10);
      if (isNaN(num)) return '無效的時間戳';

      const ms = unit === 'seconds' ? num * 1000 : num;
      const date = new Date(ms);

      if (isNaN(date.getTime())) return '無效的時間戳';

      return date.toLocaleString('zh-TW', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false,
        timeZoneName: 'short',
      });
    },
    [unit]
  );

  const dateToTimestamp = useCallback(
    (dateStr: string): string => {
      if (!dateStr.trim()) return '';
      const date = new Date(dateStr);
      if (isNaN(date.getTime())) return '無效的日期';

      const ts = date.getTime();
      return unit === 'seconds' ? Math.floor(ts / 1000).toString() : ts.toString();
    },
    [unit]
  );

  const handleCopy = useCallback(async (content: string) => {
    await navigator.clipboard.writeText(content);
    setSnackbarOpen(true);
  }, []);

  const handleUseCurrentTimestamp = useCallback(() => {
    const ts = unit === 'seconds' ? currentTimestamp : currentTimestamp * 1000;
    setTimestampInput(ts.toString());
  }, [currentTimestamp, unit]);

  const handleUseCurrentDate = useCallback(() => {
    const now = new Date();
    // 格式化為 datetime-local 格式
    const formatted = now.toISOString().slice(0, 16);
    setDateInput(formatted);
  }, []);

  const convertedDate = timestampToDate(timestampInput);
  const convertedTimestamp = dateToTimestamp(dateInput);

  return (
    <ToolPageLayout
      title="時間戳轉換"
      description="Unix Timestamp 與日期時間互相轉換"
    >
      <Stack spacing={3}>
        <Paper
          variant="outlined"
          sx={{
            p: 2,
            display: 'flex',
            alignItems: 'center',
            gap: 2,
            bgcolor: 'action.disabledBackground',
          }}
        >
          <Box sx={{ flex: 1 }}>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 0.5 }}>
              當前 Unix 時間戳
            </Typography>
            <Typography
              variant="h5"
              sx={{ fontFamily: 'monospace', fontWeight: 600 }}
            >
              {unit === 'seconds' ? currentTimestamp : currentTimestamp * 1000}
            </Typography>
          </Box>
          <Tooltip title="複製">
            <IconButton
              onClick={() =>
                handleCopy(
                  unit === 'seconds'
                    ? currentTimestamp.toString()
                    : (currentTimestamp * 1000).toString()
                )
              }
            >
              <CopyIcon />
            </IconButton>
          </Tooltip>
        </Paper>

        <Box>
          <Typography variant="subtitle2" sx={{ mb: 1, fontWeight: 600 }}>
            時間戳單位
          </Typography>
          <ToggleButtonGroup
            value={unit}
            exclusive
            onChange={(_, newUnit) => newUnit && setUnit(newUnit)}
            size="small"
          >
            <ToggleButton value="seconds">秒 (s)</ToggleButton>
            <ToggleButton value="milliseconds">毫秒 (ms)</ToggleButton>
          </ToggleButtonGroup>
        </Box>

        <Paper variant="outlined" sx={{ p: 2 }}>
          <Typography variant="subtitle2" sx={{ mb: 2, fontWeight: 600 }}>
            時間戳 → 日期
          </Typography>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: { xs: '1fr', md: '1fr auto 1fr' },
              gap: 2,
              alignItems: 'start',
            }}
          >
            <Box>
              <Box sx={{ display: 'flex', gap: 1, mb: 1 }}>
                <TextField
                  fullWidth
                  size="small"
                  value={timestampInput}
                  onChange={(e) => setTimestampInput(e.target.value)}
                  placeholder={unit === 'seconds' ? '1703001600' : '1703001600000'}
                  sx={{ '& input': { fontFamily: 'monospace' } }}
                />
                <Tooltip title="使用當前時間戳">
                  <IconButton onClick={handleUseCurrentTimestamp} size="small">
                    <RefreshIcon />
                  </IconButton>
                </Tooltip>
              </Box>
            </Box>

            <Typography
              sx={{
                alignSelf: 'center',
                color: 'text.secondary',
                display: { xs: 'none', md: 'block' },
              }}
            >
              →
            </Typography>

            <Box>
              <TextField
                fullWidth
                size="small"
                value={convertedDate}
                InputProps={{ readOnly: true }}
                placeholder="轉換結果"
                sx={{
                  '& input': { fontFamily: 'monospace' },
                  '& .MuiInputBase-root': { bgcolor: 'action.disabledBackground' },
                }}
              />
              {convertedDate && !convertedDate.includes('無效') && (
                <Box sx={{ mt: 1, display: 'flex', justifyContent: 'flex-end' }}>
                  <Button
                    size="small"
                    startIcon={<CopyIcon />}
                    onClick={() => handleCopy(convertedDate)}
                  >
                    複製
                  </Button>
                </Box>
              )}
            </Box>
          </Box>
        </Paper>

        <Paper variant="outlined" sx={{ p: 2 }}>
          <Typography variant="subtitle2" sx={{ mb: 2, fontWeight: 600 }}>
            日期 → 時間戳
          </Typography>
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: { xs: '1fr', md: '1fr auto 1fr' },
              gap: 2,
              alignItems: 'start',
            }}
          >
            <Box>
              <Box sx={{ display: 'flex', gap: 1, mb: 1 }}>
                <TextField
                  fullWidth
                  size="small"
                  type="datetime-local"
                  value={dateInput}
                  onChange={(e) => setDateInput(e.target.value)}
                />
                <Tooltip title="使用當前時間">
                  <IconButton onClick={handleUseCurrentDate} size="small">
                    <RefreshIcon />
                  </IconButton>
                </Tooltip>
              </Box>
            </Box>

            <Typography
              sx={{
                alignSelf: 'center',
                color: 'text.secondary',
                display: { xs: 'none', md: 'block' },
              }}
            >
              →
            </Typography>

            <Box>
              <TextField
                fullWidth
                size="small"
                value={convertedTimestamp}
                InputProps={{ readOnly: true }}
                placeholder="轉換結果"
                sx={{
                  '& input': { fontFamily: 'monospace' },
                  '& .MuiInputBase-root': { bgcolor: 'action.disabledBackground' },
                }}
              />
              {convertedTimestamp && !convertedTimestamp.includes('無效') && (
                <Box sx={{ mt: 1, display: 'flex', justifyContent: 'flex-end' }}>
                  <Button
                    size="small"
                    startIcon={<CopyIcon />}
                    onClick={() => handleCopy(convertedTimestamp)}
                  >
                    複製
                  </Button>
                </Box>
              )}
            </Box>
          </Box>
        </Paper>
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
