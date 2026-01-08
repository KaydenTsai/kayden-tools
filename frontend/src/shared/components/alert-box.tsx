import {cn} from '@/shared/lib/utils';
import {AlertCircle, CheckCircle2, Info, AlertTriangle} from 'lucide-react';

type AlertVariant = 'success' | 'destructive' | 'warning' | 'info';

interface AlertBoxProps {
    children: React.ReactNode;
    variant?: AlertVariant;
    icon?: boolean;
    className?: string;
}

const variantClasses: Record<AlertVariant, string> = {
    success: 'bg-success/10 border-success/30 text-success dark:bg-success/20 dark:border-success/40',
    destructive: 'bg-destructive/10 border-destructive/30 text-destructive dark:bg-destructive/20 dark:border-destructive/40',
    warning: 'bg-warning/10 border-warning/30 text-warning dark:bg-warning/20 dark:border-warning/40',
    info: 'bg-info/10 border-info/30 text-info dark:bg-info/20 dark:border-info/40',
};

const icons: Record<AlertVariant, typeof CheckCircle2> = {
    success: CheckCircle2,
    destructive: AlertCircle,
    warning: AlertTriangle,
    info: Info,
};

/**
 * A styled alert box for displaying messages
 */
export function AlertBox({children, variant = 'info', icon = true, className}: AlertBoxProps) {
    const Icon = icons[variant];

    return (
        <div
            className={cn(
                'p-3 border rounded-lg text-sm',
                variantClasses[variant],
                className
            )}
        >
            {icon ? (
                <div className="flex items-start gap-2">
                    <Icon className="h-4 w-4 mt-0.5 shrink-0"/>
                    <div>{children}</div>
                </div>
            ) : (
                children
            )}
        </div>
    );
}
