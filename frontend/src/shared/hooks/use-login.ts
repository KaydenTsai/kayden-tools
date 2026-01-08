/**
 * useLogin - 登入 Hook
 *
 * 處理 OAuth 登入流程（LINE / Google）
 */

import { useState, useCallback } from 'react';
import { useAuthStore } from '@/stores/authStore';
import {
    getApiAuthLineUrl,
    postApiAuthLineCallback,
} from '@/api/endpoints/auth/auth';
import { loginLogger } from '@/shared/lib/logger';

type LoginProvider = 'line' | 'google';

interface LoginState {
    isLoggingIn: boolean;
    error: string | null;
}

export function useLogin() {
    const [state, setState] = useState<LoginState>({
        isLoggingIn: false,
        error: null,
    });

    const { setAuth, isAuthenticated, user } = useAuthStore();

    /**
     * 開啟 OAuth 彈窗登入
     */
    const login = useCallback(async (provider: LoginProvider = 'line') => {
        loginLogger.debug('login() called, isLoggingIn:', state.isLoggingIn);
        if (state.isLoggingIn) {
            loginLogger.debug('Already logging in, returning early');
            return;
        }

        setState({ isLoggingIn: true, error: null });
        loginLogger.debug('Set isLoggingIn to true');

        try {
            // 取得 OAuth URL
            const redirectUri = `${window.location.origin}/auth/callback`;
            loginLogger.debug('Requesting auth URL with redirectUri:', redirectUri);
            let authUrl: string;

            if (provider === 'line') {
                let response;
                try {
                    response = await getApiAuthLineUrl({ redirectUri });
                    loginLogger.debug('getApiAuthLineUrl response:', response);
                } catch (apiError) {
                    loginLogger.error('getApiAuthLineUrl API error:', apiError);
                    throw apiError;
                }
                if (!response.success || !response.data) {
                    throw new Error(response.error?.message ?? '無法取得登入 URL');
                }
                // 後端回傳 { url, state }
                const data = response.data as { url: string; state: string };
                authUrl = data.url;
                loginLogger.debug('Got auth URL:', authUrl);
            } else {
                throw new Error('目前僅支援 LINE 登入');
            }

            // 開啟彈窗
            const width = 500;
            const height = 600;
            const left = window.screenX + (window.outerWidth - width) / 2;
            const top = window.screenY + (window.outerHeight - height) / 2;

            const popup = window.open(
                authUrl,
                'oauth-popup',
                `width=${width},height=${height},left=${left},top=${top}`
            );

            if (!popup) {
                throw new Error('無法開啟登入視窗，請允許彈窗');
            }

            // 監聽 postMessage
            const handleMessage = async (event: MessageEvent) => {
                loginLogger.debug('Received message:', event.origin, event.data);

                if (event.origin !== window.location.origin) {
                    loginLogger.debug('Origin mismatch, ignoring');
                    return;
                }
                if (event.data?.type !== 'oauth-callback') {
                    loginLogger.debug('Not oauth-callback, ignoring');
                    return;
                }

                window.removeEventListener('message', handleMessage);
                loginLogger.debug('Processing oauth callback:', event.data.params);

                const { code, state: oauthState, error } = event.data.params || {};

                if (error) {
                    loginLogger.error('OAuth error:', error);
                    setState({ isLoggingIn: false, error: `登入失敗: ${error}` });
                    return;
                }

                if (!code) {
                    loginLogger.error('No code received');
                    setState({ isLoggingIn: false, error: '登入失敗: 未收到授權碼' });
                    return;
                }

                try {
                    loginLogger.debug('Exchanging code for token...');
                    // 交換 Token
                    const tokenResponse = await postApiAuthLineCallback({
                        code,
                        state: oauthState,
                    });

                    loginLogger.debug('Token response:', tokenResponse);

                    if (!tokenResponse.success || !tokenResponse.data) {
                        throw new Error(tokenResponse.error?.message ?? '登入失敗');
                    }

                    const { accessToken, refreshToken, expiresAt, user: apiUser } = tokenResponse.data;

                    if (!apiUser?.id) {
                        throw new Error('登入失敗: 無法取得使用者資訊');
                    }

                    loginLogger.debug('Setting auth with user:', apiUser);
                    // 儲存到 authStore
                    setAuth(
                        accessToken ?? '',
                        refreshToken ?? '',
                        expiresAt ?? new Date(Date.now() + 3600000).toISOString(),
                        {
                            id: apiUser.id,
                            email: apiUser.email,
                            displayName: apiUser.displayName,
                            avatarUrl: apiUser.avatarUrl,
                        }
                    );

                    loginLogger.info('Login successful!');
                    setState({ isLoggingIn: false, error: null });
                } catch (err) {
                    loginLogger.error('Token exchange failed:', err);
                    setState({
                        isLoggingIn: false,
                        error: err instanceof Error ? err.message : '登入失敗',
                    });
                }
            };

            window.addEventListener('message', handleMessage);

            // 監聽彈窗關閉
            const checkClosed = setInterval(() => {
                if (popup.closed) {
                    clearInterval(checkClosed);
                    window.removeEventListener('message', handleMessage);
                    setState(prev => {
                        if (prev.isLoggingIn) {
                            return { isLoggingIn: false, error: null };
                        }
                        return prev;
                    });
                }
            }, 500);

        } catch (err) {
            setState({
                isLoggingIn: false,
                error: err instanceof Error ? err.message : '登入失敗',
            });
        }
    }, [state.isLoggingIn, setAuth]);

    /**
     * 登出
     */
    const logout = useCallback(async () => {
        const { clearAuth } = useAuthStore.getState();
        clearAuth();
    }, []);

    return {
        login,
        logout,
        isLoggingIn: state.isLoggingIn,
        error: state.error,
        isAuthenticated,
        user,
    };
}
