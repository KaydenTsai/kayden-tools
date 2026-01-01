import {
    Accordion,
    AccordionDetails,
    AccordionSummary,
    Avatar,
    Box,
    Divider,
    Paper,
    Stack,
    Typography,
} from "@mui/material";
import {
    Add as AddIcon,
    ExpandMore as ExpandMoreIcon,
    FactCheck as FactCheckIcon,
    Remove as RemoveIcon,
} from "@mui/icons-material";
import { useMemo, useState } from "react";
import type { Bill } from "@/types/snap-split";
import { useAuthStore } from "@/stores/authStore";
import {
    calculateSettlement,
    formatAmount,
    getExpenseAmount,
    getExpenseTotal,
    getMemberColor,
    getMemberName
} from "@/utils/settlement";

interface ExpenseDetail {
    expenseId: string;
    expenseName: string;
    originalAmount: number;
    serviceFeePercent: number;
    totalWithFee: number;
    share: number;
    participantCount: number;
    otherParticipants: string[];
    isPayer: boolean;
    paidAmount?: number;
    isItemized?: boolean;
    itemDetails?: {
        itemName: string;
        amount: number;
        amountWithFee: number;
        share: number;
        participantCount: number;
        otherParticipants: string[];
        isPayer: boolean;
        paidAmount?: number;
    }[];
}

interface MemberExpenseDetail {
    memberId: string;
    memberName: string;
    expenses: ExpenseDetail[];
    totalOwed: number;
    totalPaid: number;
    balance: number;
}

interface VerificationPanelProps {
    bill: Bill;
}

export function VerificationPanel({ bill }: VerificationPanelProps) {
    const { user } = useAuthStore();
    const [expandedMember, setExpandedMember] = useState<string | false>(false);

    // 判斷成員是否為「離線」狀態（已認領但非當前用戶）
    const isMemberOffline = (memberId: string) => {
        const member = bill.members.find(m => m.id === memberId);
        return !!member?.userId && member.userId !== user?.id;
    };

    const memberDetails = useMemo(() => {
        if (bill.expenses.length === 0) {
            return [];
        }

        const settlement = calculateSettlement(bill);

        const details: MemberExpenseDetail[] = bill.members.map(member => {
            const expenses: ExpenseDetail[] = [];

            for (const expense of bill.expenses) {
                if (expense.isItemized) {
                    // 品項模式：檢查每個品項
                    const serviceFeeMultiplier = 1 + expense.serviceFeePercent / 100;
                    const itemDetails: ExpenseDetail['itemDetails'] = [];
                    let memberShare = 0;
                    let memberPaid = 0;

                    for (const item of expense.items) {
                        const isParticipant = item.participants.includes(member.id);
                        const isPayer = item.paidById === member.id;

                        if (!isParticipant && !isPayer) continue;

                        const amountWithFee = item.amount * serviceFeeMultiplier;
                        const share = isParticipant ? amountWithFee / item.participants.length : 0;
                        const otherParticipants = item.participants
                            .filter(id => id !== member.id)
                            .map(id => getMemberName(bill.members, id));

                        if (isParticipant) memberShare += share;
                        if (isPayer) memberPaid += amountWithFee;

                        itemDetails.push({
                            itemName: item.name,
                            amount: item.amount,
                            amountWithFee,
                            share,
                            participantCount: item.participants.length,
                            otherParticipants,
                            isPayer,
                            paidAmount: isPayer ? amountWithFee : undefined,
                        });
                    }

                    if (itemDetails.length > 0) {
                        expenses.push({
                            expenseId: expense.id,
                            expenseName: expense.name,
                            originalAmount: getExpenseAmount(expense),
                            serviceFeePercent: expense.serviceFeePercent,
                            totalWithFee: getExpenseTotal(expense),
                            share: memberShare,
                            participantCount: 0, // 品項模式不適用
                            otherParticipants: [],
                            isPayer: memberPaid > 0,
                            paidAmount: memberPaid > 0 ? memberPaid : undefined,
                            isItemized: true,
                            itemDetails,
                        });
                    }
                } else {
                    // 簡單模式
                    const isParticipant = expense.participants.includes(member.id);
                    const isPayer = expense.paidById === member.id;

                    if (!isParticipant && !isPayer) continue;

                    const totalWithFee = getExpenseTotal(expense);
                    const participantCount = expense.participants.length;
                    const share = isParticipant ? totalWithFee / participantCount : 0;
                    const otherParticipants = expense.participants
                        .filter(id => id !== member.id)
                        .map(id => getMemberName(bill.members, id));

                    expenses.push({
                        expenseId: expense.id,
                        expenseName: expense.name,
                        originalAmount: expense.amount,
                        serviceFeePercent: expense.serviceFeePercent,
                        totalWithFee,
                        share,
                        participantCount,
                        otherParticipants,
                        isPayer,
                        paidAmount: isPayer ? totalWithFee : undefined,
                        isItemized: false,
                    });
                }
            }

            const summary = settlement.memberSummaries.find(s => s.memberId === member.id);

            return {
                memberId: member.id,
                memberName: member.name,
                expenses,
                totalOwed: summary?.totalOwed ?? 0,
                totalPaid: summary?.totalPaid ?? 0,
                balance: summary?.balance ?? 0,
            };
        });

        return details;
    }, [bill]);

    const handleAccordionChange = (memberId: string) => (_: React.SyntheticEvent, isExpanded: boolean) => {
        setExpandedMember(isExpanded ? memberId : false);
    };

    return (
        <Paper sx={{ p: 2.5, borderRadius: 3 }} elevation={2}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 2.5 }}>
                <FactCheckIcon color="primary" />
                <Typography variant="h6" fontWeight={700}>
                    個人明細
                </Typography>
            </Box>

            {bill.expenses.length === 0 ? (
                <Box sx={{ textAlign: 'center', py: 4, color: 'text.secondary' }}>
                    新增消費紀錄後即可查看明細
                </Box>
            ) : memberDetails.length === 0 ? (
                <Typography color="text.secondary" sx={{ textAlign: 'center', py: 2 }}>
                    尚無成員參與消費
                </Typography>
            ) : (
                <Stack spacing={1}>
                    {memberDetails.map(member => (
                        <Accordion
                            key={member.memberId}
                            expanded={expandedMember === member.memberId}
                            onChange={handleAccordionChange(member.memberId)}
                            elevation={0}
                            sx={{
                                border: '1px solid',
                                borderColor: 'divider',
                                borderRadius: '12px !important',
                                '&:before': { display: 'none' },
                                '&.Mui-expanded': {
                                    margin: 0,
                                },
                            }}
                        >
                            <AccordionSummary
                                expandIcon={<ExpandMoreIcon />}
                                sx={{
                                    borderRadius: 3,
                                    '&.Mui-expanded': {
                                        minHeight: 48,
                                    },
                                }}
                            >
                                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', width: '100%', pr: 1 }}>
                                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
                                        <Avatar
                                            src={bill.members.find(m => m.id === member.memberId)?.avatarUrl}
                                            sx={{
                                                bgcolor: getMemberColor(member.memberId, bill.members),
                                                width: 32,
                                                height: 32,
                                                fontSize: '0.875rem',
                                                fontWeight: 600,
                                                opacity: isMemberOffline(member.memberId) ? 0.6 : 1,
                                                filter: isMemberOffline(member.memberId) ? 'grayscale(30%)' : 'none',
                                            }}
                                        >
                                            {member.memberName.charAt(0).toUpperCase()}
                                        </Avatar>
                                        <Typography fontWeight={600}>
                                            {member.memberName}
                                        </Typography>
                                    </Box>
                                    <Typography
                                        fontWeight={700}
                                        color={
                                            member.expenses.length === 0 ? 'text.disabled' :
                                            member.balance > 0 ? 'success.main' :
                                            member.balance < 0 ? 'error.main' : 'text.secondary'
                                        }
                                    >
                                        {member.expenses.length === 0 ? '尚未參與' :
                                         member.balance > 0 ? `應收 ${formatAmount(member.balance)}` :
                                         member.balance < 0 ? `應付 ${formatAmount(Math.abs(member.balance))}` :
                                         '已結清'}
                                    </Typography>
                                </Box>
                            </AccordionSummary>
                            <AccordionDetails sx={{ pt: 0 }}>
                                {member.expenses.length === 0 ? (
                                    <Box sx={{ textAlign: 'center', py: 2, color: 'text.disabled' }}>
                                        該成員目前沒有參與任何消費
                                    </Box>
                                ) : (
                                <Stack spacing={1.5}>
                                    {member.expenses.map(expense => {
                                        const isParticipant = expense.share > 0;
                                        const netValue = (expense.paidAmount ?? 0) - expense.share;

                                        // 品項模式渲染
                                        if (expense.isItemized && expense.itemDetails) {
                                            return (
                                                <Box
                                                    key={expense.expenseId}
                                                    sx={{
                                                        p: 1.5,
                                                        bgcolor: 'action.hover',
                                                        borderRadius: 2,
                                                        border: '1px solid',
                                                        borderColor: 'divider',
                                                    }}
                                                >
                                                    <Typography variant="body2" fontWeight={600} sx={{ mb: 1 }}>
                                                        {expense.expenseName}
                                                        {expense.serviceFeePercent > 0 && (
                                                            <Typography component="span" variant="caption" color="text.secondary" sx={{ ml: 1 }}>
                                                                (+{expense.serviceFeePercent}% 服務費)
                                                            </Typography>
                                                        )}
                                                    </Typography>

                                                    <Stack spacing={0.75}>
                                                        {expense.itemDetails.map((item, idx) => {
                                                            const itemIsParticipant = item.share > 0;
                                                            return (
                                                                <Box key={idx} sx={{ pl: 1, borderLeft: '2px solid', borderColor: 'divider' }}>
                                                                    <Typography variant="caption" fontWeight={500} color="text.primary">
                                                                        {item.itemName}
                                                                    </Typography>
                                                                    <Stack spacing={0.25} sx={{ mt: 0.25 }}>
                                                                        {itemIsParticipant && (
                                                                            <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                                                                                <RemoveIcon sx={{ fontSize: 12, color: 'error.main' }} />
                                                                                <Typography variant="caption" color="text.secondary">
                                                                                    {item.participantCount === 1 ? '應付' : '平分'} {formatAmount(item.share)}
                                                                                </Typography>
                                                                                {item.participantCount > 1 && (
                                                                                    <Typography variant="caption" color="text.disabled">
                                                                                        ({formatAmount(item.amountWithFee)} ÷ {item.participantCount} 人)
                                                                                    </Typography>
                                                                                )}
                                                                            </Box>
                                                                        )}
                                                                        {item.isPayer && item.paidAmount && (
                                                                            <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                                                                                <AddIcon sx={{ fontSize: 12, color: 'success.main' }} />
                                                                                <Typography variant="caption" color="text.secondary">
                                                                                    先付 {formatAmount(item.paidAmount)}
                                                                                </Typography>
                                                                            </Box>
                                                                        )}
                                                                    </Stack>
                                                                </Box>
                                                            );
                                                        })}

                                                        {/* 該筆小計 */}
                                                        <Box
                                                            sx={{
                                                                display: 'flex',
                                                                justifyContent: 'space-between',
                                                                pt: 1,
                                                                mt: 0.5,
                                                                borderTop: '1px dashed',
                                                                borderColor: 'divider',
                                                            }}
                                                        >
                                                            <Typography variant="caption" color="text.secondary">
                                                                該筆淨值
                                                            </Typography>
                                                            <Typography
                                                                variant="caption"
                                                                fontWeight={600}
                                                                color={netValue >= 0 ? 'success.main' : 'error.main'}
                                                            >
                                                                {netValue >= 0 ? '+' : ''}{formatAmount(netValue)}
                                                            </Typography>
                                                        </Box>
                                                    </Stack>
                                                </Box>
                                            );
                                        }

                                        // 簡單模式渲染
                                        const getFormula = () => {
                                            if (!isParticipant || expense.participantCount === 1) return null;
                                            if (expense.serviceFeePercent > 0) {
                                                return `(${formatAmount(expense.originalAmount)} + ${expense.serviceFeePercent}% 服務費) ÷ ${expense.participantCount} 人`;
                                            }
                                            return `${formatAmount(expense.totalWithFee)} ÷ ${expense.participantCount} 人`;
                                        };

                                        return (
                                            <Box
                                                key={expense.expenseId}
                                                sx={{
                                                    p: 1.5,
                                                    bgcolor: 'action.hover',
                                                    borderRadius: 2,
                                                    border: '1px solid',
                                                    borderColor: 'divider',
                                                }}
                                            >
                                                <Typography variant="body2" fontWeight={600} sx={{ mb: 1 }}>
                                                    {expense.expenseName}
                                                </Typography>

                                                <Stack spacing={0.5}>
                                                    {/* 平分 (支出) */}
                                                    {isParticipant && (
                                                        <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 1 }}>
                                                            <RemoveIcon sx={{ fontSize: 16, color: 'error.main', mt: 0.25 }} />
                                                            <Box sx={{ flex: 1 }}>
                                                                <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                                                                    <Typography variant="body2" color="text.secondary">
                                                                        {expense.participantCount === 1 ? '應付' : '參與平分'}
                                                                    </Typography>
                                                                    <Typography variant="body2" color="error.main" fontWeight={600}>
                                                                        {formatAmount(expense.share)}
                                                                    </Typography>
                                                                </Box>
                                                                {getFormula() && (
                                                                    <Typography variant="caption" color="text.disabled">
                                                                        {getFormula()}
                                                                    </Typography>
                                                                )}
                                                                {expense.otherParticipants.length > 0 && (
                                                                    <Typography variant="caption" color="text.disabled" sx={{ display: 'block' }}>
                                                                        與 {expense.otherParticipants.join('、')} 平分
                                                                    </Typography>
                                                                )}
                                                            </Box>
                                                        </Box>
                                                    )}

                                                    {/* 先付 (收入) */}
                                                    {expense.isPayer && expense.paidAmount && (
                                                        <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 1 }}>
                                                            <AddIcon sx={{ fontSize: 16, color: 'success.main', mt: 0.25 }} />
                                                            <Box sx={{ flex: 1 }}>
                                                                <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                                                                    <Typography variant="body2" color="text.secondary">
                                                                        全額先付
                                                                    </Typography>
                                                                    <Typography variant="body2" color="success.main" fontWeight={600}>
                                                                        {formatAmount(expense.paidAmount)}
                                                                    </Typography>
                                                                </Box>
                                                            </Box>
                                                        </Box>
                                                    )}

                                                    {/* 該筆淨值 */}
                                                    {expense.isPayer && isParticipant && (
                                                        <Box
                                                            sx={{
                                                                display: 'flex',
                                                                justifyContent: 'space-between',
                                                                pt: 1,
                                                                mt: 0.5,
                                                                borderTop: '1px dashed',
                                                                borderColor: 'divider',
                                                            }}
                                                        >
                                                            <Typography variant="caption" color="text.secondary">
                                                                該筆淨值
                                                            </Typography>
                                                            <Typography
                                                                variant="caption"
                                                                fontWeight={600}
                                                                color={netValue >= 0 ? 'success.main' : 'error.main'}
                                                            >
                                                                {netValue >= 0 ? '+' : ''}{formatAmount(netValue)}
                                                            </Typography>
                                                        </Box>
                                                    )}
                                                </Stack>
                                            </Box>
                                        );
                                    })}

                                    <Divider />
                                    <Box
                                        sx={{
                                            p: 1.5,
                                            bgcolor: 'background.paper',
                                            borderRadius: 2,
                                            border: '2px solid',
                                            borderColor: member.balance >= 0 ? 'success.light' : 'error.light',
                                        }}
                                    >
                                        <Stack spacing={0.75}>
                                            <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                                                <Typography variant="body2" color="text.secondary">
                                                    應平分金額
                                                </Typography>
                                                <Typography variant="body2" color="error.main">
                                                    −{formatAmount(member.totalOwed)}
                                                </Typography>
                                            </Box>
                                            <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                                                <Typography variant="body2" color="text.secondary">
                                                    已先付金額
                                                </Typography>
                                                <Typography variant="body2" color="success.main">
                                                    +{formatAmount(member.totalPaid)}
                                                </Typography>
                                            </Box>
                                            <Divider sx={{ my: 0.5 }} />
                                            <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                                                <Typography variant="body2" fontWeight={700}>
                                                    最終餘額
                                                </Typography>
                                                <Typography
                                                    variant="body2"
                                                    fontWeight={700}
                                                    color={
                                                        member.balance > 0 ? 'success.main' :
                                                        member.balance < 0 ? 'error.main' : 'text.secondary'
                                                    }
                                                >
                                                    {member.balance > 0 ? `+${formatAmount(member.balance)}（應收）` :
                                                     member.balance < 0 ? `${formatAmount(member.balance)}（應付）` :
                                                     '±$0（已結清）'}
                                                </Typography>
                                            </Box>
                                        </Stack>
                                    </Box>

                                    {/* Rounding note */}
                                    <Typography variant="caption" color="text.disabled" sx={{ textAlign: 'center' }}>
                                        * 金額可能因四捨五入有微小差異
                                    </Typography>
                                </Stack>
                                )}
                            </AccordionDetails>
                        </Accordion>
                    ))}
                </Stack>
            )}
        </Paper>
    );
}
