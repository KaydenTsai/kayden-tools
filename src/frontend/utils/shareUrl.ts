import LZString from 'lz-string';
import type { Bill } from '@/types/snap-split';

const SHARE_VERSION = 1;
const URL_LENGTH_WARNING = 2000;

interface ShareData {
    v: number;
    bill: Bill;
}

function validateBill(data: unknown): data is Bill {
    if (!data || typeof data !== 'object') return false;
    const bill = data as Record<string, unknown>;

    if (
        typeof bill.id !== 'string' ||
        typeof bill.name !== 'string' ||
        !Array.isArray(bill.members) ||
        !Array.isArray(bill.expenses)
    ) {
        return false;
    }

    // Validate each expense has required fields
    for (const expense of bill.expenses as Record<string, unknown>[]) {
        if (typeof expense.id !== 'string' || typeof expense.name !== 'string') {
            return false;
        }
        // Ensure isItemized and items fields exist (migrate old format)
        if (expense.isItemized === undefined) {
            expense.isItemized = false;
        }
        if (!Array.isArray(expense.items)) {
            expense.items = [];
        }
    }

    return true;
}

export function encodeBillToUrl(bill: Bill): { url: string; isLong: boolean } {
    const shareData: ShareData = { v: SHARE_VERSION, bill };
    const json = JSON.stringify(shareData);
    const compressed = LZString.compressToEncodedURIComponent(json);
    // 使用 query parameter，相容 HashRouter
    const url = `${window.location.origin}${window.location.pathname}${window.location.hash.split('?')[0]}?snap=${compressed}`;
    return { url, isLong: url.length > URL_LENGTH_WARNING };
}

export function decodeBillFromUrl(): Bill | null {
    // 從 hash 中的 query string 或一般 query string 取得 snap 參數
    const hashQuery = window.location.hash.split('?')[1];
    const params = new URLSearchParams(hashQuery || window.location.search);
    const compressed = params.get('snap');

    if (!compressed) return null;

    try {
        const json = LZString.decompressFromEncodedURIComponent(compressed);
        if (!json) return null;

        const data = JSON.parse(json) as ShareData;
        if (!validateBill(data.bill)) return null;

        return data.bill;
    } catch {
        return null;
    }
}

export function clearShareHash(): void {
    // 移除 query string，保留 hash 路由部分
    const hashPath = window.location.hash.split('?')[0];
    history.replaceState(null, '', window.location.pathname + hashPath);
}
