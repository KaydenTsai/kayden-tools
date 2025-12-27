import { parseAmount } from "../components/AmountField";
import { ExpenseForm, type ExpenseFormState } from "../components/ExpenseForm";
import { Box, Button, Collapse, IconButton, Paper, Stack, TextField, Typography, useTheme, } from "@mui/material";
import { alpha } from "@mui/material/styles";
import {
    Add as AddIcon,
    ArrowBack as ArrowBackIcon,
    ContentCopy as CopyIcon,
    Delete as DeleteIcon,
    ExpandLess as ExpandLessIcon,
    ExpandMore as ExpandMoreIcon,
} from "@mui/icons-material";
import { useCallback, useMemo, useRef, useState } from "react";
import { applyServiceFee, formatAmount, getMemberName } from "@/utils/settlement";
import type { Bill, ExpenseItem } from "@/types/snap-split";
import { useSnapSplitStore } from "@/stores/snapSplitStore";

interface ItemizedExpenseViewProps {
    bill: Bill;
    expenseId?: string;
    onClose: () => void;
}

export function ItemizedExpenseView({ bill, expenseId, onClose }: ItemizedExpenseViewProps) {
    const theme = useTheme();
    const { addExpense, updateExpense } = useSnapSplitStore();

    const existingExpense = expenseId ? bill.expenses.find(e => e.id === expenseId) : null;

    const [expenseName, setExpenseName] = useState(existingExpense?.name || '');
    const [serviceFeePercent, setServiceFeePercent] = useState(
        existingExpense?.serviceFeePercent?.toString() || '0'
    );
    const [items, setItems] = useState<(ExpenseItem & { tempId?: string })[]>(
        existingExpense?.items || []
    );

    // 展開的品項 ID（null 表示全部收合）
    const [expandedItemId, setExpandedItemId] = useState<string | null>(null);

    // 快速新增列的表單狀態
    const [quickAdd, setQuickAdd] = useState<ExpenseFormState>({
        name: '',
        amount: '',
        paidBy: bill.members[0]?.id || '',
        participants: [],
    });

    const quickAddNameRef = useRef<HTMLInputElement>(null);
    const quickAddAmountRef = useRef<HTMLInputElement>(null);

    // 計算小計
    const itemsSubtotal = useMemo(
        () => items.reduce((sum, item) => sum + item.amount, 0),
        [items]
    );

    const serviceFeeValue = Number(serviceFeePercent) || 0;
    const totalWithFee = applyServiceFee(itemsSubtotal, serviceFeeValue);

    // 快速新增品項
    const handleQuickAdd = useCallback(() => {
        const amount = parseAmount(quickAdd.amount.toString());
        if (!quickAdd.name.trim() || amount === null || !quickAdd.paidBy || quickAdd.participants.length === 0) {
            return;
        }

        const newItem: ExpenseItem & { tempId?: string } = {
            id: crypto.randomUUID(),
            tempId: crypto.randomUUID(),
            name: quickAdd.name.trim(),
            amount,
            paidBy: quickAdd.paidBy,
            participants: quickAdd.participants,
        };

        setItems(prev => [newItem, ...prev]);

        // 重置快速新增表單，但保留付款人和參與者
        setQuickAdd(prev => ({
            name: '',
            amount: '',
            paidBy: prev.paidBy,
            participants: prev.participants,
        }));

        // 聚焦到品項名稱欄位
        setTimeout(() => quickAddNameRef.current?.focus(), 50);
    }, [quickAdd]);

    // 處理快速新增表單的 Enter 鍵
    const handleQuickAddKeyDown = (e: React.KeyboardEvent, field: keyof ExpenseFormState) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            if (field === 'name') {
                quickAddAmountRef.current?.focus();
            } else if (field === 'amount') {
                handleQuickAdd();
            }
        }
    };

    // 更新品項
    const handleUpdateItem = (index: number, updates: Partial<ExpenseItem>) => {
        setItems(prev => prev.map((item, i) =>
            i === index ? { ...item, ...updates } : item
        ));
    };

    // 刪除品項
    const handleDeleteItem = (index: number) => {
        setItems(prev => prev.filter((_, i) => i !== index));
        setExpandedItemId(null);
    };

    // 複製品項
    const handleDuplicateItem = (index: number) => {
        const item = items[index];
        const newItem = {
            ...item,
            id: crypto.randomUUID(),
            tempId: crypto.randomUUID(),
        };
        setItems(prev => [...prev.slice(0, index + 1), newItem, ...prev.slice(index + 1)]);
    };

    // 切換展開/收合
    const toggleExpand = (itemId: string) => {
        setExpandedItemId(prev => prev === itemId ? null : itemId);
    };

    // 儲存
    const handleSave = () => {
        if (!expenseName.trim() || items.length === 0) return;

        const expenseData = {
            name: expenseName.trim(),
            serviceFeePercent: serviceFeeValue,
            isItemized: true,
            amount: itemsSubtotal,
            paidBy: '',
            participants: [],
            items: items.map(({ tempId, ...item }) => item),
        };

        if (existingExpense) {
            updateExpense(existingExpense.id, expenseData);
        } else {
            addExpense(expenseData);
        }

        onClose();
    };

    const canSave = expenseName.trim() && items.length > 0;

    const quickAddAmountValue = parseAmount(quickAdd.amount.toString());

    // 取得品項摘要文字
    const getItemSummaryText = (paidBy: string, participants: string[]) => {
        const payerName = getMemberName(bill.members, paidBy);

        if (participants.length === 0) {
            return `${payerName} 付款（未指定平分）`;
        }
        if (participants.length === bill.members.length) {
            return `${payerName} 先付，大家平分`;
        }
        if (participants.length === 1) {
            const participantName = getMemberName(bill.members, participants[0]);
            if (participants[0] === paidBy) {
                return `${payerName} 自己付`;
            }
            return `${payerName} 幫 ${participantName} 付`;
        }
        return `${payerName} 幫 ${participants.length} 人先付`;
    };

    return (
        <Box
            sx={{
                position: 'fixed',
                top: 0,
                left: 0,
                right: 0,
                bottom: 0,
                zIndex: 1300,
                bgcolor: 'background.default',
                overflow: 'auto',
                pb: '120px',
            }}
        >
            {/* Sticky Header - 窄條模式 */}
            <Paper
                elevation={2}
                sx={{
                    position: 'sticky',
                    top: 0,
                    zIndex: 1400,
                    borderRadius: 0,
                    bgcolor: 'background.paper',
                }}
            >
                <Box sx={{ px: 1, py: 0.5, display: 'flex', alignItems: 'center', gap: 1 }}>
                    <IconButton size="small" onClick={onClose}>
                        <ArrowBackIcon />
                    </IconButton>
                    <TextField
                        variant="standard"
                        placeholder="輸入名稱"
                        value={expenseName}
                        onChange={(e) => setExpenseName(e.target.value)}
                        autoComplete="off"
                        sx={{
                            minWidth: 80,
                            flex: 1,
                            '& .MuiInput-root::before': { display: 'none' },
                            '& .MuiInput-root::after': { display: 'none' },
                            '& .MuiInput-input::placeholder': {
                                opacity: 0.6,
                            },
                        }}
                        slotProps={{
                            input: {
                                sx: { fontSize: '1rem', fontWeight: 600 },
                            },
                        }}
                    />
                    <Typography variant="body2" color="text.secondary" sx={{ flexShrink: 0 }}>
                        {items.length} 個品項
                    </Typography>
                </Box>
            </Paper>

            {/* 品項列表 */}
            <Stack spacing={1.5} sx={{ p: 2, position: 'relative', zIndex: 1 }}>
                {/* 快速新增列 - 放在最上方 */}
                <Paper
                    sx={{
                        pt: 2,
                        px: 2,
                        pb: 2,
                        borderRadius: 2.5,
                        border: '1.5px solid',
                        borderColor: 'primary.main',
                        bgcolor: 'primary.50',
                    }}
                    elevation={2}
                >
                    <Typography variant="subtitle2" fontWeight={700} color="primary.main" sx={{ mb: 2, display: 'flex', alignItems: 'center', gap: 0.5 }}>
                        <AddIcon fontSize="small" />
                        新增品項
                    </Typography>

                    <ExpenseForm
                        members={bill.members}
                        values={quickAdd}
                        onChange={(updates) => setQuickAdd(prev => ({ ...prev, ...updates }))}
                        onKeyDown={handleQuickAddKeyDown}
                        nameRef={quickAddNameRef}
                        amountRef={quickAddAmountRef}
                    />

                    <Box sx={{ display: 'flex', gap: 1, mt: 2 }}>
                        {(quickAdd.name || quickAdd.amount) && (
                            <Button
                                variant="text"
                                color="inherit"
                                onClick={() => setQuickAdd(prev => ({
                                    name: '',
                                    amount: '',
                                    paidBy: prev.paidBy,
                                    participants: prev.participants,
                                }))}
                                sx={{ color: 'text.secondary' }}
                            >
                                清空
                            </Button>
                        )}
                        <Button
                            fullWidth
                            variant="contained"
                            startIcon={<AddIcon />}
                            onClick={handleQuickAdd}
                            disabled={!quickAdd.name.trim() || quickAddAmountValue === null || !quickAdd.paidBy || quickAdd.participants.length === 0}
                        >
                            新增此品項
                        </Button>
                    </Box>
                </Paper>

                {/* 已新增品項標題 */}
                {items.length > 0 && (
                    <Typography variant="body2" color="text.secondary" sx={{ pt: 1 }}>
                        已新增 {items.length} 個品項（點擊展開編輯）
                    </Typography>
                )}

                {/* 已新增的品項列表 */}
                {items.map((item, index) => {
                    const itemId = item.tempId || item.id;
                    const isExpanded = expandedItemId === itemId;

                    return (
                        <Paper
                            key={itemId}
                            sx={{
                                borderRadius: 2,
                                bgcolor: 'background.paper',
                                border: '1px solid',
                                borderColor: isExpanded ? 'primary.main' : 'divider',
                                overflow: 'hidden',
                                transition: 'border-color 0.2s',
                            }}
                            elevation={isExpanded ? 2 : 0}
                        >
                            {/* 收合狀態：摘要列 */}
                            <Box
                                onClick={() => toggleExpand(itemId)}
                                sx={{
                                    p: 1.5,
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: 1,
                                    cursor: 'pointer',
                                    '&:hover': { bgcolor: 'action.hover' },
                                }}
                            >
                                <Typography variant="body2" color="text.secondary" sx={{ minWidth: 24 }}>
                                    {index + 1}.
                                </Typography>
                                <Box sx={{ flex: 1, minWidth: 0 }}>
                                    <Typography fontWeight={600} noWrap>
                                        {item.name}
                                    </Typography>
                                    <Typography variant="caption" color="text.secondary">
                                        {getItemSummaryText(item.paidBy, item.participants)}
                                    </Typography>
                                </Box>
                                <Typography fontWeight={700} color="primary.main">
                                    {formatAmount(item.amount)}
                                </Typography>
                                <IconButton size="small" sx={{ ml: 0.5 }}>
                                    {isExpanded ? <ExpandLessIcon /> : <ExpandMoreIcon />}
                                </IconButton>
                            </Box>

                            {/* 展開狀態：編輯欄位 */}
                            <Collapse in={isExpanded}>
                                <Box sx={{ p: 2, pt: 0, borderTop: '1px solid', borderColor: 'divider' }}>
                                    <Box sx={{ mt: 2 }}>
                                        <ExpenseForm
                                            members={bill.members}
                                            values={{
                                                name: item.name,
                                                amount: item.amount,
                                                paidBy: item.paidBy,
                                                participants: item.participants
                                            }}
                                            onChange={(updates) => {
                                                if (updates.amount !== undefined) {
                                                    // Ensure amount is stored as number
                                                    handleUpdateItem(index, { ...updates, amount: Number(updates.amount) || 0 });
                                                } else {
                                                    // Other fields match
                                                    handleUpdateItem(index, updates as any);
                                                }
                                            }}
                                        />
                                    </Box>

                                    {/* 操作按鈕 */}
                                    <Box sx={{ display: 'flex', gap: 1, justifyContent: 'flex-end', mt: 1 }}>
                                        <Button
                                            size="small"
                                            startIcon={<CopyIcon />}
                                            onClick={() => handleDuplicateItem(index)}
                                        >
                                            複製
                                        </Button>
                                        <Button
                                            size="small"
                                            color="error"
                                            startIcon={<DeleteIcon />}
                                            onClick={() => handleDeleteItem(index)}
                                        >
                                            刪除
                                        </Button>
                                    </Box>
                                </Box>
                            </Collapse>
                        </Paper>
                    );
                })}
            </Stack>

            {/* Sticky Footer - 極簡磨砂玻璃 */}
            <Paper
                elevation={0}
                sx={{
                    position: 'fixed',
                    bottom: 0,
                    left: 0,
                    right: 0,
                    zIndex: 1500,
                    px: 2,
                    pt: 1.5,
                    pb: 1.5,
                    bgcolor: alpha(theme.palette.background.paper, 0.9),
                    backdropFilter: 'blur(10px)',
                    borderTop: '1px solid',
                    borderColor: 'divider',
                }}
            >
                {/* 第一行：服務費輸入 */}
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 0.5 }}>
                    <TextField
                        size="small"
                        type="number"
                        value={serviceFeePercent}
                        onChange={(e) => setServiceFeePercent(e.target.value)}
                        autoComplete="off"
                        variant="outlined"
                        label="服務費"
                        slotProps={{
                            input: {
                                sx: { height: 32, fontSize: '1rem' },
                                endAdornment: <Typography sx={{ fontSize: '0.875rem', color: 'text.secondary' }}>%</Typography>,
                            },
                            htmlInput: {
                                min: 0,
                                max: 100,
                                style: { padding: '4px 8px', textAlign: 'center' },
                            },
                            inputLabel: {
                                shrink: true,
                                sx: { fontSize: '0.75rem' },
                            },
                        }}
                        sx={{
                            width: 88,
                            '& input::-webkit-outer-spin-button, & input::-webkit-inner-spin-button': {
                                WebkitAppearance: 'none',
                                margin: 0,
                            },
                            '& input[type=number]': {
                                MozAppearance: 'textfield',
                            },
                        }}
                    />
                    <Box sx={{ flex: 1 }} />
                    <Typography variant="caption" sx={{ color: 'text.disabled', letterSpacing: 0.5 }}>
                        {formatAmount(itemsSubtotal)}
                        {serviceFeeValue > 0 && ` + ${formatAmount(itemsSubtotal * serviceFeeValue / 100)}`}
                    </Typography>
                </Box>

                {/* 第二行：總計與按鈕 */}
                <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                    <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 0.5 }}>
                        <Typography variant="caption" color="primary.main" fontWeight={700}>總計</Typography>
                        <Typography variant="h5" fontWeight={800} color="primary.main" sx={{ lineHeight: 1 }}>
                            {formatAmount(totalWithFee)}
                        </Typography>
                    </Box>
                    <Button
                        variant="contained"
                        size="large"
                        onClick={handleSave}
                        disabled={!canSave}
                        sx={{ borderRadius: 2, px: 4, height: 44, boxShadow: 'none', fontWeight: 700 }}
                    >
                        儲存
                    </Button>
                </Box>
            </Paper>
        </Box>
    );
}
