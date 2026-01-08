import {cn} from '@/shared/lib/utils';

export interface ParticipantOption {
    id: string;
    name: string;
}

export interface ParticipantChipsProps {
    /** List of members to display */
    members: ParticipantOption[];
    /** List of selected member IDs */
    selectedIds: string[];
    /** Callback when a member is toggled */
    onToggle: (memberId: string) => void;
    /** Function to get member color by ID */
    getColor: (memberId: string) => string;
    /** Additional class names for container */
    className?: string;
}

/**
 * Wrapping chip list for selecting participants
 * Used for selecting who shares an expense
 */
export function ParticipantChips({
    members,
    selectedIds,
    onToggle,
    getColor,
    className,
}: ParticipantChipsProps) {
    return (
        <div className={cn('flex flex-wrap gap-1.5', className)}>
            {members.map(member => {
                const selected = selectedIds.includes(member.id);
                const color = getColor(member.id);

                return (
                    <button
                        key={member.id}
                        type="button"
                        onClick={() => onToggle(member.id)}
                        className={cn(
                            'flex items-center gap-1.5 px-2 py-1 rounded-full text-sm transition-all',
                            selected
                                ? 'text-white'
                                : 'bg-muted text-muted-foreground hover:bg-muted/80'
                        )}
                        style={selected ? {backgroundColor: color} : undefined}
                    >
                        <span className="font-medium">{member.name}</span>
                    </button>
                );
            })}
        </div>
    );
}
