import {
    Box,
    Button,
    Card,
    CardActionArea,
    CardContent,
    Dialog,
    DialogActions,
    DialogContent,
    DialogContentText,
    DialogTitle,
    Grid,
    IconButton,
    Paper,
    TextField,
    Typography,
} from "@mui/material";
import {
    Add as AddIcon,
    ChevronRight as ChevronRightIcon,
    Delete as DeleteIcon,
    Receipt as ReceiptIcon,
} from "@mui/icons-material";
import * as React from "react";
import { useState } from "react";
import { useSnapSplitStore } from "@/stores/snapSplitStore";
import { formatAmount, getExpenseTotal } from "@/utils/settlement";
import type { Bill } from "@/types/snap-split";
import { SlideTransition } from "@/components/ui/SlideTransition";
import { SyncStatusIcon } from "@/components/ui/SyncStatusIndicator";
import { useMyBillsSync } from "@/hooks/useMyBillsSync";
import { deleteBill as deleteBillApi } from "@/api/endpoints/bills/bills";

export function BillListView() {
    // 啟用自動輪詢以同步列表變更（如刪除帳單）
    useMyBillsSync(true);
    const { bills, selectBill, createBill, deleteBill } = useSnapSplitStore();

    const [newOpen, setNewOpen] = useState(false);
    const [newName, setNewName] = useState('');
    const [deleteOpen, setDeleteOpen] = useState(false);
    const [deletingBill, setDeletingBill] = useState<Bill | null>(null);

    const handleCreate = () => {
        if (newName.trim()) {
            createBill(newName.trim());
            setNewName('');
            setNewOpen(false);
        }
    };

    const handleOpenDelete = (bill: Bill, e: React.MouseEvent) => {
        e.stopPropagation();
        setDeletingBill(bill);
        setDeleteOpen(true);
    };

    const handleDelete = async () => {
        if (deletingBill) {
            // 如果帳單已同步到雲端，則呼叫 API 刪除
            if (deletingBill.remoteId) {
                try {
                    await deleteBillApi(deletingBill.remoteId);
                } catch (error) {
                    console.error('Failed to delete bill from server:', error);
                    // 即使 API 失敗，我們仍可能想從本地移除？
                    // 或者顯示錯誤並中止？這裡選擇為了 UX 流暢先不報錯，繼續移除本地
                }
            }
            deleteBill(deletingBill.id);
        }
        setDeleteOpen(false);
        setDeletingBill(null);
    };

    const formatDate = (dateString: string) => {
        const date = new Date(dateString);
        return date.toLocaleDateString('zh-TW', { year: 'numeric', month: 'short', day: 'numeric' });
    };

    const getTotalAmount = (bill: Bill) => {
        return bill.expenses.reduce((sum, e) => sum + getExpenseTotal(e), 0);
    };

    if (bills.length === 0) {
        return (
            <>
                <Paper sx={{ p: 5, textAlign: 'center', borderRadius: 3 }} elevation={2}>
                    <ReceiptIcon sx={{ fontSize: 64, color: 'text.disabled', mb: 2 }} />
                    <Typography variant="h6" color="text.secondary" sx={{ mb: 1 }}>
                        還沒有任何帳單
                    </Typography>
                    <Typography variant="body2" color="text.disabled" sx={{ mb: 3 }}>
                        建立帳單來開始記錄平分
                    </Typography>
                    <Button
                        variant="contained"
                        size="large"
                        startIcon={<AddIcon />}
                        onClick={() => setNewOpen(true)}
                        sx={{ borderRadius: 2, px: 4, py: 1.5 }}
                    >
                        建立第一筆帳單
                    </Button>
                </Paper>

                <NewBillDialog
                    open={newOpen}
                    onClose={() => setNewOpen(false)}
                    value={newName}
                    onChange={setNewName}
                    onConfirm={handleCreate}
                />
            </>
        );
    }

    return (
        <>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
                <Typography variant="h6" fontWeight={700}>
                    所有帳單
                </Typography>
                <Button
                    variant="contained"
                    startIcon={<AddIcon />}
                    onClick={() => setNewOpen(true)}
                    sx={{ borderRadius: 2 }}
                >
                    新增
                </Button>
            </Box>

            <Grid container spacing={2}>
                {bills.map(bill => (
                    <Grid size={{ xs: 12, sm: 6 }} key={bill.id}>
                        <Card
                            elevation={0}
                            sx={{
                                borderRadius: 3,
                                border: '2px solid',
                                borderColor: 'divider',
                                bgcolor: 'background.paper',
                                transition: 'all 0.25s cubic-bezier(0.4, 0, 0.2, 1)',
                                '&:hover': {
                                    borderColor: 'primary.main',
                                    boxShadow: '0 8px 24px rgba(0,0,0,0.12)',
                                    transform: 'translateY(-4px)',
                                },
                                '&:active': {
                                    transform: 'translateY(-2px) scale(0.98)',
                                    boxShadow: '0 4px 12px rgba(0,0,0,0.1)',
                                },
                            }}
                        >
                            <CardActionArea
                                onClick={() => selectBill(bill.id)}
                                sx={{ p: 0 }}
                            >
                                <CardContent sx={{ p: 2.5, pb: 2 }}>
                                    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 1.5 }}>
                                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, flex: 1, mr: 1, minWidth: 0 }}>
                                            <Typography variant="h6" fontWeight={700} noWrap>
                                                {bill.name}
                                            </Typography>
                                            <SyncStatusIcon
                                                status={bill.syncStatus}
                                                size="small"
                                                isCollaborative={bill.members.some(m => !!m.userId)}
                                            />
                                        </Box>
                                        <ChevronRightIcon color="action" />
                                    </Box>

                                    <Box sx={{ display: 'flex', gap: 2, mb: 1.5 }}>
                                        <Box>
                                            <Typography variant="caption" color="text.secondary">
                                                成員
                                            </Typography>
                                            <Typography fontWeight={600}>
                                                {bill.members.length} 人
                                            </Typography>
                                        </Box>
                                        <Box>
                                            <Typography variant="caption" color="text.secondary">
                                                消費
                                            </Typography>
                                            <Typography fontWeight={600}>
                                                {bill.expenses.length} 筆
                                            </Typography>
                                        </Box>
                                        <Box>
                                            <Typography variant="caption" color="text.secondary">
                                                總額
                                            </Typography>
                                            <Typography fontWeight={600} color="primary.main">
                                                {formatAmount(getTotalAmount(bill))}
                                            </Typography>
                                        </Box>
                                    </Box>

                                    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                        <Box>
                                            <Typography variant="caption" color="text.disabled">
                                                更新於 {formatDate(bill.updatedAt)}
                                            </Typography>
                                            {bill.lastSyncedAt && (
                                                <Typography variant="caption" color="text.disabled" sx={{ ml: 1.5 }}>
                                                    · 同步於 {formatDate(bill.lastSyncedAt)}
                                                </Typography>
                                            )}
                                        </Box>
                                        <Box onClick={(e) => e.stopPropagation()}>
                                            <IconButton
                                                size="small"
                                                onClick={(e) => handleOpenDelete(bill, e)}
                                                sx={{
                                                    bgcolor: 'action.hover',
                                                    color: 'error.main',
                                                    '&:hover': { bgcolor: 'error.main', color: 'white' },
                                                }}
                                            >
                                                <DeleteIcon fontSize="small" />
                                            </IconButton>
                                        </Box>
                                    </Box>
                                </CardContent>
                            </CardActionArea>
                        </Card>
                    </Grid>
                ))}
            </Grid>

            <NewBillDialog
                open={newOpen}
                onClose={() => setNewOpen(false)}
                value={newName}
                onChange={setNewName}
                onConfirm={handleCreate}
            />

            <Dialog open={deleteOpen} onClose={() => setDeleteOpen(false)} TransitionComponent={SlideTransition}>
                <DialogTitle>確認刪除</DialogTitle>
                <DialogContent>
                    <DialogContentText>
                        確定要刪除「{deletingBill?.name}」嗎？此操作無法復原。
                    </DialogContentText>
                </DialogContent>
                <DialogActions sx={{ p: 2, pt: 0 }}>
                    <Button onClick={() => setDeleteOpen(false)} size="large">取消</Button>
                    <Button onClick={handleDelete} color="error" variant="contained" size="large">
                        刪除
                    </Button>
                </DialogActions>
            </Dialog>
        </>
    );
}

interface NewBillDialogProps {
    open: boolean;
    onClose: () => void;
    value: string;
    onChange: (value: string) => void;
    onConfirm: () => void;
}

function NewBillDialog({ open, onClose, value, onChange, onConfirm }: NewBillDialogProps) {
    return (
        <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth TransitionComponent={SlideTransition}>
            <DialogTitle>新增帳單</DialogTitle>
            <DialogContent>
                <TextField
                    autoFocus
                    fullWidth
                    label="帳單名稱"
                    value={value}
                    onChange={(e) => onChange(e.target.value)}
                    onKeyDown={(e) => e.key === 'Enter' && value.trim() && onConfirm()}
                    sx={{ mt: 1 }}
                    placeholder="例如：日本旅遊、週五聚餐"
                />
            </DialogContent>
            <DialogActions sx={{ p: 2, pt: 0 }}>
                <Button onClick={onClose} size="large">取消</Button>
                <Button onClick={onConfirm} variant="contained" size="large" disabled={!value.trim()}>
                    建立
                </Button>
            </DialogActions>
        </Dialog>
    );
}
