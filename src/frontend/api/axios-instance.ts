import axios from 'axios';
import type { AxiosError, AxiosRequestConfig, AxiosResponse } from 'axios';

const BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5063';
const AUTH_STORAGE_KEY = 'kayden-tools-auth';

// Helper to get auth state from localStorage (avoiding circular dependency with store)
function getAuthState() {
  try {
    const stored = localStorage.getItem(AUTH_STORAGE_KEY);
    if (stored) {
      const parsed = JSON.parse(stored);
      return parsed.state || {};
    }
  } catch {
    // Ignore parse errors
  }
  return {};
}

// Helper to update auth state in localStorage
function updateAuthState(updates: Record<string, unknown>) {
  try {
    const stored = localStorage.getItem(AUTH_STORAGE_KEY);
    const current = stored ? JSON.parse(stored) : { state: {} };
    current.state = { ...current.state, ...updates };
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(current));
  } catch {
    // Ignore storage errors
  }
}

// Helper to clear auth state
function clearAuthState() {
  try {
    localStorage.removeItem(AUTH_STORAGE_KEY);
  } catch {
    // Ignore storage errors
  }
}

const instance = axios.create({
  baseURL: BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor - add auth token
instance.interceptors.request.use(
  (config) => {
    const { accessToken } = getAuthState();
    if (accessToken) {
      config.headers.Authorization = `Bearer ${accessToken}`;
    }

    return config;
  },
  (error) => Promise.reject(error)
);

// Response interceptor - handle token refresh
instance.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as AxiosRequestConfig & { _retry?: boolean };

    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;

      const { refreshToken } = getAuthState();
      if (refreshToken) {
        try {
          const response = await axios.post(`${BASE_URL}/api/Auth/refresh`, {
            refreshToken,
          });

          if (response.data?.success && response.data?.data) {
            const { accessToken, refreshToken: newRefreshToken, expiresAt, user } = response.data.data;

            updateAuthState({
              accessToken,
              refreshToken: newRefreshToken,
              expiresAt,
              user,
              isAuthenticated: true,
            });

            if (originalRequest.headers) {
              originalRequest.headers.Authorization = `Bearer ${accessToken}`;
            }

            return instance(originalRequest);
          }
        } catch {
          clearAuthState();
        }
      }
    }

    return Promise.reject(error);
  }
);

/**
 * Orval mutator function
 */
export const axiosInstance = <T>(config: AxiosRequestConfig): Promise<T> => {
  return instance(config).then((response: AxiosResponse<T>) => response.data);
};

export { instance };
