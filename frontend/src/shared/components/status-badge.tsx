import {cn} from '@/shared/lib/utils';

type StatusVariant = 'success' | 'destructive' | 'warning' | 'info' | 'default';

interface StatusBadgeProps {
    children: React.ReactNode;
    variant?: StatusVariant;
    className?: string;
}

const variantClasses: Record<StatusVariant, string> = {
    success: 'bg-success/10 text-success dark:bg-success/20',
    destructive: 'bg-destructive/10 text-destructive dark:bg-destructive/20',
    warning: 'bg-warning/10 text-warning dark:bg-warning/20',
    info: 'bg-info/10 text-info dark:bg-info/20',
    default: 'bg-muted text-muted-foreground',
};

/**
 * A small badge for showing status (settled, pending, etc.)
 */
export function StatusBadge({children, variant = 'default', className}: StatusBadgeProps) {
    return (
        <span
            className={cn(
                'inline-flex items-center px-1.5 py-0.5 text-[10px] font-medium rounded',
                variantClasses[variant],
                className
            )}
        >
            {children}
        </span>
    );
}
