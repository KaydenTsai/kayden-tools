import { Avatar, Chip } from "@mui/material";
import { getMemberColor } from "@/utils/settlement.ts";

interface ParticipantChipProps {
    member: { id: string; name: string; avatarUrl?: string };
    members: { id: string; name: string }[];
    selected: boolean;
    onClick: () => void;
    size?: "small" | "medium";
    /** 已認領但非當前用戶時顯示離線效果 */
    isOffline?: boolean;
}

export function ParticipantChip({ member, members, selected, onClick, size = "medium", isOffline = false }: ParticipantChipProps) {
    return (
        <Chip
            avatar={
                <Avatar
                    src={member.avatarUrl}
                    sx={{
                        bgcolor: getMemberColor(member.id, members),
                        color: 'common.white',
                        fontSize: size === "small" ? '0.75rem' : '0.875rem',
                        // 離線效果
                        opacity: isOffline ? 0.6 : 1,
                        filter: isOffline ? 'grayscale(30%)' : 'none',
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
