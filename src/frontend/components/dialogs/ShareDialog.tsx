import { useState } from 'react';
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
} from '@mui/icons-material';
import { SlideTransition } from '@/components/ui/SlideTransition';
import { SyncStatusIndicator } from '@/components/ui/SyncStatusIndicator';
import { encodeBillToUrl } from '@/utils/shareUrl';
import { useBillSync } from '@/hooks/useBillSync';
import type { Bill } from '@/types/snap-split';

interface ShareDialogProps {
    bill: Bill;
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
    bill,
    open,
    onClose,
    isAuthenticated = false,
}: ShareDialogProps) {
    const [tabValue, setTabValue] = useState(isAuthenticated ? 1 : 0);
    const [snackbarOpen, setSnackbarOpen] = useState(false);
    const [snackbarMessage, setSnackbarMessage] = useState('');

    const { syncBill, isUploading } = useBillSync();

    const { url: snapshotUrl, isLong } = encodeBillToUrl(bill);
    const cloudShareUrl = bill.shareCode
        ? `${window.location.origin}${window.location.pathname}#/snap-split/share/${bill.shareCode}`
        : null;

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

    const handleSyncAndShare = async () => {
        try {
            await syncBill(bill);
            setSnackbarMessage('同步成功！分享碼已產生');
            setSnackbarOpen(true);
        } catch {
            setSnackbarMessage('同步失敗，請稍後重試');
            setSnackbarOpen(true);
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
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                        分享帳單
                        <SyncStatusIndicator status={bill.syncStatus} size="small" />
                    </Box>
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
                        ) : (
                            <>
                                <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                                    將帳單上傳到雲端以獲得分享碼，對方可透過分享碼查看最新資料。
                                </Typography>

                                <Box sx={{ textAlign: 'center', py: 3 }}>
                                    <Button
                                        variant="contained"
                                        size="large"
                                        startIcon={isUploading ? <CircularProgress size={20} color="inherit" /> : <CloudIcon />}
                                        onClick={handleSyncAndShare}
                                        disabled={isUploading}
                                        sx={{ px: 4 }}
                                    >
                                        {isUploading ? '上傳中...' : '上傳並產生分享碼'}
                                    </Button>
                                </Box>

                                <Alert severity="info">
                                    上傳後，你可以在任何裝置登入並存取這份帳單。
                                </Alert>
                            </>
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
