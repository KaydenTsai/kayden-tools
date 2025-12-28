import { ItemizedExpenseView } from "./ItemizedExpenseView";
import {
    Alert,
    Box,
    Button,
    Chip,
    Dialog,
    DialogActions,
    DialogContent,
    DialogTitle,
    IconButton,
    Paper,
    Stack,
    Tab,
    Tabs,
    TextField,
    Typography,
    useMediaQuery,
    useTheme,
} from "@mui/material";
import {
    ArrowBack as ArrowBackIcon,
    Calculate as CalculateIcon,
    CloudOff as CloudOffIcon,
    FactCheck as FactCheckIcon,
    Group as GroupIcon,
    Login as LoginIcon,
    PersonAdd as PersonAddIcon,
    Receipt as ReceiptIcon,
    Share as ShareIcon,
    Person as PersonIcon,
} from "@mui/icons-material";
import { useState, useEffect } from "react";
import { ExpenseList } from "../components/ExpenseList";
import { SettlementPanel } from "../components/SettlementPanel";
import { VerificationPanel } from "../components/VerificationPanel";
import { MemberDialog } from "../components/MemberDialog";
import { ShareDialog } from "@/components/dialogs/ShareDialog";
import { ClaimPromptDialog } from "@/components/dialogs/ClaimPromptDialog";
import { LoginDialog } from "@/components/dialogs/LoginDialog";
import { SyncStatusIcon } from "@/components/ui/SyncStatusIndicator";
import { useSnapSplitStore } from "@/stores/snapSplitStore";
import { useAuthStore } from "@/stores/authStore";
import type { Bill } from "@/types/snap-split";
import { formatAmount, getExpenseTotal } from "@/utils/settlement";
import { SlideTransition } from "@/components/ui/SlideTransition";

interface BillDetailViewProps {
    bill: Bill;
    onBack: () => void;
    isReadOnly?: boolean;
    isAuthenticated?: boolean;
}

export function BillDetailView({ bill, onBack, isReadOnly = false, isAuthenticated = false }: BillDetailViewProps) {
    const { updateBillName, shouldShowClaimPrompt, skipClaimForBill, skippedClaimBillIds } = useSnapSplitStore();
    const { user } = useAuthStore();
    const theme = useTheme();
    const isMobile = useMediaQuery(theme.breakpoints.down('md'));

    // 協作模式鎖定：任何成員已綁定帳號 → 需要登入才能操作
    const isCollaborative = bill.members.some(m => !!m.userId);
    const isCloudLocked = isCollaborative && !isAuthenticated;
    // 有效唯讀狀態：原本的 isReadOnly 或雲端鎖定
    const effectiveReadOnly = isReadOnly || isCloudLocked;

    const [tabIndex, setTabIndex] = useState(effectiveReadOnly ? 2 : 0);
    const [memberDialogOpen, setMemberDialogOpen] = useState(false);
    const [shareDialogOpen, setShareDialogOpen] = useState(false);
    const [itemizedExpenseOpen, setItemizedExpenseOpen] = useState(false);
    const [editingExpenseId, setEditingExpenseId] = useState<string | undefined>(undefined);
    const [editNameOpen, setEditNameOpen] = useState(false);
    const [editName, setEditName] = useState(bill.name);
    const [claimPromptOpen, setClaimPromptOpen] = useState(false);
    const [loginDialogOpen, setLoginDialogOpen] = useState(false);

    // 檢查是否應該顯示認領提示
    const showClaimPrompt = !effectiveReadOnly && shouldShowClaimPrompt(bill.id, user?.id);
    // 是否已跳過但應該顯示提醒 banner
    const showClaimReminder = !effectiveReadOnly &&
        isAuthenticated &&
        user?.id &&
        !bill.isSnapshot &&
        skippedClaimBillIds.includes(bill.id) &&
        !bill.members.some(m => m.userId === user.id) &&
        bill.members.some(m => !m.userId);

    // 格式化最後同步時間
    const formatLastSyncTime = (dateStr?: string) => {
        if (!dateStr) return null;
        const date = new Date(dateStr);
        return date.toLocaleString('zh-TW', {
            month: 'numeric',
            day: 'numeric',
            hour: '2-digit',
            minute: '2-digit',
        });
    };

    // 進入帳單時自動彈出認領提示
    useEffect(() => {
        if (showClaimPrompt && !claimPromptOpen) {
            // 延遲一下再顯示，讓頁面先載入
            const timer = setTimeout(() => {
                setClaimPromptOpen(true);
            }, 500);
            return () => clearTimeout(timer);
        }
    }, [showClaimPrompt]);

    const handleSkipClaim = () => {
        skipClaimForBill(bill.id);
    };

    const handleSaveEditName = () => {
        if (editName.trim()) {
            updateBillName(editName.trim());
        }
        setEditNameOpen(false);
    };

    const handleOpenItemizedExpense = (expenseId?: string) => {
        setEditingExpenseId(expenseId);
        setItemizedExpenseOpen(true);
    };

    const handleCloseItemizedExpense = () => {
        setItemizedExpenseOpen(false);
        setEditingExpenseId(undefined);
    };

    const totalAmount = bill.expenses.reduce((sum, e) => sum + getExpenseTotal(e), 0);

    // 品項模式全螢幕頁面
    if (itemizedExpenseOpen) {
        return (
            <ItemizedExpenseView
                bill={bill}
                expenseId={editingExpenseId}
                onClose={handleCloseItemizedExpense}
            />
        );
    }

    return (
        <Box sx={{ pb: isMobile ? 8 : 0 }}> {/* Add padding for bottom nav only on mobile */}
            <Paper
                elevation={2}
                sx={{
                    position: 'sticky',
                    top: 0,
                    zIndex: 10,
                    borderRadius: 3,
                    mb: 2,
                    ...(isMobile && {
                        borderBottomLeftRadius: 16,
                        borderBottomRightRadius: 16,
                    }),
                }}
            >
                <Box sx={{ p: 2, display: 'flex', alignItems: 'center', gap: 1.5 }}>
                    <IconButton
                        onClick={onBack}
                        sx={{
                            color: 'text.primary',
                            transition: 'all 0.2s',
                            '&:hover': {
                                bgcolor: 'action.selected',
                                transform: 'translateX(-2px)',
                            },
                        }}
                    >
                        <ArrowBackIcon />
                    </IconButton>
                    <Box sx={{ flex: 1, minWidth: 0 }}>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
                            <Typography
                                variant="h6"
                                fontWeight={700}
                                noWrap
                                onClick={effectiveReadOnly ? undefined : () => { setEditName(bill.name); setEditNameOpen(true); }}
                                sx={effectiveReadOnly ? undefined : {
                                    cursor: 'pointer',
                                    '&:hover': { color: 'primary.main' },
                                }}
                            >
                                {bill.name}
                            </Typography>
                            <SyncStatusIcon status={bill.syncStatus} size="small" isCollaborative={isCollaborative} />
                        </Box>
                        <Typography variant="body2" color="text.secondary">
                            {bill.expenses.length} 筆消費 · 總計 {formatAmount(totalAmount)}
                        </Typography>
                    </Box>
                    {bill.members.length > 0 ? (
                        <Chip
                            icon={<GroupIcon />}
                            label={`${bill.members.length} 人`}
                            variant="outlined"
                            onClick={() => setMemberDialogOpen(true)}
                            sx={{
                                height: 36,
                                px: 0.5,
                                fontWeight: 600,
                                cursor: 'pointer',
                                borderWidth: 2,
                                borderColor: 'divider',
                                transition: 'all 0.2s',
                                '&:hover': {
                                    bgcolor: 'action.hover',
                                    borderColor: 'primary.main',
                                    color: 'primary.main',
                                    '& .MuiChip-icon': {
                                        color: 'primary.main',
                                    },
                                },
                            }}
                        />
                    ) : !effectiveReadOnly && (
                        <Chip
                            icon={<PersonAddIcon />}
                            label="新增成員"
                            color="primary"
                            onClick={() => setMemberDialogOpen(true)}
                            sx={{
                                height: 36,
                                px: 0.5,
                                fontWeight: 600,
                                cursor: 'pointer',
                                transition: 'all 0.2s',
                                '&:hover': {
                                    transform: 'scale(1.05)',
                                },
                            }}
                        />
                    )}
                    {!effectiveReadOnly && (
                        <IconButton
                            onClick={() => setShareDialogOpen(true)}
                            sx={{
                                bgcolor: 'primary.main',
                                color: 'primary.contrastText',
                                '&:hover': { bgcolor: 'primary.dark' },
                            }}
                        >
                            <ShareIcon />
                        </IconButton>
                    )}
                </Box>

                {!isMobile && (
                    <Tabs
                        value={tabIndex}
                        onChange={(_, v) => setTabIndex(v)}
                        variant="fullWidth"
                        sx={{
                            borderTop: 1,
                            borderColor: 'divider',
                            '& .MuiTab-root': {
                                py: 1.5,
                                fontWeight: 600,
                            },
                        }}
                    >
                        <Tab
                            icon={<ReceiptIcon />}
                            iconPosition="start"
                            label="記錄"
                        />
                        <Tab
                            icon={<FactCheckIcon />}
                            iconPosition="start"
                            label="明細"
                        />
                        <Tab
                            icon={<CalculateIcon />}
                            iconPosition="start"
                            label="結算"
                        />
                    </Tabs>
                )}
            </Paper>

            {/* 協作模式鎖定提示 */}
            {isCloudLocked && (
                <Alert
                    severity="warning"
                    icon={<CloudOffIcon />}
                    action={
                        <Button
                            color="inherit"
                            size="small"
                            startIcon={<LoginIcon />}
                            onClick={() => setLoginDialogOpen(true)}
                        >
                            登入
                        </Button>
                    }
                    sx={{ mb: 2, mx: 0.5, borderRadius: 2 }}
                >
                    <Typography variant="body2" fontWeight={600}>
                        此帳單有多人協作，登入後才能查看最新紀錄及編輯
                    </Typography>
                    {formatLastSyncTime(bill.updatedAt) && (
                        <Typography variant="caption" color="text.secondary">
                            目前顯示的是 {formatLastSyncTime(bill.updatedAt)} 的紀錄
                        </Typography>
                    )}
                </Alert>
            )}

            {showClaimReminder && (
                <Alert
                    severity="info"
                    icon={<PersonIcon />}
                    action={
                        <Button
                            color="inherit"
                            size="small"
                            onClick={() => setClaimPromptOpen(true)}
                        >
                            認領身分
                        </Button>
                    }
                    sx={{ mb: 2, mx: 0.5, borderRadius: 2 }}
                >
                    尚未認領您的身分，認領後可同步您的帳單資料
                </Alert>
            )}

            <Stack spacing={2} sx={{ px: 0.5 }}>
                {tabIndex === 0 && (
                    <ExpenseList
                        bill={bill}
                        isReadOnly={effectiveReadOnly}
                        onOpenMemberDialog={() => setMemberDialogOpen(true)}
                        onOpenItemizedExpense={handleOpenItemizedExpense}
                    />
                )}

                {tabIndex === 1 && (
                    <VerificationPanel bill={bill} />
                )}

                {tabIndex === 2 && (
                    <SettlementPanel bill={bill} isReadOnly={effectiveReadOnly} />
                )}
            </Stack>

            {isMobile && (
                <Box
                    sx={{
                        position: 'fixed',
                        bottom: 0,
                        left: 0,
                        right: 0,
                        zIndex: 100,
                        px: 0,
                        pb: 'env(safe-area-inset-bottom, 0px)',
                        bgcolor: 'transparent',
                        pointerEvents: 'none',
                    }}
                >
                    <Box
                        sx={{
                            position: 'relative',
                            display: 'flex',
                            bgcolor: 'background.paper',
                            borderRadius: 0,
                            p: 0.5,
                            boxShadow: '0 -2px 10px rgba(0,0,0,0.1)',
                            pointerEvents: 'auto',
                        }}
                    >
                        {/* Sliding indicator */}
                        <Box
                            sx={{
                                position: 'absolute',
                                top: 4,
                                bottom: 4,
                                left: 4,
                                width: 'calc((100% - 8px) / 3)',
                                bgcolor: 'primary.main',
                                borderRadius: 1.5,
                                transition: 'transform 0.15s cubic-bezier(0.4, 0, 0.2, 1)',
                                transform: `translateX(${tabIndex * 100}%)`,
                            }}
                        />
                        {[
                            { icon: <ReceiptIcon sx={{ fontSize: 18 }} />, label: '記錄', value: 0 },
                            { icon: <FactCheckIcon sx={{ fontSize: 18 }} />, label: '明細', value: 1 },
                            { icon: <CalculateIcon sx={{ fontSize: 18 }} />, label: '結算', value: 2 },
                        ].map((tab) => {
                            const isSelected = tabIndex === tab.value;
                            return (
                                <Box
                                    key={tab.value}
                                    onClick={() => setTabIndex(tab.value)}
                                    sx={{
                                        position: 'relative',
                                        zIndex: 1,
                                        flex: 1,
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        gap: 0.75,
                                        py: 1,
                                        px: 1.5,
                                        borderRadius: 2.5,
                                        cursor: 'pointer',
                                        transition: 'color 0.2s ease',
                                        color: isSelected ? 'primary.contrastText' : 'text.secondary',
                                        fontWeight: isSelected ? 700 : 500,
                                        fontSize: '0.8rem',
                                        WebkitTapHighlightColor: 'transparent',
                                        userSelect: 'none',
                                        '&:active': {
                                            transform: 'scale(0.96)',
                                        },
                                    }}
                                >
                                    {tab.icon}
                                    {tab.label}
                                </Box>
                            );
                        })}
                    </Box>
                </Box>
            )}

            <MemberDialog
                bill={bill}
                open={memberDialogOpen}
                onClose={() => setMemberDialogOpen(false)}
                isReadOnly={effectiveReadOnly}
            />

            <ShareDialog
                bill={bill}
                open={shareDialogOpen}
                onClose={() => setShareDialogOpen(false)}
                isAuthenticated={isAuthenticated}
            />

            <Dialog
                open={editNameOpen}
                onClose={() => setEditNameOpen(false)}
                maxWidth="xs"
                fullWidth
                TransitionComponent={SlideTransition}
            >
                <DialogTitle>編輯帳單名稱</DialogTitle>
                <DialogContent>
                    <TextField
                        autoFocus
                        fullWidth
                        value={editName}
                        onChange={(e) => setEditName(e.target.value)}
                        onKeyDown={(e) => e.key === 'Enter' && handleSaveEditName()}
                        sx={{ mt: 1 }}
                    />
                </DialogContent>
                <DialogActions sx={{ p: 2, pt: 0 }}>
                    <Button onClick={() => setEditNameOpen(false)} size="large">取消</Button>
                    <Button onClick={handleSaveEditName} variant="contained" size="large" disabled={!editName.trim()}>
                        儲存
                    </Button>
                </DialogActions>
            </Dialog>

            <ClaimPromptDialog
                bill={bill}
                open={claimPromptOpen}
                onClose={() => setClaimPromptOpen(false)}
                onSkip={handleSkipClaim}
            />

            <LoginDialog
                open={loginDialogOpen}
                onClose={() => setLoginDialogOpen(false)}
            />
        </Box>
    );
}
