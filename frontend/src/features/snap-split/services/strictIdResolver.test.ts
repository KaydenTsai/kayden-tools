import { describe, expect, it } from 'vitest';
import { StrictIdResolver } from './strictIdResolver';
import { MissingRemoteIdError } from '../types/sync';
import type { Member, Expense } from '../types/snap-split';

const createMember = (overrides: Partial<Member> = {}): Member => ({
    id: 'local-1',
    name: 'Test Member',
    ...overrides,
});

const createExpense = (overrides: Partial<Expense> = {}): Expense => ({
    id: 'exp-local-1',
    name: 'Test Expense',
    amount: 100,
    serviceFeePercent: 0,
    isItemized: false,
    paidById: 'local-1',
    participants: ['local-1'],
    items: [],
    ...overrides,
});

describe('StrictIdResolver', () => {
    describe('resolveMemberRemoteId', () => {
        it('有 remoteId 時應返回 remoteId', () => {
            const members = [createMember({ id: 'local-1', remoteId: 'remote-1' })];
            const resolver = new StrictIdResolver(members, []);

            expect(resolver.resolveMemberRemoteId('local-1')).toBe('remote-1');
        });

        it('成員不存在時應拋出 MissingRemoteIdError', () => {
            const resolver = new StrictIdResolver([], []);

            expect(() => resolver.resolveMemberRemoteId('non-existent'))
                .toThrow(MissingRemoteIdError);
        });

        it('成員存在但無 remoteId 時應拋出 MissingRemoteIdError', () => {
            const members = [createMember({ id: 'local-1', remoteId: undefined })];
            const resolver = new StrictIdResolver(members, []);

            expect(() => resolver.resolveMemberRemoteId('local-1'))
                .toThrow(MissingRemoteIdError);
        });

        it('remoteId 為空字串時應拋出 MissingRemoteIdError', () => {
            const members = [createMember({ id: 'local-1', remoteId: '' })];
            const resolver = new StrictIdResolver(members, []);

            expect(() => resolver.resolveMemberRemoteId('local-1'))
                .toThrow(MissingRemoteIdError);
        });

        it('錯誤應包含正確的 entityType 和 localId', () => {
            const members = [createMember({ id: 'local-123', remoteId: undefined })];
            const resolver = new StrictIdResolver(members, []);

            try {
                resolver.resolveMemberRemoteId('local-123');
                expect.fail('應該拋出錯誤');
            } catch (e) {
                expect(e).toBeInstanceOf(MissingRemoteIdError);
                const error = e as MissingRemoteIdError;
                expect(error.entityType).toBe('Member');
                expect(error.localId).toBe('local-123');
            }
        });
    });

    describe('resolveExpenseRemoteId', () => {
        it('有 remoteId 時應返回 remoteId', () => {
            const expenses = [createExpense({ id: 'exp-1', remoteId: 'exp-remote-1' })];
            const resolver = new StrictIdResolver([], expenses);

            expect(resolver.resolveExpenseRemoteId('exp-1')).toBe('exp-remote-1');
        });

        it('費用不存在時應拋出 MissingRemoteIdError', () => {
            const resolver = new StrictIdResolver([], []);

            expect(() => resolver.resolveExpenseRemoteId('non-existent'))
                .toThrow(MissingRemoteIdError);
        });

        it('費用存在但無 remoteId 時應拋出 MissingRemoteIdError', () => {
            const expenses = [createExpense({ id: 'exp-1', remoteId: undefined })];
            const resolver = new StrictIdResolver([], expenses);

            expect(() => resolver.resolveExpenseRemoteId('exp-1'))
                .toThrow(MissingRemoteIdError);
        });
    });

    describe('resolveItemRemoteId', () => {
        it('有 remoteId 時應返回 remoteId', () => {
            const expenses = [createExpense({
                id: 'exp-1',
                items: [{
                    id: 'item-1',
                    name: 'Item',
                    amount: 50,
                    paidById: 'local-1',
                    participants: ['local-1'],
                    remoteId: 'item-remote-1',
                }],
            })];
            const resolver = new StrictIdResolver([], expenses);

            expect(resolver.resolveItemRemoteId('item-1')).toBe('item-remote-1');
        });

        it('項目不存在時應拋出 MissingRemoteIdError', () => {
            const resolver = new StrictIdResolver([], []);

            expect(() => resolver.resolveItemRemoteId('non-existent'))
                .toThrow(MissingRemoteIdError);
        });
    });

    describe('hasMemberRemoteId', () => {
        it('有 remoteId 時應返回 true', () => {
            const members = [createMember({ id: 'local-1', remoteId: 'remote-1' })];
            const resolver = new StrictIdResolver(members, []);

            expect(resolver.hasMemberRemoteId('local-1')).toBe(true);
        });

        it('無 remoteId 時應返回 false', () => {
            const members = [createMember({ id: 'local-1', remoteId: undefined })];
            const resolver = new StrictIdResolver(members, []);

            expect(resolver.hasMemberRemoteId('local-1')).toBe(false);
        });

        it('成員不存在時應返回 false', () => {
            const resolver = new StrictIdResolver([], []);

            expect(resolver.hasMemberRemoteId('non-existent')).toBe(false);
        });
    });

    describe('areAllMembersSynced', () => {
        it('所有成員都有 remoteId 時應返回 true', () => {
            const members = [
                createMember({ id: 'local-1', remoteId: 'remote-1' }),
                createMember({ id: 'local-2', remoteId: 'remote-2' }),
            ];
            const resolver = new StrictIdResolver(members, []);

            expect(resolver.areAllMembersSynced()).toBe(true);
        });

        it('任一成員無 remoteId 時應返回 false', () => {
            const members = [
                createMember({ id: 'local-1', remoteId: 'remote-1' }),
                createMember({ id: 'local-2', remoteId: undefined }),
            ];
            const resolver = new StrictIdResolver(members, []);

            expect(resolver.areAllMembersSynced()).toBe(false);
        });

        it('無成員時應返回 true', () => {
            const resolver = new StrictIdResolver([], []);

            expect(resolver.areAllMembersSynced()).toBe(true);
        });
    });

    describe('getUnsyncedMemberIds', () => {
        it('應返回所有沒有 remoteId 的成員 localId', () => {
            const members = [
                createMember({ id: 'local-1', remoteId: 'remote-1' }),
                createMember({ id: 'local-2', remoteId: undefined }),
                createMember({ id: 'local-3', remoteId: '' }),
            ];
            const resolver = new StrictIdResolver(members, []);

            const unsynced = resolver.getUnsyncedMemberIds();
            expect(unsynced).toContain('local-2');
            expect(unsynced).toContain('local-3');
            expect(unsynced).not.toContain('local-1');
        });

        it('所有成員都已同步時應返回空陣列', () => {
            const members = [
                createMember({ id: 'local-1', remoteId: 'remote-1' }),
                createMember({ id: 'local-2', remoteId: 'remote-2' }),
            ];
            const resolver = new StrictIdResolver(members, []);

            expect(resolver.getUnsyncedMemberIds()).toEqual([]);
        });
    });
});