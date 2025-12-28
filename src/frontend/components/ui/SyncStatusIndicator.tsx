import { useState } from 'react';
import { Box, Chip, CircularProgress, Snackbar, Tooltip, useMediaQuery, useTheme } from '@mui/material';
import {
    CloudDone as SyncedIcon,
    CloudOff as LocalIcon,
    CloudSync as SyncingIcon,
    CloudUpload as ModifiedIcon,
    ErrorOutline as ErrorIcon,
    Group as CollaborativeIcon,
} from '@mui/icons-material';
import type { SyncStatus } from '@/types/snap-split';

interface SyncStatusIndicatorProps {
    status: SyncStatus;
    error?: string;
    size?: 'small' | 'medium';
    showLabel?: boolean;
    isCollaborative?: boolean;
}

type StatusKey = SyncStatus | 'collaborative';

const statusConfig: Record<StatusKey, {
    icon: React.ReactNode;
    label: string;
    color: 'default' | 'success' | 'warning' | 'info' | 'error' | 'primary';
    tooltip: string;
}> = {
    local: {
        icon: <LocalIcon fontSize="small" />,
        label: '本地',
        color: 'default',
        tooltip: '本地帳單，僅存於此裝置',
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
    collaborative: {
        icon: <CollaborativeIcon fontSize="small" />,
        label: '協作',
        color: 'primary',
        tooltip: '多人協作帳單',
    },
};

export function SyncStatusIndicator({
    status,
    error,
    size = 'small',
    showLabel = true,
    isCollaborative = false,
}: SyncStatusIndicatorProps) {
    // Default to 'local' if status is undefined
    const safeStatus = status || 'local';
    // 協作模式優先顯示
    const displayKey: StatusKey = isCollaborative ? 'collaborative' : safeStatus;
    const config = statusConfig[displayKey] || statusConfig.local;
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
    isCollaborative?: boolean;
}

export function SyncStatusIcon({ status, size = 'small', isCollaborative = false }: SyncStatusIconProps) {
    const theme = useTheme();
    const isMobile = useMediaQuery(theme.breakpoints.down('md'));
    const [snackbarOpen, setSnackbarOpen] = useState(false);

    const fontSize = size === 'small' ? 16 : size === 'medium' ? 20 : 24;

    // Default to 'local' if status is undefined
    const safeStatus = status || 'local';

    // 協作模式優先顯示
    const displayMode = isCollaborative ? 'collaborative' : safeStatus;

    const iconColors: Record<string, string> = {
        local: 'text.secondary',
        synced: 'success.main',
        modified: 'warning.main',
        syncing: 'info.main',
        error: 'error.main',
        collaborative: 'primary.main',
    };

    const tooltipTexts: Record<string, string> = {
        local: '本地帳單，僅存於此裝置',
        synced: '已與雲端同步',
        modified: '有修改尚未同步',
        syncing: '正在同步...',
        error: '同步失敗',
        collaborative: '多人協作帳單',
    };

    const handleClick = () => {
        if (isMobile) {
            setSnackbarOpen(true);
        }
    };

    if (safeStatus === 'syncing' && !isCollaborative) {
        return <CircularProgress size={fontSize} sx={{ color: iconColors.syncing }} />;
    }

    const iconMap: Record<string, typeof LocalIcon> = {
        local: LocalIcon,
        synced: SyncedIcon,
        modified: ModifiedIcon,
        syncing: SyncingIcon,
        error: ErrorIcon,
        collaborative: CollaborativeIcon,
    };

    const IconComponent = iconMap[displayMode] || LocalIcon;
    const tooltipText = tooltipTexts[displayMode] || '';

    return (
        <>
            <Tooltip
                title={tooltipText}
                arrow
                disableHoverListener={isMobile}
                disableFocusListener={isMobile}
            >
                <Box
                    component="span"
                    onClick={handleClick}
                    sx={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        cursor: isMobile ? 'pointer' : 'default',
                    }}
                >
                    <IconComponent
                        sx={{
                            fontSize,
                            color: iconColors[displayMode] || 'text.secondary',
                        }}
                    />
                </Box>
            </Tooltip>
            <Snackbar
                open={snackbarOpen}
                autoHideDuration={2000}
                onClose={() => setSnackbarOpen(false)}
                message={tooltipText}
                anchorOrigin={{ vertical: 'top', horizontal: 'center' }}
                sx={{
                    '& .MuiSnackbarContent-root': {
                        minWidth: 'auto',
                        py: 0.5,
                        px: 2,
                        borderRadius: 2,
                    },
                }}
            />
        </>
    );
}
