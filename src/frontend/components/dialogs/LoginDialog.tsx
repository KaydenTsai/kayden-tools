import { useState, useEffect } from 'react';
import {
    Dialog,
    DialogTitle,
    DialogContent,
    DialogActions,
    Button,
    Box,
    Typography,
    CircularProgress,
    Alert,
    Divider,
    Avatar,
    IconButton,
} from '@mui/material';
import {
    Login as LoginIcon,
    Logout as LogoutIcon,
    Close as CloseIcon,
} from '@mui/icons-material';
import { SlideTransition } from '@/components/ui/SlideTransition';
import { useAuthStore } from '@/stores/authStore';
import {
    useGetApiAuthProviders,
    getApiAuthLineUrl,
    getApiAuthGoogleUrl,
} from '@/api';

interface LoginDialogProps {
    open: boolean;
    onClose: () => void;
}

export function LoginDialog({ open, onClose }: LoginDialogProps) {
    const { isAuthenticated, user, logout } = useAuthStore();
    const [isLoading, setIsLoading] = useState<string | null>(null);
    const [error, setError] = useState<string | null>(null);

    const { data: providersData, isLoading: isLoadingProviders } = useGetApiAuthProviders({
        query: { enabled: open },
    });

    const providers = providersData?.data as { line?: boolean; google?: boolean } | undefined;

    const handleLogin = async (provider: 'line' | 'google') => {
        setIsLoading(provider);
        setError(null);

        try {
            let response;
            if (provider === 'line') {
                response = await getApiAuthLineUrl();
            } else {
                response = await getApiAuthGoogleUrl();
            }

            const data = response.data as { url?: string; state?: string } | undefined;
            if (data?.url) {
                // Store state for CSRF validation
                if (data.state) {
                    sessionStorage.setItem('oauth_state', data.state);
                }
                // Redirect to OAuth provider
                window.location.href = data.url;
            } else {
                setError('Failed to get login URL');
            }
        } catch {
            setError('Login failed. Please try again.');
        } finally {
            setIsLoading(null);
        }
    };

    const handleLogout = () => {
        logout();  // 現在是即時的，不需要 await
        onClose();
    };

    // Reset error when dialog opens
    useEffect(() => {
        if (open) {
            setError(null);
        }
    }, [open]);

    return (
        <Dialog
            open={open}
            onClose={onClose}
            maxWidth="xs"
            fullWidth
            TransitionComponent={SlideTransition}
        >
            <DialogTitle sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <LoginIcon color="primary" />
                    {isAuthenticated ? '帳戶' : '登入'}
                </Box>
                <IconButton size="small" onClick={onClose}>
                    <CloseIcon />
                </IconButton>
            </DialogTitle>

            <DialogContent>
                {isAuthenticated ? (
                    // Logged in view
                    <Box sx={{ textAlign: 'center', py: 2 }}>
                        <Avatar
                            src={user?.avatarUrl || undefined}
                            sx={{ width: 64, height: 64, mx: 'auto', mb: 2 }}
                        >
                            {user?.displayName?.charAt(0) || 'U'}
                        </Avatar>
                        <Typography variant="h6" gutterBottom>
                            {user?.displayName || 'User'}
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                            {user?.email || ''}
                        </Typography>
                    </Box>
                ) : (
                    // Login view
                    <>
                        <Typography variant="body2" color="text.secondary" sx={{ mb: 3, textAlign: 'center' }}>
                            登入以同步您的帳單到雲端，隨時隨地存取
                        </Typography>

                        {error && (
                            <Alert severity="error" sx={{ mb: 2 }}>
                                {error}
                            </Alert>
                        )}

                        {isLoadingProviders ? (
                            <Box sx={{ textAlign: 'center', py: 3 }}>
                                <CircularProgress size={32} />
                            </Box>
                        ) : (
                            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                                {providers?.line && (
                                    <Button
                                        variant="contained"
                                        size="large"
                                        fullWidth
                                        onClick={() => handleLogin('line')}
                                        disabled={isLoading !== null}
                                        sx={{
                                            bgcolor: '#06C755',
                                            '&:hover': { bgcolor: '#05a648' },
                                            py: 1.5,
                                        }}
                                    >
                                        {isLoading === 'line' ? (
                                            <CircularProgress size={24} color="inherit" />
                                        ) : (
                                            'LINE 登入'
                                        )}
                                    </Button>
                                )}

                                {providers?.google && (
                                    <Button
                                        variant="outlined"
                                        size="large"
                                        fullWidth
                                        onClick={() => handleLogin('google')}
                                        disabled={isLoading !== null}
                                        sx={{ py: 1.5 }}
                                    >
                                        {isLoading === 'google' ? (
                                            <CircularProgress size={24} />
                                        ) : (
                                            'Google 登入'
                                        )}
                                    </Button>
                                )}

                                {!providers?.line && !providers?.google && (
                                    <Alert severity="info">
                                        目前沒有可用的登入方式
                                    </Alert>
                                )}
                            </Box>
                        )}

                        <Divider sx={{ my: 3 }} />

                        <Typography variant="caption" color="text.disabled" sx={{ display: 'block', textAlign: 'center' }}>
                            登入後，您可以在任何裝置上存取您的帳單資料
                        </Typography>
                    </>
                )}
            </DialogContent>

            {isAuthenticated && (
                <DialogActions sx={{ p: 2, pt: 0 }}>
                    <Button
                        variant="outlined"
                        color="error"
                        onClick={handleLogout}
                        startIcon={<LogoutIcon />}
                        fullWidth
                    >
                        登出
                    </Button>
                </DialogActions>
            )}
        </Dialog>
    );
}
