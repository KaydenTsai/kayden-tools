import {useMemo, useState} from 'react';
import {Plus, Receipt, Scan, SplitSquareVertical, Trash2, UserPlus} from 'lucide-react';
import {Button} from '@/shared/components/ui/button';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '@/shared/components/ui/dialog';
import {Input} from '@/shared/components/ui/input';
import {Label} from '@/shared/components/ui/label';
import {MemberAvatar, MemberSelector, ParticipantChips} from '@/shared/components';
import {useSnapSplitStore} from '@/features/snap-split/stores/snapSplitStore';
import {formatAmount, getExpenseTotal, getMemberColor, getMemberName} from '@/features/snap-split/lib/settlement';
import type {Bill, Expense} from '@/features/snap-split/types/snap-split';
import {cn} from '@/shared/lib/utils';

interface ExpenseListProps {
    bill: Bill;
    isReadOnly?: boolean;
    onOpenMemberDialog?: () => void;
    onOpenItemizedExpense?: (expenseId?: string) => void;
}

interface ExpenseFormState {
    name: string;
    amount: string;
    paidById: string;
    participants: string[];
    serviceFeePercent: string;
}

const emptyForm: ExpenseFormState = {
    name: '',
    amount: '',
    paidById: '',
    participants: [],
    serviceFeePercent: '0',
};

export function ExpenseList({bill, isReadOnly = false, onOpenMemberDialog, onOpenItemizedExpense}: ExpenseListProps) {
    const {addExpense, updateExpense, removeExpense} = useSnapSplitStore();

    const [formOpen, setFormOpen] = useState(false);
    const [editingExpense, setEditingExpense] = useState<Expense | null>(null);
    const [form, setForm] = useState<ExpenseFormState>(emptyForm);
    const [deleteOpen, setDeleteOpen] = useState(false);
    const [deletingExpense, setDeletingExpense] = useState<Expense | null>(null);
    const [addModeOpen, setAddModeOpen] = useState(false);

    // 解析金額（支持表達式如 "100+50"）
    const parseAmount = (value: string): number | null => {
        if (!value.trim()) return null;
        try {
            // 簡單的數學表達式解析
            const sanitized = value.replace(/[^0-9+\-*/.()]/g, '');
            const result = Function(`"use strict"; return (${sanitized})`)();
            return typeof result === 'number' && !isNaN(result) ? result : null;
        } catch {
            return null;
        }
    };

    const evaluatedAmount = useMemo(() => parseAmount(form.amount), [form.amount]);

    const isFormValid = form.name.trim() && evaluatedAmount !== null && form.paidById && form.participants.length > 0;

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
        if (!isFormValid || evaluatedAmount === null) return;

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

    const handleOpenDelete = (expense: Expense, e: React.MouseEvent) => {
        e.stopPropagation();
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

    const handleToggleParticipant = (memberId: string) => {
        setForm(prev => ({
            ...prev,
            participants: prev.participants.includes(memberId)
                ? prev.participants.filter(id => id !== memberId)
                : [...prev.participants, memberId],
        }));
    };

    const handleSelectAllParticipants = () => {
        setForm(prev => ({...prev, participants: bill.members.map(m => m.id)}));
    };

    const handleClearParticipants = () => {
        setForm(prev => ({...prev, participants: []}));
    };

    // 取得逐項紀錄的付款人資訊
    const getItemizedPayerInfo = (expense: Expense) => {
        const uniquePayerIds = [...new Set(expense.items.map(item => item.paidById).filter(Boolean))];
        return {
            payerIds: uniquePayerIds,
            primaryPayerId: uniquePayerIds[0] || '',
            payerCount: uniquePayerIds.length,
        };
    };

    // 渲染費用摘要文字
    const renderExpenseSummary = (expense: Expense) => {
        // 逐項紀錄：從品項中提取付款人資訊
        if (expense.isItemized) {
            const { primaryPayerId, payerCount } = getItemizedPayerInfo(expense);
            if (payerCount === 0) {
                return '細項紀錄';
            } else if (payerCount === 1) {
                const payerName = getMemberName(bill.members, primaryPayerId);
                return `${payerName} 付款`;
            } else {
                const firstPayerName = getMemberName(bill.members, primaryPayerId);
                return `${firstPayerName} 等 ${payerCount} 人分付`;
            }
        }

        // 整筆紀錄
        const payerName = getMemberName(bill.members, expense.paidById);
        const participantCount = expense.participants.length;

        if (participantCount === 0) {
            return `${payerName} 付款`;
        } else if (participantCount === 1) {
            const participantName = getMemberName(bill.members, expense.participants[0]);
            if (expense.participants[0] === expense.paidById) {
                return `${payerName} 自己付`;
            } else {
                return `${payerName} 幫 ${participantName} 付`;
            }
        } else {
            return `${payerName} 幫 ${participantCount} 人先付`;
        }
    };

    // 空狀態：沒有成員
    if (bill.members.length === 0) {
        return (
            <div className="flex flex-col items-center justify-center py-16 px-6">
                <div className="w-16 h-16 bg-muted rounded-2xl flex items-center justify-center mb-4">
                    <Receipt className="h-8 w-8 text-muted-foreground"/>
                </div>
                <p className="text-muted-foreground text-center mb-6">
                    {isReadOnly ? '此帳單尚無成員' : '請先新增成員才能記錄消費'}
                </p>
                {!isReadOnly && onOpenMemberDialog && (
                    <Button size="lg" onClick={onOpenMemberDialog}>
                        <UserPlus className="h-5 w-5 mr-2"/>
                        新增成員
                    </Button>
                )}
            </div>
        );
    }

    return (
        <>
            <div className="m-4 p-4 bg-card rounded-xl border">
                {/* 標題 */}
                <div className="flex items-center gap-2 mb-4">
                    <Receipt className="h-5 w-5 text-primary"/>
                    <h3 className="font-bold">消費紀錄</h3>
                    {bill.expenses.length > 0 && (
                        <span className="text-sm text-muted-foreground">({bill.expenses.length})</span>
                    )}
                </div>

                {/* 空狀態：沒有消費 */}
                {bill.expenses.length === 0 ? (
                    <div className="flex flex-col items-center py-12">
                        <div className="w-14 h-14 bg-muted/50 rounded-2xl flex items-center justify-center mb-3">
                            <Receipt className="h-7 w-7 text-muted-foreground/50"/>
                        </div>
                        <p className="text-sm text-muted-foreground">
                            {isReadOnly ? '此帳單尚無消費記錄' : '點擊右下角按鈕新增消費'}
                        </p>
                    </div>
                ) : (
                    <div className="space-y-2">
                        {bill.expenses.map(expense => (
                            <div
                                key={expense.id}
                                onClick={isReadOnly ? undefined : () => handleOpenEdit(expense)}
                                className={cn(
                                    "flex items-center gap-3 p-3 rounded-xl border bg-card transition-all duration-150",
                                    !isReadOnly && "cursor-pointer hover:bg-accent hover:border-primary/50 active:scale-[0.98]"
                                )}
                            >
                                {/* Avatar */}
                                {(() => {
                                    // 逐項紀錄：使用第一位付款人，若無則顯示圖示
                                    const payerId = expense.isItemized
                                        ? getItemizedPayerInfo(expense).primaryPayerId
                                        : expense.paidById;
                                    const hasPayer = !!payerId;
                                    const payerMember = hasPayer ? bill.members.find(m => m.id === payerId) : undefined;

                                    if (expense.isItemized && !hasPayer) {
                                        // 逐項紀錄但無付款人：顯示專用圖示
                                        return (
                                            <div className="w-10 h-10 rounded-full flex items-center justify-center bg-muted shrink-0">
                                                <SplitSquareVertical className="h-5 w-5 text-muted-foreground" />
                                            </div>
                                        );
                                    }

                                    return (
                                        <MemberAvatar
                                            name={payerMember?.name ?? '?'}
                                            avatarUrl={payerMember?.avatarUrl}
                                            color={getMemberColor(payerId, bill.members)}
                                            size="lg"
                                        />
                                    );
                                })()}

                                {/* Info */}
                                <div className="flex-1 min-w-0">
                                    <div className="flex items-center justify-between gap-2 mb-0.5">
                                        <span className="font-semibold truncate">{expense.name}</span>
                                        <span className="font-bold text-primary shrink-0">
                                            {formatAmount(getExpenseTotal(expense))}
                                        </span>
                                    </div>
                                    <p className="text-xs text-muted-foreground truncate">
                                        {renderExpenseSummary(expense)}
                                    </p>
                                    {expense.isItemized && (
                                        <p className="text-xs text-muted-foreground/70">
                                            {expense.items.length} 個品項
                                        </p>
                                    )}
                                </div>

                                {/* Delete */}
                                {!isReadOnly && (
                                    <Button
                                        variant="ghost"
                                        size="icon"
                                        onClick={(e) => handleOpenDelete(expense, e)}
                                        className="text-muted-foreground hover:text-destructive hover:bg-destructive/10 shrink-0"
                                    >
                                        <Trash2 className="h-4 w-4"/>
                                    </Button>
                                )}
                            </div>
                        ))}
                    </div>
                )}
            </div>

            {/* FAB - 動態彈性效果 */}
            {!isReadOnly && bill.members.length > 0 && (
                <button
                    onClick={handleOpenAdd}
                    className="fixed bottom-20 md:bottom-8 right-4 z-40 w-14 h-14 bg-primary text-primary-foreground rounded-2xl shadow-lg shadow-primary/25 flex items-center justify-center hover:scale-105 hover:shadow-xl hover:shadow-primary/30 active:scale-95 transition-all duration-200"
                >
                    <Plus className="h-7 w-7" strokeWidth={2.5}/>
                </button>
            )}

            {/* 新增模式選擇 Dialog */}
            <Dialog open={addModeOpen} onOpenChange={setAddModeOpen}>
                <DialogContent className="sm:max-w-md">
                    <DialogHeader>
                        <DialogTitle>新增消費</DialogTitle>
                    </DialogHeader>
                    <div className="space-y-3 py-4">
                        <button
                            onClick={handleAddSimple}
                            className="w-full p-4 text-left border rounded-lg hover:border-primary hover:bg-accent transition-colors"
                        >
                            <div className="flex items-center gap-2 mb-1">
                                <Receipt className="h-4 w-4 text-primary"/>
                                <span className="font-semibold text-primary">整筆紀錄</span>
                            </div>
                            <p className="text-sm text-muted-foreground">
                                適合車資、代付或單項支出。只需輸入一個金額與平分名單。
                            </p>
                        </button>

                        <button
                            onClick={handleAddItemized}
                            className="w-full p-4 text-left border rounded-lg hover:border-primary hover:bg-accent transition-colors"
                        >
                            <div className="flex items-center gap-2 mb-1">
                                <SplitSquareVertical className="h-4 w-4 text-primary"/>
                                <span className="font-semibold text-primary">細項紀錄</span>
                            </div>
                            <p className="text-sm text-muted-foreground">
                                適合複雜紀錄。可對照收據逐項輸入，每個品項指定不同人。
                            </p>
                        </button>

                        <button
                            onClick={() => {
                                setAddModeOpen(false);
                                alert('OCR 智慧掃描即將推出！');
                            }}
                            className="w-full p-4 text-left border border-dashed border-primary/50 bg-primary/5 rounded-lg hover:bg-primary/10 transition-colors"
                        >
                            <div className="flex items-center gap-2 mb-1">
                                <Scan className="h-4 w-4 text-primary"/>
                                <span className="font-semibold text-primary">掃描收據 (AI)</span>
                                <span className="px-1.5 py-0.5 text-[10px] bg-primary text-primary-foreground rounded">
                                    Coming Soon
                                </span>
                            </div>
                            <p className="text-sm text-muted-foreground">
                                拍照自動辨識品項與金額，省去手動輸入的時間。
                            </p>
                        </button>
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setAddModeOpen(false)}>取消</Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            {/* 消費表單 Dialog */}
            <Dialog open={formOpen} onOpenChange={setFormOpen}>
                <DialogContent className="sm:max-w-md">
                    <DialogHeader>
                        <DialogTitle>{editingExpense ? '編輯消費' : '新增消費'}</DialogTitle>
                    </DialogHeader>
                    <div className="space-y-4 py-4">
                        {/* 名稱 */}
                        <div className="space-y-2">
                            <Label htmlFor="expense-name">名稱</Label>
                            <Input
                                id="expense-name"
                                placeholder="例如：晚餐"
                                value={form.name}
                                onChange={(e) => setForm(prev => ({...prev, name: e.target.value}))}
                                autoFocus
                            />
                        </div>

                        {/* 金額 */}
                        <div className="space-y-2">
                            <Label htmlFor="expense-amount">金額</Label>
                            <Input
                                id="expense-amount"
                                placeholder="例如：100 或 100+50"
                                value={form.amount}
                                onChange={(e) => setForm(prev => ({...prev, amount: e.target.value}))}
                            />
                            {evaluatedAmount !== null && form.amount.includes('+') && (
                                <p className="text-xs text-muted-foreground">= {formatAmount(evaluatedAmount)}</p>
                            )}
                        </div>

                        {/* 付款人 */}
                        <div className="space-y-2">
                            <Label>付款人</Label>
                            <MemberSelector
                                members={bill.members}
                                selectedId={form.paidById}
                                onSelect={(id) => setForm(prev => ({...prev, paidById: id}))}
                                getColor={(id) => getMemberColor(id, bill.members)}
                            />
                        </div>

                        {/* 平分者 */}
                        <div className="space-y-2">
                            <div className="flex items-center justify-between">
                                <Label>平分者</Label>
                                <div className="flex gap-1">
                                    <Button variant="ghost" size="sm" onClick={handleSelectAllParticipants}>
                                        全選
                                    </Button>
                                    <Button variant="ghost" size="sm" onClick={handleClearParticipants}>
                                        清除
                                    </Button>
                                </div>
                            </div>
                            <ParticipantChips
                                members={bill.members}
                                selectedIds={form.participants}
                                onToggle={handleToggleParticipant}
                                getColor={(id) => getMemberColor(id, bill.members)}
                            />
                        </div>

                        {/* 服務費 */}
                        <div className="space-y-2">
                            <Label htmlFor="service-fee">服務費 (%)</Label>
                            <Input
                                id="service-fee"
                                type="number"
                                placeholder="0"
                                value={form.serviceFeePercent}
                                onChange={(e) => setForm(prev => ({...prev, serviceFeePercent: e.target.value}))}
                                min={0}
                                max={100}
                            />
                            <p className="text-xs text-muted-foreground">填 0 或留空表示無服務費</p>
                        </div>
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setFormOpen(false)}>取消</Button>
                        <Button onClick={handleSubmit} disabled={!isFormValid}>
                            {editingExpense ? '儲存' : '新增'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            {/* 刪除確認 Dialog */}
            <Dialog open={deleteOpen} onOpenChange={setDeleteOpen}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>確認刪除</DialogTitle>
                        <DialogDescription>
                            確定要刪除消費「{deletingExpense?.name}」嗎？
                        </DialogDescription>
                    </DialogHeader>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setDeleteOpen(false)}>取消</Button>
                        <Button variant="destructive" onClick={handleDelete}>刪除</Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </>
    );
}
