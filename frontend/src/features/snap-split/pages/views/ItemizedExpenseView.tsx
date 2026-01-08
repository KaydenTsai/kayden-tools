import {useCallback, useMemo, useRef, useState} from 'react';
import {ArrowLeft, ChevronDown, ChevronUp, Copy, Plus, Trash2} from 'lucide-react';
import {Button} from '@/shared/components/ui/button';
import {Input} from '@/shared/components/ui/input';
import {Label} from '@/shared/components/ui/label';
import {MemberSelector, ParticipantChips} from '@/shared/components';
import {useSnapSplitStore} from '@/features/snap-split/stores/snapSplitStore';
import type {Bill, ExpenseItem, Member} from '@/features/snap-split/types/snap-split';
import {applyServiceFee, formatAmount, getMemberColor, getMemberName} from '@/features/snap-split/lib/settlement';
import {cn} from '@/shared/lib/utils';

interface ItemizedExpenseViewProps {
    bill: Bill;
    expenseId?: string;
    onClose: () => void;
}

interface ItemFormState {
    name: string;
    amount: string;
    paidById: string;
    participants: string[];
}

// 解析金額（支持表達式如 "100+50"）
const parseAmount = (value: string): number | null => {
    if (!value.trim()) return null;
    try {
        const sanitized = value.replace(/[^0-9+\-*/.()]/g, '');
        const result = Function(`"use strict"; return (${sanitized})`)();
        return typeof result === 'number' && !isNaN(result) ? result : null;
    } catch {
        return null;
    }
};

export function ItemizedExpenseView({bill, expenseId, onClose}: ItemizedExpenseViewProps) {
    const {addExpense, updateExpense} = useSnapSplitStore();

    const existingExpense = expenseId ? bill.expenses.find(e => e.id === expenseId) : null;

    const [expenseName, setExpenseName] = useState(existingExpense?.name || '');
    const [serviceFeePercent, setServiceFeePercent] = useState(
        existingExpense?.serviceFeePercent?.toString() || '0'
    );
    const [items, setItems] = useState<(ExpenseItem & { tempId?: string })[]>(
        existingExpense?.items || []
    );
    const [expandedItemId, setExpandedItemId] = useState<string | null>(null);
    const [deletedItemIds, setDeletedItemIds] = useState<string[]>([]);

    // 快速新增表單
    const [quickAdd, setQuickAdd] = useState<ItemFormState>({
        name: '',
        amount: '',
        paidById: bill.members[0]?.id || '',
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

    const quickAddAmountValue = parseAmount(quickAdd.amount);

    // 快速新增品項
    const handleQuickAdd = useCallback(() => {
        const amount = parseAmount(quickAdd.amount);
        if (!quickAdd.name.trim() || amount === null || !quickAdd.paidById || quickAdd.participants.length === 0) {
            return;
        }

        const itemId = crypto.randomUUID();
        const newItem: ExpenseItem & { tempId?: string } = {
            id: itemId,
            tempId: itemId,  // 重用相同 UUID 作為 React key
            name: quickAdd.name.trim(),
            amount,
            paidById: quickAdd.paidById,
            participants: quickAdd.participants,
        };

        setItems(prev => [newItem, ...prev]);
        setQuickAdd(prev => ({
            name: '',
            amount: '',
            paidById: prev.paidById,
            participants: prev.participants,
        }));

        setTimeout(() => quickAddNameRef.current?.focus(), 50);
    }, [quickAdd]);

    // Enter 鍵處理
    const handleQuickAddKeyDown = (e: React.KeyboardEvent, field: 'name' | 'amount') => {
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
            i === index ? {...item, ...updates} : item
        ));
    };

    // 刪除品項
    const handleDeleteItem = (index: number) => {
        const itemToDelete = items[index];
        // Track remoteId for sync if the item was previously synced
        if (itemToDelete?.remoteId) {
            setDeletedItemIds(prev => [...prev, itemToDelete.remoteId!]);
        }
        setItems(prev => prev.filter((_, i) => i !== index));
        setExpandedItemId(null);
    };

    // 複製品項
    const handleDuplicateItem = (index: number) => {
        const item = items[index];
        const newItemId = crypto.randomUUID();
        const newItem = {
            ...item,
            id: newItemId,
            tempId: newItemId,  // 重用相同 UUID 作為 React key
            // 新品項不應繼承原品項的 remoteId
            remoteId: undefined,
        };
        setItems(prev => [...prev.slice(0, index + 1), newItem, ...prev.slice(index + 1)]);
    };

    // 切換展開
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
            paidById: '',
            participants: [],
            items: items.map(({tempId, ...item}) => item),
        };

        if (existingExpense) {
            // Merge existing deletedItemIds with newly tracked ones
            const mergedDeletedItemIds = [
                ...(existingExpense.deletedItemIds ?? []),
                ...deletedItemIds,
            ];
            updateExpense(existingExpense.id, {
                ...expenseData,
                deletedItemIds: mergedDeletedItemIds.length > 0 ? mergedDeletedItemIds : undefined,
            });
        } else {
            addExpense(expenseData);
        }

        onClose();
    };

    const canSave = expenseName.trim() && items.length > 0;

    // 取得品項摘要文字
    const getItemSummaryText = (paidById: string, participants: string[]) => {
        const payerName = getMemberName(bill.members, paidById);
        if (participants.length === 0) {
            return `${payerName} 付款（未指定平分）`;
        }
        if (participants.length === bill.members.length) {
            return `${payerName} 先付，大家平分`;
        }
        if (participants.length === 1) {
            const participantName = getMemberName(bill.members, participants[0]);
            if (participants[0] === paidById) {
                return `${payerName} 自己付`;
            }
            return `${payerName} 幫 ${participantName} 付`;
        }
        return `${payerName} 幫 ${participants.length} 人先付`;
    };

    return (
        <div className="fixed inset-0 z-50 bg-background overflow-auto pb-32">
            {/* Sticky Header */}
            <div className="sticky top-0 z-10 bg-background border-b">
                <div className="flex items-center gap-2 px-2 py-2">
                    <Button variant="ghost" size="icon" onClick={onClose}>
                        <ArrowLeft className="h-5 w-5"/>
                    </Button>
                    <input
                        type="text"
                        placeholder="輸入名稱"
                        value={expenseName}
                        onChange={(e) => setExpenseName(e.target.value)}
                        className="flex-1 min-w-0 bg-transparent font-semibold text-base outline-none placeholder:text-muted-foreground/60"
                    />
                    <span className="text-sm text-muted-foreground shrink-0">
                        {items.length} 個品項
                    </span>
                </div>
            </div>

            {/* 品項列表 */}
            <div className="p-4 space-y-3">
                {/* 快速新增區塊 */}
                <div className="p-4 border-2 border-primary bg-primary/5 rounded-xl">
                    <div className="flex items-center gap-1.5 mb-3 text-primary font-semibold text-sm">
                        <Plus className="h-4 w-4"/>
                        新增品項
                    </div>

                    <ItemForm
                        members={bill.members}
                        values={quickAdd}
                        onChange={(updates) => setQuickAdd(prev => ({...prev, ...updates}))}
                        onKeyDown={handleQuickAddKeyDown}
                        nameRef={quickAddNameRef}
                        amountRef={quickAddAmountRef}
                    />

                    <div className="flex gap-2 mt-3">
                        {(quickAdd.name || quickAdd.amount) && (
                            <Button
                                variant="ghost"
                                onClick={() => setQuickAdd(prev => ({
                                    name: '',
                                    amount: '',
                                    paidById: prev.paidById,
                                    participants: prev.participants,
                                }))}
                            >
                                清空
                            </Button>
                        )}
                        <Button
                            className="flex-1"
                            onClick={handleQuickAdd}
                            disabled={!quickAdd.name.trim() || quickAddAmountValue === null || !quickAdd.paidById || quickAdd.participants.length === 0}
                        >
                            <Plus className="h-4 w-4 mr-2"/>
                            新增此品項
                        </Button>
                    </div>
                </div>

                {/* 已新增品項標題 */}
                {items.length > 0 && (
                    <p className="text-sm text-muted-foreground pt-2">
                        已新增 {items.length} 個品項（點擊展開編輯）
                    </p>
                )}

                {/* 已新增的品項列表 */}
                {items.map((item, index) => {
                    const itemId = item.tempId || item.id;
                    const isExpanded = expandedItemId === itemId;

                    return (
                        <div
                            key={itemId}
                            className={cn(
                                "border rounded-xl overflow-hidden transition-colors",
                                isExpanded ? "border-primary shadow-soft" : "border-border"
                            )}
                        >
                            {/* 摘要列 */}
                            <button
                                onClick={() => toggleExpand(itemId)}
                                className="w-full p-3 flex items-center gap-2 hover:bg-muted/50 transition-colors"
                            >
                                <span className="text-sm text-muted-foreground w-6">
                                    {index + 1}.
                                </span>
                                <div className="flex-1 min-w-0 text-left">
                                    <p className="font-semibold truncate">{item.name}</p>
                                    <p className="text-xs text-muted-foreground">
                                        {getItemSummaryText(item.paidById, item.participants)}
                                    </p>
                                </div>
                                <span className="font-bold text-primary shrink-0">
                                    {formatAmount(item.amount)}
                                </span>
                                {isExpanded ? (
                                    <ChevronUp className="h-5 w-5 text-muted-foreground"/>
                                ) : (
                                    <ChevronDown className="h-5 w-5 text-muted-foreground"/>
                                )}
                            </button>

                            {/* 展開編輯區 */}
                            {isExpanded && (
                                <div className="p-4 pt-0 border-t">
                                    <div className="pt-4">
                                        <ItemForm
                                            members={bill.members}
                                            values={{
                                                name: item.name,
                                                amount: item.amount.toString(),
                                                paidById: item.paidById,
                                                participants: item.participants,
                                            }}
                                            onChange={(updates) => {
                                                if (updates.amount !== undefined) {
                                                    const parsed = parseAmount(updates.amount);
                                                    if (parsed !== null) {
                                                        handleUpdateItem(index, {...updates, amount: parsed});
                                                    }
                                                } else {
                                                    handleUpdateItem(index, updates as Partial<ExpenseItem>);
                                                }
                                            }}
                                        />
                                    </div>

                                    {/* 操作按鈕 */}
                                    <div className="flex gap-2 justify-end mt-3">
                                        <Button
                                            variant="outline"
                                            size="sm"
                                            onClick={() => handleDuplicateItem(index)}
                                        >
                                            <Copy className="h-4 w-4 mr-1"/>
                                            複製
                                        </Button>
                                        <Button
                                            variant="outline"
                                            size="sm"
                                            className="text-destructive hover:text-destructive"
                                            onClick={() => handleDeleteItem(index)}
                                        >
                                            <Trash2 className="h-4 w-4 mr-1"/>
                                            刪除
                                        </Button>
                                    </div>
                                </div>
                            )}
                        </div>
                    );
                })}
            </div>

            {/* Sticky Footer */}
            <div className="fixed bottom-0 left-0 right-0 z-20 bg-background/95 backdrop-blur border-t px-4 py-3 safe-area-pb">
                {/* 服務費 */}
                <div className="flex items-center gap-2 mb-2">
                    <div className="flex items-center gap-1">
                        <Label htmlFor="service-fee-footer" className="text-xs shrink-0">服務費</Label>
                        <Input
                            id="service-fee-footer"
                            type="number"
                            value={serviceFeePercent}
                            onChange={(e) => setServiceFeePercent(e.target.value)}
                            className="w-16 h-8 text-center text-sm"
                            min={0}
                            max={100}
                        />
                        <span className="text-sm text-muted-foreground">%</span>
                    </div>
                    <div className="flex-1"/>
                    <span className="text-xs text-muted-foreground">
                        {formatAmount(itemsSubtotal)}
                        {serviceFeeValue > 0 && ` + ${formatAmount(itemsSubtotal * serviceFeeValue / 100)}`}
                    </span>
                </div>

                {/* 總計與儲存 */}
                <div className="flex items-center justify-between">
                    <div className="flex items-baseline gap-1">
                        <span className="text-xs font-bold text-primary">總計</span>
                        <span className="text-2xl font-bold text-primary">
                            {formatAmount(totalWithFee)}
                        </span>
                    </div>
                    <Button size="lg" onClick={handleSave} disabled={!canSave}>
                        儲存
                    </Button>
                </div>
            </div>
        </div>
    );
}

// 品項表單元件
interface ItemFormProps {
    members: Member[];
    values: ItemFormState;
    onChange: (updates: Partial<ItemFormState>) => void;
    onKeyDown?: (e: React.KeyboardEvent, field: 'name' | 'amount') => void;
    nameRef?: React.RefObject<HTMLInputElement>;
    amountRef?: React.RefObject<HTMLInputElement>;
}

function ItemForm({members, values, onChange, onKeyDown, nameRef, amountRef}: ItemFormProps) {
    const handleToggleParticipant = (memberId: string) => {
        const newParticipants = values.participants.includes(memberId)
            ? values.participants.filter(id => id !== memberId)
            : [...values.participants, memberId];
        onChange({participants: newParticipants});
    };

    const evaluatedAmount = parseAmount(values.amount);

    return (
        <div className="space-y-3">
            {/* 名稱 & 金額 */}
            <div className="grid grid-cols-2 gap-2">
                <div>
                    <Label className="text-xs">名稱</Label>
                    <Input
                        ref={nameRef}
                        placeholder="例如：漢堡"
                        value={values.name}
                        onChange={(e) => onChange({name: e.target.value})}
                        onKeyDown={(e) => onKeyDown?.(e, 'name')}
                        className="h-9"
                    />
                </div>
                <div>
                    <Label className="text-xs">金額</Label>
                    <Input
                        ref={amountRef}
                        placeholder="100"
                        value={values.amount}
                        onChange={(e) => onChange({amount: e.target.value})}
                        onKeyDown={(e) => onKeyDown?.(e, 'amount')}
                        className="h-9"
                    />
                    {evaluatedAmount !== null && values.amount.includes('+') && (
                        <p className="text-xs text-muted-foreground mt-0.5">= {formatAmount(evaluatedAmount)}</p>
                    )}
                </div>
            </div>

            {/* 付款人 */}
            <div>
                <Label className="text-xs">付款人</Label>
                <MemberSelector
                    members={members}
                    selectedId={values.paidById}
                    onSelect={(id) => onChange({paidById: id})}
                    getColor={(id) => getMemberColor(id, members)}
                    size="sm"
                    className="mt-1"
                />
            </div>

            {/* 平分者 */}
            <div>
                <div className="flex items-center justify-between mb-1">
                    <Label className="text-xs">平分者</Label>
                    <div className="flex gap-1">
                        <Button
                            variant="ghost"
                            size="sm"
                            className="h-6 px-2 text-xs"
                            onClick={() => onChange({participants: members.map(m => m.id)})}
                        >
                            全選
                        </Button>
                        <Button
                            variant="ghost"
                            size="sm"
                            className="h-6 px-2 text-xs"
                            onClick={() => onChange({participants: []})}
                        >
                            清除
                        </Button>
                    </div>
                </div>
                <ParticipantChips
                    members={members}
                    selectedIds={values.participants}
                    onToggle={handleToggleParticipant}
                    getColor={(id) => getMemberColor(id, members)}
                />
            </div>
        </div>
    );
}
