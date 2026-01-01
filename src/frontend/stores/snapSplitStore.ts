import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { Bill, SyncStatus } from "@/types/snap-split";
import { applyOperationToBill } from "@/services/operations/applier";
import type { Operation } from "@/services/signalr/billConnection";

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
});

export interface IdMappings {
    members: Record<string, string>;
    expenses: Record<string, string>;
    expenseItems: Record<string, string>;
}

export interface SnapSplitState {
    bills: Bill[];
    currentBillId: string | null;
    connectionStatus: 'disconnected' | 'connecting' | 'connected';
    skippedClaimBillIds: Set<string>;

    // Core Actions
    createBill: (name: string) => void;
    deleteBill: (id: string) => void;
    selectBill: (id: string | null) => void;
    updateBillName: (id: string, name: string) => void;

    /**
     * 套用操作（本地樂觀更新或接收到遠端通知）
     */
    applyOperation: (op: Operation) => void;

    /**
     * 發送操作到後端
     */
    dispatch: (opType: string, targetId: string | undefined, payload: any) => Promise<void>;

    // Helpers
    importBill: (bill: Bill) => string;
    importBillFromSnapshot: (bill: Bill, source?: string) => string;
    setConnectionStatus: (status: SnapSplitState['connectionStatus']) => void;

    // Sync Methods (V3)
    setBillSyncStatus: (billId: string, status: SyncStatus, error?: string) => void;
    setBillRemoteId: (billId: string, remoteId: string, shareCode?: string, version?: number) => void;
    applyIdMappings: (billId: string, mappings: IdMappings) => void;
    markBillAsSynced: (billId: string, sentUpdatedAt?: string) => void;
    importBillsFromRemote: (bills: Bill[], mode?: 'merge' | 'replace_list') => number;
    getUnsyncedBills: () => Bill[];

    // Claim Methods
    claimMember: (params: { memberId: string; userId: string; displayName?: string; avatarUrl?: string }) => void;
    unclaimMember: (memberId: string) => void;
    shouldShowClaimPrompt: (billId: string, userId: string | undefined) => boolean;
    skipClaimForBill: (billId: string) => void;

    // Compatibility Layer (V2 actions redirected to V3 dispatch)
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

            // Core Actions
            createBill: (name) => {
                const newBill = createDefaultBill(name);
                set(state => ({
                    bills: [...state.bills, newBill],
                    currentBillId: newBill.id,
                }));
            },

            selectBill: (id) => set({ currentBillId: id }),

            setConnectionStatus: (status) => set({ connectionStatus: status }),

            applyOperation: (op) => {
                set(state => {
                    // 支援匹配本地 ID 或 remoteId
                    const bill = state.bills.find(b => b.id === op.billId || b.remoteId === op.billId);
                    if (!bill) return state;

                    // 版本檢查：-1 表示樂觀更新，直接套用
                    if (op.version !== -1 && bill.version >= op.version) return state;

                    const updatedBill = applyOperationToBill(bill, op);

                    // 如果是從伺服器確認的操作（version !== -1），標記為已同步
                    // 這表示操作已成功儲存到後端
                    const newSyncStatus = op.version !== -1 ? 'synced' as const : bill.syncStatus;

                    return {
                        bills: state.bills.map(b =>
                            (b.id === bill.id) ? { ...updatedBill, syncStatus: newSyncStatus } : b
                        )
                    };
                });
            },

            dispatch: async (opType, targetId, payload) => {
                const { currentBillId, bills, applyOperation } = get();
                const bill = bills.find(b => b.id === currentBillId);
                if (!bill) return;

                // 建立本地操作
                const localOp = {
                    id: generateId(),
                    billId: bill.id,
                    version: -1, // -1 表示樂觀更新，尚未確認
                    opType,
                    targetId,
                    payload,
                    clientId: generateId(),
                    createdAt: now()
                };

                // 套用操作到本地狀態
                applyOperation(localOp as any);

                // 標記為 modified，觸發 useAutoSync
                set(state => ({
                    bills: state.bills.map(b =>
                        b.id === bill.id ? { ...b, syncStatus: 'modified' as SyncStatus, updatedAt: now() } : b
                    )
                }));
            },

            // Redirection logic
            addMember: (name) => get().dispatch("MEMBER_ADD", generateId(), { name }),
            removeMember: (id) => get().dispatch("MEMBER_REMOVE", id, {}),
            updateMember: (id, name) => get().dispatch("MEMBER_UPDATE", id, { name }),
            addExpense: (exp) => get().dispatch("EXPENSE_ADD", generateId(), exp),
            removeExpense: (id) => get().dispatch("EXPENSE_DELETE", id, {}),
            updateExpense: (id, updates) => get().dispatch("EXPENSE_UPDATE", id, updates),
            toggleSettlement: (fromId, toId) => get().dispatch("SETTLEMENT_TOGGLE", undefined, { fromId, toId }),

            importBill: (bill) => {
                const newId = generateId();
                set(state => ({
                    bills: [...state.bills, {
                        ...bill,
                        id: newId,
                        createdAt: now(),
                        updatedAt: now(),
                    }],
                    currentBillId: newId,
                }));
                return newId;
            },

            deleteBill: (id) => {
                set(state => ({
                    bills: state.bills.filter(b => b.id !== id),
                    currentBillId: state.currentBillId === id ? null : state.currentBillId,
                }));
            },

            updateBillName: (id, name) => {
                set(state => ({
                    bills: state.bills.map(b =>
                        b.id === id ? { ...b, name, updatedAt: now() } : b
                    ),
                }));
            },

            importBillFromSnapshot: (bill, source) => {
                const newId = generateId();
                set(state => ({
                    bills: [...state.bills, {
                        ...bill,
                        id: newId,
                        syncStatus: 'synced' as const,
                        source: source,
                    }],
                    currentBillId: newId,
                }));
                return newId;
            },

            // Sync Methods Implementation
            setBillSyncStatus: (billId, status, error) => {
                set(state => ({
                    bills: state.bills.map(b =>
                        b.id === billId
                            ? { ...b, syncStatus: status, syncError: error }
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
                    bills: state.bills.map(b => {
                        if (b.id !== billId) return b;
                        return {
                            ...b,
                            members: b.members.map(m => ({
                                ...m,
                                remoteId: mappings.members[m.id] ?? m.remoteId,
                            })),
                            expenses: b.expenses.map(e => ({
                                ...e,
                                remoteId: mappings.expenses[e.id] ?? e.remoteId,
                                items: e.items.map(i => ({
                                    ...i,
                                    remoteId: mappings.expenseItems[i.id] ?? i.remoteId,
                                })),
                            })),
                        };
                    }),
                }));
            },

            markBillAsSynced: (billId, sentUpdatedAt) => {
                set(state => ({
                    bills: state.bills.map(b => {
                        if (b.id !== billId) return b;
                        // 如果本地已經有更新（updatedAt 比發送時更新），維持 modified 狀態
                        if (sentUpdatedAt && b.updatedAt > sentUpdatedAt) {
                            return { ...b, syncStatus: 'modified' as const };
                        }
                        return {
                            ...b,
                            syncStatus: 'synced' as const,
                            lastSyncedAt: now(),
                            syncError: undefined,
                            // 清除待同步的變更追蹤清單
                            deletedMemberIds: undefined,
                            deletedExpenseIds: undefined,
                            unsettledTransfers: undefined,
                            expenses: b.expenses.map(e => ({
                                ...e,
                                deletedItemIds: undefined
                            }))
                        };
                    }),
                }));
            },

            importBillsFromRemote: (remoteBills, mode = 'merge') => {
                let mergedCount = 0;
                set(state => {
                    const updatedBills = [...state.bills];
                    const remoteIds = new Set(remoteBills.map(b => b.remoteId).filter(Boolean));

                    // replace_list 模式：刪除本地有但遠端沒有的「已同步」帳單
                    if (mode === 'replace_list') {
                        for (let i = updatedBills.length - 1; i >= 0; i--) {
                            const bill = updatedBills[i];
                            if (bill.syncStatus === 'synced' && bill.remoteId && !remoteIds.has(bill.remoteId)) {
                                updatedBills.splice(i, 1);
                            }
                        }
                    }

                    for (const remoteBill of remoteBills) {
                        const existingIndex = updatedBills.findIndex(
                            b => b.remoteId === remoteBill.remoteId || b.id === remoteBill.id
                        );
                        if (existingIndex >= 0) {
                            // 更新現有帳單
                            updatedBills[existingIndex] = {
                                ...updatedBills[existingIndex],
                                ...remoteBill,
                                id: updatedBills[existingIndex].id, // 保持本地 ID
                            };
                            mergedCount++;
                        } else {
                            // 新增帳單
                            updatedBills.push(remoteBill);
                            mergedCount++;
                        }
                    }
                    return { bills: updatedBills };
                });
                return mergedCount;
            },

            getUnsyncedBills: () => {
                return get().bills.filter(
                    b => b.syncStatus === 'local' || b.syncStatus === 'modified'
                );
            },

            // Claim Methods Implementation
            claimMember: ({ memberId, userId, displayName, avatarUrl }) => {
                get().dispatch("MEMBER_CLAIM", memberId, { userId, displayName, avatarUrl });
            },

            unclaimMember: (memberId) => {
                get().dispatch("MEMBER_UNCLAIM", memberId, {});
            },

            shouldShowClaimPrompt: (billId, userId) => {
                if (!userId) return false;
                const { bills, skippedClaimBillIds } = get();
                if (skippedClaimBillIds.has(billId)) return false;

                const bill = bills.find(b => b.id === billId);
                if (!bill) return false;

                // 檢查是否已經有 member 綁定此 userId
                const alreadyClaimed = bill.members.some(m => m.userId === userId);
                if (alreadyClaimed) return false;

                // 只有同步過的帳單才顯示認領提示
                return bill.syncStatus === 'synced';
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
            }),
        }
    )
);

export const useCurrentBill = () => {
    const { bills, currentBillId } = useSnapSplitStore();
    return bills.find(bill => bill.id === currentBillId) ?? null;
};
