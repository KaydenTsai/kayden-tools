import { Component, type ReactNode } from 'react';
import { Alert, AlertTitle, Box, Button, Typography } from '@mui/material';
import { Refresh as RefreshIcon, CloudOff as CloudOffIcon } from '@mui/icons-material';

interface Props {
    children: ReactNode;
    fallbackMessage?: string;
}

interface State {
    hasError: boolean;
    error: Error | null;
}

/**
 * 協作功能錯誤邊界
 * 捕獲 SignalR 連線、操作同步等協作相關錯誤
 */
export class CollaborationErrorBoundary extends Component<Props, State> {
    constructor(props: Props) {
        super(props);
        this.state = { hasError: false, error: null };
    }

    static getDerivedStateFromError(error: Error): State {
        return { hasError: true, error };
    }

    componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
        console.error('[CollaborationErrorBoundary] Error caught:', error);
        console.error('[CollaborationErrorBoundary] Component stack:', errorInfo.componentStack);
    }

    handleRetry = () => {
        this.setState({ hasError: false, error: null });
    };

    handleReload = () => {
        window.location.reload();
    };

    render() {
        if (this.state.hasError) {
            const { fallbackMessage = '協作功能發生錯誤' } = this.props;
            const isNetworkError = this.state.error?.message?.includes('SignalR') ||
                                   this.state.error?.message?.includes('network') ||
                                   this.state.error?.message?.includes('connection');

            return (
                <Box sx={{ p: 3, textAlign: 'center' }}>
                    <Alert
                        severity="error"
                        icon={<CloudOffIcon />}
                        sx={{ mb: 2, textAlign: 'left' }}
                    >
                        <AlertTitle>{fallbackMessage}</AlertTitle>
                        <Typography variant="body2" color="text.secondary">
                            {isNetworkError
                                ? '無法連線到伺服器，請檢查網路連線後重試。'
                                : '發生未預期的錯誤，請重新整理頁面或稍後再試。'}
                        </Typography>
                        {import.meta.env.DEV && this.state.error && (
                            <Typography
                                variant="caption"
                                component="pre"
                                sx={{
                                    mt: 1,
                                    p: 1,
                                    bgcolor: 'grey.100',
                                    borderRadius: 1,
                                    overflow: 'auto',
                                    maxHeight: 100,
                                    fontSize: '0.7rem',
                                }}
                            >
                                {this.state.error.message}
                            </Typography>
                        )}
                    </Alert>
                    <Box sx={{ display: 'flex', gap: 1, justifyContent: 'center' }}>
                        <Button
                            variant="outlined"
                            startIcon={<RefreshIcon />}
                            onClick={this.handleRetry}
                        >
                            重試
                        </Button>
                        <Button
                            variant="contained"
                            onClick={this.handleReload}
                        >
                            重新整理頁面
                        </Button>
                    </Box>
                </Box>
            );
        }

        return this.props.children;
    }
}