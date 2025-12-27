import { Avatar, Box, Paper, Stack, Typography, } from "@mui/material";
import {
    ArrowForward as ArrowForwardIcon,
    Calculate as CalculateIcon,
    CheckCircle as CheckCircleIcon,
    RadioButtonUnchecked as UncheckedIcon,
} from "@mui/icons-material";
import { useMemo } from "react";
import type { Bill } from "@/types/snap-split";
import { useSnapSplitStore } from "@/stores/snapSplitStore";
import {
    calculateSettlement,
    formatAmount,
    getMemberColor,
    getMemberName,
    isTransferSettled
} from "@/utils/settlement";

interface SettlementPanelProps {
    bill: Bill;
    isReadOnly?: boolean;
}

export function SettlementPanel({ bill, isReadOnly = false }: SettlementPanelProps) {
    const { toggleSettlement } = useSnapSplitStore();

    const settlement = useMemo(() => {
        if (bill.expenses.length === 0) return null;
        return calculateSettlement(bill);
    }, [bill]);


    const settledCount = settlement?.transfers.filter(t =>
        isTransferSettled(bill.settledTransfers, t.from, t.to)
    ).length ?? 0;

    const allSettled = settlement?.transfers.length === settledCount && settledCount > 0;

    return (
        <Paper sx={{ p: 2.5, borderRadius: 3 }} elevation={2}>
            <Box sx={{ mb: 2.5 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <CalculateIcon color="primary" />
                    <Typography variant="h6" fontWeight={700}>
                        結算
                    </Typography>
                </Box>
                {settlement && settlement.transfers.length > 0 && (
                    <Typography variant="caption" color="text.secondary" sx={{ ml: 4 }}>
                        已優化轉帳路徑，僅需 {settlement.transfers.length} 次轉帳即可結清
                    </Typography>
                )}
            </Box>

            {bill.expenses.length === 0 ? (
                <Box sx={{ textAlign: 'center', py: 4, color: 'text.secondary' }}>
                    新增消費紀錄後即可查看結算結果
                </Box>
            ) : settlement && (
                <>
                    <Box
                        sx={{
                            mb: 2.5,
                            p: 2,
                            borderRadius: 2,
                            border: '1px solid',
                            borderColor: 'divider',
                            bgcolor: 'background.paper',
                        }}
                    >
                        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
                            <Typography variant="body2" color="text.secondary">
                                總計
                            </Typography>
                            <Typography variant="h5" fontWeight={700} color="primary.main">
                                {formatAmount(settlement.totalWithServiceFee)}
                            </Typography>
                        </Box>
                        {settlement.totalWithServiceFee !== settlement.totalAmount && (
                            <Typography variant="caption" color="text.disabled" sx={{ display: 'block', textAlign: 'right' }}>
                                {formatAmount(settlement.totalAmount)} + 服務費
                            </Typography>
                        )}
                    </Box>

                    {/* 個人明細 */}
                    <Box sx={{ mb: 3 }}>
                        <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
                            個人狀態
                        </Typography>
                        <Stack spacing={1}>
                            {settlement.memberSummaries
                                .filter(s => s.totalPaid > 0 || s.totalOwed > 0)
                                .map(summary => (
                                    <Box
                                        key={summary.memberId}
                                        sx={{
                                            display: 'flex',
                                            justifyContent: 'space-between',
                                            alignItems: 'center',
                                            p: 1.5,
                                            bgcolor: 'action.hover',
                                            borderRadius: 2,
                                            border: '1px solid',
                                            borderColor: 'divider',
                                        }}
                                    >
                                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
                                            <Avatar
                                                sx={{
                                                    bgcolor: getMemberColor(summary.memberId, bill.members),
                                                    width: 32,
                                                    height: 32,
                                                    fontSize: '0.875rem',
                                                    fontWeight: 600
                                                }}
                                            >
                                                {getMemberName(bill.members, summary.memberId).charAt(0).toUpperCase()}
                                            </Avatar>
                                            <Typography fontWeight={600}>
                                                {getMemberName(bill.members, summary.memberId)}
                                            </Typography>
                                        </Box>
                                        <Typography
                                            fontWeight={700}
                                            color={
                                                summary.balance > 0 ? 'success.main' :
                                                summary.balance < 0 ? 'error.main' : 'text.secondary'
                                            }
                                        >
                                            {summary.balance > 0 ? `應收 ${formatAmount(summary.balance)}` :
                                             summary.balance < 0 ? `應付 ${formatAmount(Math.abs(summary.balance))}` : '已結清'}
                                        </Typography>
                                    </Box>
                                ))}
                        </Stack>
                    </Box>

                    <Box>
                        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
                            <Typography variant="body2" color="text.secondary">
                                轉帳清單
                            </Typography>
                            {settlement.transfers.length > 0 && (
                                <Typography
                                    variant="caption"
                                    fontWeight={600}
                                    color={allSettled ? 'success.main' : 'text.secondary'}
                                >
                                    {allSettled ? '✓ 全部結清' : `${settledCount}/${settlement.transfers.length}`}
                                </Typography>
                            )}
                        </Box>

                        {settlement.transfers.length === 0 ? (
                            <Box
                                sx={{
                                    textAlign: 'center',
                                    py: 3,
                                    bgcolor: 'success.50',
                                    border: '1px solid',
                                    borderColor: 'success.main',
                                    borderRadius: 2,
                                }}
                            >
                                <CheckCircleIcon sx={{ fontSize: 32, mb: 1, color: 'success.main' }} />
                                <Typography fontWeight={600} color="success.main">
                                    無需轉帳
                                </Typography>
                                <Typography variant="body2" color="text.secondary">
                                    每個人都剛好付了自己的份
                                </Typography>
                            </Box>
                        ) : (
                            <Stack spacing={1.5}>
                                {settlement.transfers.map((transfer, index) => {
                                    const isSettled = isTransferSettled(
                                        bill.settledTransfers,
                                        transfer.from,
                                        transfer.to
                                    );
                                    return (
                                        <Box
                                            key={index}
                                            onClick={isReadOnly ? undefined : () => toggleSettlement(transfer.from, transfer.to)}
                                            sx={{
                                                p: 2,
                                                borderRadius: 2.5,
                                                cursor: isReadOnly ? 'default' : 'pointer',
                                                transition: 'all 0.2s cubic-bezier(0.4, 0, 0.2, 1)',
                                                border: '1px solid',
                                                borderColor: isSettled ? 'transparent' : 'divider',
                                                bgcolor: isSettled ? 'action.selected' : 'background.paper',
                                                opacity: isSettled ? 0.6 : 1,
                                                boxShadow: isSettled ? 'none' : '0 2px 8px rgba(0,0,0,0.04)',
                                                ...(!isReadOnly && !isSettled && {
                                                    '&:hover': {
                                                        borderColor: 'primary.light',
                                                        boxShadow: '0 4px 12px rgba(0,0,0,0.08)',
                                                        transform: 'translateY(-1px)',
                                                    },
                                                    '&:active': {
                                                        transform: 'scale(0.98)',
                                                    },
                                                }),
                                            }}
                                        >
                                            <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 1.5 }}>
                                                {isSettled ? (
                                                    <CheckCircleIcon sx={{ color: 'success.main', mt: 0.25 }} />
                                                ) : (
                                                    <UncheckedIcon color="disabled" sx={{ mt: 0.25 }} />
                                                )}
                                                <Box sx={{ flex: 1 }}>
                                                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap', mb: 0.5 }}>
                                                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
                                                            <Avatar
                                                                sx={{
                                                                    bgcolor: getMemberColor(transfer.from, bill.members),
                                                                    width: 24,
                                                                    height: 24,
                                                                    fontSize: '0.75rem',
                                                                    fontWeight: 600
                                                                }}
                                                            >
                                                                {getMemberName(bill.members, transfer.from).charAt(0).toUpperCase()}
                                                            </Avatar>
                                                            <Typography fontWeight={600} fontSize="1.05rem">
                                                                {getMemberName(bill.members, transfer.from)}
                                                            </Typography>
                                                        </Box>

                                                        <ArrowForwardIcon
                                                            sx={{
                                                                fontSize: 20,
                                                                color: 'text.disabled',
                                                                mx: 0.5
                                                            }}
                                                        />

                                                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
                                                            <Avatar
                                                                sx={{
                                                                    bgcolor: getMemberColor(transfer.to, bill.members),
                                                                    width: 24,
                                                                    height: 24,
                                                                    fontSize: '0.75rem',
                                                                    fontWeight: 600
                                                                }}
                                                            >
                                                                {getMemberName(bill.members, transfer.to).charAt(0).toUpperCase()}
                                                            </Avatar>
                                                            <Typography fontWeight={600} fontSize="1.05rem">
                                                                {getMemberName(bill.members, transfer.to)}
                                                            </Typography>
                                                        </Box>
                                                    </Box>
                                                    <Typography
                                                        fontWeight={700}
                                                        fontSize="1.5rem"
                                                        sx={{
                                                            color: isSettled ? 'text.secondary' : 'text.primary',
                                                            textDecoration: isSettled ? 'line-through' : 'none',
                                                        }}
                                                    >
                                                        {formatAmount(transfer.amount)}
                                                    </Typography>
                                                    {!isReadOnly && (
                                                        <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
                                                            {isSettled ? '已結清 · 點擊取消' : '點擊標記為已結清'}
                                                        </Typography>
                                                    )}
                                                </Box>
                                            </Box>
                                        </Box>
                                    );
                                })}
                            </Stack>
                        )}
                    </Box>
                </>
            )}
        </Paper>
    );
}
