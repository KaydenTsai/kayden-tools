/**
 * useNetworkStatus - 網路狀態監控 Hook
 *
 * 功能：
 * 1. 監控瀏覽器的 online/offline 事件
 * 2. 提供網路狀態資訊
 * 3. 支援回調通知
 */

import { useState, useEffect, useCallback } from 'react';
import type { NetworkStatus } from '../types/sync';

interface NetworkStatusResult {
    /** 當前網路狀態 */
    status: NetworkStatus;
    /** 是否在線 */
    isOnline: boolean;
    /** 是否離線 */
    isOffline: boolean;
    /** 最後上線時間 */
    lastOnlineAt: Date | null;
    /** 最後離線時間 */
    lastOfflineAt: Date | null;
}

interface UseNetworkStatusOptions {
    /** 網路恢復時的回調 */
    onOnline?: () => void;
    /** 網路斷開時的回調 */
    onOffline?: () => void;
}

export function useNetworkStatus(options: UseNetworkStatusOptions = {}): NetworkStatusResult {
    const { onOnline, onOffline } = options;

    const [status, setStatus] = useState<NetworkStatus>(() =>
        typeof navigator !== 'undefined' && navigator.onLine ? 'online' : 'offline'
    );
    const [lastOnlineAt, setLastOnlineAt] = useState<Date | null>(() =>
        typeof navigator !== 'undefined' && navigator.onLine ? new Date() : null
    );
    const [lastOfflineAt, setLastOfflineAt] = useState<Date | null>(null);

    const handleOnline = useCallback(() => {
        setStatus('online');
        setLastOnlineAt(new Date());
        onOnline?.();
    }, [onOnline]);

    const handleOffline = useCallback(() => {
        setStatus('offline');
        setLastOfflineAt(new Date());
        onOffline?.();
    }, [onOffline]);

    useEffect(() => {
        // 初始狀態
        if (typeof navigator !== 'undefined') {
            setStatus(navigator.onLine ? 'online' : 'offline');
        }

        window.addEventListener('online', handleOnline);
        window.addEventListener('offline', handleOffline);

        return () => {
            window.removeEventListener('online', handleOnline);
            window.removeEventListener('offline', handleOffline);
        };
    }, [handleOnline, handleOffline]);

    return {
        status,
        isOnline: status === 'online',
        isOffline: status === 'offline',
        lastOnlineAt,
        lastOfflineAt,
    };
}
