import {cn} from '@/shared/lib/utils';
import {MemberAvatar} from './member-avatar';

export interface MemberOption {
    id: string;
    name: string;
    avatarUrl?: string;
}

export interface MemberSelectorProps {
    /** List of members to display */
    members: MemberOption[];
    /** Currently selected member ID */
    selectedId: string | null;
    /** Callback when a member is selected */
    onSelect: (memberId: string) => void;
    /** Function to get member color by ID */
    getColor: (memberId: string) => string;
    /** Size of the avatars */
    size?: 'sm' | 'md';
    /** Additional class names for container */
    className?: string;
}

/**
 * Horizontal scrollable member selector with avatars
 * Used for selecting payer in expense forms
 */
export function MemberSelector({
    members,
    selectedId,
    onSelect,
    getColor,
    size = 'md',
    className,
}: MemberSelectorProps) {
    const avatarSize = size === 'sm' ? 'md' : 'lg';
    const textSize = size === 'sm' ? 'text-[10px] max-w-[48px]' : 'text-xs max-w-[60px]';
    const padding = size === 'sm' ? 'p-1.5' : 'p-2';

    return (
        <div className={cn('flex gap-2 overflow-x-auto scrollbar-hide pb-1', className)}>
            {members.map(member => {
                const selected = selectedId === member.id;

                return (
                    <button
                        key={member.id}
                        type="button"
                        onClick={() => onSelect(member.id)}
                        className={cn(
                            'flex flex-col items-center gap-1 rounded-lg transition-all shrink-0',
                            padding,
                            selected
                                ? 'bg-primary/10 ring-2 ring-inset ring-primary'
                                : 'hover:bg-muted'
                        )}
                    >
                        <MemberAvatar
                            name={member.name}
                            avatarUrl={member.avatarUrl}
                            color={getColor(member.id)}
                            size={avatarSize}
                        />
                        <span className={cn('truncate', textSize)}>{member.name}</span>
                    </button>
                );
            })}
        </div>
    );
}
