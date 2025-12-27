import { ItemizedExpenseView } from "./ItemizedExpenseView";
import {
    Box,
    Button,
    Chip,
    Dialog,
    DialogActions,
    DialogContent,
    DialogContentText,
    DialogTitle,
    IconButton,
    Paper,
    Snackbar,
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
    FactCheck as FactCheckIcon,
    Group as GroupIcon,
    PersonAdd as PersonAddIcon,
    Receipt as ReceiptIcon,
    Share as ShareIcon,
} from "@mui/icons-material";
import { useState } from "react";
import { ExpenseList } from "../components/ExpenseList";
import { SettlementPanel } from "../components/SettlementPanel";
import { VerificationPanel } from "../components/VerificationPanel";
import { MemberDialog } from "../components/MemberDialog";
import { useSnapSplitStore } from "@/stores/snapSplitStore";
import type { Bill } from "@/types/snap-split";
import { encodeBillToUrl } from "@/utils/shareUrl";
import { formatAmount, getExpenseTotal } from "@/utils/settlement";
import { SlideTransition } from "@/components/ui/SlideTransition";

interface BillDetailViewProps {
    bill: Bill;
    onBack: () => void;
    isReadOnly?: boolean;
}

export function BillDetailView({ bill, onBack, isReadOnly = false }: BillDetailViewProps) {
    const { updateBillName } = useSnapSplitStore();
    const theme = useTheme();
    const isMobile = useMediaQuery(theme.breakpoints.down('md'));

    const [tabIndex, setTabIndex] = useState(isReadOnly ? 2 : 0);
    const [snackbarOpen, setSnackbarOpen] = useState(false);
    const [longUrlDialogOpen, setLongUrlDialogOpen] = useState(false);
    const [pendingShareUrl, setPendingShareUrl] = useState('');
    const [memberDialogOpen, setMemberDialogOpen] = useState(false);
    const [itemizedExpenseOpen, setItemizedExpenseOpen] = useState(false);
    const [editingExpenseId, setEditingExpenseId] = useState<string | undefined>(undefined);
    const [editNameOpen, setEditNameOpen] = useState(false);
    const [editName, setEditName] = useState(bill.name);

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

    const handleShare = async () => {
        const { url, isLong } = encodeBillToUrl(bill);

        if (isLong) {
            setPendingShareUrl(url);
            setLongUrlDialogOpen(true);
            return;
        }

        await performShare(url);
    };

    const performShare = async (url: string) => {
        if (navigator.share) {
            try {
                await navigator.share({
                    title: `帳單分享: ${bill.name}`,
                    text: `這是「${bill.name}」的結算明細`,
                    url,
                });
            } catch {
                // User cancelled or share failed
            }
        } else {
            await navigator.clipboard.writeText(url);
            setSnackbarOpen(true);
        }
    };

    const handleConfirmLongShare = async () => {
        setLongUrlDialogOpen(false);
        await performShare(pendingShareUrl);
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
                        <Typography
                            variant="h6"
                            fontWeight={700}
                            noWrap
                            onClick={isReadOnly ? undefined : () => { setEditName(bill.name); setEditNameOpen(true); }}
                            sx={isReadOnly ? undefined : {
                                cursor: 'pointer',
                                '&:hover': { color: 'primary.main' },
                            }}
                        >
                            {bill.name}
                        </Typography>
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
                    ) : !isReadOnly && (
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
                    {!isReadOnly && (
                        <IconButton
                            onClick={handleShare}
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

            <Stack spacing={2} sx={{ px: 0.5 }}>
                {tabIndex === 0 && (
                    <ExpenseList
                        bill={bill}
                        isReadOnly={isReadOnly}
                        onOpenMemberDialog={() => setMemberDialogOpen(true)}
                        onOpenItemizedExpense={handleOpenItemizedExpense}
                    />
                )}

                {tabIndex === 1 && (
                    <VerificationPanel bill={bill} />
                )}

                {tabIndex === 2 && (
                    <SettlementPanel bill={bill} isReadOnly={isReadOnly} />
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
                isReadOnly={isReadOnly}
            />

            <Snackbar
                open={snackbarOpen}
                autoHideDuration={3000}
                onClose={() => setSnackbarOpen(false)}
                message="已複製分享連結"
            />

            <Dialog
                open={longUrlDialogOpen}
                onClose={() => setLongUrlDialogOpen(false)}
                TransitionComponent={SlideTransition}
            >
                <DialogTitle>連結較長</DialogTitle>
                <DialogContent>
                    <DialogContentText>
                        產生的連結較長。在瀏覽器中可正常開啟，但部分通訊軟體（如 Line、WeChat）可能會截斷連結。
                        <br /><br />
                        建議直接複製連結貼到瀏覽器開啟。
                    </DialogContentText>
                </DialogContent>
                <DialogActions sx={{ p: 2, pt: 0 }}>
                    <Button onClick={() => setLongUrlDialogOpen(false)}>取消</Button>
                    <Button onClick={handleConfirmLongShare} variant="contained">
                        繼續分享
                    </Button>
                </DialogActions>
            </Dialog>

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
        </Box>
    );
}
