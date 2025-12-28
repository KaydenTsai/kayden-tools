import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { UserDto } from '@/api/models';
import {
    getApiAuthMe,
    postApiAuthRefresh,
    postApiAuthLogout,
} from '@/api';

const STORAGE_KEY = 'kayden-tools-auth';

interface AuthState {
    // State
    accessToken: string | null;
    refreshToken: string | null;
    expiresAt: string | null;
    user: UserDto | null;
    isInitialized: boolean;

    // Computed
    isAuthenticated: boolean;

    // Actions
    setAuth: (accessToken: string, refreshToken: string, expiresAt: string, user: UserDto) => void;
    clearAuth: () => void;
    refreshTokens: () => Promise<boolean>;
    fetchCurrentUser: () => Promise<void>;
    logout: () => Promise<void>;
    initialize: () => Promise<void>;
}

export const useAuthStore = create<AuthState>()(
    persist(
        (set, get) => ({
            // Initial state
            accessToken: null,
            refreshToken: null,
            expiresAt: null,
            user: null,
            isInitialized: false,
            isAuthenticated: false,

            setAuth: (accessToken, refreshToken, expiresAt, user) => {
                set({
                    accessToken,
                    refreshToken,
                    expiresAt,
                    user,
                    isAuthenticated: true,
                });
            },

            clearAuth: () => {
                set({
                    accessToken: null,
                    refreshToken: null,
                    expiresAt: null,
                    user: null,
                    isAuthenticated: false,
                });
            },

            refreshTokens: async () => {
                const { refreshToken } = get();
                if (!refreshToken) return false;

                try {
                    const response = await postApiAuthRefresh({ refreshToken });
                    if (response.success && response.data) {
                        const { accessToken, refreshToken: newRefreshToken, expiresAt, user } = response.data;
                        if (accessToken && newRefreshToken && expiresAt && user) {
                            get().setAuth(accessToken, newRefreshToken, expiresAt, user);
                            return true;
                        }
                    }
                } catch {
                    get().clearAuth();
                }
                return false;
            },

            fetchCurrentUser: async () => {
                try {
                    const response = await getApiAuthMe();
                    if (response.success && response.data) {
                        set({ user: response.data });
                    }
                } catch (error) {
                    // 錯誤時不清除登入狀態，避免競態條件導致剛登入就被登出
                    console.warn('取得使用者資訊失敗:', error);
                }
            },

            logout: async () => {
                const { refreshToken, clearAuth } = get();
                // 立即清除本地狀態，不等待 API 回應 (Fire-and-Forget)
                clearAuth();
                // 背景呼叫 API 撤銷 token，不阻塞 UI
                if (refreshToken) {
                    postApiAuthLogout({ refreshToken }).catch(() => {
                        // Ignore logout errors
                    });
                }
            },

            initialize: async () => {
                const { accessToken, expiresAt, refreshTokens, fetchCurrentUser } = get();

                if (!accessToken) {
                    set({ isInitialized: true });
                    return;
                }

                // Check if token is expired
                if (expiresAt && new Date(expiresAt) < new Date()) {
                    const refreshed = await refreshTokens();
                    if (!refreshed) {
                        set({ isInitialized: true });
                        return;
                    }
                }

                // Fetch current user to validate token
                await fetchCurrentUser();
                set({ isInitialized: true });
            },
        }),
        {
            name: STORAGE_KEY,
            partialize: (state) => ({
                accessToken: state.accessToken,
                refreshToken: state.refreshToken,
                expiresAt: state.expiresAt,
                user: state.user,
                isAuthenticated: state.isAuthenticated,
            }),
        }
    )
);

// Helper function to get access token for axios interceptor
export const getAccessToken = () => useAuthStore.getState().accessToken;
