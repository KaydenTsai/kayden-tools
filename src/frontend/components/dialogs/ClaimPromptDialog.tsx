import { useState } from 'react';
import {
    Avatar,
    Box,
    Button,
    Dialog,
    DialogActions,
    DialogContent,
    DialogTitle,
    List,
    ListItemAvatar,
    ListItemButton,
    ListItemText,
    Typography,
    CircularProgress,
    Alert,
} from '@mui/material';
import {
    Person as PersonIcon,
    Schedule as LaterIcon,
} from '@mui/icons-material';
import { SlideTransition } from '@/components/ui/SlideTransition';
import { useSnapSplitStore } from '@/stores/snapSplitStore';
import { useAuthStore } from '@/stores/authStore';
import { usePostApiMembersIdClaim } from '@/api/endpoints/members/members';
import type { Bill, Member } from '@/types/snap-split';
import { getMemberColor } from '@/utils/settlement';

interface ClaimPromptDialogProps {
    bill: Bill;
    open: boolean;
    onClose: () => void;
    onSkip: () => void;
}

export function ClaimPromptDialog({
    bill,
    open,
    onClose,
    onSkip,
}: ClaimPromptDialogProps) {
    const { user } = useAuthStore();
    const { claimMember } = useSnapSplitStore();
    const [selectedMember, setSelectedMember] = useState<Member | null>(null);
    const [error, setError] = useState<string | null>(null);

    const { mutate: claimMemberApi, isPending } = usePostApiMembersIdClaim({
        mutation: {
            onSuccess: (response) => {
                if (response.success && response.data && user?.id) {
                    // 同步更新本地 store
                    claimMember({
                        memberId: selectedMember!.id,
                        userId: user.id,
                        displayName: user.displayName ?? user.email ?? '使用者',
                        avatarUrl: user.avatarUrl ?? undefined,
                    });
                    onClose();
                }
            },
            onError: (err) => {
                setError('認領失敗，請稍後再試');
                console.error('Claim failed:', err);
            },
        },
    });

    // 取得未認領的成員
    const unclaimedMembers = bill.members.filter(m => !m.userId);

    const handleSelectMember = (member: Member) => {
        setSelectedMember(member);
        setError(null);
    };

    const handleConfirm = () => {
        if (!selectedMember || !user?.id) return;

        // 如果帳單已同步到雲端，呼叫 API
        if (bill.remoteId) {
            claimMemberApi({
                id: selectedMember.id,
                data: {
                    displayName: user.displayName ?? user.email ?? undefined,
                },
            });
        } else {
            // 本地帳單，直接更新 store
            claimMember({
                memberId: selectedMember.id,
                userId: user.id,
                displayName: user.displayName ?? user.email ?? '使用者',
                avatarUrl: user.avatarUrl ?? undefined,
            });
            onClose();
        }
    };

    const handleSkip = () => {
        onSkip();
        onClose();
    };

    return (
        <Dialog
            open={open}
            onClose={onClose}
            maxWidth="xs"
            fullWidth
            TransitionComponent={SlideTransition}
        >
            <DialogTitle>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <PersonIcon color="primary" />
                    你是誰？
                </Box>
            </DialogTitle>

            <DialogContent>
                <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                    為了同步您的代墊與分帳紀錄，請選擇您在此帳單中的身分：
                </Typography>

                {error && (
                    <Alert severity="error" sx={{ mb: 2 }}>
                        {error}
                    </Alert>
                )}

                <List sx={{ mx: -2 }}>
                    {unclaimedMembers.map((member) => (
                        <ListItemButton
                            key={member.id}
                            selected={selectedMember?.id === member.id}
                            onClick={() => handleSelectMember(member)}
                            sx={{
                                borderRadius: 2,
                                mx: 1,
                                mb: 0.5,
                                border: selectedMember?.id === member.id ? '2px solid' : '2px solid transparent',
                                borderColor: selectedMember?.id === member.id ? 'primary.main' : 'transparent',
                            }}
                        >
                            <ListItemAvatar>
                                <Avatar
                                    sx={{
                                        bgcolor: getMemberColor(member.id, bill.members),
                                        fontWeight: 600,
                                    }}
                                >
                                    {member.name.charAt(0).toUpperCase()}
                                </Avatar>
                            </ListItemAvatar>
                            <ListItemText
                                primary={member.name}
                                primaryTypographyProps={{ fontWeight: 600 }}
                            />
                        </ListItemButton>
                    ))}
                </List>

                {unclaimedMembers.length === 0 && (
                    <Box sx={{ textAlign: 'center', py: 3 }}>
                        <Typography color="text.secondary">
                            所有成員都已被認領
                        </Typography>
                    </Box>
                )}
            </DialogContent>

            <DialogActions sx={{ p: 2, pt: 1, flexDirection: 'column', gap: 1 }}>
                <Button
                    fullWidth
                    variant="contained"
                    size="large"
                    onClick={handleConfirm}
                    disabled={!selectedMember || isPending}
                    startIcon={isPending ? <CircularProgress size={20} color="inherit" /> : undefined}
                >
                    {isPending ? '認領中...' : '確認，這是我'}
                </Button>

                <Box sx={{ display: 'flex', gap: 1, width: '100%' }}>
                    <Button
                        fullWidth
                        variant="outlined"
                        size="large"
                        onClick={handleSkip}
                        startIcon={<LaterIcon />}
                        disabled={isPending}
                    >
                        稍後再說
                    </Button>
                </Box>
            </DialogActions>
        </Dialog>
    );
}
