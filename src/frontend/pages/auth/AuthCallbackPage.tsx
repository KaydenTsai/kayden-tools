import { useEffect, useState, useRef } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Box, CircularProgress, Typography, Alert, Button, Paper } from '@mui/material';
import { useAuthStore } from '@/stores/authStore';
import {
    postApiAuthLineCallback,
    postApiAuthGoogleCallback,
} from '@/api';

/**
 * OAuth 回調頁面
 *
 * 處理 LINE/Google 登入後的回調，驗證 state 參數防止 CSRF 攻擊。
 *
 * 注意：URL query string 中的 `+` 會被 URLSearchParams 解析為空格，
 * 這是 HTML form encoding 的標準行為。因此在比對 state 時需要將空格還原為 `+`。
 */
export function AuthCallbackPage() {
    const navigate = useNavigate();
    const [searchParams] = useSearchParams();
    const { setAuth } = useAuthStore();
    const [error, setError] = useState<string | null>(null);
    const [isProcessing, setIsProcessing] = useState(true);

    // 防止 React Strict Mode 二次執行
    const initialized = useRef(false);

    useEffect(() => {
        if (initialized.current) return;
        initialized.current = true;

        const handleCallback = async () => {
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

            // 從 hash query string 取得 state，並將空格還原為 +
            // (URLSearchParams 會將 + 解析為空格，需要還原以正確比對)
            const state = (() => {
                const hashQuery = window.location.hash.split('?')[1] || '';
                const match = hashQuery.match(/state=([^&#]*)/);
                if (match) {
                    return decodeURIComponent(match[1]).replace(/ /g, '+');
                }
                return searchParams.get('state')?.replace(/ /g, '+') || null;
            })();

            const provider = searchParams.get('provider') || detectProvider();

            if (!code) {
                setError('缺少授權碼');
                setIsProcessing(false);
                return;
            }

            // CSRF 驗證：比對 localStorage 中的 state
            const savedState = localStorage.getItem('oauth_state');
            localStorage.removeItem('oauth_state'); // 確保只能使用一次

            if (!savedState) {
                // 無儲存的 state（可能是瀏覽器切換），仍嘗試後端驗證
            } else if (state && savedState !== state) {
                setError('安全驗證失敗，可能是重複點擊或瀏覽器切換導致。請重新登入。');
                setIsProcessing(false);
                return;
            }

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
