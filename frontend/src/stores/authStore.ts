import {create} from 'zustand';
import {persist} from 'zustand/middleware';
import {postApiAuthRefresh, getApiAuthMe} from '@/api/endpoints/auth/auth';

const STORAGE_KEY = 'kayden-tools-auth';

// 暫時使用本地類型定義，待 API 生成後再替換
interface UserDto {
    id: string;
    email?: string | null;
    displayName?: string | null;
    avatarUrl?: string | null;
    provider?: string | null;
}

interface AuthState {
    // State
    accessToken: string | null;
    refreshToken: string | null;
    expiresAt: string | null;
    user: UserDto | null;
    isInitialized: boolean;
    /** Zustand persist hydration 完成標誌 */
    isHydrated: boolean;

    // Computed
    isAuthenticated: boolean;

    // Actions
    setAuth: (accessToken: string, refreshToken: string, expiresAt: string, user: UserDto) => void;
    clearAuth: () => void;
    refreshTokens: () => Promise<boolean>;
    fetchCurrentUser: () => Promise<void>;
    logout: () => Promise<void>;
    initialize: () => Promise<void>;
    setHydrated: () => void;
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
            isHydrated: false,
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
                const {refreshToken} = get();
                if (!refreshToken) return false;

                try {
                    const response = await postApiAuthRefresh({refreshToken});

                    if (response.success && response.data) {
                        const {accessToken, refreshToken: newRefreshToken, expiresAt, user} = response.data;

                        if (accessToken && newRefreshToken && user) {
                            set({
                                accessToken,
                                refreshToken: newRefreshToken,
                                expiresAt: expiresAt ?? null,
                                user: {
                                    id: user.id ?? '',
                                    email: user.email,
                                    displayName: user.displayName,
                                    avatarUrl: user.avatarUrl,
                                },
                                isAuthenticated: true,
                            });
                            return true;
                        }
                    }

                    get().clearAuth();
                    return false;
                } catch {
                    get().clearAuth();
                    return false;
                }
            },

            fetchCurrentUser: async () => {
                const {accessToken} = get();
                if (!accessToken) return;

                try {
                    const response = await getApiAuthMe();

                    if (response.success && response.data) {
                        set({
                            user: {
                                id: response.data.id ?? '',
                                email: response.data.email,
                                displayName: response.data.displayName,
                                avatarUrl: response.data.avatarUrl,
                            },
                        });
                    }
                } catch {
                    const refreshed = await get().refreshTokens();
                    if (!refreshed) {
                        get().clearAuth();
                    }
                }
            },

            // TODO: 接入 API 後實作
            logout: async () => {
                get().clearAuth();
            },

            initialize: async () => {
                const {accessToken, expiresAt} = get();

                if (!accessToken) {
                    set({isInitialized: true});
                    return;
                }

                // Check if token is expired
                if (expiresAt && new Date(expiresAt) < new Date()) {
                    get().clearAuth();
                    set({isInitialized: true});
                    return;
                }

                set({isInitialized: true, isAuthenticated: true});
            },

            setHydrated: () => set({isHydrated: true}),
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
            // Hydration 完成後自動設定標誌
            onRehydrateStorage: () => (state) => {
                state?.setHydrated();
            },
        }
    )
);

// Helper function to get access token for axios interceptor
export const getAccessToken = () => useAuthStore.getState().accessToken;
