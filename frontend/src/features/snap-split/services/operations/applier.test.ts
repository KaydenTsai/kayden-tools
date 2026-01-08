import { describe, expect, it } from 'vitest';
import { applyOperationToBill } from './applier';
import type { Bill, Member, Expense } from '../../types/snap-split';
import type { Operation } from '../signalr/billConnection';

const createMember = (overrides: Partial<Member> = {}): Member => ({
    id: 'member-1',
    name: 'Test Member',
    ...overrides,
});

const createExpense = (overrides: Partial<Expense> = {}): Expense => ({
    id: 'expense-1',
    name: 'Test Expense',
    amount: 100,
    serviceFeePercent: 0,
    isItemized: false,
    paidById: 'member-1',
    participants: ['member-1'],
    items: [],
    ...overrides,
});

const createBill = (overrides: Partial<Bill> = {}): Bill => ({
    id: 'bill-1',
    name: 'Test Bill',
    members: [createMember()],
    expenses: [],
    settledTransfers: [],
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
    syncStatus: 'synced',
    version: 1,
    ...overrides,
});

const createOperation = (overrides: Partial<Operation>): Operation => ({
    opType: 'EXPENSE_UPDATE',
    targetId: 'expense-1',
    payload: {},
    version: 2,
    ...overrides,
});

describe('applier - 模擬 B 收到 A 的同步訊息', () => {
    describe('EXPENSE_UPDATE - 金額同步', () => {
        it('A 修改金額 100 → 200，B 應正確收到更新', () => {
            const bill = createBill({
                expenses: [createExpense({ id: 'expense-1', amount: 100 })],
            });

            const operation = createOperation({
                opType: 'EXPENSE_UPDATE',
                targetId: 'expense-1',
                payload: { amount: 200 },
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.expenses[0].amount).toBe(200);
            expect(result.version).toBe(2);
        });

        it('A 修改費用名稱，B 應正確收到更新', () => {
            const bill = createBill({
                expenses: [createExpense({ id: 'expense-1', name: '晚餐' })],
            });

            const operation = createOperation({
                opType: 'EXPENSE_UPDATE',
                targetId: 'expense-1',
                payload: { name: '午餐' },
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.expenses[0].name).toBe('午餐');
        });

        it('A 修改付款人，B 應正確收到更新', () => {
            const bill = createBill({
                members: [
                    createMember({ id: 'alice', name: 'Alice' }),
                    createMember({ id: 'bob', name: 'Bob' }),
                ],
                expenses: [createExpense({ id: 'expense-1', paidById: 'alice' })],
            });

            const operation = createOperation({
                opType: 'EXPENSE_UPDATE',
                targetId: 'expense-1',
                payload: { paidById: 'bob' },
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.expenses[0].paidById).toBe('bob');
        });
    });

    describe('MEMBER_ADD - 成員同步', () => {
        it('A 新增成員，B 應正確收到', () => {
            const bill = createBill({ members: [] });

            const operation = createOperation({
                opType: 'MEMBER_ADD',
                targetId: 'new-member-remote-id',
                payload: { name: 'Charlie' },
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.members).toHaveLength(1);
            expect(result.members[0].id).toBe('new-member-remote-id');
            expect(result.members[0].name).toBe('Charlie');
        });
    });

    describe('EXPENSE_ADD - 費用同步', () => {
        it('A 新增費用，B 應正確收到', () => {
            const bill = createBill({
                members: [createMember({ id: 'alice' })],
                expenses: [],
            });

            const operation = createOperation({
                opType: 'EXPENSE_ADD',
                targetId: 'new-expense-remote-id',
                payload: {
                    name: '計程車',
                    amount: 350,
                    paidById: 'alice',
                },
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.expenses).toHaveLength(1);
            expect(result.expenses[0].id).toBe('new-expense-remote-id');
            expect(result.expenses[0].name).toBe('計程車');
            expect(result.expenses[0].amount).toBe(350);
        });
    });

    describe('MEMBER_REMOVE - 成員刪除同步', () => {
        it('A 刪除成員，B 應正確收到', () => {
            const bill = createBill({
                members: [
                    createMember({ id: 'alice', remoteId: 'alice-remote' }),
                    createMember({ id: 'bob', remoteId: 'bob-remote' }),
                ],
            });

            const operation = createOperation({
                opType: 'MEMBER_REMOVE',
                targetId: 'alice',
                payload: {},
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.members).toHaveLength(1);
            expect(result.members[0].id).toBe('bob');
        });

        it('刪除有 remoteId 的成員應追蹤到 deletedMemberIds', () => {
            const bill = createBill({
                members: [createMember({ id: 'alice', remoteId: 'alice-remote' })],
            });

            const operation = createOperation({
                opType: 'MEMBER_REMOVE',
                targetId: 'alice',
                payload: {},
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.deletedMemberIds).toContain('alice-remote');
        });

        it('刪除沒有 remoteId 的成員不應追蹤', () => {
            const bill = createBill({
                members: [createMember({ id: 'alice', remoteId: undefined })],
            });

            const operation = createOperation({
                opType: 'MEMBER_REMOVE',
                targetId: 'alice',
                payload: {},
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.deletedMemberIds ?? []).not.toContain('alice');
        });
    });

    describe('SETTLEMENT_MARK - 結算同步', () => {
        it('A 標記結算，B 應正確收到', () => {
            const bill = createBill({
                members: [
                    createMember({ id: 'alice' }),
                    createMember({ id: 'bob' }),
                ],
                settledTransfers: [],
            });

            const operation = createOperation({
                opType: 'SETTLEMENT_MARK',
                targetId: '',
                payload: { fromMemberId: 'alice', toMemberId: 'bob' },
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.settledTransfers).toContain('alice-bob');
        });
    });

    describe('版本號同步', () => {
        it('收到較高版本應更新本地版本', () => {
            const bill = createBill({ version: 5 });

            const operation = createOperation({
                opType: 'BILL_UPDATE_META',
                targetId: '',
                payload: { name: 'Updated' },
                version: 10,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.version).toBe(10);
        });

        it('收到較低版本應保留本地版本', () => {
            const bill = createBill({ version: 10 });

            const operation = createOperation({
                opType: 'BILL_UPDATE_META',
                targetId: '',
                payload: { name: 'Updated' },
                version: 5,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.version).toBe(10);
        });
    });

    describe('MEMBER_CLAIM - 會員認領同步', () => {
        it('A 認領成員，B 應收到 userId', () => {
            const bill = createBill({
                members: [createMember({ id: 'member-1', name: '小明' })],
            });

            const operation = createOperation({
                opType: 'MEMBER_CLAIM',
                targetId: 'member-1',
                payload: {
                    userId: 'user-alice-123',
                    avatarUrl: 'https://line.me/avatar/alice.jpg',
                },
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.members[0].userId).toBe('user-alice-123');
        });

        it('A 認領成員，B 應收到頭像 URL', () => {
            const bill = createBill({
                members: [createMember({ id: 'member-1', name: '小明' })],
            });

            const operation = createOperation({
                opType: 'MEMBER_CLAIM',
                targetId: 'member-1',
                payload: {
                    userId: 'user-alice-123',
                    avatarUrl: 'https://line.me/avatar/alice.jpg',
                },
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.members[0].avatarUrl).toBe('https://line.me/avatar/alice.jpg');
        });

        it('A 認領成員，B 應收到認領時間', () => {
            const bill = createBill({
                members: [createMember({ id: 'member-1', name: '小明' })],
            });

            const operation = createOperation({
                opType: 'MEMBER_CLAIM',
                targetId: 'member-1',
                payload: {
                    userId: 'user-alice-123',
                    avatarUrl: 'https://line.me/avatar/alice.jpg',
                },
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.members[0].claimedAt).toBeDefined();
            expect(typeof result.members[0].claimedAt).toBe('string');
        });

        it('認領時應保存原始名稱（用於取消認領還原）', () => {
            const bill = createBill({
                members: [createMember({ id: 'member-1', name: '小明' })],
            });

            const operation = createOperation({
                opType: 'MEMBER_CLAIM',
                targetId: 'member-1',
                payload: {
                    userId: 'user-alice-123',
                    avatarUrl: 'https://line.me/avatar/alice.jpg',
                },
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.members[0].originalName).toBe('小明');
        });

        it('只有目標成員應被認領，其他成員不受影響', () => {
            const bill = createBill({
                members: [
                    createMember({ id: 'member-1', name: '小明' }),
                    createMember({ id: 'member-2', name: '小華' }),
                ],
            });

            const operation = createOperation({
                opType: 'MEMBER_CLAIM',
                targetId: 'member-1',
                payload: {
                    userId: 'user-alice-123',
                    avatarUrl: 'https://line.me/avatar/alice.jpg',
                },
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.members[0].userId).toBe('user-alice-123');
            expect(result.members[1].userId).toBeUndefined();
            expect(result.members[1].avatarUrl).toBeUndefined();
            expect(result.members[1].claimedAt).toBeUndefined();
        });

        it('已被認領的成員可透過 claimedAt 判斷', () => {
            const bill = createBill({
                members: [
                    createMember({
                        id: 'member-1',
                        name: '小明',
                        userId: 'user-bob-456',
                        avatarUrl: 'https://line.me/avatar/bob.jpg',
                        claimedAt: '2024-01-01T00:00:00Z',
                    }),
                ],
            });

            // 檢查成員是否已被認領
            const isClaimed = !!bill.members[0].claimedAt;
            expect(isClaimed).toBe(true);
        });
    });

    describe('MEMBER_UNCLAIM - 會員取消認領同步', () => {
        it('A 取消認領，B 應看到 userId 被清除', () => {
            const bill = createBill({
                members: [createMember({
                    id: 'member-1',
                    name: '小明',
                    originalName: '小明',
                    userId: 'user-alice-123',
                    avatarUrl: 'https://line.me/avatar/alice.jpg',
                    claimedAt: '2024-01-01T00:00:00Z',
                })],
            });

            const operation = createOperation({
                opType: 'MEMBER_UNCLAIM',
                targetId: 'member-1',
                payload: {},
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.members[0].userId).toBeUndefined();
        });

        it('A 取消認領，B 應看到頭像被清除', () => {
            const bill = createBill({
                members: [createMember({
                    id: 'member-1',
                    name: '小明',
                    originalName: '小明',
                    userId: 'user-alice-123',
                    avatarUrl: 'https://line.me/avatar/alice.jpg',
                    claimedAt: '2024-01-01T00:00:00Z',
                })],
            });

            const operation = createOperation({
                opType: 'MEMBER_UNCLAIM',
                targetId: 'member-1',
                payload: {},
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.members[0].avatarUrl).toBeUndefined();
        });

        it('A 取消認領，B 應看到認領時間被清除', () => {
            const bill = createBill({
                members: [createMember({
                    id: 'member-1',
                    name: '小明',
                    originalName: '小明',
                    userId: 'user-alice-123',
                    avatarUrl: 'https://line.me/avatar/alice.jpg',
                    claimedAt: '2024-01-01T00:00:00Z',
                })],
            });

            const operation = createOperation({
                opType: 'MEMBER_UNCLAIM',
                targetId: 'member-1',
                payload: {},
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.members[0].claimedAt).toBeUndefined();
        });

        it('取消認領應還原原始名稱', () => {
            const bill = createBill({
                members: [createMember({
                    id: 'member-1',
                    name: 'Alice（已認領）',
                    originalName: '小明',
                    userId: 'user-alice-123',
                    avatarUrl: 'https://line.me/avatar/alice.jpg',
                    claimedAt: '2024-01-01T00:00:00Z',
                })],
            });

            const operation = createOperation({
                opType: 'MEMBER_UNCLAIM',
                targetId: 'member-1',
                payload: {},
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.members[0].name).toBe('小明');
            expect(result.members[0].originalName).toBeUndefined();
        });

        it('沒有 originalName 時取消認領應保留現有名稱', () => {
            const bill = createBill({
                members: [createMember({
                    id: 'member-1',
                    name: 'Alice',
                    originalName: undefined,
                    userId: 'user-alice-123',
                    claimedAt: '2024-01-01T00:00:00Z',
                })],
            });

            const operation = createOperation({
                opType: 'MEMBER_UNCLAIM',
                targetId: 'member-1',
                payload: {},
                version: 2,
            });

            const result = applyOperationToBill(bill, operation);

            expect(result.members[0].name).toBe('Alice');
        });
    });

    describe('認領完整流程', () => {
        it('完整流程：未認領 → A 認領 → B 看到認領狀態 → A 取消 → B 看到取消', () => {
            // 初始狀態：未認領
            let bill = createBill({
                members: [createMember({ id: 'member-1', name: '小明' })],
            });

            expect(bill.members[0].userId).toBeUndefined();
            expect(bill.members[0].claimedAt).toBeUndefined();

            // A 認領
            const claimOp = createOperation({
                opType: 'MEMBER_CLAIM',
                targetId: 'member-1',
                payload: {
                    userId: 'user-alice-123',
                    avatarUrl: 'https://line.me/avatar/alice.jpg',
                },
                version: 2,
            });

            bill = applyOperationToBill(bill, claimOp);

            // B 看到認領狀態
            expect(bill.members[0].userId).toBe('user-alice-123');
            expect(bill.members[0].avatarUrl).toBe('https://line.me/avatar/alice.jpg');
            expect(bill.members[0].claimedAt).toBeDefined();
            expect(bill.members[0].originalName).toBe('小明');

            // A 取消認領
            const unclaimOp = createOperation({
                opType: 'MEMBER_UNCLAIM',
                targetId: 'member-1',
                payload: {},
                version: 3,
            });

            bill = applyOperationToBill(bill, unclaimOp);

            // B 看到取消狀態
            expect(bill.members[0].userId).toBeUndefined();
            expect(bill.members[0].avatarUrl).toBeUndefined();
            expect(bill.members[0].claimedAt).toBeUndefined();
            expect(bill.members[0].name).toBe('小明');
        });
    });
});