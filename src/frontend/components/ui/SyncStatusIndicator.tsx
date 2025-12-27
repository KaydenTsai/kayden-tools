import { Chip, CircularProgress, Tooltip } from '@mui/material';
import {
    CloudDone as SyncedIcon,
    CloudOff as LocalIcon,
    CloudSync as SyncingIcon,
    CloudUpload as ModifiedIcon,
    ErrorOutline as ErrorIcon,
} from '@mui/icons-material';
import type { SyncStatus } from '@/types/snap-split';

interface SyncStatusIndicatorProps {
    status: SyncStatus;
    error?: string;
    size?: 'small' | 'medium';
    showLabel?: boolean;
}

const statusConfig: Record<SyncStatus, {
    icon: React.ReactNode;
    label: string;
    color: 'default' | 'success' | 'warning' | 'info' | 'error';
    tooltip: string;
}> = {
    local: {
        icon: <LocalIcon fontSize="small" />,
        label: '本地',
        color: 'default',
        tooltip: '僅存於本地，未同步到雲端',
    },
    synced: {
        icon: <SyncedIcon fontSize="small" />,
        label: '已同步',
        color: 'success',
        tooltip: '已與雲端同步',
    },
    modified: {
        icon: <ModifiedIcon fontSize="small" />,
        label: '待同步',
        color: 'warning',
        tooltip: '有修改尚未同步',
    },
    syncing: {
        icon: <CircularProgress size={16} color="inherit" />,
        label: '同步中',
        color: 'info',
        tooltip: '正在同步...',
    },
    error: {
        icon: <ErrorIcon fontSize="small" />,
        label: '同步失敗',
        color: 'error',
        tooltip: '同步失敗，點擊重試',
    },
};

export function SyncStatusIndicator({
    status,
    error,
    size = 'small',
    showLabel = true,
}: SyncStatusIndicatorProps) {
    // Default to 'local' if status is undefined
    const safeStatus = status || 'local';
    const config = statusConfig[safeStatus] || statusConfig.local;
    const tooltipText = error ? `${config.tooltip}: ${error}` : config.tooltip;

    return (
        <Tooltip title={tooltipText} arrow>
            <Chip
                icon={config.icon as React.ReactElement}
                label={showLabel ? config.label : undefined}
                color={config.color}
                size={size}
                variant="outlined"
                sx={{
                    '& .MuiChip-icon': {
                        ml: showLabel ? undefined : 0,
                        mr: showLabel ? undefined : 0,
                    },
                    minWidth: showLabel ? undefined : 32,
                }}
            />
        </Tooltip>
    );
}

interface SyncStatusIconProps {
    status: SyncStatus;
    size?: 'small' | 'medium' | 'large';
}

export function SyncStatusIcon({ status, size = 'small' }: SyncStatusIconProps) {
    const fontSize = size === 'small' ? 16 : size === 'medium' ? 20 : 24;

    const iconColors: Record<SyncStatus, string> = {
        local: 'text.secondary',
        synced: 'success.main',
        modified: 'warning.main',
        syncing: 'info.main',
        error: 'error.main',
    };

    // Default to 'local' if status is undefined
    const safeStatus = status || 'local';

    if (safeStatus === 'syncing') {
        return <CircularProgress size={fontSize} sx={{ color: iconColors[safeStatus] }} />;
    }

    const iconMap = {
        local: LocalIcon,
        synced: SyncedIcon,
        modified: ModifiedIcon,
        syncing: SyncingIcon,
        error: ErrorIcon,
    };

    const IconComponent = iconMap[safeStatus] || LocalIcon;

    return (
        <IconComponent
            sx={{
                fontSize,
                color: iconColors[safeStatus] || 'text.secondary',
            }}
        />
    );
}
