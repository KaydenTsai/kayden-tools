import type {AxiosError, AxiosRequestConfig, AxiosResponse} from 'axios';
import axios from 'axios';
import {AppError, type ProblemDetails} from '@shared/types/error';
import {useAuthStore} from '@/stores/authStore';

const BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5063';

// 取得 auth store state（非 React 環境使用）
const getAuthStore = () => useAuthStore.getState();

const instance = axios.create({
    baseURL: BASE_URL,
    headers: {
        'Content-Type': 'application/json',
    },
});

// Request interceptor - add auth token from Zustand store
instance.interceptors.request.use(
    (config) => {
        console.log('[Axios] Request interceptor, url:', config.url);
        try {
            const store = getAuthStore();
            console.log('[Axios] Store accessToken:', store.accessToken ? `${store.accessToken.slice(0, 20)}...` : 'null');
            if (store.accessToken) {
                config.headers.Authorization = `Bearer ${store.accessToken}`;
                console.log('[Axios] Authorization header set');
            } else {
                console.log('[Axios] No accessToken, skipping Authorization header');
            }
        } catch (err) {
            console.error('[Axios] Error in request interceptor:', err);
        }
        return config;
    },
    (error) => {
        console.error('[Axios] Request interceptor error:', error);
        return Promise.reject(error);
    }
);

// Response interceptor - handle token refresh using store and throw AppError
instance.interceptors.response.use(
    (response) => response,
    async (error: AxiosError<ProblemDetails>) => {
        const originalRequest = error.config as AxiosRequestConfig & { _retry?: boolean };

        // Handle 401 with token refresh
        if (error.response?.status === 401 && !originalRequest._retry) {
            originalRequest._retry = true;

            const store = getAuthStore();
            if (store.refreshToken) {
                try {
                    // 使用 store 的 refreshTokens 方法
                    const success = await store.refreshTokens();

                    if (success) {
                        // 重新取得更新後的 token
                        const newStore = getAuthStore();
                        if (originalRequest.headers && newStore.accessToken) {
                            originalRequest.headers.Authorization = `Bearer ${newStore.accessToken}`;
                        }
                        return instance(originalRequest);
                    }
                } catch {
                    store.clearAuth();
                }
            }
        }

        // Convert error to AppError
        if (error.response?.data && typeof error.response.data === 'object' && 'title' in error.response.data) {
            throw new AppError(error.response.data as ProblemDetails);
        }

        if (error.request) {
            throw new AppError({
                status: 0,
                title: 'Network Error',
                detail: '無法連接到伺服器',
                errorCode: 'NETWORK_ERROR',
            });
        }

        throw new AppError({
            status: 500,
            title: 'Unknown Error',
            detail: error.message,
            errorCode: 'UNKNOWN_ERROR',
        });
    }
);

/**
 * Orval mutator function
 */
export const customInstance = <T>(config: AxiosRequestConfig): Promise<T> => {
    console.log('[Axios] customInstance called:', config.method, config.url);
    return instance(config)
        .then((response: AxiosResponse<T>) => {
            console.log('[Axios] Response received:', config.url);
            return response.data;
        })
        .catch((error) => {
            console.error('[Axios] Request failed:', config.url, error);
            throw error;
        });
};

// Alias for backwards compatibility
export const axiosInstance = customInstance;

export {instance};
