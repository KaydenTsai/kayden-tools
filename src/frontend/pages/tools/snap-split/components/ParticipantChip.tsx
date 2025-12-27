import { Avatar, Chip } from "@mui/material";
import { getMemberColor } from "@/utils/settlement.ts";

interface ParticipantChipProps {
    member: { id: string; name: string };
    members: { id: string; name: string }[];
    selected: boolean;
    onClick: () => void;
    size?: "small" | "medium";
}

export function ParticipantChip({ member, members, selected, onClick, size = "medium" }: ParticipantChipProps) {
    return (
        <Chip
            avatar={
                <Avatar
                    sx={{
                        bgcolor: getMemberColor(member.id, members),
                        color: 'common.white',
                        fontSize: size === "small" ? '0.75rem' : '0.875rem',
                    }}
                >
                    {member.name.charAt(0).toUpperCase()}
                </Avatar>
            }
            label={member.name}
            size={size}
            color={selected ? 'primary' : 'default'}
            variant={selected ? 'filled' : 'outlined'}
            onClick={onClick}
            sx={{
                cursor: 'pointer',
                fontWeight: selected ? 600 : 400,
                '& .MuiChip-avatar': {
                    color: 'common.white',
                }
            }}
        />
    );
}
