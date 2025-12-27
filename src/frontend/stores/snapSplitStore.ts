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

export interface SnapSplitState {
    bills: Bill[];
    currentBillId: string | null;

    // Bill actions
    createBill: (name: string) => void;
    deleteBill: (id: string) => void;
    selectBill: (id: string) => void;
    updateBillName: (name: string) => void;
    importBill: (bill: Bill) => string;

    // Member actions
    addMember: (name: string) => void;
    removeMember: (id: string) => void;
    updateMember: (id: string, name: string) => void;

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
        }),
        { name: 'kayden-tools-snap-split' }
    )
);

export const useCurrentBill = () => {
    const { bills, currentBillId } = useSnapSplitStore();
    return bills.find(bill => bill.id === currentBillId) ?? null;
};
