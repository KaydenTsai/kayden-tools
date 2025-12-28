import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Box, CircularProgress, Typography, Alert, Button, Paper } from '@mui/material';
import { useAuthStore } from '@/stores/authStore';
import {
    postApiAuthLineCallback,
    postApiAuthGoogleCallback,
} from '@/api';

export function AuthCallbackPage() {
    const navigate = useNavigate();
    const [searchParams] = useSearchParams();
    const { setAuth } = useAuthStore();
    const [error, setError] = useState<string | null>(null);
    const [isProcessing, setIsProcessing] = useState(true);

    useEffect(() => {
        const handleCallback = async () => {
            // 檢查是否有錯誤（使用者取消授權等）
            const errorParam = searchParams.get('error');
            const errorDescription = searchParams.get('error_description');
            if (errorParam) {
                const errorMessage = errorDescription ||
                    (errorParam === 'access_denied' ? '您已取消授權' : `授權失敗: ${errorParam}`);
                setError(errorMessage);
                setIsProcessing(false);
                return;
            }

            const code = searchParams.get('code');
            const state = searchParams.get('state');
            const provider = searchParams.get('provider') || detectProvider();

            if (!code) {
                setError('缺少授權碼');
                setIsProcessing(false);
                return;
            }

            // 驗證 state 防止 CSRF 攻擊
            const savedState = sessionStorage.getItem('oauth_state');
            if (savedState && state !== savedState) {
                setError('安全驗證失敗，請重新登入');
                setIsProcessing(false);
                return;
            }
            sessionStorage.removeItem('oauth_state');

            try {
                let response;
                if (provider === 'google') {
                    response = await postApiAuthGoogleCallback({ code });
                } else {
                    response = await postApiAuthLineCallback({ code, state: state || '' });
                }

                if (response.success && response.data) {
                    const { accessToken, refreshToken, expiresAt, user } = response.data;
                    if (accessToken && refreshToken && expiresAt && user) {
                        setAuth(accessToken, refreshToken, expiresAt, user);
                        navigate('/', { replace: true });
                        return;
                    }
                }

                setError('登入失敗，請重試');
            } catch {
                setError('登入過程發生錯誤');
            } finally {
                setIsProcessing(false);
            }
        };

        handleCallback();
    }, [searchParams, setAuth, navigate]);

    // Try to detect provider from the callback URL pattern
    function detectProvider(): string {
        const pathname = window.location.pathname;
        if (pathname.includes('google')) return 'google';
        return 'line';
    }

    const handleRetry = () => {
        navigate('/', { replace: true });
    };

    return (
        <Box
            sx={{
                minHeight: '100vh',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                bgcolor: 'background.default',
                p: 3,
            }}
        >
            <Paper sx={{ p: 4, maxWidth: 400, width: '100%', textAlign: 'center' }}>
                {isProcessing ? (
                    <>
                        <CircularProgress size={48} sx={{ mb: 3 }} />
                        <Typography variant="h6" gutterBottom>
                            登入中...
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                            正在驗證您的身份，請稍候
                        </Typography>
                    </>
                ) : error ? (
                    <>
                        <Alert severity="error" sx={{ mb: 3 }}>
                            {error}
                        </Alert>
                        <Button
                            variant="contained"
                            onClick={handleRetry}
                            fullWidth
                        >
                            返回首頁
                        </Button>
                    </>
                ) : (
                    <>
                        <Typography variant="h6" color="success.main" gutterBottom>
                            登入成功！
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                            正在跳轉...
                        </Typography>
                    </>
                )}
            </Paper>
        </Box>
    );
}
