/**
 * useLogin - 登入 Hook
 *
 * 處理 OAuth 登入流程（LINE / Google）— redirect 模式
 */

import { useState, useCallback } from 'react';
import { useAuthStore } from '@/stores/authStore';
import { getApiAuthLineUrl } from '@/api/endpoints/auth/auth';
import { loginLogger } from '@/shared/lib/logger';

type LoginProvider = 'line' | 'google';

const RETURN_URL_KEY = 'oauth-return-url';

interface LoginState {
    isLoggingIn: boolean;
    error: string | null;
}

export function useLogin() {
    const [state, setState] = useState<LoginState>({
        isLoggingIn: false,
        error: null,
    });

    const { isAuthenticated, user } = useAuthStore();

    /**
     * 同頁重導向 OAuth 登入
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
                const data = response.data as { url: string; state: string };
                authUrl = data.url;
                loginLogger.debug('Got auth URL:', authUrl);
            } else {
                throw new Error('目前僅支援 LINE 登入');
            }

            // 記錄當前路徑，登入完成後導回
            const currentPath = window.location.pathname + window.location.search;
            sessionStorage.setItem(RETURN_URL_KEY, currentPath);
            loginLogger.debug('Saved return URL:', currentPath);

            // 同頁跳轉到 OAuth provider
            window.location.href = authUrl;
        } catch (err) {
            setState({
                isLoggingIn: false,
                error: err instanceof Error ? err.message : '登入失敗',
            });
        }
    }, [state.isLoggingIn]);

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
