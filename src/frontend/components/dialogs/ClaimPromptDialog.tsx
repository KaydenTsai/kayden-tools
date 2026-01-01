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
import { useBillSync } from '@/hooks/useBillSync';
import type { Member } from '@/types/snap-split';
import { getMemberColor } from '@/utils/settlement';

interface ClaimPromptDialogProps {
    billId: string;
    open: boolean;
    onClose: () => void;
    onSkip: () => void;
}

export function ClaimPromptDialog({
    billId,
    open,
    onClose,
    onSkip,
}: ClaimPromptDialogProps) {
    const { user } = useAuthStore();
    const { claimMember, bills } = useSnapSplitStore();
    const { syncBill } = useBillSync();

    // 從 store 直接讀取，確保同步後能即時反映最新狀態
    const bill = bills.find(b => b.id === billId);
    const [selectedMember, setSelectedMember] = useState<Member | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [isSyncing, setIsSyncing] = useState(false);

    const { mutate: claimMemberApi, isPending: isClaimPending } = usePostApiMembersIdClaim({
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

    // 帳單不存在時不渲染
    if (!bill) return null;

    // 取得未認領的成員
    const unclaimedMembers = bill.members.filter(m => !m.userId);

    const handleSelectMember = (member: Member) => {
        setSelectedMember(member);
        setError(null);
    };

    const handleConfirm = async () => {
        if (!selectedMember || !user?.id) return;
        setError(null);

        let memberRemoteId = selectedMember.remoteId;

        // 如果帳單尚未同步到雲端，先同步
        if (!bill.remoteId) {
            setIsSyncing(true);
            try {
                const syncResult = await syncBill(bill);
                // 從同步結果取得成員的 remoteId
                memberRemoteId = syncResult.idMappings?.members?.[selectedMember.id] ?? undefined;
                if (!memberRemoteId) {
                    throw new Error('成員同步失敗');
                }
            } catch (err) {
                setError('同步帳單失敗，請稍後再試');
                console.error('Sync failed:', err);
                setIsSyncing(false);
                return;
            }
            setIsSyncing(false);
        }

        // 呼叫認領 API（使用 remoteId）
        if (memberRemoteId) {
            claimMemberApi({
                id: memberRemoteId,
                data: {
                    displayName: user.displayName ?? user.email ?? undefined,
                },
            });
        } else {
            setError('成員尚未同步，請稍後再試');
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
                    disabled={!selectedMember || isSyncing || isClaimPending}
                    startIcon={(isSyncing || isClaimPending) ? <CircularProgress size={20} color="inherit" /> : undefined}
                >
                    {isSyncing ? '同步中...' : isClaimPending ? '認領中...' : '確認，這是我'}
                </Button>

                <Box sx={{ display: 'flex', gap: 1, width: '100%' }}>
                    <Button
                        fullWidth
                        variant="outlined"
                        size="large"
                        onClick={handleSkip}
                        startIcon={<LaterIcon />}
                        disabled={isSyncing || isClaimPending}
                    >
                        稍後再說
                    </Button>
                </Box>
            </DialogActions>
        </Dialog>
    );
}
