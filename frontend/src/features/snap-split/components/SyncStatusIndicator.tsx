/**
 * SyncStatusIndicator - 同步狀態指示器
 *
 * 顯示在 BillDetailView Header 中，指示當前帳單的同步狀態
 */

import { Check, Loader2, AlertCircle, Cloud, CloudOff, AlertTriangle } from 'lucide-react';
import type { SyncStatus } from '../types/snap-split';
import { cn } from '@/shared/lib/utils';

interface SyncStatusIndicatorProps {
    /** 同步狀態 */
    status: SyncStatus;
    /** 重試回調 */
    onRetry?: () => void;
    /** 錯誤訊息 */
    errorMessage?: string;
    /** 額外的 className */
    className?: string;
    /** 是否顯示文字 */
    showLabel?: boolean;
}

const statusConfig: Record<SyncStatus, {
    icon: React.ElementType;
    label: string;
    colorClass: string;
    bgClass: string;
}> = {
    synced: {
        icon: Check,
        label: '已同步',
        colorClass: 'text-success',
        bgClass: 'bg-success/10',
    },
    syncing: {
        icon: Loader2,
        label: '同步中',
        colorClass: 'text-info',
        bgClass: 'bg-info/10',
    },
    modified: {
        icon: Cloud,
        label: '待同步',
        colorClass: 'text-warning',
        bgClass: 'bg-warning/10',
    },
    local: {
        icon: CloudOff,
        label: '僅本地',
        colorClass: 'text-muted-foreground',
        bgClass: 'bg-muted',
    },
    error: {
        icon: AlertCircle,
        label: '同步失敗',
        colorClass: 'text-destructive',
        bgClass: 'bg-destructive/10',
    },
    conflict: {
        icon: AlertTriangle,
        label: '版本衝突',
        colorClass: 'text-warning',
        bgClass: 'bg-warning/10',
    },
};

export function SyncStatusIndicator({
    status,
    onRetry,
    errorMessage,
    className,
    showLabel = false,
}: SyncStatusIndicatorProps) {
    const config = statusConfig[status];
    const Icon = config.icon;
    const isClickable = status === 'error' && onRetry;

    return (
        <button
            type="button"
            onClick={isClickable ? onRetry : undefined}
            disabled={!isClickable}
            className={cn(
                'inline-flex items-center gap-1.5 px-2 py-1 rounded-full text-xs font-medium transition-colors',
                config.bgClass,
                config.colorClass,
                isClickable && 'cursor-pointer hover:opacity-80',
                !isClickable && 'cursor-default',
                className
            )}
            title={errorMessage || config.label}
        >
            <Icon
                className={cn(
                    'h-3.5 w-3.5',
                    status === 'syncing' && 'animate-spin'
                )}
            />
            {showLabel && <span>{config.label}</span>}
        </button>
    );
}

/**
 * 小型同步狀態指示器（僅圖示）
 */
export function SyncStatusDot({
    status,
    className,
}: {
    status: SyncStatus;
    className?: string;
}) {
    const config = statusConfig[status];
    const Icon = config.icon;

    return (
        <span
            className={cn(
                'inline-flex items-center justify-center',
                config.colorClass,
                className
            )}
            title={config.label}
        >
            <Icon
                className={cn(
                    'h-4 w-4',
                    status === 'syncing' && 'animate-spin'
                )}
            />
        </span>
    );
}
