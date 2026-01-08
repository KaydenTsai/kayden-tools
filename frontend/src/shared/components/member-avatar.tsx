import {cn} from '@/shared/lib/utils';

export interface MemberAvatarProps {
    /** Member name (used for initial) */
    name: string;
    /** Avatar image URL (optional) */
    avatarUrl?: string;
    /** Background color (hex or hsl) */
    color?: string;
    /** Size variant */
    size?: 'xs' | 'sm' | 'md' | 'lg';
    /** Use primary color instead of custom color */
    isPrimary?: boolean;
    /** Additional class names */
    className?: string;
}

const sizeClasses = {
    xs: 'w-5 h-5 text-[10px]',
    sm: 'w-6 h-6 text-xs',
    md: 'w-8 h-8 text-sm',
    lg: 'w-10 h-10 text-base',
};

/**
 * A circular avatar showing member's initial or image
 */
export function MemberAvatar({
    name,
    avatarUrl,
    color,
    size = 'md',
    isPrimary = false,
    className,
}: MemberAvatarProps) {
    const initial = name?.charAt(0)?.toUpperCase() || '?';

    if (avatarUrl) {
        return (
            <img
                src={avatarUrl}
                alt={name}
                className={cn(
                    'rounded-full object-cover shrink-0',
                    sizeClasses[size],
                    className
                )}
            />
        );
    }

    return (
        <div
            className={cn(
                'rounded-full flex items-center justify-center font-semibold text-white shrink-0',
                sizeClasses[size],
                isPrimary && 'bg-primary',
                className
            )}
            style={!isPrimary && color ? {backgroundColor: color} : undefined}
        >
            {initial}
        </div>
    );
}
