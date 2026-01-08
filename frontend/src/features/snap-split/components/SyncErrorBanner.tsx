/**
 * SyncErrorBanner - 顯示同步失敗的操作
 * @see SNAPSPLIT_SPEC.md Section 8.5
 */

import { AlertCircle, RefreshCw, Trash2 } from 'lucide-react';
import { useSyncQueue } from '../services/syncQueue';
import { Button } from '@/shared/components/ui/button';
import { cn } from '@/shared/lib/utils';

interface SyncErrorBannerProps {
    className?: string;
}

export function SyncErrorBanner({ className }: SyncErrorBannerProps) {
    const failedActions = useSyncQueue(state => state.getFailedActions());
    const retryAction = useSyncQueue(state => state.retryAction);
    const discardAction = useSyncQueue(state => state.discardAction);

    if (failedActions.length === 0) {
        return null;
    }

    return (
        <div
            className={cn(
                'p-3 border rounded-lg bg-destructive/10 border-destructive/30',
                className
            )}
        >
            <div className="flex items-start gap-2">
                <AlertCircle className="h-4 w-4 mt-0.5 shrink-0 text-destructive" />
                <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-destructive">
                        {failedActions.length} 個同步操作失敗
                    </p>
                    <p className="text-xs text-muted-foreground mt-1">
                        {failedActions[0]?.errorMessage || '發生未知錯誤'}
                    </p>
                    <div className="flex gap-2 mt-2">
                        <Button
                            variant="outline"
                            size="sm"
                            onClick={() => {
                                failedActions.forEach(action => retryAction(action.id));
                            }}
                            className="h-7 text-xs"
                        >
                            <RefreshCw className="h-3 w-3 mr-1" />
                            重試全部
                        </Button>
                        <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => {
                                failedActions.forEach(action => discardAction(action.id));
                            }}
                            className="h-7 text-xs text-muted-foreground"
                        >
                            <Trash2 className="h-3 w-3 mr-1" />
                            捨棄變更
                        </Button>
                    </div>
                </div>
            </div>
        </div>
    );
}
