/**
 * SyncHandshakeDialog - 同步握手對話框
 *
 * 登入後顯示，讓用戶選擇是否同步本地帳單到雲端
 */

import { useState } from 'react';
import {
    Dialog,
    DialogContent,
    DialogHeader,
    DialogTitle,
    DialogDescription,
    DialogFooter,
} from '@/shared/components/ui/dialog';
import { Button } from '@/shared/components/ui/button';
import { Cloud, CloudOff, Check, Loader2 } from 'lucide-react';
import type { Bill } from '../types/snap-split';

interface SyncHandshakeDialogProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    unsyncedBills: Bill[];
    syncProgress: {
        total: number;
        completed: number;
        failed: number;
    };
    onSyncAll: () => Promise<void>;
    onSyncSelected: (billIds: string[]) => Promise<void>;
    onDismiss: () => void;
}

export function SyncHandshakeDialog({
    open,
    onOpenChange,
    unsyncedBills,
    syncProgress,
    onSyncAll,
    onSyncSelected,
    onDismiss,
}: SyncHandshakeDialogProps) {
    const [selectedBillIds, setSelectedBillIds] = useState<Set<string>>(
        new Set(unsyncedBills.map(b => b.id))
    );
    const [isSyncing, setIsSyncing] = useState(false);

    const toggleBill = (billId: string) => {
        const newSelected = new Set(selectedBillIds);
        if (newSelected.has(billId)) {
            newSelected.delete(billId);
        } else {
            newSelected.add(billId);
        }
        setSelectedBillIds(newSelected);
    };

    const handleSyncSelected = async () => {
        setIsSyncing(true);
        try {
            if (selectedBillIds.size === unsyncedBills.length) {
                // 全選時使用 onSyncAll
                await onSyncAll();
            } else {
                await onSyncSelected(Array.from(selectedBillIds));
            }
        } finally {
            setIsSyncing(false);
        }
    };

    const isInProgress = syncProgress.total > 0 && syncProgress.completed < syncProgress.total;

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="sm:max-w-md">
                <DialogHeader>
                    <DialogTitle className="flex items-center gap-2">
                        <Cloud className="h-5 w-5 text-primary" />
                        同步本地帳單
                    </DialogTitle>
                    <DialogDescription>
                        發現 {unsyncedBills.length} 個本地帳單尚未同步到雲端。
                        同步後可在其他裝置存取。
                    </DialogDescription>
                </DialogHeader>

                {/* 帳單列表 */}
                <div className="max-h-60 overflow-y-auto space-y-2 py-4">
                    {unsyncedBills.map(bill => (
                        <label
                            key={bill.id}
                            className="flex items-center gap-3 p-3 rounded-lg border bg-muted/30 hover:bg-muted/50 cursor-pointer transition-colors"
                        >
                            <input
                                type="checkbox"
                                checked={selectedBillIds.has(bill.id)}
                                onChange={() => toggleBill(bill.id)}
                                disabled={isSyncing}
                                className="h-4 w-4 rounded border-gray-300 text-primary focus:ring-primary"
                            />
                            <div className="flex-1 min-w-0">
                                <p className="font-medium truncate">{bill.name || '未命名帳單'}</p>
                                <p className="text-xs text-muted-foreground">
                                    {bill.members.length} 人 · {bill.expenses.length} 筆費用
                                </p>
                            </div>
                            <CloudOff className="h-4 w-4 text-muted-foreground flex-shrink-0" />
                        </label>
                    ))}
                </div>

                {/* 進度顯示 */}
                {isInProgress && (
                    <div className="py-2">
                        <div className="flex items-center justify-between text-sm mb-1">
                            <span className="text-muted-foreground">同步進度</span>
                            <span>{syncProgress.completed} / {syncProgress.total}</span>
                        </div>
                        <div className="w-full bg-muted rounded-full h-2">
                            <div
                                className="bg-primary h-2 rounded-full transition-all duration-300"
                                style={{ width: `${(syncProgress.completed / syncProgress.total) * 100}%` }}
                            />
                        </div>
                        {syncProgress.failed > 0 && (
                            <p className="text-xs text-destructive mt-1">
                                {syncProgress.failed} 個帳單同步失敗
                            </p>
                        )}
                    </div>
                )}

                <DialogFooter className="flex-col sm:flex-row gap-2">
                    <Button
                        variant="outline"
                        onClick={onDismiss}
                        disabled={isSyncing}
                        className="w-full sm:w-auto"
                    >
                        稍後再說
                    </Button>
                    <Button
                        onClick={handleSyncSelected}
                        disabled={isSyncing || selectedBillIds.size === 0}
                        className="w-full sm:w-auto"
                    >
                        {isSyncing ? (
                            <>
                                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                同步中...
                            </>
                        ) : (
                            <>
                                <Check className="mr-2 h-4 w-4" />
                                同步選取 ({selectedBillIds.size})
                            </>
                        )}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
