import {useMemo, useState} from 'react';
import {ChevronDown, ClipboardCheck, Minus, Plus} from 'lucide-react';
import {Card, CardContent} from '@/shared/components/ui/card';
import {MemberAvatar} from '@/shared/components';
import type {Bill} from '@/features/snap-split/types/snap-split';
import {
    calculateSettlement,
    formatAmount,
    getExpenseAmount,
    getExpenseTotal,
    getMemberColor,
    getMemberName,
} from '@/features/snap-split/lib/settlement';
import {cn} from '@/shared/lib/utils';

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

export function VerificationPanel({bill}: VerificationPanelProps) {
    const [expandedMember, setExpandedMember] = useState<string | null>(null);

    const memberDetails = useMemo(() => {
        if (bill.expenses.length === 0) return [];

        const settlement = calculateSettlement(bill);

        const details: MemberExpenseDetail[] = bill.members.map(member => {
            const expenses: ExpenseDetail[] = [];

            for (const expense of bill.expenses) {
                if (expense.isItemized) {
                    // 品項模式
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
                            participantCount: 0,
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

    const toggleMember = (memberId: string) => {
        setExpandedMember(prev => prev === memberId ? null : memberId);
    };

    return (
        <div className="p-4 space-y-4">
            {/* 標題 */}
            <div className="flex items-center gap-2">
                <ClipboardCheck className="h-5 w-5 text-primary"/>
                <h2 className="text-lg font-bold">個人明細</h2>
            </div>

            {/* 空狀態 */}
            {bill.expenses.length === 0 ? (
                <Card>
                    <CardContent className="py-8 text-center text-muted-foreground">
                        新增消費紀錄後即可查看明細
                    </CardContent>
                </Card>
            ) : memberDetails.length === 0 ? (
                <Card>
                    <CardContent className="py-4 text-center text-muted-foreground">
                        尚無成員參與消費
                    </CardContent>
                </Card>
            ) : (
                <div className="space-y-2">
                    {memberDetails.map(member => {
                        const isExpanded = expandedMember === member.memberId;
                        const memberData = bill.members.find(m => m.id === member.memberId);

                        return (
                            <Card key={member.memberId} className="overflow-hidden">
                                {/* 摘要列（點擊展開） */}
                                <button
                                    onClick={() => toggleMember(member.memberId)}
                                    className="w-full flex items-center justify-between p-4 hover:bg-muted/50 transition-colors"
                                >
                                    <div className="flex items-center gap-3">
                                        <MemberAvatar
                                            name={memberData?.name ?? '?'}
                                            avatarUrl={memberData?.avatarUrl}
                                            color={getMemberColor(member.memberId, bill.members)}
                                        />
                                        <span className="font-semibold">{member.memberName}</span>
                                    </div>
                                    <div className="flex items-center gap-3">
                                        <span
                                            className={cn(
                                                "font-bold",
                                                member.expenses.length === 0 && "text-muted-foreground",
                                                member.balance > 0 && "text-positive",
                                                member.balance < 0 && "text-negative",
                                                member.balance === 0 && member.expenses.length > 0 && "text-muted-foreground"
                                            )}
                                        >
                                            {member.expenses.length === 0
                                                ? '尚未參與'
                                                : member.balance > 0
                                                    ? `應收 ${formatAmount(member.balance)}`
                                                    : member.balance < 0
                                                        ? `應付 ${formatAmount(Math.abs(member.balance))}`
                                                        : '已結清'}
                                        </span>
                                        <ChevronDown
                                            className={cn(
                                                "h-5 w-5 text-muted-foreground transition-transform",
                                                isExpanded && "rotate-180"
                                            )}
                                        />
                                    </div>
                                </button>

                                {/* 展開內容 */}
                                {isExpanded && (
                                    <CardContent className="pt-0 space-y-3">
                                        {member.expenses.length === 0 ? (
                                            <p className="text-center py-4 text-muted-foreground">
                                                該成員目前沒有參與任何消費
                                            </p>
                                        ) : (
                                            <>
                                                {member.expenses.map(expense => (
                                                    <ExpenseDetailCard
                                                        key={expense.expenseId}
                                                        expense={expense}
                                                    />
                                                ))}

                                                {/* 總計 */}
                                                <div
                                                    className={cn(
                                                        "p-3 rounded-lg border-2",
                                                        member.balance >= 0
                                                            ? "border-positive/30"
                                                            : "border-negative/30"
                                                    )}
                                                >
                                                    <div className="space-y-2">
                                                        <div className="flex justify-between text-sm">
                                                            <span className="text-muted-foreground">應平分金額</span>
                                                            <span className="text-negative">
                                                                −{formatAmount(member.totalOwed)}
                                                            </span>
                                                        </div>
                                                        <div className="flex justify-between text-sm">
                                                            <span className="text-muted-foreground">已先付金額</span>
                                                            <span className="text-positive">
                                                                +{formatAmount(member.totalPaid)}
                                                            </span>
                                                        </div>
                                                        <div className="border-t pt-2 flex justify-between">
                                                            <span className="font-bold">最終餘額</span>
                                                            <span
                                                                className={cn(
                                                                    "font-bold",
                                                                    member.balance > 0 && "text-positive",
                                                                    member.balance < 0 && "text-negative",
                                                                    member.balance === 0 && "text-muted-foreground"
                                                                )}
                                                            >
                                                                {member.balance > 0
                                                                    ? `+${formatAmount(member.balance)}（應收）`
                                                                    : member.balance < 0
                                                                        ? `${formatAmount(member.balance)}（應付）`
                                                                        : '±$0（已結清）'}
                                                            </span>
                                                        </div>
                                                    </div>
                                                </div>

                                                <p className="text-xs text-muted-foreground text-center">
                                                    金額已四捨五入至小數點後兩位
                                                </p>
                                            </>
                                        )}
                                    </CardContent>
                                )}
                            </Card>
                        );
                    })}
                </div>
            )}
        </div>
    );
}

// 消費明細卡片
function ExpenseDetailCard({expense}: { expense: ExpenseDetail }) {
    const netValue = (expense.paidAmount ?? 0) - expense.share;
    const isParticipant = expense.share > 0;

    // 品項模式
    if (expense.isItemized && expense.itemDetails) {
        return (
            <div className="p-3 bg-muted/50 rounded-lg border">
                <p className="font-semibold text-sm mb-2">
                    {expense.expenseName}
                    {expense.serviceFeePercent > 0 && (
                        <span className="text-xs text-muted-foreground ml-2">
                            (+{expense.serviceFeePercent}% 服務費)
                        </span>
                    )}
                </p>

                <div className="space-y-2">
                    {expense.itemDetails.map((item, idx) => {
                        const itemIsParticipant = item.share > 0;
                        return (
                            <div key={idx} className="pl-3 border-l-2 border-muted-foreground/30">
                                <p className="text-xs font-medium">{item.itemName}</p>
                                <div className="space-y-0.5 mt-1">
                                    {itemIsParticipant && (
                                        <div className="flex items-center gap-1.5 text-xs">
                                            <Minus className="h-3 w-3 text-negative"/>
                                            <span className="text-muted-foreground">
                                                {item.participantCount === 1 ? '應付' : '平分'} {formatAmount(item.share)}
                                            </span>
                                            {item.participantCount > 1 && (
                                                <span className="text-muted-foreground/60">
                                                    ({formatAmount(item.amountWithFee)} ÷ {item.participantCount} 人)
                                                </span>
                                            )}
                                        </div>
                                    )}
                                    {item.isPayer && item.paidAmount && (
                                        <div className="flex items-center gap-1.5 text-xs">
                                            <Plus className="h-3 w-3 text-positive"/>
                                            <span className="text-muted-foreground">
                                                先付 {formatAmount(item.paidAmount)}
                                            </span>
                                        </div>
                                    )}
                                </div>
                            </div>
                        );
                    })}

                    {/* 該筆小計 */}
                    <div className="flex justify-between pt-2 mt-1 border-t border-dashed">
                        <span className="text-xs text-muted-foreground">該筆淨值</span>
                        <span
                            className={cn(
                                "text-xs font-semibold",
                                netValue >= 0 ? "text-positive" : "text-negative"
                            )}
                        >
                            {netValue >= 0 ? '+' : ''}{formatAmount(netValue)}
                        </span>
                    </div>
                </div>
            </div>
        );
    }

    // 簡單模式
    const getFormula = () => {
        if (!isParticipant || expense.participantCount === 1) return null;
        if (expense.serviceFeePercent > 0) {
            return `(${formatAmount(expense.originalAmount)} + ${expense.serviceFeePercent}% 服務費) ÷ ${expense.participantCount} 人`;
        }
        return `${formatAmount(expense.totalWithFee)} ÷ ${expense.participantCount} 人`;
    };

    return (
        <div className="p-3 bg-muted/50 rounded-lg border">
            <p className="font-semibold text-sm mb-2">{expense.expenseName}</p>

            <div className="space-y-2">
                {/* 平分 (支出) */}
                {isParticipant && (
                    <div className="flex items-start gap-2">
                        <Minus className="h-4 w-4 text-negative mt-0.5 shrink-0"/>
                        <div className="flex-1">
                            <div className="flex justify-between text-sm">
                                <span className="text-muted-foreground">
                                    {expense.participantCount === 1 ? '應付' : '參與平分'}
                                </span>
                                <span className="text-negative font-semibold">
                                    {formatAmount(expense.share)}
                                </span>
                            </div>
                            {getFormula() && (
                                <p className="text-xs text-muted-foreground/60">{getFormula()}</p>
                            )}
                            {expense.otherParticipants.length > 0 && (
                                <p className="text-xs text-muted-foreground/60">
                                    與 {expense.otherParticipants.join('、')} 平分
                                </p>
                            )}
                        </div>
                    </div>
                )}

                {/* 先付 (收入) */}
                {expense.isPayer && expense.paidAmount && (
                    <div className="flex items-start gap-2">
                        <Plus className="h-4 w-4 text-positive mt-0.5 shrink-0"/>
                        <div className="flex-1">
                            <div className="flex justify-between text-sm">
                                <span className="text-muted-foreground">全額先付</span>
                                <span className="text-positive font-semibold">
                                    {formatAmount(expense.paidAmount)}
                                </span>
                            </div>
                        </div>
                    </div>
                )}

                {/* 該筆淨值 */}
                {expense.isPayer && isParticipant && (
                    <div className="flex justify-between pt-2 mt-1 border-t border-dashed">
                        <span className="text-xs text-muted-foreground">該筆淨值</span>
                        <span
                            className={cn(
                                "text-xs font-semibold",
                                netValue >= 0 ? "text-positive" : "text-negative"
                            )}
                        >
                            {netValue >= 0 ? '+' : ''}{formatAmount(netValue)}
                        </span>
                    </div>
                )}
            </div>
        </div>
    );
}
