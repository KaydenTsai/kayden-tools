import {
    Avatar,
    Box,
    Button,
    Chip,
    Dialog,
    DialogActions,
    DialogContent,
    DialogContentText,
    DialogTitle,
    Fab,
    IconButton,
    InputAdornment,
    Paper,
    Stack,
    TextField,
    Tooltip,
    Typography,
    useMediaQuery,
    useTheme,
    Zoom,
} from "@mui/material";
import {
    Add as AddIcon,
    Delete as DeleteIcon,
    DocumentScanner as DocumentScannerIcon,
    Receipt as ReceiptIcon,
} from "@mui/icons-material";
import { parseAmount } from "./AmountField";
import { ExpenseForm, type ExpenseFormState } from "./ExpenseForm";
import { useMemo, useState } from "react";
import type { Bill, Expense } from "@/types/snap-split";
import { useSnapSplitStore } from "@/stores/snapSplitStore";
import { useAuthStore } from "@/stores/authStore";
import { formatAmount, getExpenseTotal, getMemberColor, getMemberName } from "@/utils/settlement";
import { SlideTransition } from "@/components/ui/SlideTransition";

type SimpleExpenseFormState = ExpenseFormState & {
    serviceFeePercent: string;
};

const emptyForm: SimpleExpenseFormState = {
    name: '',
    amount: '',
    paidById: '',
    participants: [],
    serviceFeePercent: '0',
};

interface ExpenseListProps {
    bill: Bill;
    isReadOnly?: boolean;
    onOpenMemberDialog?: () => void;
    onOpenItemizedExpense?: (expenseId?: string) => void;
}

export function ExpenseList({ bill, isReadOnly = false, onOpenMemberDialog, onOpenItemizedExpense }: ExpenseListProps) {
    const { addExpense, updateExpense, removeExpense } = useSnapSplitStore();
    const { user } = useAuthStore();
    const theme = useTheme();
    const isMobile = useMediaQuery(theme.breakpoints.down('md'));

    // 判斷成員是否為「離線」狀態（已認領但非當前用戶）
    const isMemberOffline = (memberId: string) => {
        const member = bill.members.find(m => m.id === memberId);
        return !!member?.userId && member.userId !== user?.id;
    };

    const [formOpen, setFormOpen] = useState(false);
    const [editingExpense, setEditingExpense] = useState<Expense | null>(null);
    const [form, setForm] = useState<SimpleExpenseFormState>(emptyForm);
    const [deleteOpen, setDeleteOpen] = useState(false);
    const [deletingExpense, setDeletingExpense] = useState<Expense | null>(null);
    const [addModeOpen, setAddModeOpen] = useState(false);

    const evaluatedAmount = useMemo(() => parseAmount(String(form.amount)), [form.amount]);

    const handleOpenAdd = () => {
        setAddModeOpen(true);
    };

    const handleAddSimple = () => {
        setAddModeOpen(false);
        setEditingExpense(null);
        setForm({
            ...emptyForm,
            paidById: bill.members[0]?.id || '',
            participants: [],
            serviceFeePercent: '0',
        });
        setFormOpen(true);
    };

    const handleAddItemized = () => {
        setAddModeOpen(false);
        onOpenItemizedExpense?.();
    };

    const handleScanReceipt = () => {
        // Future PRO feature entry point
        // For now, just close the dialog or show a toast
        setAddModeOpen(false);
        // You could trigger a "Coming Soon" toast here if you had a global toast system
        alert("OCR 智慧掃描即將推出！");
    };

    const handleOpenEdit = (expense: Expense) => {
        if (expense.isItemized) {
            onOpenItemizedExpense?.(expense.id);
        } else {
            setEditingExpense(expense);
            setForm({
                name: expense.name,
                amount: expense.amount.toString(),
                paidById: expense.paidById,
                participants: [...expense.participants],
                serviceFeePercent: (expense.serviceFeePercent ?? 0).toString(),
            });
            setFormOpen(true);
        }
    };

    const handleSubmit = () => {
        if (!form.name.trim() || evaluatedAmount === null || !form.paidById || form.participants.length === 0) {
            return;
        }

        const expenseData: Omit<Expense, 'id'> = {
            name: form.name.trim(),
            amount: evaluatedAmount,
            paidById: form.paidById,
            participants: form.participants,
            serviceFeePercent: Math.max(0, Number(form.serviceFeePercent) || 0),
            isItemized: false,
            items: [],
        };

        if (editingExpense) {
            updateExpense(editingExpense.id, expenseData);
        } else {
            addExpense(expenseData);
        }

        setFormOpen(false);
        setEditingExpense(null);
    };

    const handleOpenDelete = (expense: Expense) => {
        setDeletingExpense(expense);
        setDeleteOpen(true);
    };

    const handleDelete = () => {
        if (deletingExpense) {
            removeExpense(deletingExpense.id);
        }
        setDeleteOpen(false);
        setDeletingExpense(null);
    };

    const isFormValid = form.name.trim() && evaluatedAmount !== null && form.paidById && form.participants.length > 0;

    const renderExpenseInfo = (expense: Expense) => {
        // 取得摘要文字
        let summaryText: string;

        if (expense.isItemized) {
            const uniquePayers = [...new Set(expense.items.map(item => item.paidById))];
            if (uniquePayers.length === 0) {
                summaryText = '尚未指定付款人';
            } else if (uniquePayers.length === 1) {
                summaryText = `${getMemberName(bill.members, uniquePayers[0])} 先付`;
            } else {
                summaryText = `${uniquePayers.length} 人分別先付`;
            }
        } else {
            const payerName = getMemberName(bill.members, expense.paidById);
            const participantCount = expense.participants.length;

            if (participantCount === 0) {
                summaryText = `${payerName} 付款（未指定平分）`;
            } else if (participantCount === bill.members.length) {
                summaryText = `${payerName} 先付，大家平分`;
            } else if (participantCount === 1) {
                const participantName = getMemberName(bill.members, expense.participants[0]);
                if (expense.participants[0] === expense.paidById) {
                    summaryText = `${payerName} 自己付`;
                } else {
                    summaryText = `${payerName} 幫 ${participantName} 付`;
                }
            } else {
                summaryText = `${payerName} 幫 ${participantCount} 人先付`;
            }
        }

        return (
            <Box sx={{ flex: 1, minWidth: 0, overflow: 'hidden' }}>
                {/* 第一行：標題與金額 */}
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
                    <Typography variant="subtitle1" fontWeight={600} noWrap sx={{ flex: 1, minWidth: 0 }}>
                        {expense.name}
                    </Typography>
                    <Typography variant="subtitle1" fontWeight={700} color="primary.main" sx={{ flexShrink: 0 }}>
                        {formatAmount(getExpenseTotal(expense))}
                    </Typography>
                </Box>

                {/* 第二行：摘要 */}
                <Typography variant="caption" color="text.secondary" noWrap sx={{ display: 'block', mb: 0.2 }}>
                    {summaryText}
                </Typography>

                {/* 第三行：細項資訊 */}
                {expense.isItemized && (
                    <Typography variant="caption" color="text.disabled" noWrap sx={{ display: 'block' }}>
                        {expense.items.length} 個品項
                    </Typography>
                )}
            </Box>
        );
    };

    return (
        <>
            <Paper sx={{ p: 2.5, borderRadius: 3, overflow: 'hidden' }} elevation={2}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 2 }}>
                    <ReceiptIcon color="primary"/>
                    <Typography variant="h6" fontWeight={700}>
                        消費紀錄
                    </Typography>
                    {bill.expenses.length > 0 && (
                        <Typography variant="body2" color="text.secondary">
                            ({bill.expenses.length})
                        </Typography>
                    )}
                </Box>

                {bill.members.length === 0 ? (
                    <Box sx={{ textAlign: 'center', py: 4 }}>
                        <Typography color="text.secondary" sx={{ mb: 2 }}>
                            {isReadOnly ? '此帳單尚無成員' : '請先新增成員才能記錄消費'}
                        </Typography>
                        {!isReadOnly && onOpenMemberDialog && (
                            <Button variant="contained" startIcon={<AddIcon/>} onClick={onOpenMemberDialog}>
                                新增成員
                            </Button>
                        )}
                    </Box>
                ) : bill.expenses.length === 0 ? (
                    <Box sx={{ textAlign: 'center', py: 4, color: 'text.secondary' }}>
                        {isReadOnly ? '此帳單尚無消費記錄' : '點擊右下角按鈕新增消費'}
                    </Box>
                ) : (
                    <Stack spacing={1.5}>
                        {bill.expenses.map(expense => (
                            <Box
                                key={expense.id}
                                onClick={isReadOnly ? undefined : () => handleOpenEdit(expense)}
                                sx={{
                                    p: 2,
                                    bgcolor: 'background.paper',
                                    borderRadius: 2.5,
                                    border: '1px solid',
                                    borderColor: 'divider',
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: 1.5,
                                    cursor: isReadOnly ? 'default' : 'pointer',
                                    transition: 'all 0.2s cubic-bezier(0.4, 0, 0.2, 1)',
                                    '&:hover': isReadOnly ? {} : {
                                        borderColor: 'primary.light',
                                        boxShadow: '0 4px 12px rgba(0,0,0,0.08)',
                                    },
                                    '&:active': isReadOnly ? {} : {
                                        transform: 'scale(0.99)',
                                    },
                                }}
                            >
                                <Avatar
                                    src={bill.members.find(m => m.id === expense.paidById)?.avatarUrl}
                                    sx={{
                                        bgcolor: getMemberColor(expense.paidById, bill.members),
                                        width: 40,
                                        height: 40,
                                        fontSize: '1rem',
                                        fontWeight: 600,
                                        // 離線效果
                                        opacity: isMemberOffline(expense.paidById) ? 0.6 : 1,
                                        filter: isMemberOffline(expense.paidById) ? 'grayscale(30%)' : 'none',
                                    }}
                                >
                                    {getMemberName(bill.members, expense.paidById).charAt(0).toUpperCase()}
                                </Avatar>

                                {renderExpenseInfo(expense)}

                                {!isReadOnly && (
                                    <IconButton
                                        size="small"
                                        onClick={(e) => {
                                            e.stopPropagation();
                                            handleOpenDelete(expense);
                                        }}
                                        sx={{
                                            flexShrink: 0,
                                            color: 'text.disabled',
                                            '&:hover': {
                                                color: 'error.main',
                                                bgcolor: 'error.50',
                                            },
                                        }}
                                    >
                                        <DeleteIcon fontSize="small" />
                                    </IconButton>
                                )}
                            </Box>
                        ))}
                    </Stack>
                )}
            </Paper>

            {/* 新增模式選擇 Dialog */}
            <Dialog open={addModeOpen} onClose={() => setAddModeOpen(false)} TransitionComponent={SlideTransition}>
                <DialogTitle>新增消費</DialogTitle>
                <DialogContent>
                    <Stack spacing={2} sx={{ pt: 1 }}>
                        <Button
                            variant="outlined"
                            size="large"
                            onClick={handleAddSimple}
                            sx={{
                                py: 2,
                                justifyContent: 'flex-start',
                                textAlign: 'left',
                                borderColor: 'divider',
                                color: 'text.primary'
                            }}
                        >
                            <Box>
                                <Typography fontWeight={600} color="primary">整筆紀錄</Typography>
                                <Typography variant="body2" color="text.secondary">
                                    適合車資、代付或單項支出。只需輸入一個金額與平分名單。
                                </Typography>
                            </Box>
                        </Button>
                        <Button
                            variant="outlined"
                            size="large"
                            onClick={handleAddItemized}
                            sx={{
                                py: 2,
                                justifyContent: 'flex-start',
                                textAlign: 'left',
                                borderColor: 'divider',
                                color: 'text.primary'
                            }}
                        >
                            <Box>
                                <Typography fontWeight={600} color="primary">細項紀錄</Typography>
                                <Typography variant="body2" color="text.secondary">
                                    適合複雜紀錄。可對照收據逐項輸入，每個品項指定不同人。
                                </Typography>
                            </Box>
                        </Button>

                        {/* Upsell / Pro Feature Placeholder */}
                        <Tooltip title="Pro 版限定功能 (開發中)" arrow placement="top">
                            <Button
                                variant="outlined"
                                size="large"
                                onClick={handleScanReceipt}
                                sx={{
                                    py: 2,
                                    justifyContent: 'flex-start',
                                    textAlign: 'left',
                                    borderStyle: 'dashed',
                                    borderColor: 'primary.light',
                                    bgcolor: 'primary.50',
                                    '&:hover': {
                                        bgcolor: 'primary.100',
                                        borderStyle: 'dashed',
                                    }
                                }}
                            >
                                <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, width: '100%' }}>
                                    <Box sx={{ flex: 1 }}>
                                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                                            <DocumentScannerIcon color="primary" fontSize="small" />
                                            <Typography fontWeight={600} color="primary.main">
                                                掃描收據 (AI)
                                            </Typography>
                                            <Chip label="Coming Soon" size="small" color="primary" sx={{ height: 20, fontSize: '0.65rem' }} />
                                        </Box>
                                        <Typography variant="body2" color="text.secondary">
                                            拍照自動辨識品項與金額，省去手動輸入的時間。
                                        </Typography>
                                    </Box>
                                </Box>
                            </Button>
                        </Tooltip>
                    </Stack>
                </DialogContent>
                <DialogActions sx={{ p: 2, pt: 0 }}>
                    <Button onClick={() => setAddModeOpen(false)}>取消</Button>
                </DialogActions>
            </Dialog>

            {/* 簡單模式表單 Dialog */}
            <Dialog
                open={formOpen}
                onClose={() => setFormOpen(false)}
                maxWidth="xs"
                fullWidth
                TransitionComponent={SlideTransition}
            >
                <DialogTitle>{editingExpense ? '編輯消費' : '新增消費'}</DialogTitle>
                <DialogContent>
                    <Box sx={{ mt: 1 }}>
                        <ExpenseForm
                            members={bill.members}
                            values={form}
                            onChange={(updates) => setForm(prev => ({ ...prev, ...updates }))}
                        />
                        <TextField
                            fullWidth
                            label="服務費"
                            type="number"
                            value={form.serviceFeePercent}
                            onChange={(e) => setForm(prev => ({ ...prev, serviceFeePercent: e.target.value }))}
                            autoComplete="off"
                            slotProps={{
                                input: {
                                    endAdornment: <InputAdornment position="end">%</InputAdornment>,
                                },
                                htmlInput: { inputMode: 'decimal', min: 0, max: 100 },
                            }}
                            helperText="填 0 或留空表示無服務費"
                            sx={{ mt: 2 }}
                        />
                    </Box>
                </DialogContent>
                <DialogActions sx={{ p: 2, pt: 0 }}>
                    <Button onClick={() => setFormOpen(false)} size="large">取消</Button>
                    <Button onClick={handleSubmit} variant="contained" size="large" disabled={!isFormValid}>
                        {editingExpense ? '儲存' : '新增'}
                    </Button>
                </DialogActions>
            </Dialog>

            {/* 刪除確認 Dialog */}
            <Dialog open={deleteOpen} onClose={() => setDeleteOpen(false)} TransitionComponent={SlideTransition}>
                <DialogTitle>確認刪除</DialogTitle>
                <DialogContent>
                    <DialogContentText>
                        確定要刪除消費「{deletingExpense?.name}」嗎？
                    </DialogContentText>
                </DialogContent>
                <DialogActions sx={{ p: 2, pt: 0 }}>
                    <Button onClick={() => setDeleteOpen(false)} size="large">取消</Button>
                    <Button onClick={handleDelete} color="error" variant="contained" size="large">
                        刪除
                    </Button>
                </DialogActions>
            </Dialog>

            {/* FAB */}
            <Zoom in={!isReadOnly && bill.members.length > 0} unmountOnExit>
                <Fab
                    color="primary"
                    onClick={handleOpenAdd}
                    sx={{
                        position: 'fixed',
                        bottom: isMobile ? 90 : 32,
                        right: 24,
                        zIndex: 1000,
                        boxShadow: '0 4px 16px rgba(0,0,0,0.2)',
                        transition: 'all 0.2s cubic-bezier(0.4, 0, 0.2, 1)',
                        '&:hover': { transform: 'scale(1.1)', boxShadow: '0 6px 20px rgba(0,0,0,0.25)' },
                        '&:active': { transform: 'scale(0.95)' },
                    }}
                >
                    <AddIcon/>
                </Fab>
            </Zoom>
        </>
    );
}
