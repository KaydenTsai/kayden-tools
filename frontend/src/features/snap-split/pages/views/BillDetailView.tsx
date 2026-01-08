import {useState, useEffect} from 'react';
import {ArrowLeft, Calculator, ClipboardCheck, CloudOff, Loader2, LogIn, Receipt, RefreshCw, Share2, UserPlus, Users} from 'lucide-react';
import {useLogin} from '@/shared/hooks/use-login';
import {Button} from '@/shared/components/ui/button';
import {
    Dialog,
    DialogContent,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '@/shared/components/ui/dialog';
import {Input} from '@/shared/components/ui/input';
import {useSnapSplitStore} from '@/features/snap-split/stores/snapSplitStore';
import {useAuthStore} from '@/stores/authStore';
import type {Bill} from '@/features/snap-split/types/snap-split';
import {formatAmount, getExpenseTotal} from '@/features/snap-split/lib/settlement';
import {cn} from '@/shared/lib/utils';
import {ExpenseList} from '@/features/snap-split/components/ExpenseList';
import {SettlementPanel} from '@/features/snap-split/components/SettlementPanel';
import {MemberDialog} from '@/features/snap-split/components/MemberDialog';
import {ShareDialog} from '@/features/snap-split/components/ShareDialog';
import {VerificationPanel} from '@/features/snap-split/components/VerificationPanel';
import {ItemizedExpenseView} from '@/features/snap-split/pages/views/ItemizedExpenseView';
import {SyncStatusIndicator} from '@/features/snap-split/components/SyncStatusIndicator';
import {SyncErrorBanner} from '@/features/snap-split/components/SyncErrorBanner';
import {ConflictBanner} from '@/features/snap-split/components/ConflictBanner';
import {useAutoSync} from '@/features/snap-split/hooks/useAutoSync';
import {getBillByShareCode} from '@/api/endpoints/bills/bills';

interface BillDetailViewProps {
    bill: Bill;
    onBack: () => void;
    isReadOnly?: boolean;
    isAuthenticated?: boolean;
    isSyncing?: boolean;
    isConnected?: boolean;
}

type TabValue = 'expenses' | 'verification' | 'settlement';

const tabs: { value: TabValue; label: string; icon: React.ReactNode }[] = [
    {value: 'expenses', label: '記錄', icon: <Receipt className="h-4 w-4"/>},
    {value: 'verification', label: '明細', icon: <ClipboardCheck className="h-4 w-4"/>},
    {value: 'settlement', label: '結算', icon: <Calculator className="h-4 w-4"/>},
];

export function BillDetailView({bill, onBack, isReadOnly = false, isAuthenticated = false, isSyncing = false, isConnected: _isConnected = false}: BillDetailViewProps) {
    const {updateBillName, skippedClaimBillIds, rebaseBillFromServer} = useSnapSplitStore();
    const {user} = useAuthStore();
    const {login, isLoggingIn} = useLogin();
    const {syncNow, isSyncing: isAutoSyncing} = useAutoSync({enabled: isAuthenticated});

    // 進入帳單時自動拉取最新版本（雲端帳單 + 已登入）
    useEffect(() => {
        if (!bill.shareCode || !isAuthenticated || isReadOnly) return;

        const fetchLatest = async () => {
            try {
                const response = await getBillByShareCode(bill.shareCode!);
                if (!response.success || !response.data) return;

                // 只有版本較新時才更新
                const serverVersion = response.data.version ?? 0;
                if (serverVersion > bill.version) {
                    rebaseBillFromServer(bill.id, response.data);
                }
            } catch (error) {
                // 靜默失敗，使用本地快取
                console.warn('[BillDetailView] Failed to fetch latest bill on enter:', error);
            }
        };

        fetchLatest();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [bill.id, bill.shareCode, isAuthenticated]);

    // 衝突處理：取得最新版本
    const handleRefreshFromServer = async () => {
        if (!bill.shareCode) return;
        try {
            const response = await getBillByShareCode(bill.shareCode);
            if (!response.success || !response.data) return;

            rebaseBillFromServer(bill.id, response.data);
        } catch (error) {
            console.error('[BillDetailView] Failed to fetch latest bill:', error);
        }
    };

    // 是否顯示手動同步按鈕
    const showManualSync = isAuthenticated && (bill.syncStatus === 'error' || bill.syncStatus === 'modified');
    const syncInProgress = isSyncing || isAutoSyncing;

    // 雲端帳單 + 未登入 → 顯示快照提示
    const isCloudBill = !!bill.shareCode;
    const showSnapshotWarning = isCloudBill && !isAuthenticated;

    // 協作模式鎖定：任何成員已綁定帳號 → 需要登入才能操作
    const isCollaborative = bill.members.some(m => !!m.userId);
    const isCloudLocked = isCollaborative && !isAuthenticated;
    const effectiveReadOnly = isReadOnly || isCloudLocked;

    const [activeTab, setActiveTab] = useState<TabValue>(effectiveReadOnly ? 'settlement' : 'expenses');
    const [memberDialogOpen, setMemberDialogOpen] = useState(false);
    const [shareDialogOpen, setShareDialogOpen] = useState(false);
    const [editNameOpen, setEditNameOpen] = useState(false);
    const [editName, setEditName] = useState(bill.name);
    const [itemizedExpenseOpen, setItemizedExpenseOpen] = useState(false);
    const [editingItemizedExpenseId, setEditingItemizedExpenseId] = useState<string | undefined>(undefined);

    // 檢查是否應該顯示認領提示
    const showClaimReminder = !effectiveReadOnly &&
        isAuthenticated &&
        user?.id &&
        skippedClaimBillIds.has(bill.id) &&
        !bill.members.some(m => m.userId === user.id) &&
        bill.members.some(m => !m.userId);

    const handleSaveEditName = () => {
        if (editName.trim()) {
            updateBillName(bill.id, editName.trim());
        }
        setEditNameOpen(false);
    };

    const handleOpenItemizedExpense = (expenseId?: string) => {
        setEditingItemizedExpenseId(expenseId);
        setItemizedExpenseOpen(true);
    };

    const handleCloseItemizedExpense = () => {
        setItemizedExpenseOpen(false);
        setEditingItemizedExpenseId(undefined);
    };

    const totalAmount = bill.expenses.reduce((sum, e) => sum + getExpenseTotal(e), 0);

    return (
        <div className="flex flex-col h-full">
            {/* Header */}
            <header className="sticky top-0 z-10 bg-background border-b">
                <div className="flex items-center gap-3 p-4">
                    <button
                        onClick={onBack}
                        className="p-2 -ml-2 rounded-lg hover:bg-muted transition-colors"
                    >
                        <ArrowLeft className="h-5 w-5"/>
                    </button>

                    <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2">
                            <h1
                                className={cn(
                                    "font-bold truncate",
                                    !effectiveReadOnly && "cursor-pointer hover:text-primary"
                                )}
                                onClick={effectiveReadOnly ? undefined : () => {
                                    setEditName(bill.name);
                                    setEditNameOpen(true);
                                }}
                            >
                                {bill.name}
                            </h1>
                            {/* 同步狀態指示器 */}
                            {bill.syncStatus !== 'local' && (
                                <div className="flex items-center gap-1">
                                    <SyncStatusIndicator
                                        status={syncInProgress ? 'syncing' : bill.syncStatus}
                                        showLabel={false}
                                    />
                                    {/* 手動同步按鈕 */}
                                    {showManualSync && !syncInProgress && (
                                        <button
                                            onClick={syncNow}
                                            className="p-1 rounded hover:bg-muted transition-colors"
                                            title="手動同步"
                                        >
                                            <RefreshCw className="h-3.5 w-3.5 text-muted-foreground hover:text-foreground" />
                                        </button>
                                    )}
                                </div>
                            )}
                        </div>
                        <p className="text-sm text-muted-foreground">
                            {bill.expenses.length} 筆消費 · 總計 {formatAmount(totalAmount)}
                        </p>
                    </div>

                    {/* Action Buttons */}
                    <div className="flex items-center gap-2">
                        {/* 成員按鈕 */}
                        {bill.members.length > 0 ? (
                            <button
                                onClick={() => setMemberDialogOpen(true)}
                                className="h-9 flex items-center gap-1.5 px-3 bg-muted hover:bg-muted/70 active:scale-95 rounded-full text-sm font-medium transition-all duration-150"
                            >
                                <Users className="h-4 w-4"/>
                                <span>{bill.members.length}</span>
                            </button>
                        ) : !effectiveReadOnly && (
                            <button
                                onClick={() => setMemberDialogOpen(true)}
                                className="h-9 flex items-center gap-1.5 px-3 bg-secondary text-secondary-foreground hover:bg-secondary/80 active:scale-95 rounded-full text-sm font-medium transition-all duration-150"
                            >
                                <UserPlus className="h-4 w-4"/>
                                <span>新增</span>
                            </button>
                        )}

                        {/* 分享按鈕 */}
                        {!effectiveReadOnly && (
                            <button
                                onClick={() => setShareDialogOpen(true)}
                                className="h-9 w-9 flex items-center justify-center bg-primary text-primary-foreground hover:bg-primary/90 active:scale-90 rounded-full shadow-sm transition-all duration-150"
                            >
                                <Share2 className="h-4 w-4"/>
                            </button>
                        )}
                    </div>
                </div>

                {/* Desktop Tabs */}
                <div className="hidden md:flex border-t">
                    {tabs.map(tab => (
                        <button
                            key={tab.value}
                            onClick={() => setActiveTab(tab.value)}
                            className={cn(
                                "flex-1 flex items-center justify-center gap-2 py-3 font-semibold text-sm transition-colors",
                                activeTab === tab.value
                                    ? "text-primary border-b-2 border-primary"
                                    : "text-muted-foreground hover:text-foreground"
                            )}
                        >
                            {tab.icon}
                            {tab.label}
                        </button>
                    ))}
                </div>
            </header>

            {/* Cloud Locked Alert - 協作模式，需登入才能編輯 */}
            {isCloudLocked && (
                <div className="mx-4 mt-4 p-4 bg-warning/10 border border-warning/30 rounded-lg">
                    <div className="flex items-start justify-between gap-4">
                        <div className="flex items-start gap-3">
                            <CloudOff className="h-5 w-5 text-warning mt-0.5 shrink-0"/>
                            <div>
                                <p className="font-semibold text-warning">
                                    此帳單有多人協作
                                </p>
                                <p className="text-sm text-warning/80 mt-0.5">
                                    登入後才能查看最新紀錄及編輯
                                </p>
                            </div>
                        </div>
                        <Button
                            variant="outline"
                            size="sm"
                            className="shrink-0"
                            onClick={() => login('line')}
                            disabled={isLoggingIn}
                        >
                            {isLoggingIn ? (
                                <Loader2 className="h-4 w-4 mr-2 animate-spin"/>
                            ) : (
                                <LogIn className="h-4 w-4 mr-2"/>
                            )}
                            登入
                        </Button>
                    </div>
                </div>
            )}

            {/* Snapshot Warning - 雲端帳單但未登入，資料不會自動更新 */}
            {showSnapshotWarning && !isCloudLocked && (
                <div className="mx-4 mt-4 p-4 bg-info/10 border border-info/30 rounded-lg">
                    <div className="flex items-start justify-between gap-4">
                        <div className="flex items-start gap-3">
                            <CloudOff className="h-5 w-5 text-info mt-0.5 shrink-0"/>
                            <div>
                                <p className="font-semibold text-info">
                                    目前顯示的是開啟連結時的資料
                                </p>
                                <p className="text-sm text-info/80 mt-0.5">
                                    登入後可查看最新紀錄並與他人同步編輯
                                </p>
                            </div>
                        </div>
                        <Button
                            variant="outline"
                            size="sm"
                            className="shrink-0"
                            onClick={() => login('line')}
                            disabled={isLoggingIn}
                        >
                            {isLoggingIn ? (
                                <Loader2 className="h-4 w-4 mr-2 animate-spin"/>
                            ) : (
                                <LogIn className="h-4 w-4 mr-2"/>
                            )}
                            登入
                        </Button>
                    </div>
                </div>
            )}

            {/* Claim Reminder */}
            {showClaimReminder && (
                <div className="mx-4 mt-4 p-4 bg-info/10 border border-info/30 rounded-lg">
                    <div className="flex items-center justify-between gap-4">
                        <p className="text-info">
                            尚未認領您的身分，認領後可同步您的帳單資料
                        </p>
                        <Button
                            variant="outline"
                            size="sm"
                            className="shrink-0"
                            onClick={() => setMemberDialogOpen(true)}
                        >
                            認領身分
                        </Button>
                    </div>
                </div>
            )}

            {/* Sync Error Banner */}
            <SyncErrorBanner className="mx-4 mt-4" />

            {/* Conflict Banner */}
            <ConflictBanner
                bill={bill}
                onRefresh={handleRefreshFromServer}
                className="mx-4 mt-4"
            />

            {/* Content */}
            <div className="flex-1 overflow-auto pb-20 md:pb-4">
                {activeTab === 'expenses' && (
                    <ExpenseList
                        bill={bill}
                        isReadOnly={effectiveReadOnly}
                        onOpenMemberDialog={() => setMemberDialogOpen(true)}
                        onOpenItemizedExpense={handleOpenItemizedExpense}
                    />
                )}
                {activeTab === 'verification' && (
                    <VerificationPanel bill={bill}/>
                )}
                {activeTab === 'settlement' && (
                    <SettlementPanel bill={bill} isReadOnly={effectiveReadOnly}/>
                )}
            </div>

            {/* Mobile Bottom Navigation - 動態滑動指示器 */}
            <div className="fixed bottom-0 left-0 right-0 md:hidden bg-background/95 backdrop-blur-md border-t safe-area-pb">
                <div className="flex p-1.5 relative">
                    {/* 滑動指示器 */}
                    <div
                        className="absolute top-1.5 bottom-1.5 bg-primary rounded-xl transition-all duration-300 ease-out"
                        style={{
                            width: `calc((100% - 12px) / ${tabs.length})`,
                            left: `calc(6px + ${tabs.findIndex(t => t.value === activeTab)} * (100% - 12px) / ${tabs.length})`,
                        }}
                    />
                    {tabs.map(tab => (
                        <button
                            key={tab.value}
                            onClick={() => setActiveTab(tab.value)}
                            className={cn(
                                "relative z-10 flex-1 flex items-center justify-center gap-1.5 py-2.5 text-sm font-semibold transition-colors duration-200",
                                activeTab === tab.value
                                    ? "text-primary-foreground"
                                    : "text-muted-foreground active:text-foreground"
                            )}
                        >
                            {tab.icon}
                            <span>{tab.label}</span>
                        </button>
                    ))}
                </div>
            </div>

            {/* Dialogs */}
            <MemberDialog
                bill={bill}
                open={memberDialogOpen}
                onClose={() => setMemberDialogOpen(false)}
                isReadOnly={effectiveReadOnly}
            />

            <ShareDialog
                billId={bill.id}
                open={shareDialogOpen}
                onClose={() => setShareDialogOpen(false)}
                isAuthenticated={isAuthenticated}
            />

            {/* Edit Name Dialog */}
            <Dialog open={editNameOpen} onOpenChange={setEditNameOpen}>
                <DialogContent className="sm:max-w-md">
                    <DialogHeader>
                        <DialogTitle>編輯帳單名稱</DialogTitle>
                    </DialogHeader>
                    <div className="py-4">
                        <Input
                            value={editName}
                            onChange={(e) => setEditName(e.target.value)}
                            onKeyDown={(e) => e.key === 'Enter' && handleSaveEditName()}
                            autoFocus
                        />
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setEditNameOpen(false)}>
                            取消
                        </Button>
                        <Button onClick={handleSaveEditName} disabled={!editName.trim()}>
                            儲存
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            {/* Itemized Expense View */}
            {itemizedExpenseOpen && (
                <ItemizedExpenseView
                    bill={bill}
                    expenseId={editingItemizedExpenseId}
                    onClose={handleCloseItemizedExpense}
                />
            )}
        </div>
    );
}
