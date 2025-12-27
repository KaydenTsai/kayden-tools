import { useCallback, useState } from 'react';
import {
    Box,
    Button,
    IconButton,
    Paper,
    Slider,
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

type UuidType = 'v4' | 'v7';

// UUID v4 產生器
function generateUuidV4(): string {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

// UUID v7 產生器 (時間排序)
function generateUuidV7(): string {
  const now = Date.now();
  const timestamp = now.toString(16).padStart(12, '0');
  
  const randomBits = Array.from({ length: 4 }, () =>
    Math.floor(Math.random() * 65536)
      .toString(16)
      .padStart(4, '0')
  ).join('');

  // 格式: xxxxxxxx-xxxx-7xxx-yxxx-xxxxxxxxxxxx
  const uuid = `${timestamp.slice(0, 8)}-${timestamp.slice(8, 12)}-7${randomBits.slice(0, 3)}-${(
    (parseInt(randomBits.slice(3, 4), 16) & 0x3) |
    0x8
  ).toString(16)}${randomBits.slice(4, 7)}-${randomBits.slice(7, 19)}`;

  return uuid;
}

export function UuidGeneratorPage() {
  const [uuids, setUuids] = useState<string[]>([]);
  const [uuidType, setUuidType] = useState<UuidType>('v4');
  const [count, setCount] = useState(1);
  const [uppercase, setUppercase] = useState(false);
  const [snackbarOpen, setSnackbarOpen] = useState(false);

  const generateUuids = useCallback(() => {
    const generator = uuidType === 'v4' ? generateUuidV4 : generateUuidV7;
    const newUuids = Array.from({ length: count }, () => {
      const uuid = generator();
      return uppercase ? uuid.toUpperCase() : uuid;
    });
    setUuids(newUuids);
  }, [uuidType, count, uppercase]);

  const handleCopy = useCallback(async (content: string) => {
    await navigator.clipboard.writeText(content);
    setSnackbarOpen(true);
  }, []);

  const handleCopyAll = useCallback(async () => {
    await navigator.clipboard.writeText(uuids.join('\n'));
    setSnackbarOpen(true);
  }, [uuids]);

  return (
    <ToolPageLayout
      title="UUID 產生器"
      description="產生 UUID v4（隨機）、v7（時間排序）"
    >
      <Stack spacing={3}>
        <Paper variant="outlined" sx={{ p: 2 }}>
          <Stack spacing={3}>
            <Box>
              <Typography variant="subtitle2" sx={{ mb: 1, fontWeight: 600 }}>
                UUID 版本
              </Typography>
              <ToggleButtonGroup
                value={uuidType}
                exclusive
                onChange={(_, newType) => newType && setUuidType(newType)}
                size="small"
              >
                <ToggleButton value="v4">
                  <Box sx={{ textAlign: 'left' }}>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      v4
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      隨機
                    </Typography>
                  </Box>
                </ToggleButton>
                <ToggleButton value="v7">
                  <Box sx={{ textAlign: 'left' }}>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      v7
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      時間排序
                    </Typography>
                  </Box>
                </ToggleButton>
              </ToggleButtonGroup>
            </Box>

            <Box>
              <Typography variant="subtitle2" sx={{ mb: 1, fontWeight: 600 }}>
                產生數量：{count}
              </Typography>
              <Slider
                value={count}
                onChange={(_, value) => setCount(value as number)}
                min={1}
                max={20}
                marks={[
                  { value: 1, label: '1' },
                  { value: 5, label: '5' },
                  { value: 10, label: '10' },
                  { value: 20, label: '20' },
                ]}
                sx={{ maxWidth: 300 }}
              />
            </Box>

            <Box>
              <ToggleButtonGroup
                value={uppercase ? 'upper' : 'lower'}
                exclusive
                onChange={(_, value) => setUppercase(value === 'upper')}
                size="small"
              >
                <ToggleButton value="lower">小寫</ToggleButton>
                <ToggleButton value="upper">大寫</ToggleButton>
              </ToggleButtonGroup>
            </Box>

            <Box>
              <Button
                variant="contained"
                onClick={generateUuids}
                startIcon={<RefreshIcon />}
                size="large"
              >
                產生 UUID
              </Button>
            </Box>
          </Stack>
        </Paper>

        {uuids.length > 0 && (
          <Paper variant="outlined" sx={{ p: 2 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 600, flex: 1 }}>
                產生結果
              </Typography>
              {uuids.length > 1 && (
                <Button size="small" startIcon={<CopyIcon />} onClick={handleCopyAll}>
                  全部複製
                </Button>
              )}
            </Box>
            <Stack spacing={1}>
              {uuids.map((uuid, index) => (
                <Box
                  key={index}
                  sx={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 1,
                    p: 1.5,
                    bgcolor: 'action.disabledBackground',
                    borderRadius: 1,
                  }}
                >
                  <TextField
                    fullWidth
                    size="small"
                    value={uuid}
                    InputProps={{ readOnly: true }}
                    sx={{
                      '& input': {
                        fontFamily: 'monospace',
                        fontSize: '0.9rem',
                      },
                      '& .MuiOutlinedInput-notchedOutline': {
                        border: 'none',
                      },
                    }}
                  />
                  <Tooltip title="複製">
                    <IconButton size="small" onClick={() => handleCopy(uuid)}>
                      <CopyIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </Box>
              ))}
            </Stack>
          </Paper>
        )}

        <Paper variant="outlined" sx={{ p: 2 }}>
          <Typography variant="subtitle2" sx={{ mb: 1.5, fontWeight: 600 }}>
            UUID 版本說明
          </Typography>
          <Stack spacing={1.5}>
            <Box>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                UUID v4
              </Typography>
              <Typography variant="body2" color="text.secondary">
                完全隨機產生，最常用的版本。適合大多數場景，碰撞機率極低。
              </Typography>
            </Box>
            <Box>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                UUID v7
              </Typography>
              <Typography variant="body2" color="text.secondary">
                基於時間戳的 UUID，可排序。適合需要時間順序的場景，如資料庫主鍵。
              </Typography>
            </Box>
          </Stack>
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
