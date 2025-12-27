import {
    Avatar,
    Box,
    Button,
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
    Typography,
} from "@mui/material";
import {
    Add as AddIcon,
    Delete as DeleteIcon,
    Edit as EditIcon,
    PersonAdd as PersonAddIcon,
} from "@mui/icons-material";
import { useState } from "react";
import type { Bill, Member } from "@/types/snap-split";
import { SlideTransition } from "@/components/ui/SlideTransition";
import { useSnapSplitStore } from "@/stores/snapSplitStore";
import { getMemberColor } from "@/utils/settlement";

interface MemberDialogProps {
    bill: Bill;
    open: boolean;
    onClose: () => void;
    isReadOnly?: boolean;
}

export function MemberDialog({ bill, open, onClose, isReadOnly = false }: MemberDialogProps) {
    const { addMember, removeMember, updateMember } = useSnapSplitStore();

    const [addOpen, setAddOpen] = useState(false);
    const [newName, setNewName] = useState('');
    const [editOpen, setEditOpen] = useState(false);
    const [editingMember, setEditingMember] = useState<Member | null>(null);
    const [editName, setEditName] = useState('');
    const [deleteOpen, setDeleteOpen] = useState(false);
    const [deletingMember, setDeletingMember] = useState<Member | null>(null);

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
                            {bill.members.map(member => (
                                <ListItem
                                    key={member.id}
                                    sx={{
                                        borderRadius: 2,
                                        mb: 0.5,
                                        '&:hover': !isReadOnly ? { bgcolor: 'action.hover' } : {},
                                    }}
                                    secondaryAction={
                                        !isReadOnly && (
                                            <Box>
                                                <IconButton
                                                    size="small"
                                                    onClick={() => handleOpenEdit(member)}
                                                    sx={{ mr: 0.5 }}
                                                >
                                                    <EditIcon fontSize="small" />
                                                </IconButton>
                                                <IconButton
                                                    size="small"
                                                    onClick={() => handleOpenDelete(member)}
                                                    color="error"
                                                >
                                                    <DeleteIcon fontSize="small" />
                                                </IconButton>
                                            </Box>
                                        )
                                    }
                                >
                                    <ListItemAvatar>
                                        <Avatar
                                            sx={{
                                                bgcolor: getMemberColor(member.id, bill.members),
                                                fontSize: '1rem',
                                                fontWeight: 600
                                            }}
                                        >
                                            {member.name.charAt(0).toUpperCase()}
                                        </Avatar>
                                    </ListItemAvatar>
                                    <ListItemText
                                        primary={member.name}
                                        primaryTypographyProps={{ fontWeight: 600 }}
                                    />
                                </ListItem>
                            ))}
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
        </>
    );
}
