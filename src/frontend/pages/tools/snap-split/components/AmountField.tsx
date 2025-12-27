import { InputAdornment, TextField, type TextFieldProps } from "@mui/material";
import { formatAmount } from "../../../../utils/settlement";

function evaluateExpression(expr: string): number | null {
    if (!expr.trim()) return null;
    if (!/^[\d+\-*/.\s()]+$/.test(expr)) return null;
    try {
        const result = new Function(`"use strict"; return (${expr})`)();
        if (typeof result === 'number' && isFinite(result) && result >= 0) {
            return Math.round(result * 100) / 100;
        }
        return null;
    } catch {
        return null;
    }
}

function isExpressionInput(value: string): boolean {
    return /[+\-*/]/.test(value);
}

interface AmountFieldProps extends Omit<TextFieldProps, 'value' | 'onChange' | 'helperText'> {
    value: string;
    onChange: (value: string) => void;
    /** 顯示運算提示文字（固定顯示，不會晃動）*/
    showHint?: boolean;
}

/**
 * 金額輸入欄位，支援四則運算
 * - 輸入運算式時，Label 會顯示計算結果（綠色）
 * - 運算式無效時，顯示錯誤狀態（紅色）
 * - helperText 固定顯示，不會造成版面晃動
 */
export function AmountField({
    value,
    onChange,
    showHint = false,
    label = '金額',
    size = 'small',
    ...props
}: AmountFieldProps) {
    const isExpression = isExpressionInput(value);
    const evaluatedAmount = evaluateExpression(value);
    const hasError = isExpression && value.trim() !== '' && evaluatedAmount === null;
    const hasResult = isExpression && evaluatedAmount !== null;

    // 動態 Label：顯示計算結果或錯誤提示
    const displayLabel = hasResult
        ? `= ${formatAmount(evaluatedAmount)}`
        : hasError
            ? '無效算式'
            : label;

    return (
        <TextField
            value={value}
            onChange={(e) => onChange(e.target.value)}
            label={displayLabel}
            size={size}
            error={hasError}
            helperText={showHint ? '支援四則運算，如 100+50、1000/3' : undefined}
            slotProps={{
                input: {
                    startAdornment: <InputAdornment position="start">$</InputAdornment>,
                },
                inputLabel: (hasResult || hasError) ? {
                    shrink: true,
                    sx: {
                        color: hasError ? 'error.main' : 'success.main',
                        fontWeight: 600,
                    },
                } : undefined,
            }}
            {...props}
        />
    );
}

/**
 * 解析金額字串，支援運算式
 * @returns 計算後的數值，無效時回傳 null
 */
export function parseAmount(value: string): number | null {
    return evaluateExpression(value);
}

/**
 * 檢查是否為有效金額（包含運算式）
 */
export function isValidAmount(value: string): boolean {
    const result = evaluateExpression(value);
    return result !== null && result > 0;
}
