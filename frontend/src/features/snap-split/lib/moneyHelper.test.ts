import { describe, expect, it } from 'vitest';
import {
    allocate,
    calculateAmountWithServiceFee,
    calculateServiceFee,
} from './moneyHelper';

describe('MoneyHelper', () => {
    describe('allocate', () => {
        it('單人應返回原始金額', () => {
            const result = allocate(100, 1);
            expect(result).toEqual([100]);
        });

        it('整除金額應平均分配', () => {
            const result = allocate(100, 4);
            expect(result).toEqual([25, 25, 25, 25]);
            expect(result.reduce((a, b) => a + b, 0)).toBe(100);
        });

        it('無法整除應使用 Penny 分配', () => {
            // 100 / 3 = 33.333...
            const result = allocate(100, 3);
            expect(result).toEqual([33.34, 33.33, 33.33]);
            expect(result.reduce((a, b) => a + b, 0)).toBeCloseTo(100, 2);
        });

        it('兩分錢餘數前兩人各多一分', () => {
            // 10 / 3 = 3.333...
            const result = allocate(10, 3);
            expect(result).toEqual([3.34, 3.33, 3.33]);
            expect(result.reduce((a, b) => a + b, 0)).toBeCloseTo(10, 2);
        });

        it('五人分攤應正確分配', () => {
            // 100 / 5 = 20 (整除)
            const result = allocate(100, 5);
            expect(result).toEqual([20, 20, 20, 20, 20]);
            expect(result.reduce((a, b) => a + b, 0)).toBe(100);
        });

        it('七人分攤非整除應正確分配 Penny', () => {
            // 100 / 7 = 14.285714...
            // 基礎金額: 14.28
            // 餘數: 100 - (14.28 * 7) = 100 - 99.96 = 0.04 = 4 分
            const result = allocate(100, 7);
            expect(result).toHaveLength(7);
            expect(result.reduce((a, b) => a + b, 0)).toBeCloseTo(100, 2);
            // 前 4 人得 14.29，後 3 人得 14.28
            expect(result[0]).toBe(14.29);
            expect(result[1]).toBe(14.29);
            expect(result[2]).toBe(14.29);
            expect(result[3]).toBe(14.29);
            expect(result[4]).toBe(14.28);
            expect(result[5]).toBe(14.28);
            expect(result[6]).toBe(14.28);
        });

        it('零金額應返回零陣列', () => {
            const result = allocate(0, 3);
            expect(result).toEqual([0, 0, 0]);
        });

        it('人數為零應拋出異常', () => {
            expect(() => allocate(100, 0)).toThrow('人數必須大於 0');
        });

        it('負人數應拋出異常', () => {
            expect(() => allocate(100, -1)).toThrow('人數必須大於 0');
        });

        it('小金額應正確分配', () => {
            // 0.01 / 2 = 0.005 => 基礎 0.00，餘數 1 分
            const result = allocate(0.01, 2);
            expect(result).toEqual([0.01, 0]);
            expect(result.reduce((a, b) => a + b, 0)).toBeCloseTo(0.01, 2);
        });

        it('大金額應正確分配', () => {
            const result = allocate(1000000.99, 3);
            expect(result).toHaveLength(3);
            expect(result.reduce((a, b) => a + b, 0)).toBeCloseTo(1000000.99, 2);
        });
    });

    describe('calculateAmountWithServiceFee', () => {
        it('10% 服務費應正確計算', () => {
            expect(calculateAmountWithServiceFee(100, 10)).toBe(110);
        });

        it('無服務費應返回原金額', () => {
            expect(calculateAmountWithServiceFee(100, 0)).toBe(100);
        });

        it('小數服務費應四捨五入', () => {
            // 100 * 1.155 = 115.5
            expect(calculateAmountWithServiceFee(100, 15.5)).toBe(115.5);
        });
    });

    describe('calculateServiceFee', () => {
        it('10% 服務費應正確計算', () => {
            expect(calculateServiceFee(100, 10)).toBe(10);
        });

        it('無服務費應返回零', () => {
            expect(calculateServiceFee(100, 0)).toBe(0);
        });

        it('需四捨五入應正確處理', () => {
            // 99 * 0.1 = 9.9
            expect(calculateServiceFee(99, 10)).toBe(9.9);
        });
    });
});
