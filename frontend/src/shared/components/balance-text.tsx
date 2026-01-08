import {cn} from '@/shared/lib/utils';

interface BalanceTextProps {
    value: number;
    formatter?: (value: number) => string;
    showSign?: boolean;
    className?: string;
}

/**
 * Display balance with semantic colors (green for positive, red for negative)
 */
export function BalanceText({
    value,
    formatter = (v) => v.toLocaleString(),
    showSign = false,
    className,
}: BalanceTextProps) {
    const formattedValue = formatter(Math.abs(value));
    const prefix = showSign ? (value > 0 ? '+' : value < 0 ? '-' : '') : (value < 0 ? '-' : '');

    return (
        <span
            className={cn(
                value > 0 && 'text-success',
                value < 0 && 'text-destructive',
                value === 0 && 'text-muted-foreground',
                className
            )}
        >
            {prefix}{formattedValue}
        </span>
    );
}
