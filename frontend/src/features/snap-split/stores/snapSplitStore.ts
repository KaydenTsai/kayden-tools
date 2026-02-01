/**
 * SnapSplit Store Convention:
 * Store action 中 get() 與 set() 之間不得有 await，
 * 否則會產生競態條件（state 在 await 期間可能被其他 action 修改）。
 * 若需要 async 操作，應先完成 await，再同步執行 get() → 計算 → set() 。
 */
import {create} from "zustand";
import {persist} from "zustand/middleware";
import type {Bill, Expense, Member, SyncStatus} from "@/features/snap-split/types/snap-split";
import type {IdMappings} from "@/features/snap-split/types/sync";
import {applyIdMappingsToBill, rebaseBillFromServer} from "@/features/snap-split/services/billAdapter";
import {createBillSnapshot} from "@/features/snap-split/services/deltaFactory";
import {createSettledKey, matchSettledKeyPrefix} from "@/features/snap-split/lib/settlement";
import type {BillDto} from "@/api/models";
import {deleteBill as deleteBillApi} from "@/api/endpoints/bills/bills";
import {syncLogger} from "@/shared/lib/logger";

/** 刪除結果類型 */
export type DeleteBillResult =
    | { action: 'deleted' }
    | { action: 'left' }
    | { action: 'confirm_needed'; bill: Bill };

const generateId = () => crypto.randomUUID();
const now = () => new Date().toISOString();

const createDefaultBill = (name: string): Bill => ({
    id: generateId(),
    name,
    members: [],
    expenses: [],
    settledTransfers: [],
    createdAt: now(),
    updatedAt: now(),
    syncStatus: 'local',
    version: 0,
    isDeleted: false,
});

export interface SnapSplitState {
    bills: Bill[];
    currentBillId: string | null;
    connectionStatus: 'disconnected' | 'connecting' | 'connected';
    skippedClaimBillIds: Set<string>;

    // Core Actions
    createBill: (name: string) => void;
    deleteBill: (id: string) => void;
    softDeleteBill: (id: string) => void;
    selectBill: (id: string | null) => void;
    updateBillName: (id: string, name: string) => void;

    // Delete with Sync
    deleteBillWithSync: (id: string, currentUserId?: string) => DeleteBillResult | null;
    confirmDeleteBill: (id: string, deleteFromCloud: boolean) => Promise<void>;

    // Helpers
    importBill: (bill: Bill) => string;
    setConnectionStatus: (status: SnapSplitState['connectionStatus']) => void;

    // Sync Methods
    setBillSyncStatus: (billId: string, status: SyncStatus, error?: string) => void;
    setBillConflict: (billId: string) => void;
    setBillRemoteId: (billId: string, remoteId: string, shareCode?: string, version?: number) => void;
    applyIdMappings: (billId: string, mappings: IdMappings) => void;
    markBillAsSynced: (billId: string, sentUpdatedAt?: string) => void;
    importBillsFromRemote: (bills: Bill[], mode?: 'merge' | 'replace_list') => number;
    getUnsyncedBills: () => Bill[];
    rebaseBillFromServer: (billId: string, serverBill: BillDto) => void;

    // Claim Methods (Local only)
    claimMember: (params: { memberId: string; userId: string; displayName?: string; avatarUrl?: string }) => void;
    unclaimMember: (memberId: string) => void;
    shouldShowClaimPrompt: (billId: string, userId: string | undefined) => boolean;
    skipClaimForBill: (billId: string) => void;

    // Actions
    addMember: (name: string) => void;
    removeMember: (id: string) => void;
    updateMember: (id: string, name: string) => void;
    addExpense: (expense: any) => void;
    removeExpense: (id: string) => void;
    updateExpense: (id: string, updates: any) => void;
    toggleSettlement: (fromId: string, toId: string) => void;

}

export const useSnapSplitStore = create<SnapSplitState>()(
    persist(
        (set, get) => ({
            bills: [],
            currentBillId: null,
            connectionStatus: 'disconnected',
            skippedClaimBillIds: new Set<string>(),

            createBill: (name) => {
                const newBill = createDefaultBill(name);
                set(state => ({
                    bills: [...state.bills, newBill],
                    currentBillId: newBill.id,
                }));
            },

            selectBill: (id) => set({currentBillId: id}),
            setConnectionStatus: (status) => set({connectionStatus: status}),

            addMember: (name) => {
                const {currentBillId} = get();
                if (!currentBillId) return;

                // 使用 functional update 確保原子性操作，避免競態條件
                set(state => {
                    const bill = state.bills.find(b => b.id === currentBillId);
                    if (!bill) return state;

                    // 防止重複新增：檢查是否已存在相同名稱的成員（在 500ms 內新增的）
                    const recentThreshold = 500;
                    const currentTime = Date.now();
                    const duplicateExists = bill.members.some(m =>
                        m.name === name &&
                        m.createdAt &&
                        (currentTime - new Date(m.createdAt).getTime()) < recentThreshold
                    );

                    if (duplicateExists) {
                        return state;
                    }

                    const newMember: Member = {id: generateId(), name, createdAt: now()};
                    const updatedMembers = [...bill.members, newMember];

                    return {
                        bills: state.bills.map(b => b.id === bill.id ? {
                            ...b,
                            members: updatedMembers,
                            syncStatus: b.remoteId ? 'modified' : 'local',
                            updatedAt: now()
                        } : b)
                    };
                });
            },

            removeMember: (id) => {
                const {currentBillId, bills} = get();
                const bill = bills.find(b => b.id === currentBillId);
                if (!bill) return;

                const memberToRemove = bill.members.find(m => m.id === id);
                const updatedMembers = bill.members.filter(m => m.id !== id);

                // 追蹤刪除的遠端 ID 以供同步
                const deletedMemberIds = [...(bill.deletedMemberIds ?? [])];
                if (memberToRemove?.remoteId) {
                    deletedMemberIds.push(memberToRemove.remoteId);
                }

                // 追蹤刪除的消費 ID 以供同步
                const deletedExpenseIds = [...(bill.deletedExpenseIds ?? [])];

                // 處理消費紀錄
                const updatedExpenses = bill.expenses
                    .filter(e => {
                        if (!e.isItemized) {
                            // 非逐項紀錄：如果該成員是付款人，刪除整筆消費
                            if (e.paidById === id) {
                                if (e.remoteId) deletedExpenseIds.push(e.remoteId);
                                return false;
                            }
                            // 如果該成員是唯一平分人，也刪除整筆消費
                            if (e.participants.length === 1 && e.participants[0] === id) {
                                if (e.remoteId) deletedExpenseIds.push(e.remoteId);
                                return false;
                            }
                        }
                        return true;
                    })
                    .map(e => {
                        if (e.isItemized) {
                            // 逐項紀錄：處理品項
                            const deletedItemIds = [...(e.deletedItemIds ?? [])];
                            const updatedItems = e.items
                                .filter(item => {
                                    // 如果該成員是品項付款人，刪除該品項
                                    if (item.paidById === id) {
                                        if (item.remoteId) deletedItemIds.push(item.remoteId);
                                        return false;
                                    }
                                    // 如果該成員是唯一平分人，也刪除該品項
                                    if (item.participants.length === 1 && item.participants[0] === id) {
                                        if (item.remoteId) deletedItemIds.push(item.remoteId);
                                        return false;
                                    }
                                    return true;
                                })
                                .map(item => ({
                                    ...item,
                                    // 從品項平分人中移除該成員
                                    participants: item.participants.filter(p => p !== id)
                                }));

                            return {
                                ...e,
                                items: updatedItems,
                                deletedItemIds: deletedItemIds.length > 0 ? deletedItemIds : undefined,
                                // 從消費平分人中移除（雖然逐項紀錄通常不用這個）
                                participants: e.participants.filter(p => p !== id)
                            };
                        } else {
                            // 一般消費：從平分人中移除該成員
                            return {
                                ...e,
                                participants: e.participants.filter(p => p !== id)
                            };
                        }
                    });

                set(state => ({
                    bills: state.bills.map(b => b.id === bill.id ? {
                        ...b,
                        members: updatedMembers,
                        expenses: updatedExpenses,
                        deletedMemberIds,
                        deletedExpenseIds: deletedExpenseIds.length > 0 ? deletedExpenseIds : b.deletedExpenseIds,
                        syncStatus: 'modified',
                        updatedAt: now()
                    } : b)
                }));
            },

            updateMember: (id, name) => {
                const {currentBillId, bills} = get();
                const bill = bills.find(b => b.id === currentBillId);
                if (!bill) return;

                const updatedMembers = bill.members.map(m => m.id === id ? {...m, name} : m);
                set(state => ({
                    bills: state.bills.map(b => b.id === bill.id ? {
                        ...b,
                        members: updatedMembers,
                        syncStatus: b.remoteId ? 'modified' : 'local',
                        updatedAt: now()
                    } : b)
                }));
            },

            addExpense: (expenseData) => {
                const {currentBillId} = get();
                if (!currentBillId) return;

                // 使用 functional update 確保原子性操作，避免競態條件
                set(state => {
                    const bill = state.bills.find(b => b.id === currentBillId);
                    if (!bill) return state;

                    // 防止重複新增：檢查是否已存在相同名稱和金額的 expense（在 500ms 內新增的）
                    const recentThreshold = 500; // 500ms
                    const currentTime = Date.now();
                    const duplicateExists = bill.expenses.some(e =>
                        e.name === (expenseData.name || "New Expense") &&
                        e.amount === (expenseData.amount || 0) &&
                        e.createdAt &&
                        (currentTime - new Date(e.createdAt).getTime()) < recentThreshold
                    );

                    if (duplicateExists) {
                        return state; // 不做任何變更
                    }

                    const newExpense: Expense = {
                        id: generateId(),
                        name: expenseData.name || "New Expense",
                        amount: expenseData.amount || 0,
                        serviceFeePercent: expenseData.serviceFeePercent || 0,
                        isItemized: expenseData.isItemized || false,
                        paidById: expenseData.paidById || "",
                        participants: expenseData.participants || [],
                        items: expenseData.items || [],
                        createdAt: now(), // 新增時間戳以便追蹤
                    };

                    // 在 set() 回調內計算 updatedExpenses，使用最新的 bill.expenses
                    const updatedExpenses = [...bill.expenses, newExpense];

                    return {
                        bills: state.bills.map(b => b.id === bill.id ? {
                            ...b,
                            expenses: updatedExpenses,
                            syncStatus: b.remoteId ? 'modified' : 'local',
                            updatedAt: now()
                        } : b)
                    };
                });
            },

            removeExpense: (id) => {
                const {currentBillId, bills} = get();
                const bill = bills.find(b => b.id === currentBillId);
                if (!bill) return;

                const expenseToRemove = bill.expenses.find(e => e.id === id);
                const updatedExpenses = bill.expenses.filter(e => e.id !== id);

                // 追蹤刪除的遠端 ID 以供同步
                const deletedExpenseIds = [...(bill.deletedExpenseIds ?? [])];
                if (expenseToRemove?.remoteId) {
                    deletedExpenseIds.push(expenseToRemove.remoteId);
                }

                set(state => ({
                    bills: state.bills.map(b => b.id === bill.id ? {
                        ...b,
                        expenses: updatedExpenses,
                        deletedExpenseIds,
                        syncStatus: 'modified',
                        updatedAt: now()
                    } : b)
                }));
            },

            updateExpense: (id, updates) => {
                const {currentBillId, bills} = get();
                const bill = bills.find(b => b.id === currentBillId);
                if (!bill) return;

                const updatedExpenses = bill.expenses.map(e => e.id === id ? {...e, ...updates} : e);
                set(state => ({
                    bills: state.bills.map(b => b.id === bill.id ? {
                        ...b,
                        expenses: updatedExpenses,
                        syncStatus: b.remoteId ? 'modified' : 'local',
                        updatedAt: now()
                    } : b)
                }));
            },

            toggleSettlement: (fromId, toId) => {
                const {currentBillId, bills} = get();
                const bill = bills.find(b => b.id === currentBillId);
                if (!bill) return;

                const key = createSettledKey(fromId, toId);
                let newSettled = [...bill.settledTransfers];
                let unsettledTransfers = [...(bill.unsettledTransfers ?? [])];

                if (newSettled.some(k => matchSettledKeyPrefix(k, fromId, toId))) {
                    // 取消結算 - 如果有 remoteId 則追蹤
                    newSettled = newSettled.filter(k => !matchSettledKeyPrefix(k, fromId, toId));
                    if (bill.remoteId) {
                        // 解析成員的 remoteId
                        const fromMember = bill.members.find(m => m.id === fromId);
                        const toMember = bill.members.find(m => m.id === toId);
                        if (fromMember?.remoteId && toMember?.remoteId) {
                            unsettledTransfers.push(createSettledKey(fromMember.remoteId, toMember.remoteId));
                        }
                    }
                } else {
                    newSettled.push(key);
                }

                set(state => ({
                    bills: state.bills.map(b => b.id === bill.id ? {
                        ...b,
                        settledTransfers: newSettled,
                        unsettledTransfers,
                        syncStatus: b.remoteId ? 'modified' : 'local',
                        updatedAt: now()
                    } : b)
                }));
            },

            claimMember: ({memberId, userId, displayName, avatarUrl}) => {
                const {currentBillId, bills} = get();
                if (!currentBillId) return;
                const bill = bills.find(b => b.id === currentBillId);
                if (!bill) return;

                set(state => ({
                    bills: state.bills.map(b => b.id === currentBillId ? {
                        ...b,
                        members: b.members.map(m => m.id === memberId ? {
                            ...m,
                            userId,
                            avatarUrl,
                            // 保存原始名稱（若尚未保存），並更新為 Line 顯示名稱
                            originalName: m.originalName ?? m.name,
                            name: displayName ?? m.name,
                            claimedAt: now(),
                        } : m),
                        syncStatus: b.remoteId ? 'modified' : 'local',
                        updatedAt: now(),
                    } : b)
                }));
            },

            unclaimMember: (memberId) => {
                const {currentBillId, bills} = get();
                if (!currentBillId) return;
                const bill = bills.find(b => b.id === currentBillId);
                if (!bill) return;

                set(state => ({
                    bills: state.bills.map(b => b.id === currentBillId ? {
                        ...b,
                        members: b.members.map(m => m.id === memberId ? {
                            ...m,
                            userId: undefined,
                            avatarUrl: undefined,
                            name: m.originalName || m.name,
                            originalName: undefined,
                            claimedAt: undefined,
                        } : m),
                        syncStatus: b.remoteId ? 'modified' : 'local',
                        updatedAt: now(),
                    } : b)
                }));
            },

            importBill: (bill) => {
                let resultId = '';

                // 使用 functional update 確保原子性，防止競態條件
                set(state => {
                    // 檢查是否已存在相同的帳單（基於 remoteId 或 shareCode）
                    const existingBill = state.bills.find(b =>
                        (bill.remoteId && b.remoteId === bill.remoteId) ||
                        (bill.shareCode && b.shareCode === bill.shareCode)
                    );

                    if (existingBill) {
                        // 已存在，直接選中並返回現有 ID
                        resultId = existingBill.id;
                        return { currentBillId: existingBill.id };
                    }

                    const newId = generateId();
                    resultId = newId;
                    return {
                        bills: [...state.bills, {
                            ...bill,
                            id: newId,
                            createdAt: now(),
                            updatedAt: now(),
                        }],
                        currentBillId: newId,
                    };
                });

                return resultId;
            },

            deleteBill: (id) => {
                set(state => ({
                    bills: state.bills.filter(b => b.id !== id),
                    currentBillId: state.currentBillId === id ? null : state.currentBillId,
                }));
            },

            softDeleteBill: (id) => {
                set(state => ({
                    bills: state.bills.map(b =>
                        b.id === id
                            ? { ...b, isDeleted: true, deletedAt: now(), syncStatus: 'modified' as SyncStatus }
                            : b
                    ),
                    currentBillId: state.currentBillId === id ? null : state.currentBillId,
                }));
            },

            deleteBillWithSync: (id, currentUserId) => {
                const {bills} = get();
                const bill = bills.find(b => b.id === id);
                if (!bill) return null;

                // 情況 1: 純本地帳單 (無 remoteId) → 直接刪除
                if (!bill.remoteId) {
                    get().deleteBill(id);
                    return {action: 'deleted'};
                }

                // 情況 2: 判斷是否為協作帳單 (其他成員已認領)
                const hasOtherClaimedMembers = bill.members.some(
                    m => m.userId && m.userId !== currentUserId
                );

                if (hasOtherClaimedMembers) {
                    // 協作帳單 → 軟刪除，等 autoSync 同步 DELETE 後再硬刪除
                    get().softDeleteBill(id);
                    return {action: 'left'};
                }

                // 情況 3: 獨佔帳單 → 詢問使用者
                return {action: 'confirm_needed', bill};
            },

            confirmDeleteBill: async (id, deleteFromCloud) => {
                const {bills} = get();
                const bill = bills.find(b => b.id === id);
                if (!bill?.remoteId) {
                    get().deleteBill(id);  // 純本地帳單，直接硬刪
                    return;
                }

                if (deleteFromCloud) {
                    // 先軟刪除（標記 syncStatus: 'modified'）
                    get().softDeleteBill(id);

                    try {
                        await deleteBillApi(bill.remoteId);
                        syncLogger.info('Bill deleted from cloud:', bill.remoteId);
                        // API 成功 → 硬刪除本地資料
                        get().deleteBill(id);
                    } catch (error) {
                        syncLogger.error('Failed to delete from cloud:', error);
                        // API 失敗 → 保留軟刪除狀態，等 autoSync 重試
                    }
                } else {
                    // 使用者選擇不刪除雲端 → 只刪除本地
                    get().deleteBill(id);
                }
            },

            updateBillName: (id, name) => {
                set(state => ({
                    bills: state.bills.map(b =>
                        b.id === id
                            ? {...b, name, updatedAt: now(), syncStatus: b.remoteId ? 'modified' as const : 'local' as const}
                            : b
                    ),
                }));
            },

            // Sync methods implementation
            setBillSyncStatus: (billId, status, error) => {
                set(state => ({
                    bills: state.bills.map(b =>
                        b.id === billId
                            ? {...b, syncStatus: status, syncError: error}
                            : b
                    ),
                }));
            },

            setBillConflict: (billId) => {
                set(state => ({
                    bills: state.bills.map(b =>
                        b.id === billId
                            ? {...b, syncStatus: 'conflict' as SyncStatus}
                            : b
                    ),
                }));
            },

            setBillRemoteId: (billId, remoteId, shareCode, version) => {
                set(state => ({
                    bills: state.bills.map(b =>
                        b.id === billId
                            ? {
                                ...b,
                                remoteId,
                                shareCode: shareCode ?? b.shareCode,
                                version: version ?? b.version,
                            }
                            : b
                    ),
                }));
            },

            applyIdMappings: (billId, mappings) => {
                set(state => ({
                    bills: state.bills.map(b =>
                        b.id === billId ? applyIdMappingsToBill(b, mappings) : b
                    ),
                }));
            },

            markBillAsSynced: (billId, _sentUpdatedAt) => {
                const timestamp = now();
                set(state => ({
                    bills: state.bills.map(b => {
                        if (b.id !== billId) return b;

                        // 先清理 expenses 的 deletedItemIds
                        const cleanedExpenses = b.expenses.map(e => ({...e, deletedItemIds: undefined}));

                        // 建立清理後的帳單狀態（用於 snapshot）
                        const cleanedBill = {
                            ...b,
                            expenses: cleanedExpenses,
                            deletedMemberIds: undefined,
                            deletedExpenseIds: undefined,
                            unsettledTransfers: undefined,
                        };

                        return {
                            ...cleanedBill,
                            syncStatus: 'synced' as SyncStatus,
                            lastSyncedAt: timestamp,
                            syncError: undefined,
                            // 儲存同步快照（用於下次 delta sync 比較）
                            syncSnapshot: createBillSnapshot(cleanedBill),
                        };
                    }),
                }));
            },

            importBillsFromRemote: (bills, mode = 'merge') => {
                let importedCount = 0;

                // 使用 functional update 確保原子性操作
                set(state => {
                    const existingBills = state.bills;

                    if (mode === 'replace_list') {
                        // 替換整個列表（保留本地未同步的帳單）
                        const localOnlyBills = existingBills.filter(
                            b => !b.remoteId || b.syncStatus === 'local'
                        );
                        importedCount = bills.length;
                        return {bills: [...localOnlyBills, ...bills]};
                    } else {
                        // 合併模式：只新增不存在的帳單（使用最新狀態檢查）
                        const existingRemoteIds = new Set(
                            existingBills.filter(b => b.remoteId).map(b => b.remoteId)
                        );
                        const newBills = bills.filter(
                            b => b.remoteId && !existingRemoteIds.has(b.remoteId)
                        );
                        importedCount = newBills.length;
                        if (newBills.length > 0) {
                            return {bills: [...existingBills, ...newBills]};
                        }
                        return state; // 沒有變更時返回原狀態
                    }
                });

                return importedCount;
            },

            getUnsyncedBills: () => {
                const {bills} = get();
                return bills.filter(
                    b => b.syncStatus === 'local' || b.syncStatus === 'modified' || b.syncStatus === 'error'
                );
            },

            rebaseBillFromServer: (billId: string, serverBill: BillDto) => {
                set(state => ({
                    bills: state.bills.map(b =>
                        b.id === billId ? rebaseBillFromServer(b, serverBill) : b
                    ),
                }));
            },

            shouldShowClaimPrompt: (billId, userId) => {
                if (!userId) return false;
                const {bills, skippedClaimBillIds} = get();
                if (skippedClaimBillIds.has(billId)) return false;

                const bill = bills.find(b => b.id === billId);
                if (!bill) return false;

                // 檢查是否有未認領的成員，且當前用戶尚未認領任何成員
                const hasUnclaimedMembers = bill.members.some(m => !m.userId);
                const userAlreadyClaimed = bill.members.some(m => m.userId === userId);
                return hasUnclaimedMembers && !userAlreadyClaimed;
            },

            skipClaimForBill: (billId) => {
                set(state => ({
                    skippedClaimBillIds: new Set([...state.skippedClaimBillIds, billId]),
                }));
            },

        }),
        {
            name: 'kayden-tools-snap-split-v3',
            partialize: (state) => ({
                bills: state.bills,
                currentBillId: state.currentBillId,
                // 將 Set 轉換為 Array 以支援 JSON 序列化
                skippedClaimBillIds: Array.from(state.skippedClaimBillIds),
            }),
            storage: {
                getItem: (name) => {
                    const str = localStorage.getItem(name);
                    if (!str) return null;
                    try {
                        const parsed = JSON.parse(str);
                        // 將 Array 轉回 Set
                        if (parsed.state?.skippedClaimBillIds && Array.isArray(parsed.state.skippedClaimBillIds)) {
                            parsed.state.skippedClaimBillIds = new Set(parsed.state.skippedClaimBillIds);
                        }
                        return parsed;
                    } catch {
                        return null;
                    }
                },
                setItem: (name, value) => {
                    localStorage.setItem(name, JSON.stringify(value));
                },
                removeItem: (name) => {
                    localStorage.removeItem(name);
                },
            },
        }
    )
);

export const useCurrentBill = () => {
    const {bills, currentBillId} = useSnapSplitStore();
    const bill = bills.find(bill => bill.id === currentBillId) ?? null;
    return bill?.isDeleted ? null : bill;
};
