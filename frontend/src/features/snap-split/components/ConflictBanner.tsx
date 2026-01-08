/**
 * ConflictBanner - 顯示版本衝突警告
 * @see SNAPSPLIT_SPEC.md Section 8.5
 */

import { AlertTriangle, RefreshCw } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { cn } from '@/shared/lib/utils';
import type { Bill } from '../types/snap-split';

interface ConflictBannerProps {
    bill: Bill;
    onRefresh?: () => void;
    className?: string;
}

export function ConflictBanner({ bill, onRefresh, className }: ConflictBannerProps) {
    if (bill.syncStatus !== 'conflict') {
        return null;
    }

    return (
        <div
            className={cn(
                'p-3 border rounded-lg bg-warning/10 border-warning/30',
                className
            )}
        >
            <div className="flex items-start gap-2">
                <AlertTriangle className="h-4 w-4 mt-0.5 shrink-0 text-warning" />
                <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-warning">
                        版本衝突
                    </p>
                    <p className="text-xs text-muted-foreground mt-1">
                        此帳單已被其他人修改，請查看最新版本後再繼續編輯。
                    </p>
                    {onRefresh && (
                        <Button
                            variant="outline"
                            size="sm"
                            onClick={onRefresh}
                            className="h-7 text-xs mt-2"
                        >
                            <RefreshCw className="h-3 w-3 mr-1" />
                            取得最新版本
                        </Button>
                    )}
                </div>
            </div>
        </div>
    );
}
