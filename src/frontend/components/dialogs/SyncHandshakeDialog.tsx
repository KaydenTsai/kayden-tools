import { useState } from 'react';
import {
    Dialog,
    DialogTitle,
    DialogContent,
    DialogActions,
    Button,
    Box,
    Typography,
    List,
    ListItem,
    ListItemButton,
    ListItemIcon,
    ListItemText,
    Checkbox,
    LinearProgress,
    Alert,
} from '@mui/material';
import {
    CloudUpload as UploadIcon,
    Receipt as BillIcon,
} from '@mui/icons-material';
import { SlideTransition } from '@/components/ui/SlideTransition';
import { SyncStatusIcon } from '@/components/ui/SyncStatusIndicator';
import type { Bill } from '@/types/snap-split';

interface SyncHandshakeDialogProps {
    open: boolean;
    onClose: () => void;
    unsyncedBills: Bill[];
    syncProgress: {
        total: number;
        completed: number;
        failed: number;
    };
    onSyncSelected: (billIds: string[]) => Promise<void>;
    onSyncAll: () => Promise<void>;
}

export function SyncHandshakeDialog({
    open,
    onClose,
    unsyncedBills,
    syncProgress,
    onSyncSelected,
    onSyncAll,
}: SyncHandshakeDialogProps) {
    const [selectedBills, setSelectedBills] = useState<Set<string>>(
        new Set(unsyncedBills.map(b => b.id))
    );
    const [isSyncing, setIsSyncing] = useState(false);

    const handleToggle = (billId: string) => {
        setSelectedBills(prev => {
            const next = new Set(prev);
            if (next.has(billId)) {
                next.delete(billId);
            } else {
                next.add(billId);
            }

            return next;
        });
    };

    const handleSelectAll = () => {
        if (selectedBills.size === unsyncedBills.length) {
            setSelectedBills(new Set());
        } else {
            setSelectedBills(new Set(unsyncedBills.map(b => b.id)));
        }
    };

    const handleSync = async () => {
        setIsSyncing(true);
        try {
            if (selectedBills.size === unsyncedBills.length) {
                await onSyncAll();
            } else {
                await onSyncSelected(Array.from(selectedBills));
            }
        } finally {
            setIsSyncing(false);
        }
    };

    const progress = syncProgress.total > 0
        ? ((syncProgress.completed + syncProgress.failed) / syncProgress.total) * 100
        : 0;

    const hasFailures = syncProgress.failed > 0;

    return (
        <Dialog
            open={open}
            onClose={isSyncing ? undefined : onClose}
            maxWidth="sm"
            fullWidth
            TransitionComponent={SlideTransition}
        >
            <DialogTitle>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <UploadIcon color="primary" />
                    同步本地帳單
                </Box>
            </DialogTitle>

            <DialogContent>
                {isSyncing ? (
                    <Box sx={{ py: 3 }}>
                        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                            正在同步帳單到雲端...
                        </Typography>
                        <LinearProgress
                            variant="determinate"
                            value={progress}
                            sx={{ mb: 2, height: 8, borderRadius: 4 }}
                        />
                        <Typography variant="body2" color="text.secondary" align="center">
                            {syncProgress.completed + syncProgress.failed} / {syncProgress.total}
                            {hasFailures && (
                                <Typography component="span" color="error.main" sx={{ ml: 1 }}>
                                    ({syncProgress.failed} 失敗)
                                </Typography>
                            )}
                        </Typography>
                    </Box>
                ) : (
                    <>
                        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                            偵測到 {unsyncedBills.length} 個本地帳單尚未同步到雲端。
                            選擇要同步的帳單：
                        </Typography>

                        {hasFailures && (
                            <Alert severity="warning" sx={{ mb: 2 }}>
                                部分帳單同步失敗，請稍後重試。
                            </Alert>
                        )}

                        <List sx={{ maxHeight: 300, overflow: 'auto' }}>
                            {unsyncedBills.map(bill => (
                                <ListItem key={bill.id} disablePadding>
                                    <ListItemButton
                                        onClick={() => handleToggle(bill.id)}
                                        dense
                                        sx={{ borderRadius: 1 }}
                                    >
                                        <ListItemIcon sx={{ minWidth: 42 }}>
                                            <Checkbox
                                                edge="start"
                                                checked={selectedBills.has(bill.id)}
                                                tabIndex={-1}
                                                disableRipple
                                            />
                                        </ListItemIcon>
                                        <ListItemIcon sx={{ minWidth: 36 }}>
                                            <BillIcon color="action" />
                                        </ListItemIcon>
                                        <ListItemText
                                            primary={bill.name}
                                            secondary={`${bill.members.length} 人 · ${bill.expenses.length} 筆消費`}
                                        />
                                        <SyncStatusIcon status={bill.syncStatus} size="small" />
                                    </ListItemButton>
                                </ListItem>
                            ))}
                        </List>
                    </>
                )}
            </DialogContent>

            <DialogActions sx={{ p: 2, pt: 1, justifyContent: 'space-between' }}>
                {!isSyncing && (
                    <Button onClick={handleSelectAll} size="large">
                        {selectedBills.size === unsyncedBills.length ? '取消全選' : '全選'}
                    </Button>
                )}
                <Box sx={{ display: 'flex', gap: 1 }}>
                    <Button
                        onClick={onClose}
                        disabled={isSyncing}
                        size="large"
                    >
                        {isSyncing ? '請稍候' : '稍後再說'}
                    </Button>
                    <Button
                        variant="contained"
                        onClick={handleSync}
                        disabled={isSyncing || selectedBills.size === 0}
                        startIcon={<UploadIcon />}
                        size="large"
                    >
                        同步 ({selectedBills.size})
                    </Button>
                </Box>
            </DialogActions>
        </Dialog>
    );
}
