import {z} from 'zod';

/**
 * Common validation schemas for reuse across forms
 */

// Required string with minimum length
export const requiredString = (message = '此欄位為必填') =>
    z.string().min(1, {message});

// Optional string that can be empty
export const optionalString = () =>
    z.string().optional();

// Positive number (greater than 0)
export const positiveNumber = (message = '請輸入正數') =>
    z.number().positive({message});

// Non-negative number (greater than or equal to 0)
export const nonNegativeNumber = (message = '請輸入非負數') =>
    z.number().nonnegative({message});

// Email validation
export const email = (message = '請輸入有效的電子郵件') =>
    z.string().email({message});

// Amount validation (string that can be parsed to number, supports expressions)
export const amountString = (message = '請輸入有效金額') =>
    z.string().refine(
        (val) => {
            if (!val.trim()) return false;
            try {
                const sanitized = val.replace(/[^0-9+\-*/.()]/g, '');
                const result = Function(`"use strict"; return (${sanitized})`)();
                return typeof result === 'number' && !isNaN(result) && result > 0;
            } catch {
                return false;
            }
        },
        {message}
    );

// At least one item in array
export const nonEmptyArray = <T extends z.ZodTypeAny>(schema: T, message = '請至少選擇一項') =>
    z.array(schema).min(1, {message});

// UUID validation
export const uuid = (message = '無效的 ID 格式') =>
    z.string().uuid({message});

/**
 * Parse amount string expression to number
 */
export function parseAmountExpression(value: string): number | null {
    if (!value.trim()) return null;
    try {
        const sanitized = value.replace(/[^0-9+\-*/.()]/g, '');
        const result = Function(`"use strict"; return (${sanitized})`)();
        return typeof result === 'number' && !isNaN(result) ? result : null;
    } catch {
        return null;
    }
}
