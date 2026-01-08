import { describe, expect, it } from 'vitest';
import { buildDeltaSyncRequest, isDeltaEmpty, createBillSnapshot } from './deltaFactory';
import { MissingRemoteIdError } from '../types/sync';
import type { Bill, Member, Expense, ExpenseItem } from '../types/snap-split';
import type { BillSnapshot } from '../types/sync';

const createMember = (overrides: Partial<Member> = {}): Member => ({
    id: 'member-local-1',
    name: 'Test Member',
    ...overrides,
});

const createExpenseItem = (overrides: Partial<ExpenseItem> = {}): ExpenseItem => ({
    id: 'item-local-1',
    name: 'Test Item',
    amount: 50,
    paidById: 'member-local-1',
    participants: ['member-local-1'],
    ...overrides,
});

const createExpense = (overrides: Partial<Expense> = {}): Expense => ({
    id: 'expense-local-1',
    name: 'Test Expense',
    amount: 100,
    serviceFeePercent: 0,
    isItemized: false,
    paidById: 'member-local-1',
    participants: ['member-local-1'],
    items: [],
    ...overrides,
});

const createBill = (overrides: Partial<Bill> = {}): Bill => ({
    id: 'bill-local-1',
    name: 'Test Bill',
    members: [createMember()],
    expenses: [],
    settledTransfers: [],
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
    syncStatus: 'local',
    version: 1,
    ...overrides,
});

describe('deltaFactory', () => {
    describe('buildDeltaSyncRequest', () => {
        describe('Member 變更', () => {
            it('新成員應加入 add 陣列（使用 localId）', () => {
                const bill = createBill({
                    members: [
                        createMember({ id: 'new-member', remoteId: undefined }),
                    ],
                });

                const result = buildDeltaSyncRequest(bill, undefined);

                expect(result.members?.add).toHaveLength(1);
                expect(result.members?.add?.[0].localId).toBe('new-member');
            });

            it('已同步成員更新應加入 update 陣列（使用 remoteId）', () => {
                const member = createMember({
                    id: 'member-1',
                    remoteId: 'member-remote-1',
                    name: 'Updated Name',
                });
                const bill = createBill({ members: [member] });

                const snapshot: BillSnapshot = {
                    name: 'Test Bill',
                    members: [createMember({ id: 'member-1', remoteId: 'member-remote-1', name: 'Old Name' })],
                    expenses: [],
                    settledTransfers: [],
                    version: 1,
                };

                const result = buildDeltaSyncRequest(bill, snapshot);

                expect(result.members?.update).toHaveLength(1);
                expect(result.members?.update?.[0].remoteId).toBe('member-remote-1');
            });

            it('刪除的成員應使用 deletedMemberIds', () => {
                const bill = createBill({
                    members: [],
                    deletedMemberIds: ['member-remote-deleted'],
                });

                const result = buildDeltaSyncRequest(bill, undefined);

                expect(result.members?.delete).toContain('member-remote-deleted');
            });
        });

        describe('Expense 變更', () => {
            it('新費用應加入 add 陣列（參照成員可使用 localId fallback）', () => {
                const bill = createBill({
                    members: [createMember({ id: 'payer', remoteId: undefined })],
                    expenses: [createExpense({
                        id: 'new-expense',
                        remoteId: undefined,
                        paidById: 'payer',
                        participants: ['payer'],
                    })],
                });

                const result = buildDeltaSyncRequest(bill, undefined);

                expect(result.expenses?.add).toHaveLength(1);
                expect(result.expenses?.add?.[0].localId).toBe('new-expense');
                expect(result.expenses?.add?.[0].paidByMemberId).toBe('payer');
            });

            it('新費用參照已同步成員應使用 remoteId', () => {
                const bill = createBill({
                    members: [createMember({ id: 'payer', remoteId: 'payer-remote' })],
                    expenses: [createExpense({
                        id: 'new-expense',
                        remoteId: undefined,
                        paidById: 'payer',
                        participants: ['payer'],
                    })],
                });

                const result = buildDeltaSyncRequest(bill, undefined);

                expect(result.expenses?.add?.[0].paidByMemberId).toBe('payer-remote');
                expect(result.expenses?.add?.[0].participantIds).toContain('payer-remote');
            });

            it('費用更新應使用嚴格解析（成員須有 remoteId）', () => {
                const member = createMember({ id: 'payer', remoteId: 'payer-remote' });
                const expense = createExpense({
                    id: 'expense-1',
                    remoteId: 'expense-remote-1',
                    paidById: 'payer',
                    participants: ['payer'],
                    amount: 200,
                });
                const bill = createBill({
                    members: [member],
                    expenses: [expense],
                });

                const snapshot: BillSnapshot = {
                    name: 'Test Bill',
                    members: [member],
                    expenses: [{ ...expense, amount: 100 }],
                    settledTransfers: [],
                    version: 1,
                };

                const result = buildDeltaSyncRequest(bill, snapshot);

                expect(result.expenses?.update).toHaveLength(1);
                expect(result.expenses?.update?.[0].remoteId).toBe('expense-remote-1');
                expect(result.expenses?.update?.[0].paidByMemberId).toBe('payer-remote');
            });

            it('費用更新時成員無 remoteId 且在快照中存在應拋出錯誤', () => {
                const memberWithoutRemoteId = createMember({ id: 'payer', remoteId: undefined });
                const expense = createExpense({
                    id: 'expense-1',
                    remoteId: 'expense-remote-1',
                    paidById: 'payer',
                    amount: 200,
                });
                const bill = createBill({
                    members: [memberWithoutRemoteId],
                    expenses: [expense],
                });

                const snapshot: BillSnapshot = {
                    name: 'Test Bill',
                    members: [createMember({ id: 'payer', remoteId: 'payer-remote' })],
                    expenses: [{ ...expense, amount: 100 }],
                    settledTransfers: [],
                    version: 1,
                };

                expect(() => buildDeltaSyncRequest(bill, snapshot))
                    .toThrow(MissingRemoteIdError);
            });

            it('費用更新參照新成員（不在快照中）應允許使用 localId', () => {
                const newMember = createMember({ id: 'new-payer', remoteId: undefined });
                const existingMember = createMember({ id: 'old-payer', remoteId: 'old-payer-remote' });
                const expense = createExpense({
                    id: 'expense-1',
                    remoteId: 'expense-remote-1',
                    paidById: 'new-payer',
                    participants: ['new-payer'],
                    amount: 200,
                });
                const bill = createBill({
                    members: [existingMember, newMember],
                    expenses: [expense],
                });

                const snapshot: BillSnapshot = {
                    name: 'Test Bill',
                    members: [existingMember],
                    expenses: [{ ...expense, amount: 100, paidById: 'old-payer', participants: ['old-payer'] }],
                    settledTransfers: [],
                    version: 1,
                };

                const result = buildDeltaSyncRequest(bill, snapshot);

                expect(result.expenses?.update?.[0].paidByMemberId).toBe('new-payer');
            });
        });

        describe('ExpenseItem 變更', () => {
            it('新項目應加入 add 陣列', () => {
                const bill = createBill({
                    members: [createMember({ id: 'payer', remoteId: 'payer-remote' })],
                    expenses: [createExpense({
                        id: 'expense-1',
                        remoteId: 'expense-remote-1',
                        isItemized: true,
                        items: [createExpenseItem({
                            id: 'new-item',
                            remoteId: undefined,
                            paidById: 'payer',
                        })],
                    })],
                });

                const result = buildDeltaSyncRequest(bill, undefined);

                expect(result.expenseItems?.add).toHaveLength(1);
                expect(result.expenseItems?.add?.[0].localId).toBe('new-item');
                expect(result.expenseItems?.add?.[0].expenseId).toBe('expense-remote-1');
            });

            it('新項目的 expenseId 應使用費用的 remoteId 或 localId', () => {
                const bill = createBill({
                    members: [createMember({ id: 'payer', remoteId: 'payer-remote' })],
                    expenses: [createExpense({
                        id: 'new-expense',
                        remoteId: undefined,
                        isItemized: true,
                        items: [createExpenseItem({
                            id: 'new-item',
                            remoteId: undefined,
                            paidById: 'payer',
                        })],
                    })],
                });

                const result = buildDeltaSyncRequest(bill, undefined);

                expect(result.expenseItems?.add?.[0].expenseId).toBe('new-expense');
            });
        });

        describe('Settlement 變更', () => {
            it('新結算應使用嚴格解析', () => {
                const bill = createBill({
                    members: [
                        createMember({ id: 'from', remoteId: 'from-remote' }),
                        createMember({ id: 'to', remoteId: 'to-remote' }),
                    ],
                    settledTransfers: ['from::to'],
                });

                const snapshot: BillSnapshot = {
                    name: 'Test Bill',
                    members: bill.members,
                    expenses: [],
                    settledTransfers: [],
                    version: 1,
                };

                const result = buildDeltaSyncRequest(bill, snapshot);

                expect(result.settlements?.mark).toHaveLength(1);
                expect(result.settlements?.mark?.[0].fromMemberId).toBe('from-remote');
                expect(result.settlements?.mark?.[0].toMemberId).toBe('to-remote');
            });

            it('結算的成員無 remoteId 且在快照中存在應拋出錯誤', () => {
                const bill = createBill({
                    members: [
                        createMember({ id: 'from', remoteId: undefined }),
                        createMember({ id: 'to', remoteId: 'to-remote' }),
                    ],
                    settledTransfers: ['from::to'],
                });

                const snapshot: BillSnapshot = {
                    name: 'Test Bill',
                    members: [
                        createMember({ id: 'from', remoteId: 'from-remote' }),
                        createMember({ id: 'to', remoteId: 'to-remote' }),
                    ],
                    expenses: [],
                    settledTransfers: [],
                    version: 1,
                };

                expect(() => buildDeltaSyncRequest(bill, snapshot))
                    .toThrow(MissingRemoteIdError);
            });
        });

        describe('BillMeta 變更', () => {
            it('名稱變更應加入 billMeta', () => {
                const bill = createBill({ name: 'New Name' });

                const snapshot: BillSnapshot = {
                    name: 'Old Name',
                    members: [],
                    expenses: [],
                    settledTransfers: [],
                    version: 1,
                };

                const result = buildDeltaSyncRequest(bill, snapshot);

                expect(result.billMeta?.name).toBe('New Name');
            });

            it('名稱未變更不應包含 billMeta', () => {
                const bill = createBill({ name: 'Same Name' });

                const snapshot: BillSnapshot = {
                    name: 'Same Name',
                    members: [],
                    expenses: [],
                    settledTransfers: [],
                    version: 1,
                };

                const result = buildDeltaSyncRequest(bill, snapshot);

                expect(result.billMeta).toBeUndefined();
            });
        });
    });

    describe('isDeltaEmpty', () => {
        it('空請求應返回 true', () => {
            expect(isDeltaEmpty({ baseVersion: 1 })).toBe(true);
        });

        it('有 members 變更應返回 false', () => {
            expect(isDeltaEmpty({
                baseVersion: 1,
                members: { add: [{ localId: '1', name: 'Test', displayOrder: 0 }] },
            })).toBe(false);
        });

        it('有 expenses 變更應返回 false', () => {
            expect(isDeltaEmpty({
                baseVersion: 1,
                expenses: { delete: ['remote-1'] },
            })).toBe(false);
        });

        it('有 billMeta 變更應返回 false', () => {
            expect(isDeltaEmpty({
                baseVersion: 1,
                billMeta: { name: 'New Name' },
            })).toBe(false);
        });
    });

    describe('createBillSnapshot', () => {
        it('應建立帳單快照', () => {
            const member = createMember({ id: 'm1', remoteId: 'rm1' });
            const expense = createExpense({
                id: 'e1',
                remoteId: 're1',
                items: [createExpenseItem({ id: 'i1', remoteId: 'ri1' })],
            });
            const bill = createBill({
                name: 'Test',
                members: [member],
                expenses: [expense],
                settledTransfers: ['a::b'],
                version: 5,
            });

            const snapshot = createBillSnapshot(bill);

            expect(snapshot.name).toBe('Test');
            expect(snapshot.members).toHaveLength(1);
            expect(snapshot.expenses).toHaveLength(1);
            expect(snapshot.settledTransfers).toContain('a::b');
            expect(snapshot.version).toBe(5);
        });

        it('快照應為深拷貝（修改原帳單不影響快照）', () => {
            const bill = createBill({
                members: [createMember({ name: 'Original' })],
            });

            const snapshot = createBillSnapshot(bill);
            bill.members[0].name = 'Modified';

            expect(snapshot.members[0].name).toBe('Original');
        });
    });
});