import {useMemo, useState} from 'react';
import {AlertTriangle, CheckCircle2, Link2, Link2Off, Pencil, Plus, Trash2, UserPlus} from 'lucide-react';
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
import {MemberAvatar} from '@/shared/components';
import {useSnapSplitStore} from '@/features/snap-split/stores/snapSplitStore';
import {useAuthStore} from '@/stores/authStore';
import type {Bill, Member} from '@/features/snap-split/types/snap-split';
import {formatAmount, getMemberColor} from '@/features/snap-split/lib/settlement';
import {cn} from '@/shared/lib/utils';

interface AffectedExpenses {
    /** 將被刪除的消費（付款人或唯一平分人） */
    deletedExpenses: { name: string; amount: number; reason: 'payer' | 'sole_participant' }[];
    /** 將被刪除的品項（付款人或唯一平分人） */
    deletedItems: { expenseName: string; itemName: string; amount: number; reason: 'payer' | 'sole_participant' }[];
    /** 該成員是平分人的消費（將重新分攤） */
    participantExpenses: { name: string; currentCount: number }[];
    /** 該成員是平分人的品項（將重新分攤） */
    participantItems: { expenseName: string; itemName: string; currentCount: number }[];
}

/**
 * 計算刪除成員會影響的消費紀錄
 */
function getAffectedExpenses(bill: Bill, memberId: string): AffectedExpenses {
    const result: AffectedExpenses = {
        deletedExpenses: [],
        deletedItems: [],
        participantExpenses: [],
        participantItems: [],
    };

    for (const expense of bill.expenses) {
        if (expense.isItemized) {
            // 逐項紀錄：檢查每個品項
            for (const item of expense.items) {
                if (item.paidById === memberId) {
                    // 付款人 → 刪除
                    result.deletedItems.push({
                        expenseName: expense.name,
                        itemName: item.name,
                        amount: item.amount,
                        reason: 'payer',
                    });
                } else if (item.participants.includes(memberId)) {
                    if (item.participants.length === 1) {
                        // 唯一平分人 → 刪除
                        result.deletedItems.push({
                            expenseName: expense.name,
                            itemName: item.name,
                            amount: item.amount,
                            reason: 'sole_participant',
                        });
                    } else {
                        // 多人平分 → 重新分攤
                        result.participantItems.push({
                            expenseName: expense.name,
                            itemName: item.name,
                            currentCount: item.participants.length,
                        });
                    }
                }
            }
        } else {
            // 一般消費
            if (expense.paidById === memberId) {
                // 付款人 → 刪除
                result.deletedExpenses.push({
                    name: expense.name,
                    amount: expense.amount,
                    reason: 'payer',
                });
            } else if (expense.participants.includes(memberId)) {
                if (expense.participants.length === 1) {
                    // 唯一平分人 → 刪除
                    result.deletedExpenses.push({
                        name: expense.name,
                        amount: expense.amount,
                        reason: 'sole_participant',
                    });
                } else {
                    // 多人平分 → 重新分攤
                    result.participantExpenses.push({
                        name: expense.name,
                        currentCount: expense.participants.length,
                    });
                }
            }
        }
    }

    return result;
}

interface MemberDialogProps {
    bill: Bill;
    open: boolean;
    onClose: () => void;
    isReadOnly?: boolean;
}

export function MemberDialog({bill, open, onClose, isReadOnly = false}: MemberDialogProps) {
    const {addMember, removeMember, updateMember, claimMember, unclaimMember} = useSnapSplitStore();
    const {user, isAuthenticated} = useAuthStore();

    const [addOpen, setAddOpen] = useState(false);
    const [newName, setNewName] = useState('');
    const [editOpen, setEditOpen] = useState(false);
    const [editingMember, setEditingMember] = useState<Member | null>(null);
    const [editName, setEditName] = useState('');
    const [deleteOpen, setDeleteOpen] = useState(false);
    const [deletingMember, setDeletingMember] = useState<Member | null>(null);
    const [claimConfirmOpen, setClaimConfirmOpen] = useState(false);
    const [claimingMember, setClaimingMember] = useState<Member | null>(null);
    const [unclaimConfirmOpen, setUnclaimConfirmOpen] = useState(false);
    const [unclaimingMember, setUnclaimingMember] = useState<Member | null>(null);

    // 計算刪除成員會影響的消費紀錄
    const affectedExpenses = useMemo(() => {
        if (!deletingMember) return null;
        return getAffectedExpenses(bill, deletingMember.id);
    }, [bill, deletingMember]);

    const hasAffectedRecords = useMemo(() => {
        if (!affectedExpenses) return false;
        return (
            affectedExpenses.deletedExpenses.length > 0 ||
            affectedExpenses.deletedItems.length > 0 ||
            affectedExpenses.participantExpenses.length > 0 ||
            affectedExpenses.participantItems.length > 0
        );
    }, [affectedExpenses]);

    // 找出當前使用者已認領的成員
    const myClaimedMember = user?.id ? bill.members.find(m => m.userId === user.id) : undefined;

    const canClaimMember = (member: Member) => {
        if (!isAuthenticated || !user?.id) return false;
        if (member.userId) return false;
        if (myClaimedMember) return false;
        return true;
    };

    const canUnclaimMember = (member: Member) => {
        if (!user?.id) return false;
        return member.userId === user.id || bill.ownerId === user.id;
    };

    const handleOpenClaimConfirm = (member: Member) => {
        setClaimingMember(member);
        setClaimConfirmOpen(true);
    };

    const handleClaim = () => {
        if (claimingMember && user?.id) {
            claimMember({
                memberId: claimingMember.id,
                userId: user.id,
                displayName: user.displayName ?? user.email ?? '使用者',
                avatarUrl: user.avatarUrl ?? undefined,
            });
        }
        setClaimConfirmOpen(false);
        setClaimingMember(null);
    };

    const handleOpenUnclaimConfirm = (member: Member) => {
        setUnclaimingMember(member);
        setUnclaimConfirmOpen(true);
    };

    const handleUnclaim = () => {
        if (unclaimingMember) {
            unclaimMember(unclaimingMember.id);
        }
        setUnclaimConfirmOpen(false);
        setUnclaimingMember(null);
    };

    const handleAdd = () => {
        if (newName.trim()) {
            addMember(newName.trim());
            setNewName('');
            setAddOpen(false);
        }
    };

    const handleOpenEdit = (member: Member) => {
        setEditingMember(member);
        setEditName(member.name);
        setEditOpen(true);
    };

    const handleSaveEdit = () => {
        if (editingMember && editName.trim()) {
            updateMember(editingMember.id, editName.trim());
        }
        setEditOpen(false);
        setEditingMember(null);
    };

    const handleOpenDelete = (member: Member) => {
        setDeletingMember(member);
        setDeleteOpen(true);
    };

    const handleDelete = () => {
        if (deletingMember) {
            removeMember(deletingMember.id);
        }
        setDeleteOpen(false);
        setDeletingMember(null);
    };

    return (
        <>
            {/* 主對話框 */}
            <Dialog open={open} onOpenChange={onClose}>
                <DialogContent className="sm:max-w-md">
                    <DialogHeader>
                        <DialogTitle>
                            成員管理
                            {bill.members.length > 0 && (
                                <span className="text-muted-foreground font-normal ml-2">
                                    ({bill.members.length} 人)
                                </span>
                            )}
                        </DialogTitle>
                    </DialogHeader>

                    {/* 成員列表 */}
                    <div className="mt-4">
                        {bill.members.length === 0 && !isReadOnly ? (
                            <div className="flex flex-col items-center py-8">
                                <div className="w-16 h-16 bg-muted rounded-2xl flex items-center justify-center mb-4">
                                    <UserPlus className="h-8 w-8 text-muted-foreground"/>
                                </div>
                                <p className="text-muted-foreground mb-6">
                                    新增成員來開始分帳
                                </p>
                                <Button size="lg" onClick={() => setAddOpen(true)}>
                                    <Plus className="h-5 w-5 mr-2"/>
                                    新增第一位成員
                                </Button>
                            </div>
                        ) : (
                            <div className="space-y-2 max-h-[50vh] overflow-y-auto">
                                {bill.members.map(member => {
                                    const isClaimed = !!member.userId;
                                    const isMyMember = !!user?.id && member.userId === user.id;

                                    return (
                                        <div
                                            key={member.id}
                                            className={cn(
                                                "flex items-center justify-between p-3 rounded-lg",
                                                isMyMember
                                                    ? "bg-primary/10 border-2 border-primary"
                                                    : "bg-muted/50 hover:bg-muted"
                                            )}
                                        >
                                            <div className="flex items-center gap-3">
                                                <MemberAvatar
                                                    name={member.name}
                                                    avatarUrl={member.avatarUrl}
                                                    color={isClaimed ? undefined : getMemberColor(member.id, bill.members)}
                                                    isPrimary={isClaimed}
                                                    size="lg"
                                                />
                                                <div>
                                                    <div className="flex items-center gap-2">
                                                        <span className="font-semibold">{member.name}</span>
                                                        {isMyMember && (
                                                            <span className="inline-flex items-center gap-1 px-2 py-0.5 text-xs font-medium rounded-full bg-primary text-primary-foreground">
                                                                <CheckCircle2 className="h-3 w-3"/>
                                                                我
                                                            </span>
                                                        )}
                                                        {isClaimed && !isMyMember && (
                                                            <CheckCircle2 className="h-4 w-4 text-success"/>
                                                        )}
                                                    </div>
                                                    {member.originalName && member.originalName !== member.name && (
                                                        <p className="text-xs text-muted-foreground">
                                                            ({member.originalName})
                                                        </p>
                                                    )}
                                                </div>
                                            </div>

                                            {/* 操作按鈕 */}
                                            {!isReadOnly && (
                                                <div className="flex items-center gap-1">
                                                    {/* 認領/取消認領 */}
                                                    {isAuthenticated && (
                                                        isClaimed ? (
                                                            canUnclaimMember(member) && (
                                                                <Button
                                                                    variant="ghost"
                                                                    size="icon"
                                                                    onClick={() => handleOpenUnclaimConfirm(member)}
                                                                    className="text-warning hover:text-warning hover:bg-warning/10"
                                                                    title="取消認領"
                                                                >
                                                                    <Link2Off className="h-4 w-4"/>
                                                                </Button>
                                                            )
                                                        ) : (
                                                            canClaimMember(member) && (
                                                                <Button
                                                                    variant="ghost"
                                                                    size="icon"
                                                                    onClick={() => handleOpenClaimConfirm(member)}
                                                                    className="text-primary hover:text-primary hover:bg-primary/10"
                                                                    title="這是我"
                                                                >
                                                                    <Link2 className="h-4 w-4"/>
                                                                </Button>
                                                            )
                                                        )
                                                    )}

                                                    {/* 編輯 */}
                                                    {(!isClaimed || isMyMember) && (
                                                        <Button
                                                            variant="ghost"
                                                            size="icon"
                                                            onClick={() => handleOpenEdit(member)}
                                                            title="編輯名稱"
                                                        >
                                                            <Pencil className="h-4 w-4"/>
                                                        </Button>
                                                    )}

                                                    {/* 刪除 */}
                                                    {!isClaimed && (
                                                        <Button
                                                            variant="ghost"
                                                            size="icon"
                                                            onClick={() => handleOpenDelete(member)}
                                                            className="text-destructive hover:text-destructive hover:bg-destructive/10"
                                                            title="刪除成員"
                                                        >
                                                            <Trash2 className="h-4 w-4"/>
                                                        </Button>
                                                    )}
                                                </div>
                                            )}
                                        </div>
                                    );
                                })}
                            </div>
                        )}
                    </div>

                    <DialogFooter className="mt-6 gap-2">
                        {!isReadOnly && bill.members.length > 0 && (
                            <Button variant="outline" onClick={() => setAddOpen(true)} className="sm:mr-auto">
                                <Plus className="h-4 w-4 mr-2"/>
                                新增成員
                            </Button>
                        )}
                        <Button onClick={onClose}>完成</Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            {/* 新增成員對話框 */}
            <Dialog open={addOpen} onOpenChange={setAddOpen}>
                <DialogContent className="sm:max-w-md">
                    <DialogHeader>
                        <DialogTitle>新增成員</DialogTitle>
                    </DialogHeader>
                    <div className="mt-4 space-y-4">
                        <div className="space-y-2">
                            <Label htmlFor="new-member-name">成員名稱</Label>
                            <Input
                                id="new-member-name"
                                placeholder="例如：小明"
                                value={newName}
                                onChange={(e) => setNewName(e.target.value)}
                                onKeyDown={(e) => e.key === 'Enter' && handleAdd()}
                                autoFocus
                            />
                        </div>
                    </div>
                    <DialogFooter className="mt-6">
                        <Button variant="outline" onClick={() => setAddOpen(false)}>
                            取消
                        </Button>
                        <Button onClick={handleAdd} disabled={!newName.trim()}>
                            新增
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            {/* 編輯成員對話框 */}
            <Dialog open={editOpen} onOpenChange={setEditOpen}>
                <DialogContent className="sm:max-w-md">
                    <DialogHeader>
                        <DialogTitle>編輯成員</DialogTitle>
                    </DialogHeader>
                    <div className="mt-4 space-y-4">
                        <div className="space-y-2">
                            <Label htmlFor="edit-member-name">成員名稱</Label>
                            <Input
                                id="edit-member-name"
                                value={editName}
                                onChange={(e) => setEditName(e.target.value)}
                                onKeyDown={(e) => e.key === 'Enter' && handleSaveEdit()}
                                autoFocus
                            />
                        </div>
                    </div>
                    <DialogFooter className="mt-6">
                        <Button variant="outline" onClick={() => setEditOpen(false)}>
                            取消
                        </Button>
                        <Button onClick={handleSaveEdit} disabled={!editName.trim()}>
                            儲存
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            {/* 刪除確認對話框 */}
            <Dialog open={deleteOpen} onOpenChange={setDeleteOpen}>
                <DialogContent className="sm:max-w-md">
                    <DialogHeader>
                        <DialogTitle className="flex items-center gap-2">
                            {hasAffectedRecords && <AlertTriangle className="h-5 w-5 text-warning"/>}
                            確認刪除
                        </DialogTitle>
                        <DialogDescription>
                            確定要刪除成員「{deletingMember?.name}」嗎？
                        </DialogDescription>
                    </DialogHeader>

                    {/* 受影響的紀錄警告 */}
                    {hasAffectedRecords && affectedExpenses && (
                        <div className="space-y-3 mt-4">
                            {/* 將被刪除的紀錄 */}
                            {(affectedExpenses.deletedExpenses.length > 0 || affectedExpenses.deletedItems.length > 0) && (
                                <div className="rounded-lg bg-destructive/10 p-3 space-y-2">
                                    <p className="text-sm font-medium text-destructive flex items-center gap-1.5">
                                        <Trash2 className="h-4 w-4"/>
                                        以下紀錄將被刪除
                                    </p>
                                    <ul className="text-sm text-destructive/90 space-y-1 ml-5">
                                        {affectedExpenses.deletedExpenses.map((exp, i) => (
                                            <li key={`exp-${i}`} className="list-disc">
                                                {exp.name}（{formatAmount(exp.amount)}）
                                                {exp.reason === 'sole_participant' && (
                                                    <span className="text-xs ml-1">- 唯一平分人</span>
                                                )}
                                            </li>
                                        ))}
                                        {affectedExpenses.deletedItems.map((item, i) => (
                                            <li key={`item-${i}`} className="list-disc">
                                                {item.expenseName} - {item.itemName}（{formatAmount(item.amount)}）
                                                {item.reason === 'sole_participant' && (
                                                    <span className="text-xs ml-1">- 唯一平分人</span>
                                                )}
                                            </li>
                                        ))}
                                    </ul>
                                </div>
                            )}

                            {/* 將重新分攤的紀錄 */}
                            {(affectedExpenses.participantExpenses.length > 0 || affectedExpenses.participantItems.length > 0) && (
                                <div className="rounded-lg bg-warning/10 p-3 space-y-2">
                                    <p className="text-sm font-medium text-warning flex items-center gap-1.5">
                                        <AlertTriangle className="h-4 w-4"/>
                                        以下紀錄將重新分攤
                                    </p>
                                    <ul className="text-sm text-warning/90 space-y-1 ml-5">
                                        {affectedExpenses.participantExpenses.map((exp, i) => (
                                            <li key={`pexp-${i}`} className="list-disc">
                                                {exp.name}（/{exp.currentCount} → /{exp.currentCount - 1}）
                                            </li>
                                        ))}
                                        {affectedExpenses.participantItems.map((item, i) => (
                                            <li key={`pitem-${i}`} className="list-disc">
                                                {item.expenseName} - {item.itemName}（/{item.currentCount} → /{item.currentCount - 1}）
                                            </li>
                                        ))}
                                    </ul>
                                </div>
                            )}
                        </div>
                    )}

                    <DialogFooter className="mt-6">
                        <Button variant="outline" onClick={() => setDeleteOpen(false)}>
                            取消
                        </Button>
                        <Button variant="destructive" onClick={handleDelete}>
                            {hasAffectedRecords ? '確認刪除' : '刪除'}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            {/* 認領確認對話框 */}
            <Dialog open={claimConfirmOpen} onOpenChange={setClaimConfirmOpen}>
                <DialogContent className="sm:max-w-md">
                    <DialogHeader>
                        <DialogTitle>確認認領</DialogTitle>
                    </DialogHeader>
                    <div className="mt-4">
                        <div className="flex items-center gap-4 mb-4">
                            {user?.avatarUrl ? (
                                <img
                                    src={user.avatarUrl}
                                    alt={user.displayName ?? ''}
                                    className="h-12 w-12 rounded-full"
                                />
                            ) : (
                                <div className="h-12 w-12 rounded-full bg-primary flex items-center justify-center text-primary-foreground font-semibold text-lg">
                                    {(user?.displayName ?? user?.email)?.charAt(0)}
                                </div>
                            )}
                            <div>
                                <p className="font-semibold">
                                    {user?.displayName ?? user?.email}
                                </p>
                                <p className="text-sm text-muted-foreground">你的帳號</p>
                            </div>
                        </div>
                        <p className="text-muted-foreground">
                            確定將成員「{claimingMember?.name}」與你的帳號綁定？
                        </p>
                        <p className="text-sm text-muted-foreground mt-2">
                            綁定後，成員名稱會更新為你的 LINE 顯示名稱。
                        </p>
                    </div>
                    <DialogFooter className="mt-6">
                        <Button variant="outline" onClick={() => setClaimConfirmOpen(false)}>
                            取消
                        </Button>
                        <Button onClick={handleClaim}>
                            <Link2 className="h-4 w-4 mr-2"/>
                            確認認領
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            {/* 取消認領確認對話框 */}
            <Dialog open={unclaimConfirmOpen} onOpenChange={setUnclaimConfirmOpen}>
                <DialogContent className="sm:max-w-md">
                    <DialogHeader>
                        <DialogTitle>取消認領</DialogTitle>
                    </DialogHeader>
                    <div className="mt-4">
                        <p className="text-muted-foreground">
                            確定要取消認領成員「{unclaimingMember?.name}」？
                        </p>
                        <p className="text-sm text-muted-foreground mt-2">
                            取消後，成員名稱將還原為原本的名稱
                            {unclaimingMember?.originalName && `「${unclaimingMember.originalName}」`}
                            。
                        </p>
                    </div>
                    <DialogFooter className="mt-6">
                        <Button variant="outline" onClick={() => setUnclaimConfirmOpen(false)}>
                            取消
                        </Button>
                        <Button
                            variant="outline"
                            className="text-warning hover:text-warning border-warning/50 hover:bg-warning/10"
                            onClick={handleUnclaim}
                        >
                            <Link2Off className="h-4 w-4 mr-2"/>
                            確認取消
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </>
    );
}
