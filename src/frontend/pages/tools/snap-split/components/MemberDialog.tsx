import {
    Avatar,
    Box,
    Button,
    Chip,
    Dialog,
    DialogActions,
    DialogContent,
    DialogContentText,
    DialogTitle,
    IconButton,
    List,
    ListItem,
    ListItemAvatar,
    ListItemText,
    TextField,
    Tooltip,
    Typography,
} from "@mui/material";
import {
    Add as AddIcon,
    Delete as DeleteIcon,
    Edit as EditIcon,
    PersonAdd as PersonAddIcon,
    Link as LinkIcon,
    LinkOff as UnlinkIcon,
    CheckCircle as ClaimedIcon,
} from "@mui/icons-material";
import { useState } from "react";
import type { Bill, Member } from "@/types/snap-split";
import { SlideTransition } from "@/components/ui/SlideTransition";
import { useSnapSplitStore } from "@/stores/snapSplitStore";
import { useAuthStore } from "@/stores/authStore";
import { getMemberColor } from "@/utils/settlement";

interface MemberDialogProps {
    bill: Bill;
    open: boolean;
    onClose: () => void;
    isReadOnly?: boolean;
}

export function MemberDialog({ bill, open, onClose, isReadOnly = false }: MemberDialogProps) {
    const { addMember, removeMember, updateMember, claimMember, unclaimMember } = useSnapSplitStore();
    const { user, isAuthenticated } = useAuthStore();

    const [addOpen, setAddOpen] = useState(false);
    const [newName, setNewName] = useState('');
    const [editOpen, setEditOpen] = useState(false);
    const [editingMember, setEditingMember] = useState<Member | null>(null);
    const [editName, setEditName] = useState('');
    const [deleteOpen, setDeleteOpen] = useState(false);
    const [deletingMember, setDeletingMember] = useState<Member | null>(null);
    const [claimConfirmOpen, setClaimConfirmOpen] = useState(false);
    const [claimingMember, setClaimingMember] = useState<Member | null>(null);
    const [unclaimConfirmOpen, setUnclaimConfirmOpen] = useState(false);
    const [unclaimingMember, setUnclaimingMember] = useState<Member | null>(null);

    // 找出當前使用者已認領的成員
    const myClaimedMember = user?.id ? bill.members.find(m => m.userId === user.id) : undefined;

    const canClaimMember = (member: Member) => {
        // 必須已登入且有 user id
        if (!isAuthenticated || !user?.id) return false;
        // 成員尚未被認領
        if (member.userId) return false;
        // 使用者尚未認領其他成員
        if (myClaimedMember) return false;
        return true;
    };

    const canUnclaimMember = (member: Member) => {
        if (!user?.id) return false;
        // 只有認領者本人或帳單擁有者可以取消
        return member.userId === user.id || bill.ownerId === user.id;
    };

    const handleOpenClaimConfirm = (member: Member) => {
        setClaimingMember(member);
        setClaimConfirmOpen(true);
    };

    const handleClaim = () => {
        if (claimingMember && user?.id) {
            claimMember({
                memberId: claimingMember.id,
                userId: user.id,
                displayName: user.displayName ?? user.email ?? '使用者',
                avatarUrl: user.avatarUrl ?? undefined,
            });
        }
        setClaimConfirmOpen(false);
        setClaimingMember(null);
    };

    const handleOpenUnclaimConfirm = (member: Member) => {
        setUnclaimingMember(member);
        setUnclaimConfirmOpen(true);
    };

    const handleUnclaim = () => {
        if (unclaimingMember) {
            unclaimMember(unclaimingMember.id);
        }
        setUnclaimConfirmOpen(false);
        setUnclaimingMember(null);
    };

    const handleAdd = () => {
        if (newName.trim()) {
            addMember(newName.trim());
            setNewName('');
            setAddOpen(false);
        }
    };

    const handleOpenEdit = (member: Member) => {
        setEditingMember(member);
        setEditName(member.name);
        setEditOpen(true);
    };

    const handleSaveEdit = () => {
        if (editingMember && editName.trim()) {
            updateMember(editingMember.id, editName.trim());
        }
        setEditOpen(false);
        setEditingMember(null);
    };

    const handleOpenDelete = (member: Member) => {
        setDeletingMember(member);
        setDeleteOpen(true);
    };

    const handleDelete = () => {
        if (deletingMember) {
            removeMember(deletingMember.id);
        }
        setDeleteOpen(false);
        setDeletingMember(null);
    };

    return (
        <>
            <Dialog
                open={open}
                onClose={onClose}
                maxWidth="xs"
                fullWidth
                TransitionComponent={SlideTransition}
            >
                <DialogTitle sx={{ pb: 1 }}>
                    成員管理
                    {bill.members.length > 0 && (
                        <Typography component="span" color="text.secondary" sx={{ ml: 1 }}>
                            ({bill.members.length} 人)
                        </Typography>
                    )}
                </DialogTitle>
                <DialogContent sx={{ p: 0 }}>
                    {bill.members.length === 0 && !isReadOnly ? (
                        <Box sx={{ textAlign: 'center', py: 4, px: 3 }}>
                            <PersonAddIcon sx={{ fontSize: 56, color: 'text.disabled', mb: 2 }} />
                            <Typography color="text.secondary" sx={{ mb: 3 }}>
                                新增成員來開始分帳
                            </Typography>
                            <Button
                                variant="contained"
                                size="large"
                                startIcon={<AddIcon />}
                                onClick={() => setAddOpen(true)}
                                sx={{ borderRadius: 2, px: 3 }}
                            >
                                新增第一位成員
                            </Button>
                        </Box>
                    ) : (
                        <List sx={{ px: 1 }}>
                            {bill.members.map(member => {
                                const isClaimed = !!member.userId;
                                const isMyMember = !!user?.id && member.userId === user.id;

                                return (
                                    <ListItem
                                        key={member.id}
                                        sx={{
                                            borderRadius: 2,
                                            mb: 0.5,
                                            bgcolor: isMyMember ? 'primary.50' : undefined,
                                            border: isMyMember ? '2px solid' : undefined,
                                            borderColor: isMyMember ? 'primary.main' : undefined,
                                            '&:hover': !isReadOnly ? { bgcolor: isMyMember ? 'primary.100' : 'action.hover' } : {},
                                        }}
                                        secondaryAction={
                                            <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                                                {/* 認領/取消認領按鈕 */}
                                                {!isReadOnly && isAuthenticated && (
                                                    isClaimed ? (
                                                        canUnclaimMember(member) && (
                                                            <Tooltip title="取消認領">
                                                                <IconButton
                                                                    size="small"
                                                                    onClick={() => handleOpenUnclaimConfirm(member)}
                                                                    color="warning"
                                                                >
                                                                    <UnlinkIcon fontSize="small" />
                                                                </IconButton>
                                                            </Tooltip>
                                                        )
                                                    ) : (
                                                        canClaimMember(member) && (
                                                            <Tooltip title="這是我">
                                                                <IconButton
                                                                    size="small"
                                                                    onClick={() => handleOpenClaimConfirm(member)}
                                                                    color="primary"
                                                                >
                                                                    <LinkIcon fontSize="small" />
                                                                </IconButton>
                                                            </Tooltip>
                                                        )
                                                    )
                                                )}
                                                {/* 編輯按鈕：只有未認領或本人可編輯 */}
                                                {!isReadOnly && (!isClaimed || isMyMember) && (
                                                    <Tooltip title="編輯名稱">
                                                        <IconButton
                                                            size="small"
                                                            onClick={() => handleOpenEdit(member)}
                                                        >
                                                            <EditIcon fontSize="small" />
                                                        </IconButton>
                                                    </Tooltip>
                                                )}
                                                {/* 已認領但非本人：顯示鎖定提示 */}
                                                {!isReadOnly && isClaimed && !isMyMember && (
                                                    <Tooltip title="此成員已連結帳號，無法修改名稱">
                                                        <Box sx={{ p: 1, display: 'flex', alignItems: 'center' }}>
                                                            <EditIcon fontSize="small" sx={{ color: 'action.disabled' }} />
                                                        </Box>
                                                    </Tooltip>
                                                )}
                                                {/* 刪除按鈕：只有未認領的成員可刪除 */}
                                                {!isReadOnly && !isClaimed && (
                                                    <Tooltip title="刪除成員">
                                                        <IconButton
                                                            size="small"
                                                            onClick={() => handleOpenDelete(member)}
                                                            color="error"
                                                        >
                                                            <DeleteIcon fontSize="small" />
                                                        </IconButton>
                                                    </Tooltip>
                                                )}
                                            </Box>
                                        }
                                    >
                                        <ListItemAvatar>
                                            <Avatar
                                                src={member.avatarUrl}
                                                sx={{
                                                    bgcolor: isClaimed ? 'primary.main' : getMemberColor(member.id, bill.members),
                                                    fontSize: '1rem',
                                                    fontWeight: 600,
                                                    // 已認領但非當前用戶時，降低不透明度表示「離線」
                                                    opacity: isClaimed && !isMyMember ? 0.6 : 1,
                                                    filter: isClaimed && !isMyMember ? 'grayscale(30%)' : 'none',
                                                }}
                                            >
                                                {member.name.charAt(0).toUpperCase()}
                                            </Avatar>
                                        </ListItemAvatar>
                                        <ListItemText
                                            primary={
                                                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                                                    <Typography fontWeight={600}>
                                                        {member.name}
                                                    </Typography>
                                                    {isMyMember && (
                                                        <Chip
                                                            icon={<ClaimedIcon />}
                                                            label="我"
                                                            size="small"
                                                            color="primary"
                                                            sx={{ height: 20, fontSize: '0.7rem' }}
                                                        />
                                                    )}
                                                    {isClaimed && !isMyMember && (
                                                        <Tooltip title={`已被認領`}>
                                                            <ClaimedIcon
                                                                sx={{
                                                                    fontSize: 16,
                                                                    color: 'success.main',
                                                                }}
                                                            />
                                                        </Tooltip>
                                                    )}
                                                </Box>
                                            }
                                            secondary={
                                                member.originalName && member.originalName !== member.name
                                                    ? `(${member.originalName})`
                                                    : undefined
                                            }
                                        />
                                    </ListItem>
                                );
                            })}
                        </List>
                    )}
                </DialogContent>
                <DialogActions sx={{ p: 2, pt: 1, justifyContent: 'space-between' }}>
                    {!isReadOnly && bill.members.length > 0 ? (
                        <Button
                            startIcon={<AddIcon />}
                            onClick={() => setAddOpen(true)}
                            size="large"
                        >
                            新增成員
                        </Button>
                    ) : <Box />}
                    <Button onClick={onClose} variant="contained" size="large">
                        完成
                    </Button>
                </DialogActions>
            </Dialog>

            <Dialog
                open={addOpen}
                onClose={() => setAddOpen(false)}
                maxWidth="xs"
                fullWidth
                TransitionComponent={SlideTransition}
            >
                <DialogTitle>新增成員</DialogTitle>
                <DialogContent>
                    <TextField
                        autoFocus
                        fullWidth
                        label="成員名稱"
                        value={newName}
                        onChange={(e) => setNewName(e.target.value)}
                        onKeyDown={(e) => e.key === 'Enter' && handleAdd()}
                        sx={{ mt: 1 }}
                        placeholder="例如：小明"
                    />
                </DialogContent>
                <DialogActions sx={{ p: 2, pt: 0 }}>
                    <Button onClick={() => setAddOpen(false)} size="large">取消</Button>
                    <Button onClick={handleAdd} variant="contained" size="large" disabled={!newName.trim()}>
                        新增
                    </Button>
                </DialogActions>
            </Dialog>

            <Dialog
                open={editOpen}
                onClose={() => setEditOpen(false)}
                maxWidth="xs"
                fullWidth
                TransitionComponent={SlideTransition}
            >
                <DialogTitle>編輯成員</DialogTitle>
                <DialogContent>
                    <TextField
                        autoFocus
                        fullWidth
                        label="成員名稱"
                        value={editName}
                        onChange={(e) => setEditName(e.target.value)}
                        onKeyDown={(e) => e.key === 'Enter' && handleSaveEdit()}
                        sx={{ mt: 1 }}
                    />
                </DialogContent>
                <DialogActions sx={{ p: 2, pt: 0 }}>
                    <Button onClick={() => setEditOpen(false)} size="large">取消</Button>
                    <Button onClick={handleSaveEdit} variant="contained" size="large" disabled={!editName.trim()}>
                        儲存
                    </Button>
                </DialogActions>
            </Dialog>

            <Dialog
                open={deleteOpen}
                onClose={() => setDeleteOpen(false)}
                TransitionComponent={SlideTransition}
            >
                <DialogTitle>確認刪除</DialogTitle>
                <DialogContent>
                    <DialogContentText>
                        確定要刪除成員「{deletingMember?.name}」嗎？
                    </DialogContentText>
                </DialogContent>
                <DialogActions sx={{ p: 2, pt: 0 }}>
                    <Button onClick={() => setDeleteOpen(false)} size="large">取消</Button>
                    <Button onClick={handleDelete} color="error" variant="contained" size="large">
                        刪除
                    </Button>
                </DialogActions>
            </Dialog>

            {/* 認領確認對話框 */}
            <Dialog
                open={claimConfirmOpen}
                onClose={() => setClaimConfirmOpen(false)}
                TransitionComponent={SlideTransition}
            >
                <DialogTitle>確認認領</DialogTitle>
                <DialogContent>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 2 }}>
                        <Avatar
                            src={user?.avatarUrl ?? undefined}
                            sx={{ width: 48, height: 48 }}
                        >
                            {user?.displayName?.charAt(0) ?? user?.email?.charAt(0)}
                        </Avatar>
                        <Box>
                            <Typography fontWeight={600}>
                                {user?.displayName ?? user?.email}
                            </Typography>
                            <Typography variant="body2" color="text.secondary">
                                你的帳號
                            </Typography>
                        </Box>
                    </Box>
                    <DialogContentText>
                        確定將成員「{claimingMember?.name}」與你的帳號綁定？
                        <br />
                        <Typography component="span" variant="body2" color="text.secondary">
                            綁定後，成員名稱會更新為你的 LINE 顯示名稱。
                        </Typography>
                    </DialogContentText>
                </DialogContent>
                <DialogActions sx={{ p: 2, pt: 0 }}>
                    <Button onClick={() => setClaimConfirmOpen(false)} size="large">取消</Button>
                    <Button onClick={handleClaim} variant="contained" size="large" startIcon={<LinkIcon />}>
                        確認認領
                    </Button>
                </DialogActions>
            </Dialog>

            {/* 取消認領確認對話框 */}
            <Dialog
                open={unclaimConfirmOpen}
                onClose={() => setUnclaimConfirmOpen(false)}
                TransitionComponent={SlideTransition}
            >
                <DialogTitle>取消認領</DialogTitle>
                <DialogContent>
                    <DialogContentText>
                        確定要取消認領成員「{unclaimingMember?.name}」？
                        <br />
                        <Typography component="span" variant="body2" color="text.secondary">
                            取消後，成員名稱將還原為原本的名稱
                            {unclaimingMember?.originalName && `「${unclaimingMember.originalName}」`}
                            。
                        </Typography>
                    </DialogContentText>
                </DialogContent>
                <DialogActions sx={{ p: 2, pt: 0 }}>
                    <Button onClick={() => setUnclaimConfirmOpen(false)} size="large">取消</Button>
                    <Button onClick={handleUnclaim} color="warning" variant="contained" size="large" startIcon={<UnlinkIcon />}>
                        確認取消
                    </Button>
                </DialogActions>
            </Dialog>
        </>
    );
}
