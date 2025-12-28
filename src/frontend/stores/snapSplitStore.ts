import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { Bill, Expense, ExpenseItem, SyncStatus } from "@/types/snap-split";

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
});

const updateCurrentBill = (
    state: SnapSplitState,
    updater: (bill: Bill) => Partial<Bill>
): { bills: Bill[] } => ({
    bills: state.bills.map(bill => {
        if (bill.id !== state.currentBillId) return bill;
        const updates = updater(bill);
        const newSyncStatus = bill.syncStatus === 'synced' ? 'modified' : bill.syncStatus;

        return { ...bill, ...updates, updatedAt: now(), syncStatus: newSyncStatus };
    }),
});

export interface ClaimMemberParams {
    memberId: string;
    userId: string;
    displayName: string;
    avatarUrl?: string;
}

export interface SnapSplitState {
    bills: Bill[];
    currentBillId: string | null;
    /** Session-only: 本次瀏覽已跳過認領的帳單 ID（不持久化） */
    skippedClaimBillIds: string[];

    // Bill actions
    createBill: (name: string) => void;
    deleteBill: (id: string) => void;
    selectBill: (id: string) => void;
    updateBillName: (name: string) => void;
    importBill: (bill: Bill) => string;
    /** 從快照匯入帳單（產生新 ID，標記為快照） */
    importBillFromSnapshot: (bill: Bill, source?: string) => string;
    /** 設定帳單擁有者 */
    setBillOwner: (billId: string, userId: string) => void;

    // Member actions
    addMember: (name: string) => void;
    removeMember: (id: string) => void;
    updateMember: (id: string, name: string) => void;
    /** 認領成員（綁定 userId） */
    claimMember: (params: ClaimMemberParams) => void;
    /** 取消認領成員 */
    unclaimMember: (memberId: string) => void;
    /** 檢查成員是否已被認領 */
    isMemberClaimed: (memberId: string) => boolean;
    /** 取得當前使用者在此帳單中認領的成員 */
    getMyClaimedMember: (userId: string) => string | null;

    // Claim prompt actions
    /** 標記帳單為已跳過認領（本次 session） */
    skipClaimForBill: (billId: string) => void;
    /** 檢查是否應該顯示認領提示 */
    shouldShowClaimPrompt: (billId: string, userId: string | undefined) => boolean;

    // Expense actions
    addExpense: (expense: Omit<Expense, 'id'>) => void;
    removeExpense: (expenseId: string) => void;
    updateExpense: (expenseId: string, updates: Partial<Expense>) => void;

    // Expense Item actions
    addExpenseItem: (expenseId: string, item: Omit<ExpenseItem, 'id'>) => void;
    updateExpenseItem: (expenseId: string, itemId: string, updates: Partial<ExpenseItem>) => void;
    removeExpenseItem: (expenseId: string, itemId: string) => void;

    // Settlement actions
    toggleSettlement: (fromId: string, toId: string) => void;
    clearAllSettlements: () => void;

    // Sync actions
    setBillSyncStatus: (billId: string, status: SyncStatus, error?: string) => void;
    setBillRemoteId: (billId: string, remoteId: string, shareCode?: string) => void;
    applyIdMappings: (billId: string, mappings: IdMappings) => void;
    markBillAsSynced: (billId: string) => void;
    getUnsyncedBills: () => Bill[];
    /** 從遠端批次匯入帳單（跳過已存在的，不改變 currentBillId） */
    importBillsFromRemote: (bills: Bill[]) => number;
}

export interface IdMappings {
    members: Record<string, string>;
    expenses: Record<string, string>;
    expenseItems: Record<string, string>;
}

export const useSnapSplitStore = create<SnapSplitState>()(
    persist(
        (set, get) => ({
            bills: [],
            currentBillId: null,
            skippedClaimBillIds: [],

            createBill: (name) => {
                const newBill = createDefaultBill(name);
                set(state => ({
                    bills: [...state.bills, newBill],
                    currentBillId: newBill.id,
                }));
            },

            deleteBill: (id) => set(state => ({
                bills: state.bills.filter(bill => bill.id !== id),
                currentBillId: state.currentBillId === id ? null : state.currentBillId,
            })),

            selectBill: (id) => set({ currentBillId: id }),

            updateBillName: (name) => set(state =>
                updateCurrentBill(state, () => ({ name }))
            ),

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

            importBillFromSnapshot: (bill, source) => {
                const newId = generateId();
                set(state => ({
                    bills: [...state.bills, {
                        ...bill,
                        id: newId,
                        createdAt: now(),
                        updatedAt: now(),
                        syncStatus: 'local',
                        isSnapshot: true,
                        snapshotSource: source,
                        // 清除遠端相關欄位
                        remoteId: undefined,
                        shareCode: undefined,
                        lastSyncedAt: undefined,
                        ownerId: undefined,
                        // 清除成員的認領狀態
                        members: bill.members.map(m => ({
                            ...m,
                            userId: undefined,
                            avatarUrl: undefined,
                            originalName: undefined,
                            claimedAt: undefined,
                            remoteId: undefined,
                        })),
                        // 清除費用的遠端 ID
                        expenses: bill.expenses.map(e => ({
                            ...e,
                            remoteId: undefined,
                            items: e.items.map(item => ({
                                ...item,
                                remoteId: undefined,
                            })),
                        })),
                    }],
                    currentBillId: newId,
                }));
                return newId;
            },

            setBillOwner: (billId, userId) => set(state => ({
                bills: state.bills.map(bill =>
                    bill.id === billId
                        ? { ...bill, ownerId: userId, updatedAt: now() }
                        : bill
                ),
            })),

            addMember: (name) => set(state =>
                updateCurrentBill(state, (bill) => ({
                    members: [...bill.members, { id: generateId(), name }],
                }))
            ),

            removeMember: (id) => set(state =>
                updateCurrentBill(state, (bill) => ({
                    members: bill.members.filter(m => m.id !== id),
                    expenses: bill.expenses.map(exp => ({
                        ...exp,
                        participants: exp.participants.filter(p => p !== id),
                        paidBy: exp.paidBy === id ? '' : exp.paidBy,
                        items: exp.items.map(item => ({
                            ...item,
                            participants: item.participants.filter(p => p !== id),
                            paidBy: item.paidBy === id ? '' : item.paidBy,
                        })),
                    })),
                }))
            ),

            updateMember: (id, name) => set(state =>
                updateCurrentBill(state, (bill) => ({
                    members: bill.members.map(m => m.id === id ? { ...m, name } : m),
                }))
            ),

            claimMember: ({ memberId, userId, displayName, avatarUrl }) => set(state =>
                updateCurrentBill(state, (bill) => ({
                    members: bill.members.map(m =>
                        m.id === memberId
                            ? {
                                ...m,
                                userId,
                                avatarUrl,
                                originalName: m.originalName ?? m.name,
                                name: displayName,
                                claimedAt: now(),
                            }
                            : m
                    ),
                }))
            ),

            unclaimMember: (memberId) => set(state =>
                updateCurrentBill(state, (bill) => ({
                    members: bill.members.map(m =>
                        m.id === memberId
                            ? {
                                ...m,
                                name: m.originalName ?? m.name,
                                userId: undefined,
                                avatarUrl: undefined,
                                originalName: undefined,
                                claimedAt: undefined,
                            }
                            : m
                    ),
                }))
            ),

            isMemberClaimed: (memberId) => {
                const state = get();
                const bill = state.bills.find(b => b.id === state.currentBillId);
                if (!bill) return false;
                const member = bill.members.find(m => m.id === memberId);
                return !!member?.userId;
            },

            getMyClaimedMember: (userId) => {
                const state = get();
                const bill = state.bills.find(b => b.id === state.currentBillId);
                if (!bill) return null;
                const member = bill.members.find(m => m.userId === userId);
                return member?.id ?? null;
            },

            skipClaimForBill: (billId) => set(state => ({
                skippedClaimBillIds: [...state.skippedClaimBillIds, billId],
            })),

            shouldShowClaimPrompt: (billId, userId) => {
                const state = get();
                // 必須已登入
                if (!userId) return false;

                const bill = state.bills.find(b => b.id === billId);
                if (!bill) return false;

                // 快照帳單不需要認領
                if (bill.isSnapshot) return false;

                // 本次 session 已跳過
                if (state.skippedClaimBillIds.includes(billId)) return false;

                // 使用者已在此帳單認領成員
                const alreadyClaimed = bill.members.some(m => m.userId === userId);
                if (alreadyClaimed) return false;

                // 還有未認領的成員
                const hasUnclaimedMembers = bill.members.some(m => !m.userId);
                if (!hasUnclaimedMembers) return false;

                return true;
            },

            addExpense: (expense) => set(state =>
                updateCurrentBill(state, (bill) => ({
                    expenses: [...bill.expenses, { ...expense, id: generateId() }],
                }))
            ),

            removeExpense: (expenseId) => set(state =>
                updateCurrentBill(state, (bill) => ({
                    expenses: bill.expenses.filter(e => e.id !== expenseId),
                }))
            ),

            updateExpense: (expenseId, updates) => set(state =>
                updateCurrentBill(state, (bill) => ({
                    expenses: bill.expenses.map(e =>
                        e.id === expenseId ? { ...e, ...updates } : e
                    ),
                }))
            ),

            addExpenseItem: (expenseId, item) => set(state =>
                updateCurrentBill(state, (bill) => ({
                    expenses: bill.expenses.map(e =>
                        e.id === expenseId
                            ? { ...e, items: [...e.items, { ...item, id: generateId() }] }
                            : e
                    ),
                }))
            ),

            updateExpenseItem: (expenseId, itemId, updates) => set(state =>
                updateCurrentBill(state, (bill) => ({
                    expenses: bill.expenses.map(e =>
                        e.id === expenseId
                            ? {
                                ...e,
                                items: e.items.map(item =>
                                    item.id === itemId ? { ...item, ...updates } : item
                                ),
                            }
                            : e
                    ),
                }))
            ),

            removeExpenseItem: (expenseId, itemId) => set(state =>
                updateCurrentBill(state, (bill) => ({
                    expenses: bill.expenses.map(e =>
                        e.id === expenseId
                            ? { ...e, items: e.items.filter(item => item.id !== itemId) }
                            : e
                    ),
                }))
            ),

            toggleSettlement: (fromId, toId) => set(state =>
                updateCurrentBill(state, (bill) => {
                    const key = `${fromId}-${toId}`;
                    const isSettled = bill.settledTransfers.includes(key);
                    return {
                        settledTransfers: isSettled
                            ? bill.settledTransfers.filter(t => t !== key)
                            : [...bill.settledTransfers, key],
                    };
                })
            ),

            clearAllSettlements: () => set(state =>
                updateCurrentBill(state, () => ({ settledTransfers: [] }))
            ),

            setBillSyncStatus: (billId, status, error) => set(state => ({
                bills: state.bills.map(bill =>
                    bill.id === billId
                        ? { ...bill, syncStatus: status, syncError: error }
                        : bill
                ),
            })),

            setBillRemoteId: (billId, remoteId, shareCode) => set(state => ({
                bills: state.bills.map(bill =>
                    bill.id === billId
                        ? { ...bill, remoteId, shareCode, lastSyncedAt: now() }
                        : bill
                ),
            })),

            applyIdMappings: (billId, mappings) => set(state => ({
                bills: state.bills.map(bill => {
                    if (bill.id !== billId) return bill;

                    return {
                        ...bill,
                        members: bill.members.map(m => ({
                            ...m,
                            remoteId: mappings.members[m.id] ?? m.remoteId,
                        })),
                        expenses: bill.expenses.map(e => ({
                            ...e,
                            remoteId: mappings.expenses[e.id] ?? e.remoteId,
                            items: e.items.map(item => ({
                                ...item,
                                remoteId: mappings.expenseItems[item.id] ?? item.remoteId,
                            })),
                        })),
                    };
                }),
            })),

            markBillAsSynced: (billId) => set(state => ({
                bills: state.bills.map(bill =>
                    bill.id === billId
                        ? { ...bill, syncStatus: 'synced' as SyncStatus, lastSyncedAt: now(), syncError: undefined }
                        : bill
                ),
            })),

            getUnsyncedBills: () => {
                return get().bills.filter(
                    bill => bill.syncStatus === 'local' || bill.syncStatus === 'modified'
                );
            },

            importBillsFromRemote: (remoteBills) => {
                let addedCount = 0;
                let updatedCount = 0;

                set(state => {
                    const currentBills = [...state.bills];
                    const newBills: Bill[] = [];
                    let hasChanges = false;

                    for (const remoteBill of remoteBills) {
                        const index = currentBills.findIndex(local => local.remoteId === remoteBill.remoteId);

                        if (index === -1 && remoteBill.remoteId) {
                            // 來自遠端的新帳單
                            newBills.push({
                                ...remoteBill,
                                id: generateId(),
                                createdAt: remoteBill.createdAt || now(),
                                updatedAt: remoteBill.updatedAt || now(),
                            });
                            addedCount++;
                            hasChanges = true;
                        } else if (index !== -1) {
                            const localBill = currentBills[index];
                            // 如果本地沒有修改，或者我們想要強制刷新認領狀態，則更新現有帳單
                            // 目前如果已經是 'synced' 狀態，我們可以安全地用遠端狀態刷新它
                            if (localBill.syncStatus === 'synced') {
                                currentBills[index] = {
                                    ...remoteBill,
                                    id: localBill.id, // 保留我們內部的本地 ID
                                    syncStatus: 'synced',
                                };
                                updatedCount++;
                                hasChanges = true;
                            }
                        }
                    }

                    if (!hasChanges) return state;

                    return {
                        bills: [...currentBills, ...newBills],
                    };
                });

                return addedCount + updatedCount;
            },
        }),
        {
            name: 'kayden-tools-snap-split',
            // 排除 session-only 狀態
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
