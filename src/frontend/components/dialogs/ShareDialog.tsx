import { useState, useEffect, useRef } from 'react';
import {
    Dialog,
    DialogTitle,
    DialogContent,
    DialogActions,
    Button,
    Box,
    Typography,
    TextField,
    IconButton,
    Tabs,
    Tab,
    Alert,
    Snackbar,
    CircularProgress,
} from '@mui/material';
import {
    ContentCopy as CopyIcon,
    Link as LinkIcon,
    Cloud as CloudIcon,
    QrCode as QrCodeIcon,
    Refresh as RetryIcon,
} from '@mui/icons-material';
import { SlideTransition } from '@/components/ui/SlideTransition';
import { encodeBillToUrl } from '@/utils/shareUrl';
import { useBillSync } from '@/hooks/useBillSync';
import { useSnapSplitStore } from '@/stores/snapSplitStore';

interface ShareDialogProps {
    billId: string;
    open: boolean;
    onClose: () => void;
    isAuthenticated?: boolean;
}

interface TabPanelProps {
    children?: React.ReactNode;
    index: number;
    value: number;
}

function TabPanel({ children, value, index }: TabPanelProps) {
    return (
        <Box hidden={value !== index} sx={{ pt: 2 }}>
            {value === index && children}
        </Box>
    );
}

export function ShareDialog({
    billId,
    open,
    onClose,
    isAuthenticated = false,
}: ShareDialogProps) {
    // 從 store 直接讀取，確保同步後能即時反映最新狀態
    const bill = useSnapSplitStore(state => state.bills.find(b => b.id === billId));

    const [tabValue, setTabValue] = useState(isAuthenticated ? 1 : 0);
    const [snackbarOpen, setSnackbarOpen] = useState(false);
    const [snackbarMessage, setSnackbarMessage] = useState('');
    const [syncError, setSyncError] = useState<string | null>(null);

    const { syncBill, isUploading } = useBillSync();
    const hasSyncedRef = useRef(false);

    // 帳單不存在時不渲染
    if (!bill) return null;

    const { url: snapshotUrl, isLong } = encodeBillToUrl(bill);
    const cloudShareUrl = bill.shareCode
        ? `${window.location.origin}${window.location.pathname}#/snap-split/share/${bill.shareCode}`
        : null;

    // 自動同步：當雲端分享 tab 被選中且帳單尚未同步時
    const needsSync = isAuthenticated && !bill.shareCode && bill.syncStatus !== 'syncing';

    useEffect(() => {
        if (open && tabValue === 1 && needsSync && !hasSyncedRef.current && !isUploading) {
            hasSyncedRef.current = true;
            setSyncError(null);
            syncBill(bill).catch((err) => {
                setSyncError(err instanceof Error ? err.message : '同步失敗');
            });
        }
    }, [open, tabValue, needsSync, isUploading, bill, syncBill]);

    // 重置 ref 當 dialog 關閉時
    useEffect(() => {
        if (!open) {
            hasSyncedRef.current = false;
            setSyncError(null);
        }
    }, [open]);

    const handleCopy = async (text: string, label: string) => {
        try {
            await navigator.clipboard.writeText(text);
            setSnackbarMessage(`${label}已複製到剪貼簿`);
            setSnackbarOpen(true);
        } catch {
            setSnackbarMessage('複製失敗');
            setSnackbarOpen(true);
        }
    };

    const handleRetrySync = async () => {
        setSyncError(null);
        try {
            await syncBill(bill);
        } catch (err) {
            setSyncError(err instanceof Error ? err.message : '同步失敗');
        }
    };

    return (
        <>
            <Dialog
                open={open}
                onClose={onClose}
                maxWidth="sm"
                fullWidth
                TransitionComponent={SlideTransition}
            >
                <DialogTitle sx={{ pb: 1 }}>
                    分享帳單
                </DialogTitle>

                <DialogContent>
                    <Tabs
                        value={tabValue}
                        onChange={(_, v) => setTabValue(v)}
                        sx={{ borderBottom: 1, borderColor: 'divider' }}
                    >
                        <Tab
                            icon={<LinkIcon />}
                            iconPosition="start"
                            label="快照連結"
                        />
                        <Tab
                            icon={<CloudIcon />}
                            iconPosition="start"
                            label="雲端分享"
                            disabled={!isAuthenticated}
                        />
                    </Tabs>

                    <TabPanel value={tabValue} index={0}>
                        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                            產生一個包含帳單資料的連結，對方打開連結即可查看當前帳單快照。
                        </Typography>

                        {isLong && (
                            <Alert severity="warning" sx={{ mb: 2 }}>
                                連結較長，部分平台可能無法正確分享。建議使用雲端分享功能。
                            </Alert>
                        )}

                        <TextField
                            fullWidth
                            value={snapshotUrl}
                            InputProps={{
                                readOnly: true,
                                endAdornment: (
                                    <IconButton
                                        onClick={() => handleCopy(snapshotUrl, '連結')}
                                        edge="end"
                                    >
                                        <CopyIcon />
                                    </IconButton>
                                ),
                            }}
                            sx={{ mb: 2 }}
                        />

                        <Alert severity="info" icon={<QrCodeIcon />}>
                            這是靜態快照，對方看到的是分享當下的資料。如需即時同步，請使用雲端分享。
                        </Alert>
                    </TabPanel>

                    <TabPanel value={tabValue} index={1}>
                        {!isAuthenticated ? (
                            <Alert severity="info">
                                請先登入以使用雲端分享功能
                            </Alert>
                        ) : bill.shareCode ? (
                            <>
                                <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                                    使用分享碼或連結分享帳單，對方打開後可查看最新資料。
                                </Typography>

                                <Box sx={{ mb: 3 }}>
                                    <Typography variant="caption" color="text.secondary">
                                        分享碼
                                    </Typography>
                                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                                        <Typography
                                            variant="h4"
                                            sx={{
                                                fontFamily: 'monospace',
                                                fontWeight: 700,
                                                letterSpacing: 2,
                                            }}
                                        >
                                            {bill.shareCode}
                                        </Typography>
                                        <IconButton
                                            onClick={() => handleCopy(bill.shareCode!, '分享碼')}
                                            size="small"
                                        >
                                            <CopyIcon />
                                        </IconButton>
                                    </Box>
                                </Box>

                                <TextField
                                    fullWidth
                                    label="分享連結"
                                    value={cloudShareUrl}
                                    InputProps={{
                                        readOnly: true,
                                        endAdornment: (
                                            <IconButton
                                                onClick={() => handleCopy(cloudShareUrl!, '連結')}
                                                edge="end"
                                            >
                                                <CopyIcon />
                                            </IconButton>
                                        ),
                                    }}
                                    sx={{ mb: 2 }}
                                />

                                <Alert severity="success">
                                    雲端分享會顯示最新資料，你對帳單的修改會自動同步。
                                </Alert>
                            </>
                        ) : syncError ? (
                            // 同步失敗 - 顯示重試按鈕
                            <>
                                <Alert severity="error" sx={{ mb: 2 }}>
                                    {syncError}
                                </Alert>
                                <Box sx={{ textAlign: 'center', py: 2 }}>
                                    <Button
                                        variant="contained"
                                        size="large"
                                        startIcon={isUploading ? <CircularProgress size={20} color="inherit" /> : <RetryIcon />}
                                        onClick={handleRetrySync}
                                        disabled={isUploading}
                                    >
                                        {isUploading ? '同步中...' : '重試'}
                                    </Button>
                                </Box>
                            </>
                        ) : (
                            // 同步中 - 顯示 loading
                            <Box sx={{ textAlign: 'center', py: 4 }}>
                                <CircularProgress size={40} sx={{ mb: 2 }} />
                                <Typography color="text.secondary">
                                    正在同步帳單到雲端...
                                </Typography>
                            </Box>
                        )}
                    </TabPanel>
                </DialogContent>

                <DialogActions sx={{ p: 2, pt: 1 }}>
                    <Button onClick={onClose} variant="contained" size="large">
                        關閉
                    </Button>
                </DialogActions>
            </Dialog>

            <Snackbar
                open={snackbarOpen}
                autoHideDuration={2000}
                onClose={() => setSnackbarOpen(false)}
                message={snackbarMessage}
            />
        </>
    );
}
