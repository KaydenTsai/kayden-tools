import {useState, useEffect, useRef} from 'react';
import {Check, Copy, Loader2, LogIn, RefreshCw} from 'lucide-react';
import {Button} from '@/shared/components/ui/button';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '@/shared/components/ui/dialog';
import {Input} from '@/shared/components/ui/input';
import {useSnapSplitStore} from '@/features/snap-split/stores/snapSplitStore';
import {useBillSync} from '@/features/snap-split/hooks/useBillSync';
import {useLogin} from '@/shared/hooks/use-login';

interface ShareDialogProps {
    billId: string;
    open: boolean;
    onClose: () => void;
    isAuthenticated?: boolean;
}

export function ShareDialog({billId, open, onClose, isAuthenticated = false}: ShareDialogProps) {
    const bill = useSnapSplitStore(state => state.bills.find(b => b.id === billId));
    const [copied, setCopied] = useState<string | null>(null);
    const [syncError, setSyncError] = useState<string | null>(null);

    const {syncBill, isUploading} = useBillSync();
    const {login, isLoggingIn} = useLogin();
    const hasSyncedRef = useRef(false);

    // 帳單不存在時不渲染
    if (!bill) return null;

    const shareUrl = bill.shareCode
        ? `${window.location.origin}/snap-split/share/${bill.shareCode}`
        : null;

    // 自動同步：已登入且帳單尚未同步時
    const needsSync = isAuthenticated && !bill.shareCode && bill.syncStatus !== 'syncing';

    useEffect(() => {
        if (open && isAuthenticated && needsSync && !hasSyncedRef.current && !isUploading) {
            hasSyncedRef.current = true;
            setSyncError(null);
            syncBill(bill).catch((err) => {
                setSyncError(err instanceof Error ? err.message : '同步失敗');
            });
        }
    }, [open, isAuthenticated, needsSync, isUploading, bill, syncBill]);

    // 重置 ref 當 dialog 關閉時
    useEffect(() => {
        if (!open) {
            hasSyncedRef.current = false;
            setSyncError(null);
        }
    }, [open]);

    const handleCopy = async (text: string, key: string) => {
        try {
            await navigator.clipboard.writeText(text);
            setCopied(key);
            setTimeout(() => setCopied(null), 2000);
        } catch {
            // 複製失敗
        }
    };

    const handleRetrySync = async () => {
        setSyncError(null);
        try {
            await syncBill(bill);
        } catch (err) {
            setSyncError(err instanceof Error ? err.message : '同步失敗');
        }
    };

    // 未登入狀態
    if (!isAuthenticated) {
        return (
            <Dialog open={open} onOpenChange={onClose}>
                <DialogContent className="sm:max-w-md">
                    <DialogHeader>
                        <DialogTitle>分享帳單</DialogTitle>
                        <DialogDescription>
                            登入後即可產生分享連結，邀請朋友一起編輯帳單
                        </DialogDescription>
                    </DialogHeader>

                    <div className="flex flex-col items-center py-6">
                        <p className="text-sm text-muted-foreground mb-4 text-center">
                            分享功能需要登入，帳單將同步至雲端以便多人協作
                        </p>
                        <Button onClick={() => login('line')} disabled={isLoggingIn}>
                            {isLoggingIn ? (
                                <Loader2 className="h-4 w-4 mr-2 animate-spin"/>
                            ) : (
                                <LogIn className="h-4 w-4 mr-2"/>
                            )}
                            登入以分享
                        </Button>
                    </div>

                    <DialogFooter>
                        <Button variant="outline" onClick={onClose}>取消</Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        );
    }

    return (
        <Dialog open={open} onOpenChange={onClose}>
            <DialogContent className="sm:max-w-md">
                <DialogHeader>
                    <DialogTitle>分享帳單</DialogTitle>
                    <DialogDescription>
                        使用分享碼或連結邀請朋友加入帳單
                    </DialogDescription>
                </DialogHeader>

                <div className="space-y-4 mt-4">
                    {bill.shareCode ? (
                        <>
                            {/* 分享碼 */}
                            <div>
                                <p className="text-xs text-muted-foreground mb-1">分享碼</p>
                                <div className="flex items-center gap-2">
                                    <span className="text-3xl font-bold font-mono tracking-widest">
                                        {bill.shareCode}
                                    </span>
                                    <Button
                                        variant="ghost"
                                        size="icon"
                                        onClick={() => handleCopy(bill.shareCode!, 'code')}
                                    >
                                        {copied === 'code' ? (
                                            <Check className="h-4 w-4 text-success"/>
                                        ) : (
                                            <Copy className="h-4 w-4"/>
                                        )}
                                    </Button>
                                </div>
                            </div>

                            {/* 分享連結 */}
                            <div>
                                <p className="text-xs text-muted-foreground mb-1">分享連結</p>
                                <div className="flex gap-2">
                                    <Input
                                        value={shareUrl ?? ''}
                                        readOnly
                                        className="font-mono text-sm"
                                    />
                                    <Button
                                        variant="outline"
                                        size="icon"
                                        onClick={() => handleCopy(shareUrl!, 'link')}
                                        className="shrink-0"
                                    >
                                        {copied === 'link' ? (
                                            <Check className="h-4 w-4 text-success"/>
                                        ) : (
                                            <Copy className="h-4 w-4"/>
                                        )}
                                    </Button>
                                </div>
                            </div>

                            <p className="text-sm text-muted-foreground">
                                對方開啟連結後登入即可一起編輯，所有變更會自動同步
                            </p>
                        </>
                    ) : syncError ? (
                        // 同步失敗
                        <div className="text-center py-4">
                            <div className="p-3 mb-4 bg-destructive/10 border border-destructive/30 rounded-lg text-sm text-destructive">
                                {syncError}
                            </div>
                            <Button
                                onClick={handleRetrySync}
                                disabled={isUploading}
                            >
                                {isUploading ? (
                                    <Loader2 className="h-4 w-4 mr-2 animate-spin"/>
                                ) : (
                                    <RefreshCw className="h-4 w-4 mr-2"/>
                                )}
                                {isUploading ? '同步中...' : '重試'}
                            </Button>
                        </div>
                    ) : (
                        // 同步中
                        <div className="text-center py-6">
                            <Loader2 className="h-8 w-8 animate-spin text-primary mx-auto mb-3"/>
                            <p className="text-sm text-muted-foreground">
                                正在同步帳單到雲端...
                            </p>
                        </div>
                    )}
                </div>

                <DialogFooter className="mt-6">
                    <Button onClick={onClose}>關閉</Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
