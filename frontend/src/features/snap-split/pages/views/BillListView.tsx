import {useState} from 'react';
import {ChevronRight, Plus, Receipt, Trash2} from 'lucide-react';
import {Button} from '@/shared/components/ui/button';
import {Card, CardContent} from '@/shared/components/ui/card';
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
import {useSnapSplitStore} from '@/features/snap-split/stores/snapSplitStore';
import {formatAmount, getExpenseTotal} from '@/features/snap-split/lib/settlement';
import type {Bill} from '@/features/snap-split/types/snap-split';
import {useAuthStore} from '@/stores/authStore';
import {useToast} from '@/shared/hooks/use-toast';
import {DeleteBillDialog} from '../../components/DeleteBillDialog';

export function BillListView() {
    const {bills, selectBill, createBill, deleteBillWithSync, confirmDeleteBill} = useSnapSplitStore();
    const {user} = useAuthStore();
    const {toast} = useToast();

    const [newOpen, setNewOpen] = useState(false);
    const [newName, setNewName] = useState('');
    const [cloudDeleteOpen, setCloudDeleteOpen] = useState(false);
    const [deletingBill, setDeletingBill] = useState<Bill | null>(null);
    const [isDeleting, setIsDeleting] = useState(false);

    const handleCreate = () => {
        if (newName.trim()) {
            createBill(newName.trim());
            setNewName('');
            setNewOpen(false);
        }
    };

    const handleOpenDelete = (bill: Bill, e: React.MouseEvent) => {
        e.stopPropagation();

        // 使用分類刪除邏輯
        const result = deleteBillWithSync(bill.id, user?.id);

        if (!result) return;

        switch (result.action) {
            case 'deleted':
            case 'left':
                // 成功刪除/離開，列表會自動更新，不需要 Toast
                break;
            case 'confirm_needed':
                // 需要確認：顯示雲端刪除對話框
                setDeletingBill(result.bill);
                setCloudDeleteOpen(true);
                break;
        }
    };

    const handleConfirmDelete = async (deleteFromCloud: boolean) => {
        if (!deletingBill) return;

        setIsDeleting(true);
        try {
            await confirmDeleteBill(deletingBill.id, deleteFromCloud);
            // 成功刪除，列表會自動更新，不需要 Toast
        } catch {
            toast({
                variant: 'destructive',
                title: '刪除失敗',
                description: '無法刪除雲端資料，請稍後再試',
            });
        } finally {
            setIsDeleting(false);
            setCloudDeleteOpen(false);
            setDeletingBill(null);
        }
    };

    const formatDate = (dateString: string) => {
        const date = new Date(dateString);
        return date.toLocaleDateString('zh-TW', {year: 'numeric', month: 'short', day: 'numeric'});
    };

    const getTotalAmount = (bill: Bill) => {
        return bill.expenses.reduce((sum, e) => sum + getExpenseTotal(e), 0);
    };

    // 空狀態
    if (bills.length === 0) {
        return (
            <div className="p-6">
                <div className="flex flex-col items-center justify-center py-12 px-6 bg-card rounded-xl border">
                    <Receipt className="h-16 w-16 text-muted-foreground/50 mb-4"/>
                    <h3 className="text-lg font-semibold text-muted-foreground mb-1">
                        還沒有任何帳單
                    </h3>
                    <p className="text-sm text-muted-foreground/70 mb-6">
                        建立帳單來開始記錄平分
                    </p>
                    <Button size="lg" onClick={() => setNewOpen(true)}>
                        <Plus className="h-5 w-5 mr-2"/>
                        建立第一筆帳單
                    </Button>
                </div>

                <NewBillDialog
                    open={newOpen}
                    onOpenChange={setNewOpen}
                    value={newName}
                    onChange={setNewName}
                    onConfirm={handleCreate}
                />
            </div>
        );
    }

    return (
        <div className="p-4 space-y-4">
            {/* 標題列 */}
            <div className="flex items-center justify-between">
                <h2 className="text-lg font-bold">所有帳單</h2>
                <Button onClick={() => setNewOpen(true)}>
                    <Plus className="h-4 w-4 mr-2"/>
                    新增
                </Button>
            </div>

            {/* 帳單列表 */}
            <div className="grid gap-3 sm:grid-cols-2">
                {bills.map(bill => (
                    <Card
                        key={bill.id}
                        className="cursor-pointer hover:border-primary hover:shadow-elevated hover:-translate-y-1 active:translate-y-0 active:scale-[0.99]"
                        onClick={() => selectBill(bill.id)}
                    >
                        <CardContent className="p-4">
                            {/* 標題列 */}
                            <div className="flex items-start justify-between mb-3">
                                <div className="flex items-center gap-2 min-w-0 flex-1 mr-2">
                                    <h3 className="font-bold truncate">{bill.name}</h3>
                                    <SyncStatusBadge status={bill.syncStatus}/>
                                </div>
                                <ChevronRight className="h-5 w-5 text-muted-foreground shrink-0"/>
                            </div>

                            {/* 統計資訊 */}
                            <div className="flex gap-4 mb-3">
                                <div>
                                    <p className="text-xs text-muted-foreground">成員</p>
                                    <p className="font-semibold">{bill.members.length} 人</p>
                                </div>
                                <div>
                                    <p className="text-xs text-muted-foreground">消費</p>
                                    <p className="font-semibold">{bill.expenses.length} 筆</p>
                                </div>
                                <div>
                                    <p className="text-xs text-muted-foreground">總額</p>
                                    <p className="font-semibold text-primary">
                                        {formatAmount(getTotalAmount(bill))}
                                    </p>
                                </div>
                            </div>

                            {/* 底部資訊 */}
                            <div className="flex items-center justify-between">
                                <p className="text-xs text-muted-foreground">
                                    更新於 {formatDate(bill.updatedAt)}
                                </p>
                                <button
                                    onClick={(e) => handleOpenDelete(bill, e)}
                                    className="p-2 rounded-lg bg-muted hover:bg-destructive hover:text-destructive-foreground transition-colors"
                                >
                                    <Trash2 className="h-4 w-4"/>
                                </button>
                            </div>
                        </CardContent>
                    </Card>
                ))}
            </div>

            {/* 新增帳單對話框 */}
            <NewBillDialog
                open={newOpen}
                onOpenChange={setNewOpen}
                value={newName}
                onChange={setNewName}
                onConfirm={handleCreate}
            />

            {/* 雲端刪除確認對話框 */}
            <DeleteBillDialog
                open={cloudDeleteOpen}
                onOpenChange={setCloudDeleteOpen}
                billName={deletingBill?.name ?? ''}
                onConfirm={handleConfirmDelete}
                isDeleting={isDeleting}
            />
        </div>
    );
}

// 同步狀態徽章
function SyncStatusBadge({status}: { status: string }) {
    if (status === 'synced') {
        return (
            <span className="inline-flex items-center px-1.5 py-0.5 text-[10px] font-medium rounded bg-success/10 text-success">
                已同步
            </span>
        );
    }
    if (status === 'syncing') {
        return (
            <span className="inline-flex items-center px-1.5 py-0.5 text-[10px] font-medium rounded bg-info/10 text-info">
                同步中
            </span>
        );
    }
    if (status === 'error') {
        return (
            <span className="inline-flex items-center px-1.5 py-0.5 text-[10px] font-medium rounded bg-destructive/10 text-destructive">
                同步失敗
            </span>
        );
    }
    return null; // local 或 modified 不顯示
}

// 新增帳單對話框
interface NewBillDialogProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    value: string;
    onChange: (value: string) => void;
    onConfirm: () => void;
}

function NewBillDialog({open, onOpenChange, value, onChange, onConfirm}: NewBillDialogProps) {
    const handleKeyDown = (e: React.KeyboardEvent) => {
        if (e.key === 'Enter' && value.trim()) {
            onConfirm();
        }
    };

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="sm:max-w-md">
                <DialogHeader>
                    <DialogTitle>新增帳單</DialogTitle>
                    <DialogDescription>
                        輸入帳單名稱來建立新帳單
                    </DialogDescription>
                </DialogHeader>
                <div className="space-y-4 py-4">
                    <div className="space-y-2">
                        <Label htmlFor="bill-name">帳單名稱</Label>
                        <Input
                            id="bill-name"
                            placeholder="例如：日本旅遊、週五聚餐"
                            value={value}
                            onChange={(e) => onChange(e.target.value)}
                            onKeyDown={handleKeyDown}
                            autoFocus
                        />
                    </div>
                </div>
                <DialogFooter>
                    <Button variant="outline" onClick={() => onOpenChange(false)}>
                        取消
                    </Button>
                    <Button onClick={onConfirm} disabled={!value.trim()}>
                        建立
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
