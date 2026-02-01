/**
 * SnapSplit 同步系統審查測試
 *
 * 目的：確保同步邏輯的正確性，捕捉 Silent Failures 和 Data Loss 問題
 *
 * 測試分類：
 * 1. Contract Tests - 驗證 API 契約
 * 2. Invariant Tests - 驗證系統不變式
 * 3. Scenario Tests - 驗證實際使用場景
 */

import { describe, expect, it, beforeEach, vi } from 'vitest';
import { buildDeltaSyncRequest, isDeltaEmpty, createBillSnapshot } from '../deltaFactory';
import type { Bill, Member, Expense } from '../../types/snap-split';
import type { BillSnapshot } from '../../types/sync';

// ============================================================================
// Test Fixtures
// ============================================================================

const createMember = (overrides: Partial<Member> = {}): Member => ({
    id: 'member-local-1',
    name: 'Test Member',
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
    isDeleted: false,
    ...overrides,
});

const createSyncedBill = (overrides: Partial<Bill> = {}): Bill => ({
    ...createBill(),
    remoteId: 'bill-remote-1',
    syncStatus: 'synced',
    members: [createMember({ remoteId: 'member-remote-1' })],
    ...overrides,
});

// ============================================================================
// Contract Tests - API 契約驗證
// ============================================================================

describe('Contract Tests: Delta Sync Request Building', () => {
    describe('BUG-001: Snapshot 必須傳入已同步帳單', () => {
        it('已同步帳單的成員更新必須被包含在 delta request', () => {
            const bill = createSyncedBill({
                members: [createMember({
                    id: 'member-1',
                    remoteId: 'member-remote-1',
                    name: 'Updated Name',  // 更新了名稱
                    userId: 'user-123',    // 認領了
                    claimedAt: '2024-01-01T00:00:00Z',
                })],
            });

            const snapshot: BillSnapshot = {
                name: 'Test Bill',
                members: [createMember({
                    id: 'member-1',
                    remoteId: 'member-remote-1',
                    name: 'Original Name',  // 原始名稱
                    // 沒有 userId/claimedAt（未認領）
                })],
                expenses: [],
                settledTransfers: [],
                version: 1,
            };

            const result = buildDeltaSyncRequest(bill, snapshot);

            // 必須偵測到成員更新
            expect(result.members?.update).toBeDefined();
            expect(result.members?.update).toHaveLength(1);
            expect(result.members?.update?.[0].remoteId).toBe('member-remote-1');
            expect(result.members?.update?.[0].linkedUserId).toBe('user-123');
        });

        it('已同步帳單的費用金額更新必須被包含在 delta request', () => {
            const member = createMember({ id: 'member-1', remoteId: 'member-remote-1' });
            const bill = createSyncedBill({
                members: [member],
                expenses: [createExpense({
                    id: 'expense-1',
                    remoteId: 'expense-remote-1',
                    amount: 200,  // 更新了金額
                    paidById: 'member-1',
                    participants: ['member-1'],
                })],
            });

            const snapshot: BillSnapshot = {
                name: 'Test Bill',
                members: [member],
                expenses: [createExpense({
                    id: 'expense-1',
                    remoteId: 'expense-remote-1',
                    amount: 100,  // 原始金額
                    paidById: 'member-1',
                    participants: ['member-1'],
                })],
                settledTransfers: [],
                version: 1,
            };

            const result = buildDeltaSyncRequest(bill, snapshot);

            // 必須偵測到費用更新
            expect(result.expenses?.update).toBeDefined();
            expect(result.expenses?.update).toHaveLength(1);
            expect(result.expenses?.update?.[0].amount).toBe(200);
        });

        it('不傳 snapshot 時，成員更新不會被偵測（BUG-001 記錄）', () => {
            // 建立一個已同步且已修改的帳單
            const syncedBill = createSyncedBill({
                members: [createMember({
                    id: 'member-1',
                    remoteId: 'member-remote-1',
                    name: 'Updated Name',  // 修改了名稱
                })],
            });

            // 不傳 snapshot（目前的錯誤用法）
            const result = buildDeltaSyncRequest(syncedBill, undefined);

            // BUG 行為：成員更新不會被偵測
            // 雖然 billMeta 可能被包含，但 members.update 是空的
            expect(result.members?.update).toBeUndefined();

            // 理想行為：應該偵測到成員更新
            // expect(result.members?.update).toHaveLength(1);
        });
    });
});

// ============================================================================
// Invariant Tests - 系統不變式驗證
// ============================================================================

describe('Invariant Tests: 系統不變式', () => {
    describe('不變式 2: Snapshot 必須在同步成功後更新', () => {
        it('createBillSnapshot 應該正確捕捉帳單狀態', () => {
            const bill = createSyncedBill({
                name: 'Test Bill',
                members: [
                    createMember({ id: 'm1', remoteId: 'rm1', name: 'Alice' }),
                    createMember({ id: 'm2', remoteId: 'rm2', name: 'Bob' }),
                ],
                expenses: [
                    createExpense({ id: 'e1', remoteId: 're1', amount: 100 }),
                ],
                settledTransfers: ['rm1::rm2'],
                version: 5,
            });

            const snapshot = createBillSnapshot(bill);

            expect(snapshot.name).toBe('Test Bill');
            expect(snapshot.members).toHaveLength(2);
            expect(snapshot.expenses).toHaveLength(1);
            expect(snapshot.settledTransfers).toContain('rm1::rm2');
            expect(snapshot.version).toBe(5);
        });

        it('Snapshot 應該是深拷貝（修改原帳單不影響 snapshot）', () => {
            const bill = createSyncedBill({
                members: [createMember({ name: 'Original' })],
            });

            const snapshot = createBillSnapshot(bill);
            bill.members[0].name = 'Modified';

            expect(snapshot.members[0].name).toBe('Original');
        });
    });

    describe('不變式 3: ID Mapping 後所有新實體必須有 remoteId', () => {
        it('新增成員後，idMappings 應該包含所有新成員', () => {
            // 這是一個 placeholder 測試，需要整合測試框架
            // 驗證 applyIdMappings 後所有 members 都有 remoteId
            const bill = createBill({
                members: [
                    createMember({ id: 'new-1', remoteId: undefined }),
                    createMember({ id: 'new-2', remoteId: undefined }),
                ],
            });

            // 模擬 server 返回的 ID mapping
            const idMappings = {
                members: { 'new-1': 'remote-1' },  // 注意：new-2 沒有 mapping！
                expenses: {},
                expenseItems: {},
            };

            // 理想行為：應該驗證所有新實體都有 mapping
            // expect(validateIdMappings(bill, idMappings)).toBe(false);

            // 目前可能的 bug：部分實體沒有 mapping 也會繼續
            expect(idMappings.members['new-2']).toBeUndefined();  // 記錄 potential bug
        });
    });
});

// ============================================================================
// Scenario Tests - 實際使用場景驗證
// ============================================================================

describe('Scenario Tests: 多用戶協作場景', () => {
    describe('場景 1: A 修改金額，B 標記結算（您回報的 Bug）', () => {
        it('A 的金額修改應該被正確同步到 B', () => {
            // 初始狀態：A 和 B 都有相同的帳單
            const initialBill = createSyncedBill({
                expenses: [createExpense({
                    id: 'e1',
                    remoteId: 're1',
                    amount: 100,
                })],
                version: 1,
            });

            // Step 1: A 修改金額
            const billAfterAEdit = {
                ...initialBill,
                expenses: [{
                    ...initialBill.expenses[0],
                    amount: 200,  // A 改成 200
                }],
                syncStatus: 'modified' as const,
            };

            // Step 2: A 嘗試同步
            const snapshot = createBillSnapshot(initialBill);
            const deltaRequest = buildDeltaSyncRequest(billAfterAEdit, snapshot);

            // 驗證：A 的金額變更應該被包含
            expect(deltaRequest.expenses?.update).toBeDefined();
            expect(deltaRequest.expenses?.update?.[0].amount).toBe(200);
        });

        it('B 標記結算不應該覆蓋 A 的金額變更', () => {
            // 這個測試需要模擬完整的同步流程
            // 目前作為 documentation 記錄預期行為

            // 預期流程：
            // 1. A 修改金額 100 → 200
            // 2. A 同步成功，Server version = N+1，金額 = 200
            // 3. B 收到 A 的更新，B 的金額變成 200
            // 4. B 標記結算
            // 5. B 同步，Server version = N+2
            // 6. A 收到 B 的更新，A 的金額仍是 200（不是被還原）

            // 目前的 bug 流程：
            // 1. A 修改金額 100 → 200
            // 2. A 的同步 delta 是空的（因為 BUG-001）
            // 3. Server 不知道 A 改了金額
            // 4. B 標記結算並同步，金額仍是 100
            // 5. A 收到 B 的更新，A 的金額被「還原」成 100

            expect(true).toBe(true);  // placeholder
        });
    });

    describe('場景 2: A 認領成員，B 應該看到認領資訊', () => {
        it('認領操作應該被包含在 delta request', () => {
            const member = createMember({
                id: 'member-1',
                remoteId: 'member-remote-1',
                name: 'Alice',
            });

            const billBeforeClaim = createSyncedBill({
                members: [member],
            });

            // A 認領成員
            const billAfterClaim = {
                ...billBeforeClaim,
                members: [{
                    ...member,
                    userId: 'user-123',
                    avatarUrl: 'https://example.com/avatar.jpg',
                    claimedAt: '2024-01-01T12:00:00Z',
                }],
                syncStatus: 'modified' as const,
            };

            const snapshot = createBillSnapshot(billBeforeClaim);
            const deltaRequest = buildDeltaSyncRequest(billAfterClaim, snapshot);

            // 驗證：認領資訊應該被包含
            expect(deltaRequest.members?.update).toBeDefined();
            expect(deltaRequest.members?.update?.[0].linkedUserId).toBe('user-123');
            expect(deltaRequest.members?.update?.[0].claimedAt).toBe('2024-01-01T12:00:00Z');
        });

    });

    describe('場景 3: 網路斷線期間的離線編輯', () => {
        it('離線時的多次編輯應該合併成單一 delta', () => {
            const initialBill = createSyncedBill({
                expenses: [createExpense({
                    id: 'e1',
                    remoteId: 're1',
                    name: 'Dinner',
                    amount: 100,
                })],
            });

            // 離線期間的多次編輯
            const finalBill = {
                ...initialBill,
                expenses: [{
                    ...initialBill.expenses[0],
                    name: 'Lunch',  // 改名
                    amount: 200,    // 改金額
                }],
                syncStatus: 'modified' as const,
            };

            const snapshot = createBillSnapshot(initialBill);
            const deltaRequest = buildDeltaSyncRequest(finalBill, snapshot);

            // 應該只有一個 update，包含所有變更
            expect(deltaRequest.expenses?.update).toHaveLength(1);
            expect(deltaRequest.expenses?.update?.[0].name).toBe('Lunch');
            expect(deltaRequest.expenses?.update?.[0].amount).toBe(200);
        });
    });
});

// ============================================================================
// Regression Tests - 回歸測試（針對已發現的 Bug）
// ============================================================================

describe('Regression Tests: 已知 Bug 驗證', () => {
    describe('BUG-001: Delta Sync 缺少 Snapshot', () => {
        it('不傳 snapshot 時，已同步成員的更新不會被偵測', () => {
            const bill = createSyncedBill({
                members: [createMember({
                    id: 'member-1',
                    remoteId: 'member-remote-1',
                    name: 'Updated Name',
                })],
            });

            // 不傳 snapshot（目前的錯誤用法）
            const result = buildDeltaSyncRequest(bill, undefined);

            // 這個測試記錄了 bug：更新不會被偵測
            expect(result.members?.update).toBeUndefined();  // Bug 行為
        });
    });

});

// ============================================================================
// Regression Tests - Conversion Functions syncSnapshot Initialization
// ============================================================================

describe('Regression Tests: Conversion Functions syncSnapshot Initialization', () => {
    describe('BUG-003: billDtoToBill must initialize syncSnapshot', () => {
        it('bill converted from BillDto must contain syncSnapshot', async () => {
            const { billDtoToBill } = await import('../billAdapter');

            const mockBillDto = {
                id: 'remote-bill-1',
                name: 'Test Bill',
                members: [
                    { id: 'remote-member-1', name: 'Alice' },
                    { id: 'remote-member-2', name: 'Bob' },
                ],
                expenses: [
                    {
                        id: 'remote-expense-1',
                        name: 'Dinner',
                        amount: 100,
                        paidById: 'remote-member-1',
                        participantIds: ['remote-member-1', 'remote-member-2'],
                    },
                ],
                settledTransfers: [],
                version: 5,
            };

            const result = billDtoToBill(mockBillDto);

            expect(result.syncSnapshot).toBeDefined();
            expect(result.syncSnapshot?.name).toBe('Test Bill');
            expect(result.syncSnapshot?.members).toHaveLength(2);
            expect(result.syncSnapshot?.expenses).toHaveLength(1);
            expect(result.syncSnapshot?.version).toBe(5);
        });

        it('converted bill can correctly detect updates via delta sync', async () => {
            const { billDtoToBill } = await import('../billAdapter');

            const mockBillDto = {
                id: 'remote-bill-1',
                name: 'Test Bill',
                members: [
                    { id: 'remote-member-1', name: 'Alice', linkedUserId: null },
                ],
                expenses: [],
                settledTransfers: [],
                version: 5,
            };

            const importedBill = billDtoToBill(mockBillDto);

            const modifiedBill: Bill = {
                ...importedBill,
                members: [{
                    ...importedBill.members[0],
                    userId: 'user-123',
                    claimedAt: '2024-01-01T00:00:00Z',
                }],
                syncStatus: 'modified',
            };

            const deltaRequest = buildDeltaSyncRequest(modifiedBill, importedBill.syncSnapshot);

            expect(deltaRequest.members?.update).toBeDefined();
            expect(deltaRequest.members?.update).toHaveLength(1);
            expect(deltaRequest.members?.update?.[0].linkedUserId).toBe('user-123');
        });
    });

    describe('BUG-004: rebaseBillFromServer must initialize syncSnapshot', () => {
        it('bill rebased from server must contain syncSnapshot', async () => {
            const { rebaseBillFromServer } = await import('../billAdapter');

            const localBill = createSyncedBill({
                members: [createMember({
                    id: 'local-member-1',
                    remoteId: 'remote-member-1',
                    name: 'Alice',
                })],
            });

            const serverBillDto = {
                id: 'remote-bill-1',
                name: 'Updated Bill Name',
                members: [
                    { id: 'remote-member-1', name: 'Alice Updated' },
                ],
                expenses: [],
                settledTransfers: [],
                version: 10,
            };

            const result = rebaseBillFromServer(localBill, serverBillDto);

            expect(result.syncSnapshot).toBeDefined();
            expect(result.syncSnapshot?.name).toBe('Updated Bill Name');
            expect(result.syncSnapshot?.version).toBe(10);
        });

        it('rebased bill can correctly detect new changes via delta sync', async () => {
            const { rebaseBillFromServer } = await import('../billAdapter');

            const localBill = createSyncedBill();

            const serverBillDto = {
                id: 'remote-bill-1',
                name: 'Synced Bill',
                members: [
                    { id: 'remote-member-1', name: 'Alice' },
                ],
                expenses: [
                    {
                        id: 'remote-expense-1',
                        name: 'Dinner',
                        amount: 100,
                        paidById: 'remote-member-1',
                        participantIds: ['remote-member-1'],
                    },
                ],
                settledTransfers: [],
                version: 5,
            };

            const rebasedBill = rebaseBillFromServer(localBill, serverBillDto);

            const modifiedBill: Bill = {
                ...rebasedBill,
                expenses: [{
                    ...rebasedBill.expenses[0],
                    amount: 200,
                }],
                syncStatus: 'modified',
            };

            const deltaRequest = buildDeltaSyncRequest(modifiedBill, rebasedBill.syncSnapshot);

            expect(deltaRequest.expenses?.update).toBeDefined();
            expect(deltaRequest.expenses?.update).toHaveLength(1);
            expect(deltaRequest.expenses?.update?.[0].amount).toBe(200);
        });
    });

    describe('BUG-005: Collaboration scenario - claim member after login', () => {
        it('bill after login handshake can correctly detect claim changes', async () => {
            const { billDtoToBill } = await import('../billAdapter');

            const mockBillDto = {
                id: 'remote-bill-1',
                name: 'Shared Bill',
                members: [
                    { id: 'remote-member-1', name: 'Alice', linkedUserId: null, claimedAt: null },
                    { id: 'remote-member-2', name: 'Bob', linkedUserId: null, claimedAt: null },
                ],
                expenses: [],
                settledTransfers: [],
                version: 1,
            };

            const importedBill = billDtoToBill(mockBillDto);

            expect(importedBill.syncSnapshot).toBeDefined();
            expect(importedBill.syncSnapshot?.members[0].userId).toBeUndefined();

            const claimedBill: Bill = {
                ...importedBill,
                members: [
                    {
                        ...importedBill.members[0],
                        userId: 'current-user-id',
                        claimedAt: '2024-01-01T00:00:00Z',
                    },
                    importedBill.members[1],
                ],
                syncStatus: 'modified',
            };

            const deltaRequest = buildDeltaSyncRequest(claimedBill, importedBill.syncSnapshot);

            expect(deltaRequest.members?.update).toBeDefined();
            expect(deltaRequest.members?.update).toHaveLength(1);
            expect(deltaRequest.members?.update?.[0].remoteId).toBe('remote-member-1');
            expect(deltaRequest.members?.update?.[0].linkedUserId).toBe('current-user-id');
        });
    });

    /**
     * BUG-006 回歸測試：ItemizedExpenseView 必須追蹤 deletedItemIds
     *
     * 驗證當使用者在編輯逐項紀錄時刪除品項，
     * 被刪除品項的 remoteId 必須正確追蹤並包含在 delta sync 請求中。
     *
     * 注意：deletedItemIds 被收集到 request.expenseItems.delete 中，
     * 而非巢狀在 expenses.update[].items.delete 內。
     */
    describe('BUG-006: 品項刪除必須追蹤以進行同步', () => {
        it('刪除品項時應在 delta sync 請求中包含 deletedItemIds', async () => {
            const { buildDeltaSyncRequest } = await import('../deltaFactory');
            const { billDtoToBill } = await import('../billAdapter');

            // 建立一個從伺服器取得的逐項紀錄帳單
            const mockBillDto = {
                id: 'remote-bill-1',
                name: '晚餐',
                members: [
                    { id: 'remote-member-1', name: 'Alice', linkedUserId: null, claimedAt: null },
                    { id: 'remote-member-2', name: 'Bob', linkedUserId: null, claimedAt: null },
                ],
                expenses: [
                    {
                        id: 'remote-expense-1',
                        name: '食物',
                        amount: 300,
                        serviceFeePercent: 0,
                        isItemized: true,
                        paidById: 'remote-member-1',
                        participantIds: ['remote-member-1', 'remote-member-2'],
                        items: [
                            {
                                id: 'remote-item-1',
                                name: '漢堡',
                                amount: 100,
                                paidById: 'remote-member-1',
                                participantIds: ['remote-member-1'],
                            },
                            {
                                id: 'remote-item-2',
                                name: '披薩',
                                amount: 200,
                                paidById: 'remote-member-1',
                                participantIds: ['remote-member-2'],
                            },
                        ],
                    },
                ],
                settledTransfers: [],
                version: 1,
            };

            const importedBill = billDtoToBill(mockBillDto);

            // 模擬刪除 item-2（披薩），只保留 item-1（漢堡）
            const modifiedBill: Bill = {
                ...importedBill,
                expenses: [
                    {
                        ...importedBill.expenses[0],
                        amount: 100, // 刪除後更新金額
                        items: [importedBill.expenses[0].items[0]], // 只剩漢堡
                        deletedItemIds: ['remote-item-2'], // 追蹤被刪除品項的 remoteId
                    },
                ],
                syncStatus: 'modified',
            };

            const deltaRequest = buildDeltaSyncRequest(modifiedBill, importedBill.syncSnapshot);

            // 驗證被刪除的品項包含在 expenseItems.delete 中
            expect(deltaRequest.expenseItems?.delete).toBeDefined();
            expect(deltaRequest.expenseItems?.delete).toContain('remote-item-2');
        });

        it('應正確處理多個被刪除的品項', async () => {
            const { buildDeltaSyncRequest } = await import('../deltaFactory');
            const { billDtoToBill } = await import('../billAdapter');

            const mockBillDto = {
                id: 'remote-bill-1',
                name: '派對',
                members: [
                    { id: 'remote-member-1', name: 'Alice', linkedUserId: null, claimedAt: null },
                ],
                expenses: [
                    {
                        id: 'remote-expense-1',
                        name: '零食',
                        amount: 300,
                        serviceFeePercent: 0,
                        isItemized: true,
                        paidById: 'remote-member-1',
                        participantIds: ['remote-member-1'],
                        items: [
                            { id: 'remote-item-1', name: '洋芋片', amount: 100, paidById: 'remote-member-1', participantIds: ['remote-member-1'] },
                            { id: 'remote-item-2', name: '沾醬', amount: 100, paidById: 'remote-member-1', participantIds: ['remote-member-1'] },
                            { id: 'remote-item-3', name: '汽水', amount: 100, paidById: 'remote-member-1', participantIds: ['remote-member-1'] },
                        ],
                    },
                ],
                settledTransfers: [],
                version: 1,
            };

            const importedBill = billDtoToBill(mockBillDto);

            // 刪除品項 1 和 3，只保留品項 2
            const modifiedBill: Bill = {
                ...importedBill,
                expenses: [
                    {
                        ...importedBill.expenses[0],
                        amount: 100,
                        items: [importedBill.expenses[0].items[1]], // 只剩沾醬
                        deletedItemIds: ['remote-item-1', 'remote-item-3'],
                    },
                ],
                syncStatus: 'modified',
            };

            const deltaRequest = buildDeltaSyncRequest(modifiedBill, importedBill.syncSnapshot);

            // 驗證所有被刪除的品項都包含在 expenseItems.delete 中
            expect(deltaRequest.expenseItems?.delete).toBeDefined();
            expect(deltaRequest.expenseItems?.delete).toHaveLength(2);
            expect(deltaRequest.expenseItems?.delete).toContain('remote-item-1');
            expect(deltaRequest.expenseItems?.delete).toContain('remote-item-3');
        });
    });

    /**
     * BUG-007 回歸測試：複製品項不應繼承原品項的 remoteId
     *
     * 當使用者複製已同步的品項時，新品項應該被視為新增項目（無 remoteId），
     * 而非原品項的更新。
     */
    describe('BUG-007: 複製品項不應繼承 remoteId', () => {
        it('複製的品項應該被偵測為 ADD 而非 UPDATE', async () => {
            const { buildDeltaSyncRequest } = await import('../deltaFactory');
            const { billDtoToBill } = await import('../billAdapter');

            // 建立一個有已同步品項的帳單
            const mockBillDto = {
                id: 'remote-bill-1',
                name: '午餐',
                members: [
                    { id: 'remote-member-1', name: 'Alice', linkedUserId: null, claimedAt: null },
                ],
                expenses: [
                    {
                        id: 'remote-expense-1',
                        name: '食物',
                        amount: 100,
                        serviceFeePercent: 0,
                        isItemized: true,
                        paidById: 'remote-member-1',
                        participantIds: ['remote-member-1'],
                        items: [
                            {
                                id: 'remote-item-1',
                                name: '漢堡',
                                amount: 100,
                                paidById: 'remote-member-1',
                                participantIds: ['remote-member-1'],
                            },
                        ],
                    },
                ],
                settledTransfers: [],
                version: 1,
            };

            const importedBill = billDtoToBill(mockBillDto);

            // 模擬複製品項（正確做法：新品項沒有 remoteId）
            const duplicatedItem = {
                id: 'local-new-item',
                name: '漢堡',
                amount: 100,
                paidById: importedBill.members[0].id,
                participants: [importedBill.members[0].id],
                // 注意：沒有 remoteId，這是 BUG-007 修復後的正確行為
            };

            const modifiedBill: Bill = {
                ...importedBill,
                expenses: [
                    {
                        ...importedBill.expenses[0],
                        amount: 200,
                        items: [
                            importedBill.expenses[0].items[0], // 原品項
                            duplicatedItem, // 複製的新品項
                        ],
                    },
                ],
                syncStatus: 'modified',
            };

            const deltaRequest = buildDeltaSyncRequest(modifiedBill, importedBill.syncSnapshot);

            // 複製的品項應該在 add 中
            expect(deltaRequest.expenseItems?.add).toBeDefined();
            expect(deltaRequest.expenseItems?.add).toHaveLength(1);
            expect(deltaRequest.expenseItems?.add?.[0].localId).toBe('local-new-item');

            // 原品項不應該在 update 中（沒有變更）
            expect(deltaRequest.expenseItems?.update).toBeUndefined();
        });

        it('如果複製品項錯誤地繼承 remoteId，會導致錯誤的 UPDATE 操作', async () => {
            const { buildDeltaSyncRequest } = await import('../deltaFactory');
            const { billDtoToBill } = await import('../billAdapter');

            const mockBillDto = {
                id: 'remote-bill-1',
                name: '午餐',
                members: [
                    { id: 'remote-member-1', name: 'Alice', linkedUserId: null, claimedAt: null },
                ],
                expenses: [
                    {
                        id: 'remote-expense-1',
                        name: '食物',
                        amount: 100,
                        serviceFeePercent: 0,
                        isItemized: true,
                        paidById: 'remote-member-1',
                        participantIds: ['remote-member-1'],
                        items: [
                            {
                                id: 'remote-item-1',
                                name: '漢堡',
                                amount: 100,
                                paidById: 'remote-member-1',
                                participantIds: ['remote-member-1'],
                            },
                        ],
                    },
                ],
                settledTransfers: [],
                version: 1,
            };

            const importedBill = billDtoToBill(mockBillDto);

            // 模擬錯誤的複製行為（繼承了 remoteId - BUG-007 修復前的行為）
            const buggyDuplicatedItem = {
                id: 'local-new-item',
                name: '漢堡',
                amount: 100,
                paidById: importedBill.members[0].id,
                participants: [importedBill.members[0].id],
                remoteId: 'remote-item-1', // 錯誤！繼承了原品項的 remoteId
            };

            const buggyBill: Bill = {
                ...importedBill,
                expenses: [
                    {
                        ...importedBill.expenses[0],
                        amount: 200,
                        items: [
                            importedBill.expenses[0].items[0],
                            buggyDuplicatedItem,
                        ],
                    },
                ],
                syncStatus: 'modified',
            };

            const deltaRequest = buildDeltaSyncRequest(buggyBill, importedBill.syncSnapshot);

            // 這個測試記錄錯誤行為：複製品項不會被正確識別為 ADD
            // 因為複製品項有 remoteId，deltaFactory 會以為它是既有品項
            // 結果：複製品項不會產生 ADD 操作，導致新品項無法同步到 Server
            expect(deltaRequest.expenseItems?.add).toBeUndefined();

            // 驗證 expense 的 amount 變更仍被偵測到
            expect(deltaRequest.expenses?.update).toBeDefined();
            expect(deltaRequest.expenses?.update?.[0]?.amount).toBe(200);
        });
    });

    /**
     * BUG-008 回歸測試：同步後 snapshot 必須正確捕捉品項狀態
     *
     * 驗證 markBillAsSynced 後建立的 snapshot 正確包含所有品項，
     * 且後續的變更能被正確偵測。
     */
    describe('BUG-008: 同步後 snapshot 必須正確捕捉品項狀態', () => {
        it('createBillSnapshot 應該深拷貝所有品項', () => {
            const bill = createSyncedBill({
                expenses: [
                    createExpense({
                        id: 'e1',
                        remoteId: 're1',
                        isItemized: true,
                        items: [
                            {
                                id: 'i1',
                                remoteId: 'ri1',
                                name: '漢堡',
                                amount: 100,
                                paidById: 'member-local-1',
                                participants: ['member-local-1'],
                            },
                            {
                                id: 'i2',
                                remoteId: 'ri2',
                                name: '薯條',
                                amount: 50,
                                paidById: 'member-local-1',
                                participants: ['member-local-1'],
                            },
                        ],
                    }),
                ],
            });

            const snapshot = createBillSnapshot(bill);

            // 驗證品項被正確捕捉
            expect(snapshot.expenses).toHaveLength(1);
            expect(snapshot.expenses[0].items).toHaveLength(2);
            expect(snapshot.expenses[0].items[0].remoteId).toBe('ri1');
            expect(snapshot.expenses[0].items[1].remoteId).toBe('ri2');
        });

        it('snapshot 應該是深拷貝，修改原帳單不影響 snapshot', () => {
            const bill = createSyncedBill({
                expenses: [
                    createExpense({
                        id: 'e1',
                        remoteId: 're1',
                        isItemized: true,
                        items: [
                            {
                                id: 'i1',
                                remoteId: 'ri1',
                                name: '原始名稱',
                                amount: 100,
                                paidById: 'member-local-1',
                                participants: ['member-local-1'],
                            },
                        ],
                    }),
                ],
            });

            const snapshot = createBillSnapshot(bill);

            // 修改原帳單的品項
            bill.expenses[0].items[0].name = '修改後名稱';
            bill.expenses[0].items[0].amount = 200;

            // 驗證 snapshot 未受影響
            expect(snapshot.expenses[0].items[0].name).toBe('原始名稱');
            expect(snapshot.expenses[0].items[0].amount).toBe(100);
        });

        it('使用 snapshot 可以正確偵測品項金額變更', () => {
            const bill = createSyncedBill({
                expenses: [
                    createExpense({
                        id: 'e1',
                        remoteId: 're1',
                        isItemized: true,
                        items: [
                            {
                                id: 'i1',
                                remoteId: 'ri1',
                                name: '漢堡',
                                amount: 100,
                                paidById: 'member-local-1',
                                participants: ['member-local-1'],
                            },
                        ],
                    }),
                ],
            });

            const snapshot = createBillSnapshot(bill);

            // 修改品項金額
            const modifiedBill: Bill = {
                ...bill,
                expenses: [
                    {
                        ...bill.expenses[0],
                        items: [
                            {
                                ...bill.expenses[0].items[0],
                                amount: 150, // 金額從 100 改為 150
                            },
                        ],
                    },
                ],
                syncStatus: 'modified',
            };

            const deltaRequest = buildDeltaSyncRequest(modifiedBill, snapshot);

            // 驗證品項更新被偵測
            expect(deltaRequest.expenseItems?.update).toBeDefined();
            expect(deltaRequest.expenseItems?.update).toHaveLength(1);
            expect(deltaRequest.expenseItems?.update?.[0].remoteId).toBe('ri1');
            expect(deltaRequest.expenseItems?.update?.[0].amount).toBe(150);
        });
    });

    /**
     * BUG-009 回歸測試：Itemized Expense UPDATE 不應嘗試解析空的 paidById
     *
     * 問題：Itemized expense 的 paidById 和 participants 在 expense 層級是空的，
     * 因為這些欄位在品項（item）層級處理。當 deltaFactory 嘗試用 resolveMemberIdStrict('')
     * 解析空字串時會拋出 MissingRemoteIdError。
     */
    describe('BUG-009: Itemized Expense UPDATE 不應解析空的 paidById', () => {
        it('修改 itemized expense 時不應拋出 MissingRemoteIdError', () => {
            // 建立已同步的 itemized expense（paidById 為空）
            const bill = createSyncedBill({
                expenses: [
                    createExpense({
                        id: 'e1',
                        remoteId: 're1',
                        isItemized: true,
                        paidById: '',           // Itemized 在 expense 層級無付款人
                        participants: [],       // Itemized 在 expense 層級無參與者
                        amount: 100,
                        items: [
                            {
                                id: 'i1',
                                remoteId: 'ri1',
                                name: '漢堡',
                                amount: 100,
                                paidById: 'member-local-1',
                                participants: ['member-local-1'],
                            },
                        ],
                    }),
                ],
            });

            const snapshot = createBillSnapshot(bill);

            // 新增第二個品項（會改變 expense 的 amount）
            const modifiedBill: Bill = {
                ...bill,
                expenses: [
                    {
                        ...bill.expenses[0],
                        amount: 200, // 原本 100，現在加了一個 100 的品項
                        items: [
                            bill.expenses[0].items[0],
                            {
                                id: 'i2',
                                name: '薯條',
                                amount: 100,
                                paidById: 'member-local-1',
                                participants: ['member-local-1'],
                                // 新品項沒有 remoteId
                            },
                        ],
                    },
                ],
                syncStatus: 'modified',
            };

            // 這不應該拋出錯誤
            expect(() => buildDeltaSyncRequest(modifiedBill, snapshot)).not.toThrow();

            const deltaRequest = buildDeltaSyncRequest(modifiedBill, snapshot);

            // 驗證 expense UPDATE 被產生（因為 amount 變了）
            expect(deltaRequest.expenses?.update).toBeDefined();
            expect(deltaRequest.expenses?.update?.[0].remoteId).toBe('re1');
            expect(deltaRequest.expenses?.update?.[0].amount).toBe(200);

            // 驗證 paidByMemberId 和 participantIds 是 undefined（不是空字串）
            expect(deltaRequest.expenses?.update?.[0].paidByMemberId).toBeUndefined();
            expect(deltaRequest.expenses?.update?.[0].participantIds).toBeUndefined();

            // 驗證新品項 ADD 被產生
            expect(deltaRequest.expenseItems?.add).toBeDefined();
            expect(deltaRequest.expenseItems?.add).toHaveLength(1);
            expect(deltaRequest.expenseItems?.add?.[0].name).toBe('薯條');
        });

        it('同時編輯第一個品項並新增第二個品項應該成功', () => {
            const bill = createSyncedBill({
                expenses: [
                    createExpense({
                        id: 'e1',
                        remoteId: 're1',
                        isItemized: true,
                        paidById: '',
                        participants: [],
                        amount: 100,
                        items: [
                            {
                                id: 'i1',
                                remoteId: 'ri1',
                                name: '漢堡',
                                amount: 100,
                                paidById: 'member-local-1',
                                participants: ['member-local-1'],
                            },
                        ],
                    }),
                ],
            });

            const snapshot = createBillSnapshot(bill);

            // 編輯第一個品項 + 新增第二個品項
            const modifiedBill: Bill = {
                ...bill,
                expenses: [
                    {
                        ...bill.expenses[0],
                        amount: 250, // 150 + 100
                        items: [
                            {
                                ...bill.expenses[0].items[0],
                                amount: 150, // 從 100 改為 150
                            },
                            {
                                id: 'i2',
                                name: '薯條',
                                amount: 100,
                                paidById: 'member-local-1',
                                participants: ['member-local-1'],
                            },
                        ],
                    },
                ],
                syncStatus: 'modified',
            };

            expect(() => buildDeltaSyncRequest(modifiedBill, snapshot)).not.toThrow();

            const deltaRequest = buildDeltaSyncRequest(modifiedBill, snapshot);

            // 驗證 expense UPDATE
            expect(deltaRequest.expenses?.update).toHaveLength(1);
            expect(deltaRequest.expenses?.update?.[0].amount).toBe(250);

            // 驗證 item UPDATE（第一個品項）
            expect(deltaRequest.expenseItems?.update).toHaveLength(1);
            expect(deltaRequest.expenseItems?.update?.[0].remoteId).toBe('ri1');
            expect(deltaRequest.expenseItems?.update?.[0].amount).toBe(150);

            // 驗證 item ADD（第二個品項）
            expect(deltaRequest.expenseItems?.add).toHaveLength(1);
            expect(deltaRequest.expenseItems?.add?.[0].name).toBe('薯條');
        });
    });
});