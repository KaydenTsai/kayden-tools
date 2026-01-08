import {useMemo} from 'react';
import {ArrowRight, Calculator, CheckCircle2, Circle} from 'lucide-react';
import {Card, CardContent, CardHeader, CardTitle} from '@/shared/components/ui/card';
import {MemberAvatar} from '@/shared/components';
import {useSnapSplitStore} from '@/features/snap-split/stores/snapSplitStore';
import type {Bill} from '@/features/snap-split/types/snap-split';
import {
    calculateSettlement,
    formatAmount,
    getMemberColor,
    getMemberName,
    isTransferSettled,
} from '@/features/snap-split/lib/settlement';
import {cn} from '@/shared/lib/utils';

interface SettlementPanelProps {
    bill: Bill;
    isReadOnly?: boolean;
}

export function SettlementPanel({bill, isReadOnly = false}: SettlementPanelProps) {
    const {toggleSettlement} = useSnapSplitStore();

    const settlement = useMemo(() => {
        if (bill.expenses.length === 0) return null;
        return calculateSettlement(bill);
    }, [bill]);

    const settledCount = settlement?.transfers.filter(t =>
        isTransferSettled(bill.settledTransfers, t.from, t.to)
    ).length ?? 0;

    const allSettled = settlement?.transfers.length === settledCount && settledCount > 0;

    return (
        <div className="p-4 space-y-4">
            {/* 標題 */}
            <div className="flex items-center gap-2">
                <Calculator className="h-5 w-5 text-primary"/>
                <h2 className="text-lg font-bold">結算</h2>
            </div>

            {settlement && settlement.transfers.length > 0 && (
                <p className="text-sm text-muted-foreground -mt-2 ml-7">
                    已優化轉帳路徑，僅需 {settlement.transfers.length} 次轉帳即可結清
                </p>
            )}

            {/* 空狀態 */}
            {bill.expenses.length === 0 ? (
                <Card>
                    <CardContent className="py-8 text-center text-muted-foreground">
                        新增消費紀錄後即可查看結算結果
                    </CardContent>
                </Card>
            ) : settlement && (
                <>
                    {/* 總金額 */}
                    <Card>
                        <CardContent className="p-4">
                            <div className="flex justify-between items-baseline">
                                <span className="text-sm text-muted-foreground">總計</span>
                                <span className="text-2xl font-bold text-primary">
                                    {formatAmount(settlement.totalWithServiceFee)}
                                </span>
                            </div>
                            {settlement.totalWithServiceFee !== settlement.totalAmount && (
                                <p className="text-xs text-muted-foreground text-right mt-1">
                                    {formatAmount(settlement.totalAmount)} + 服務費
                                </p>
                            )}
                        </CardContent>
                    </Card>

                    {/* 個人狀態 */}
                    <Card>
                        <CardHeader className="pb-2">
                            <CardTitle className="text-sm font-medium text-muted-foreground">
                                個人狀態
                            </CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-2">
                            {settlement.memberSummaries
                                .filter(s => s.totalPaid > 0 || s.totalOwed > 0)
                                .map(summary => {
                                    const member = bill.members.find(m => m.id === summary.memberId);
                                    return (
                                        <div
                                            key={summary.memberId}
                                            className="flex items-center justify-between p-3 bg-muted/50 rounded-lg"
                                        >
                                            <div className="flex items-center gap-3">
                                                <MemberAvatar
                                                    name={member?.name ?? '?'}
                                                    avatarUrl={member?.avatarUrl}
                                                    color={getMemberColor(summary.memberId, bill.members)}
                                                                                                    />
                                                <span className="font-semibold">
                                                    {getMemberName(bill.members, summary.memberId)}
                                                </span>
                                            </div>
                                            <span
                                                className={cn(
                                                    "font-bold",
                                                    summary.balance > 0 && "text-positive",
                                                    summary.balance < 0 && "text-negative",
                                                    summary.balance === 0 && "text-muted-foreground"
                                                )}
                                            >
                                                {summary.balance > 0
                                                    ? `應收 ${formatAmount(summary.balance)}`
                                                    : summary.balance < 0
                                                        ? `應付 ${formatAmount(Math.abs(summary.balance))}`
                                                        : '已結清'}
                                            </span>
                                        </div>
                                    );
                                })}
                        </CardContent>
                    </Card>

                    {/* 轉帳清單 */}
                    <Card>
                        <CardHeader className="pb-2">
                            <div className="flex items-center justify-between">
                                <CardTitle className="text-sm font-medium text-muted-foreground">
                                    轉帳清單
                                </CardTitle>
                                {settlement.transfers.length > 0 && (
                                    <span
                                        className={cn(
                                            "text-xs font-semibold",
                                            allSettled ? "text-success" : "text-muted-foreground"
                                        )}
                                    >
                                        {allSettled ? '✓ 全部結清' : `${settledCount}/${settlement.transfers.length}`}
                                    </span>
                                )}
                            </div>
                        </CardHeader>
                        <CardContent>
                            {settlement.transfers.length === 0 ? (
                                <div className="py-6 text-center bg-success/10 border border-success/30 rounded-lg">
                                    <CheckCircle2 className="h-8 w-8 text-success mx-auto mb-2"/>
                                    <p className="font-semibold text-success">
                                        無需轉帳
                                    </p>
                                    <p className="text-sm text-muted-foreground mt-1">
                                        每個人都剛好付了自己的份
                                    </p>
                                </div>
                            ) : (
                                <div className="space-y-3">
                                    {settlement.transfers.map((transfer, index) => {
                                        const isSettled = isTransferSettled(
                                            bill.settledTransfers,
                                            transfer.from,
                                            transfer.to
                                        );
                                        const fromMember = bill.members.find(m => m.id === transfer.from);
                                        const toMember = bill.members.find(m => m.id === transfer.to);

                                        return (
                                            <div
                                                key={index}
                                                onClick={isReadOnly ? undefined : () => toggleSettlement(transfer.from, transfer.to)}
                                                className={cn(
                                                    "p-4 rounded-xl border transition-all",
                                                    isReadOnly ? "cursor-default" : "cursor-pointer",
                                                    isSettled
                                                        ? "bg-muted/50 opacity-60"
                                                        : "bg-background shadow-soft hover:border-primary hover:shadow-elevated hover:-translate-y-0.5 active:scale-[0.98]"
                                                )}
                                            >
                                                <div className="flex items-start gap-3">
                                                    {isSettled ? (
                                                        <CheckCircle2 className="h-5 w-5 text-success mt-0.5 shrink-0"/>
                                                    ) : (
                                                        <Circle className="h-5 w-5 text-muted-foreground/50 mt-0.5 shrink-0"/>
                                                    )}
                                                    <div className="flex-1 min-w-0">
                                                        {/* 轉帳方向 */}
                                                        <div className="flex items-center gap-2 flex-wrap mb-1">
                                                            <div className="flex items-center gap-1.5">
                                                                <MemberAvatar
                                                                    name={fromMember?.name ?? '?'}
                                                                    avatarUrl={fromMember?.avatarUrl}
                                                                    color={getMemberColor(transfer.from, bill.members)}
                                                                                                                                        size="sm"
                                                                />
                                                                <span className="font-semibold">
                                                                    {getMemberName(bill.members, transfer.from)}
                                                                </span>
                                                            </div>
                                                            <ArrowRight className="h-4 w-4 text-muted-foreground"/>
                                                            <div className="flex items-center gap-1.5">
                                                                <MemberAvatar
                                                                    name={toMember?.name ?? '?'}
                                                                    avatarUrl={toMember?.avatarUrl}
                                                                    color={getMemberColor(transfer.to, bill.members)}
                                                                                                                                        size="sm"
                                                                />
                                                                <span className="font-semibold">
                                                                    {getMemberName(bill.members, transfer.to)}
                                                                </span>
                                                            </div>
                                                        </div>
                                                        {/* 金額 */}
                                                        <p
                                                            className={cn(
                                                                "text-2xl font-bold",
                                                                isSettled && "text-muted-foreground line-through"
                                                            )}
                                                        >
                                                            {formatAmount(transfer.amount)}
                                                        </p>
                                                        {/* 提示文字 */}
                                                        {!isReadOnly && (
                                                            <p className="text-xs text-muted-foreground mt-1">
                                                                {isSettled ? '已結清 · 點擊取消' : '點擊標記為已結清'}
                                                            </p>
                                                        )}
                                                    </div>
                                                </div>
                                            </div>
                                        );
                                    })}
                                </div>
                            )}
                        </CardContent>
                    </Card>
                </>
            )}
        </div>
    );
}
