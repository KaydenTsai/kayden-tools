/**
 * AuthCallbackPage - OAuth redirect callback handler
 *
 * Handles the OAuth callback after redirect flow:
 * 1. Reads code/state/error from URL search params
 * 2. Exchanges code for tokens via API
 * 3. Sets auth state and navigates back to the original page
 */

import { useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate, Link } from 'react-router-dom';
import { postApiAuthLineCallback } from '@/api/endpoints/auth/auth';
import { useAuthStore } from '@/stores/authStore';
import { loginLogger } from '@/shared/lib/logger';

const RETURN_URL_KEY = 'oauth-return-url';

export function AuthCallbackPage() {
    const location = useLocation();
    const navigate = useNavigate();
    const setAuth = useAuthStore((s) => s.setAuth);
    const [error, setError] = useState<string | null>(null);
    const processedRef = useRef(false);

    useEffect(() => {
        if (processedRef.current) return;
        processedRef.current = true;

        const params = new URLSearchParams(location.search);
        const code = params.get('code');
        const state = params.get('state');
        const oauthError = params.get('error');

        if (oauthError) {
            loginLogger.error('OAuth error from provider:', oauthError);
            setError(`登入失敗: ${oauthError}`);
            return;
        }

        if (!code) {
            loginLogger.error('No code in callback URL');
            setError('登入失敗: 未收到授權碼');
            return;
        }

        (async () => {
            try {
                loginLogger.debug('Exchanging code for token...');
                const tokenResponse = await postApiAuthLineCallback({
                    code,
                    state: state ?? undefined,
                });

                if (!tokenResponse.success || !tokenResponse.data) {
                    throw new Error(tokenResponse.error?.message ?? '登入失敗');
                }

                const { accessToken, refreshToken, expiresAt, user } = tokenResponse.data;

                if (!user?.id) {
                    throw new Error('登入失敗: 無法取得使用者資訊');
                }

                setAuth(
                    accessToken ?? '',
                    refreshToken ?? '',
                    expiresAt ?? new Date(Date.now() + 3600000).toISOString(),
                    {
                        id: user.id,
                        email: user.email,
                        displayName: user.displayName,
                        avatarUrl: user.avatarUrl,
                    },
                );

                loginLogger.info('Login successful!');

                const returnUrl = sessionStorage.getItem(RETURN_URL_KEY) || '/';
                sessionStorage.removeItem(RETURN_URL_KEY);
                navigate(returnUrl, { replace: true });
            } catch (err) {
                loginLogger.error('Token exchange failed:', err);
                setError(err instanceof Error ? err.message : '登入失敗');
            }
        })();
    }, [location.search, navigate, setAuth]);

    if (error) {
        return (
            <div className="flex flex-col items-center justify-center gap-4 p-6">
                <p className="text-destructive">{error}</p>
                <Link to="/" className="text-primary underline">
                    返回首頁
                </Link>
            </div>
        );
    }

    return (
        <div className="flex items-center justify-center p-6">
            <p className="text-muted-foreground">登入處理中...</p>
        </div>
    );
}
