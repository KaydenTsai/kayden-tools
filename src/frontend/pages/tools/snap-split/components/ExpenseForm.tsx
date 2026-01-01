import { Box, Button, Stack, TextField, Typography } from "@mui/material";
import { AmountField } from "./AmountField";
import { UserAvatar } from "./UserAvatar";
import { ParticipantChip } from "./ParticipantChip";
import type { Member } from "@/types/snap-split";
import { getMemberColor } from "@/utils/settlement";
import { useAuthStore } from "@/stores/authStore";

export interface ExpenseFormState {
    name: string;
    amount: string | number;
    paidById: string;
    participants: string[];
}

interface ExpenseFormProps {
    members: Member[];
    values: ExpenseFormState;
    onChange: (updates: Partial<ExpenseFormState>) => void;
    onKeyDown?: (e: React.KeyboardEvent, field: keyof ExpenseFormState) => void;
    
    // UI Options
    showName?: boolean;
    showAmount?: boolean;
    showPayer?: boolean;
    showParticipants?: boolean;
    
    // Refs for focus management
    nameRef?: React.RefObject<HTMLInputElement | null>;
    amountRef?: React.RefObject<HTMLInputElement | null>;
}

export function ExpenseForm({
    members,
    values,
    onChange,
    onKeyDown,
    showName = true,
    showAmount = true,
    showPayer = true,
    showParticipants = true,
    nameRef,
    amountRef,
}: ExpenseFormProps) {
    const { user } = useAuthStore();

    // 判斷成員是否為「離線」狀態（已認領但非當前用戶）
    const isMemberOffline = (member: Member) => {
        return !!member.userId && member.userId !== user?.id;
    };

    const handleToggleParticipant = (memberId: string) => {
        const current = values.participants;
        const newParticipants = current.includes(memberId)
            ? current.filter(id => id !== memberId)
            : [...current, memberId];
        onChange({ participants: newParticipants });
    };

    const handleSelectAll = () => {
        onChange({ participants: members.map(m => m.id) });
    };

    const handleClearAll = () => {
        onChange({ participants: [] });
    };

    return (
        <Box>
            <Stack spacing={1.5} sx={{ mb: 2 }}>
                {showName && (
                    <TextField
                        inputRef={nameRef}
                        size="small"
                        label="名稱"
                        value={values.name}
                        onChange={(e) => onChange({ name: e.target.value })}
                        onKeyDown={(e) => onKeyDown?.(e, 'name')}
                        placeholder="例如：晚餐"
                        autoComplete="off"
                        fullWidth
                    />
                )}
                {showAmount && (
                    <AmountField
                        inputRef={amountRef}
                        value={typeof values.amount === 'number' ? values.amount.toString() : values.amount}
                        onChange={(val) => onChange({ amount: val })}
                        onKeyDown={(e) => onKeyDown?.(e, 'amount')}
                        showHint
                        autoComplete="off"
                        fullWidth
                        size="small"
                    />
                )}
            </Stack>

            {showPayer && (
                <Box sx={{ mb: 2 }}>
                    <Typography variant="caption" color="text.secondary" sx={{ mb: 1, display: 'block' }}>
                        付款人
                    </Typography>
                    <Box sx={{
                        display: 'flex',
                        gap: 1,
                        overflowX: 'auto',
                        pb: 1,
                        '::-webkit-scrollbar': { display: 'none' },
                        scrollbarWidth: 'none',
                    }}>
                        {members.map(member => (
                            <UserAvatar
                                key={member.id}
                                name={member.name}
                                color={getMemberColor(member.id, members)}
                                avatarUrl={member.avatarUrl}
                                selected={values.paidById === member.id}
                                onClick={() => onChange({ paidById: member.id })}
                                size={40}
                                isOffline={isMemberOffline(member)}
                            />
                        ))}
                    </Box>
                </Box>
            )}

            {showParticipants && (
                <Box sx={{ mb: 1 }}>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
                        <Typography variant="caption" color="text.secondary">平分者</Typography>
                        <Box sx={{ display: 'flex', gap: 0.5 }}>
                            <Button
                                size="small"
                                onClick={handleSelectAll}
                                sx={{ minWidth: 0, px: 1, fontSize: '0.75rem' }}
                            >
                                全選
                            </Button>
                            <Button
                                size="small"
                                onClick={handleClearAll}
                                sx={{ minWidth: 0, px: 1, fontSize: '0.75rem' }}
                            >
                                清除
                            </Button>
                        </Box>
                    </Box>
                    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
                        {members.map(member => {
                            const isSelected = values.participants.includes(member.id);
                            return (
                                <ParticipantChip
                                    key={member.id}
                                    member={member}
                                    members={members}
                                    selected={isSelected}
                                    size="small"
                                    onClick={() => handleToggleParticipant(member.id)}
                                    isOffline={isMemberOffline(member)}
                                />
                            );
                        })}
                    </Box>
                </Box>
            )}
        </Box>
    );
}
